import SwiftUI
import NomiCore

struct LiveTranscriptionView: View {
    @EnvironmentObject private var state: AppState

    var body: some View {
        LiveTranscriptionContent(demoService: state.demoTranscriptionService) {
            state.transcriptionLanguage
        }
    }
}

private struct LiveTranscriptionContent: View {
    @StateObject private var model: LiveTranscriptionViewModel

    init(demoService: any LiveTranscriptionService, languageProvider: @escaping () -> String) {
        _model = StateObject(wrappedValue: LiveTranscriptionViewModel(demoService: demoService, languageProvider: languageProvider))
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                NomiSectionTitle("Transcription en direct", subtitle: "Interface discrète, consentement explicite et reprise après interruption.")

                VStack(alignment: .leading, spacing: 12) {
                    Picker("Mode", selection: Binding(get: { model.mode }, set: { model.mode = $0 })) {
                        ForEach(LiveTranscriptionViewModel.Mode.allCases) { mode in
                            Text(mode.rawValue).tag(mode)
                        }
                    }
                    .pickerStyle(.segmented)
                    .disabled(model.state == .recording || model.state == .paused)

                    Toggle("J'ai vérifié que l'enregistrement de ce cours est autorisé", isOn: Binding(get: { model.consentAcknowledged }, set: { model.consentAcknowledged = $0 }))
                        .font(.subheadline)
                        .tint(NomiTheme.course)

                    Text("La diarisation des locuteurs n'est pas exposée nativement par Apple Speech. Le prototype regroupe sous « Enseignant/Locuteur » ; une stratégie future devra combiner segmentation audio et validation humaine.")
                        .font(.footnote)
                        .foregroundStyle(NomiTheme.ink2)
                }
                .nomiCard(tint: NomiTheme.course)

                VStack(spacing: 16) {
                    TimelineView(.periodic(from: .now, by: 0.5)) { _ in
                        Text(formatDuration(model.elapsed))
                            .font(.system(size: 48, weight: .bold, design: .rounded))
                            .monospacedDigit()
                            .foregroundStyle(model.state == .recording ? NomiTheme.course : NomiTheme.ink)
                    }
                    HStack(spacing: 6) {
                        ForEach(0..<18, id: \.self) { index in
                            RoundedRectangle(cornerRadius: 3)
                                .fill(model.state == .recording ? NomiTheme.course : NomiTheme.line)
                                .frame(width: 5, height: CGFloat(10 + ((index * 7) % 28)))
                        }
                    }
                    .frame(height: 42)
                    .accessibilityHidden(true)

                    HStack(spacing: 10) {
                        if model.state == .recording {
                            Button("Pause", systemImage: "pause.fill") { model.pause() }
                                .buttonStyle(.bordered)
                            Button("Arrêter", systemImage: "stop.fill") { model.stop() }
                                .buttonStyle(.borderedProminent)
                        } else if model.state == .paused {
                            Button("Reprendre", systemImage: "play.fill") { Task { await model.resume() } }
                                .buttonStyle(.borderedProminent)
                            Button("Arrêter", systemImage: "stop.fill") { model.stop() }
                                .buttonStyle(.bordered)
                        } else {
                            Button("Démarrer", systemImage: "record.circle") {
                                Task { await model.start() }
                            }
                            .buttonStyle(.borderedProminent)
                            .controlSize(.large)
                        }
                    }
                    .frame(minHeight: 44)

                    if case .error(let message) = model.state {
                        Label(message, systemImage: "exclamationmark.triangle")
                            .font(.footnote)
                            .foregroundStyle(.orange)
                    }
                    if let interruption = model.interruptionMessage {
                        Label(interruption, systemImage: "bell.slash")
                            .font(.footnote)
                            .foregroundStyle(NomiTheme.warning)
                    }
                }
                .frame(maxWidth: .infinity)
                .nomiCard(tint: NomiTheme.course)

                VStack(alignment: .leading, spacing: 12) {
                    HStack {
                        NomiSectionTitle("Transcription", subtitle: "\(model.segments.count) segment(s), horodatés.")
                        Spacer()
                        if !model.segments.isEmpty {
                            NavigationLink("Fiche") {
                                RevisionSheetView(sourceText: model.transcript.plainText, sourceTitle: model.transcript.title)
                            }
                            .buttonStyle(.borderedProminent)
                        }
                    }
                    if model.segments.isEmpty {
                        ContentUnavailableView("Aucun segment", systemImage: "waveform", description: Text("Choisissez la démonstration sur simulateur ou le microphone réel sur appareil."))
                    } else {
                        ForEach(model.segments) { segment in
                            VStack(alignment: .leading, spacing: 5) {
                                HStack {
                                    Text(formatDuration(segment.start))
                                        .font(.caption.monospacedDigit())
                                        .foregroundStyle(NomiTheme.course)
                                    Text(segment.speaker ?? "Locuteur")
                                        .font(.caption.weight(.semibold))
                                        .foregroundStyle(NomiTheme.ink2)
                                    if !segment.isFinal { NomiBadge("en cours", tint: NomiTheme.warning) }
                                }
                                Text(segment.text)
                                    .font(.body)
                                    .foregroundStyle(NomiTheme.ink)
                                    .fixedSize(horizontal: false, vertical: true)
                            }
                            .padding(.vertical, 7)
                            .accessibilityElement(children: .combine)
                        }
                    }
                }
                .nomiCard(tint: NomiTheme.course)

                VStack(alignment: .leading, spacing: 8) {
                    Label("Résumé en direct", systemImage: "sparkles")
                        .font(.headline)
                    Text("Points clés, définitions et actions seront produits par le même service IA que les fiches. Dans ce prototype, le résumé est généré après arrêt plutôt qu'en continu pour limiter batterie et coûts.")
                        .font(.subheadline)
                        .foregroundStyle(NomiTheme.ink2)
                }
                .nomiCard(tint: NomiTheme.accent)
            }
            .padding()
        }
        .nomiScreenBackground()
        .navigationTitle("Cours")
        .onDisappear {
            model.stop()
        }
    }

    private func formatDuration(_ value: TimeInterval) -> String {
        let total = Int(value)
        return String(format: "%02d:%02d", total / 60, total % 60)
    }
}
