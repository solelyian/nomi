import SwiftUI
import NomiCore

struct LiveTranscriptionView: View {
    @EnvironmentObject private var state: AppState

    var body: some View {
        LiveTranscriptionContent(model: state.liveTranscriptionModel)
    }
}

private struct LiveTranscriptionContent: View {
    @ObservedObject var model: LiveTranscriptionViewModel

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                header
                recorder
                consentPanel
                transcriptList
                summaryNote
            }
            .padding(.horizontal, 18)
            .padding(.top, 14)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
        .nomiChildNavigation()
        .navigationTitle("")
        .onDisappear { model.stop() }
        .onReceive(NotificationCenter.default.publisher(for: .nomiCompactTabDidChange)) { notification in
            if (notification.object as? CompactTab) != .more { model.stop() }
        }
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Transcription")
                .font(.system(.largeTitle, design: .serif).weight(.semibold))
                .tracking(-0.7)
                .foregroundStyle(NomiTheme.ink)
            Text("Enregistrement explicite · horodatage local")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
        }
    }

    private var recorder: some View {
        VStack(alignment: .leading, spacing: 13) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 2) {
                    Text("ENREGISTREMENT")
                        .font(.caption2.weight(.bold))
                        .tracking(1.1)
                        .foregroundStyle(NomiTheme.ink3)
                    TimelineView(.periodic(from: .now, by: 0.5)) { _ in
                        Text(formatDuration(model.elapsed))
                            .font(.system(size: 42, weight: .light, design: .monospaced))
                            .tracking(-1.6)
                            .foregroundStyle(NomiTheme.ink)
                    }
                }
                Spacer()
                Circle()
                    .fill(model.state == .recording ? NomiTheme.accent : NomiTheme.ink3.opacity(0.35))
                    .frame(width: 9, height: 9)
                    .overlay(Circle().stroke(NomiTheme.accent.opacity(model.state == .recording ? 0.26 : 0), lineWidth: 5))
                    .accessibilityHidden(true)
            }

            Picker("Mode", selection: Binding(get: { model.mode }, set: { model.mode = $0 })) {
                ForEach(LiveTranscriptionViewModel.Mode.allCases) { mode in
                    Text(mode.rawValue).tag(mode)
                }
            }
            .pickerStyle(.segmented)
            .disabled(model.state == .recording || model.state == .paused)

            HStack(spacing: 7) {
                if model.state == .recording {
                    Button("Pause", systemImage: "pause.fill") { model.pause() }
                        .buttonStyle(.nomiSecondary)
                    Button("Arrêter", systemImage: "stop.fill") { model.stop() }
                        .buttonStyle(.nomiPrimary)
                } else if model.state == .paused {
                    Button("Reprendre", systemImage: "play.fill") { Task { await model.resume() } }
                        .buttonStyle(.nomiPrimary)
                    Button("Arrêter", systemImage: "stop.fill") { model.stop() }
                        .buttonStyle(.nomiSecondary)
                } else {
                    Button("Démarrer", systemImage: "record.circle") { Task { await model.start() } }
                        .buttonStyle(.nomiPrimary)
                        .disabled(!model.consentAcknowledged)
                }
            }

            if !model.consentAcknowledged, model.state != .recording, model.state != .paused {
                Text("Confirmez le consentement avant de démarrer.")
                    .font(.caption2)
                    .foregroundStyle(NomiTheme.ink3)
            }
            if case .error(let message) = model.state {
                Label(message, systemImage: "exclamationmark.triangle")
                    .font(.caption)
                    .foregroundStyle(.orange)
            }
            if let interruption = model.interruptionMessage {
                Label(interruption, systemImage: "bell.slash")
                    .font(.caption)
                    .foregroundStyle(NomiTheme.warning)
            }
        }
        .padding(13)
        .background(NomiTheme.surface.opacity(0.94), in: RoundedRectangle(cornerRadius: 15, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 15, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.8))
    }

    private var consentPanel: some View {
        VStack(alignment: .leading, spacing: 8) {
            Toggle("Enregistrement autorisé", isOn: Binding(get: { model.consentAcknowledged }, set: { model.consentAcknowledged = $0 }))
                .font(.subheadline.weight(.medium))
                .tint(NomiTheme.accent)
            Text("Apple Speech n’expose pas de diarisation fiable ; les segments restent regroupés sous « Enseignant/Locuteur ».")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
                .fixedSize(horizontal: false, vertical: true)
        }
        .padding(12)
        .background(NomiTheme.surface.opacity(0.72), in: RoundedRectangle(cornerRadius: 13, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 13, style: .continuous).stroke(NomiTheme.line.opacity(0.68), lineWidth: 0.7))
    }

    private var transcriptList: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .firstTextBaseline) {
                Text("SEGMENTS")
                    .font(.caption2.weight(.bold))
                    .tracking(1.1)
                    .foregroundStyle(NomiTheme.ink3)
                Spacer()
                Text("\(model.segments.count)")
                    .font(.caption2.monospaced())
                    .foregroundStyle(NomiTheme.ink3)
                if !model.segments.isEmpty {
                    NavigationLink("Fiche") {
                        RevisionSheetView(sourceText: model.transcript.plainText, sourceTitle: model.transcript.title)
                    }
                    .font(.caption.weight(.semibold))
                }
            }
            .padding(.horizontal, 1)

            VStack(spacing: 0) {
                ForEach(Array(model.segments.enumerated()), id: \.element.id) { index, segment in
                    if index > 0 { Divider().overlay(NomiTheme.line.opacity(0.6)).padding(.leading, 64) }
                    segmentRow(segment)
                }
                if model.segments.isEmpty {
                    VStack(alignment: .leading, spacing: 5) {
                        Text("Aucun segment")
                            .font(.subheadline.weight(.medium))
                            .foregroundStyle(NomiTheme.ink)
                        Text("Démarrez une démonstration sur simulateur ou le microphone sur appareil.")
                            .font(.caption)
                            .foregroundStyle(NomiTheme.ink3)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(12)
                }
            }
            .background(NomiTheme.surface.opacity(0.93), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.7))
        }
    }

    private func segmentRow(_ segment: TranscriptSegment) -> some View {
        HStack(alignment: .top, spacing: 10) {
            Text(formatDuration(segment.start))
                .font(.caption2.monospaced())
                .foregroundStyle(NomiTheme.accent)
                .frame(width: 43, alignment: .leading)
                .padding(.top, 2)
            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 6) {
                    Text(segment.speaker ?? "Locuteur")
                        .font(.caption2.weight(.semibold))
                        .foregroundStyle(NomiTheme.ink2)
                    if !segment.isFinal {
                        Text("EN COURS")
                            .font(.system(size: 8, weight: .bold, design: .monospaced))
                            .tracking(0.5)
                            .foregroundStyle(NomiTheme.warning)
                    }
                }
                Text(segment.text)
                    .font(.subheadline)
                    .foregroundStyle(NomiTheme.ink)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.horizontal, 10)
        .padding(.vertical, 9)
        .accessibilityElement(children: .combine)
    }

    private var summaryNote: some View {
        HStack(alignment: .top, spacing: 9) {
            Image(systemName: "sparkles")
                .font(.system(size: 12, weight: .medium))
                .foregroundStyle(NomiTheme.accent)
                .frame(width: 23, height: 23)
                .background(NomiTheme.accentSoft, in: RoundedRectangle(cornerRadius: 7, style: .continuous))
            Text("Le résumé est généré après arrêt, plutôt qu’en continu, pour limiter batterie et traitement local.")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
                .fixedSize(horizontal: false, vertical: true)
        }
        .padding(10)
        .background(NomiTheme.surface.opacity(0.64), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
    }

    private func formatDuration(_ value: TimeInterval) -> String {
        let total = Int(value)
        return String(format: "%02d:%02d", total / 60, total % 60)
    }
}
