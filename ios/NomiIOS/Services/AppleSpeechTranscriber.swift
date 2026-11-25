import Foundation
import Speech
import AVFoundation

/// Semi-real transcription path backed by Apple Speech.
/// It is deliberately isolated behind a small interface so the rest of the app
/// can run with the simulator-friendly demo service.
@MainActor
final class AppleSpeechTranscriber: NSObject {
    enum TranscriberError: LocalizedError {
        case speechDenied
        case microphoneDenied
        case recognizerUnavailable
        case alreadyRunning

        var errorDescription: String? {
            switch self {
            case .speechDenied: return "La reconnaissance vocale est refusée ou indisponible."
            case .microphoneDenied: return "Le microphone est refusé. La démonstration reste disponible."
            case .recognizerUnavailable: return "Le recognizer Apple n'est pas disponible pour cette langue sur cet appareil."
            case .alreadyRunning: return "Une transcription est déjà en cours."
            }
        }
    }

    private var audioEngine: AVAudioEngine?
    private var request: SFSpeechAudioBufferRecognitionRequest?
    private var task: SFSpeechRecognitionTask?
    private var continuation: AsyncStream<TranscriptSegmentBox>.Continuation?
    private var startedAt = Date()

    struct TranscriptSegmentBox: Sendable {
        var start: TimeInterval
        var end: TimeInterval
        var text: String
        var isFinal: Bool
    }

    static func requestPermissions() async -> Bool {
        let speech = await withCheckedContinuation { continuation in
            SFSpeechRecognizer.requestAuthorization { status in
                continuation.resume(returning: status == .authorized)
            }
        }
        guard speech else { return false }
        return await withCheckedContinuation { continuation in
            AVAudioApplication.requestRecordPermission { granted in
                continuation.resume(returning: granted)
            }
        }
    }

    func start(localeIdentifier: String) async throws -> AsyncStream<TranscriptSegmentBox> {
        guard task == nil else { throw TranscriberError.alreadyRunning }
        guard await Self.requestPermissions() else { throw TranscriberError.microphoneDenied }
        guard let recognizer = SFSpeechRecognizer(locale: Locale(identifier: localeIdentifier)), recognizer.isAvailable else {
            throw TranscriberError.recognizerUnavailable
        }

        let session = AVAudioSession.sharedInstance()
        try session.setCategory(.record, mode: .measurement, options: [.duckOthers])
        try session.setActive(true, options: .notifyOthersOnDeactivation)

        let engine = AVAudioEngine()
        let request = SFSpeechAudioBufferRecognitionRequest()
        request.shouldReportPartialResults = true
        if #available(iOS 16, *) {
            request.addsPunctuation = true
        }
        startedAt = .now
        audioEngine = engine
        self.request = request

        let stream = AsyncStream<TranscriptSegmentBox> { continuation in
            self.continuation = continuation
            task = recognizer.recognitionTask(with: request) { [weak self] result, error in
                guard let self else { return }
                if let result {
                    let elapsed = Date().timeIntervalSince(self.startedAt)
                    continuation.yield(TranscriptSegmentBox(
                        start: max(0, elapsed - 5),
                        end: elapsed,
                        text: result.bestTranscription.formattedString,
                        isFinal: result.isFinal
                    ))
                }
                if error != nil || result?.isFinal == true {
                    continuation.finish()
                }
            }
            continuation.onTermination = { [weak self] _ in
                Task { @MainActor in self?.stop() }
            }
        }

        let input = engine.inputNode
        let format = input.outputFormat(forBus: 0)
        input.installTap(onBus: 0, bufferSize: 1024, format: format) { buffer, _ in
            request.append(buffer)
        }
        engine.prepare()
        try engine.start()
        return stream
    }

    func stop() {
        audioEngine?.inputNode.removeTap(onBus: 0)
        audioEngine?.stop()
        request?.endAudio()
        task?.cancel()
        task = nil
        request = nil
        audioEngine = nil
        try? AVAudioSession.sharedInstance().setActive(false, options: .notifyOthersOnDeactivation)
    }
}
