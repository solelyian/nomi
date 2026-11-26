import Foundation
import AVFoundation
import NomiCore

@MainActor
final class LiveTranscriptionViewModel: ObservableObject {
    enum Mode: String, CaseIterable, Identifiable {
        case demo = "Démonstration"
        case real = "Microphone réel"
        var id: String { rawValue }
    }

    enum State: Equatable {
        case idle, recording, paused, error(String)
    }

    @Published var mode: Mode = .demo
    @Published var state: State = .idle
    @Published var segments: [TranscriptSegment] = []
    @Published var elapsed: TimeInterval = 0
    @Published var consentAcknowledged = false
    @Published var interruptionMessage: String?

    private let demoService: any LiveTranscriptionService
    var languageProvider: () -> String
    private var streamTask: Task<Void, Never>?
    private var clockTask: Task<Void, Never>?
    private var startedAt: Date?
    private var realTranscriber = AppleSpeechTranscriber()
    private var interruptionObserver: NSObjectProtocol?
    private var activeMode: Mode?

    init(demoService: any LiveTranscriptionService, languageProvider: @escaping () -> String = { "fr-FR" }) {
        self.demoService = demoService
        self.languageProvider = languageProvider
    }

    var transcript: CourseTranscript {
        CourseTranscript(title: "Cours du \(Date.now.formatted(date: .abbreviated, time: .omitted))", segments: segments)
    }

    func start() async {
        guard consentAcknowledged else {
            state = .error("Confirmez d'abord que l'enregistrement est autorisé dans votre contexte de cours.")
            return
        }
        segments = []
        elapsed = 0
        interruptionMessage = nil
        state = .recording
        startedAt = .now
        observeInterruptions()
        startClock()
        streamTask?.cancel()
        activeMode = mode
        if mode == .demo {
            streamTask = Task { [weak self] in
                guard let self else { return }
                for await segment in self.demoService.transcribeDemoCourse() {
                    if Task.isCancelled { break }
                    self.segments.append(segment)
                }
                if !Task.isCancelled { self.finishStream() }
            }
        } else {
            streamTask = Task { [weak self] in
                guard let self else { return }
                do {
                    let stream = try await self.realTranscriber.start(localeIdentifier: self.languageProvider())
                    for await box in stream {
                        if Task.isCancelled { break }
                        self.upsert(TranscriptSegment(start: box.start, end: box.end, speaker: "Locuteur", text: box.text, isFinal: box.isFinal))
                    }
                    if !Task.isCancelled { self.finishStream() }
                } catch {
                    self.clockTask?.cancel()
                    self.state = .error(error.localizedDescription)
                }
            }
        }
    }

    func pause() {
        guard state == .recording else { return }
        state = .paused
        clockTask?.cancel()
        if activeMode == .real {
            realTranscriber.stop()
        } else {
            streamTask?.cancel()
        }
    }

    func resume() async {
        guard state == .paused else { return }
        // Dans ce prototype, la reprise relance proprement la session.
        // Une version production doit conserver le fichier audio et reprendre avec un nouvel horodatage.
        await start()
    }

    func stop() {
        if activeMode == .demo {
            streamTask?.cancel()
        }
        clockTask?.cancel()
        realTranscriber.stop()
        if let observer = interruptionObserver {
            NotificationCenter.default.removeObserver(observer)
            interruptionObserver = nil
        }
        state = .idle
        activeMode = nil
        consentAcknowledged = false
    }

    private func finishStream() {
        clockTask?.cancel()
        if state == .recording {
            state = .idle
            activeMode = nil
        }
    }

    private func upsert(_ segment: TranscriptSegment) {
        if let last = segments.last, !last.isFinal, segment.start <= last.end {
            segments[segments.count - 1] = segment
        } else {
            segments.append(segment)
        }
    }

    private func startClock() {
        clockTask?.cancel()
        clockTask = Task { [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(nanoseconds: 500_000_000)
                guard let self, let startedAt = self.startedAt else { return }
                self.elapsed = Date().timeIntervalSince(startedAt)
            }
        }
    }

    private func observeInterruptions() {
        if let observer = interruptionObserver {
            NotificationCenter.default.removeObserver(observer)
        }
        interruptionObserver = NotificationCenter.default.addObserver(forName: AVAudioSession.interruptionNotification, object: nil, queue: .main) { [weak self] notification in
            Task { @MainActor in
                guard let self else { return }
                let type = notification.userInfo?[AVAudioSessionInterruptionTypeKey] as? UInt
                if type == AVAudioSession.InterruptionType.began.rawValue {
                    self.interruptionMessage = "Interruption audio détectée : la transcription est mise en pause. Reprenez quand le cours continue."
                    self.pause()
                } else {
                    self.interruptionMessage = "Interruption terminée : vérifiez la route audio avant de reprendre."
                }
            }
        }
    }
}
