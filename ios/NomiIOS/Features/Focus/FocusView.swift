import SwiftUI
import NomiCore

struct FocusView: View {
    @EnvironmentObject private var state: AppState
    @State private var objective = ""
    @State private var notes = ""

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                header
                activeSection
                suggestions
                history
            }
            .padding(.horizontal, 18)
            .padding(.top, 14)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Focus")
                .font(.system(size: 32, weight: .semibold, design: .serif))
                .tracking(-0.7)
                .foregroundStyle(NomiTheme.ink)
            Text("Minuteur local · objectif, pause et notes explicites")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
        }
    }

    private var activeSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            sectionLabel(state.focusSession == nil ? "Nouveau focus" : "Session active", detail: state.focusSession?.status.frenchTitle.uppercased() ?? "PRÊT")
            if let session = state.focusSession {
                TimelineView(.periodic(from: .now, by: 1)) { context in
                    VStack(alignment: .leading, spacing: 13) {
                        HStack(alignment: .firstTextBaseline) {
                            Text(duration(session, at: context.date))
                                .font(.system(size: 54, weight: .light, design: .monospaced))
                                .tracking(-2.4)
                                .foregroundStyle(NomiTheme.ink)
                            Spacer()
                            Text(session.status == .running ? "EN COURS" : "PAUSE")
                                .font(.caption2.monospaced().weight(.bold))
                                .foregroundStyle(session.status == .running ? NomiTheme.accent : NomiTheme.warning)
                        }
                        Text(session.objective)
                            .font(.subheadline.weight(.semibold))
                            .foregroundStyle(NomiTheme.ink)
                            .fixedSize(horizontal: false, vertical: true)
                        Rectangle()
                            .fill(NomiTheme.ink.opacity(0.08))
                            .frame(height: 3)
                            .overlay(alignment: .leading) {
                                Rectangle()
                                    .fill(NomiTheme.accent)
                                    .frame(width: 132)
                            }
                            .clipShape(Capsule())
                        TextField("Notes de contexte à conserver", text: $notes, axis: .vertical)
                            .textFieldStyle(.plain)
                            .font(.subheadline)
                            .padding(.horizontal, 11)
                            .frame(minHeight: 44)
                            .background(NomiTheme.surface2.opacity(0.45), in: RoundedRectangle(cornerRadius: 11, style: .continuous))
                            .overlay(RoundedRectangle(cornerRadius: 11, style: .continuous).stroke(NomiTheme.line.opacity(0.68), lineWidth: 0.7))
                        HStack(spacing: 8) {
                            if session.status == .running {
                                Button("Pause", systemImage: "pause.fill") { state.pauseFocus() }
                                    .buttonStyle(.nomiSecondary)
                            } else {
                                Button("Reprendre", systemImage: "play.fill") { state.resumeFocus() }
                                    .buttonStyle(.nomiSecondary)
                            }
                            Button("Terminer", systemImage: "checkmark") {
                                state.finishFocus(notes: notes)
                                notes = ""
                            }
                            .buttonStyle(.nomiPrimary)
                        }
                    }
                    .padding(14)
                    .background(NomiTheme.surface.opacity(0.94), in: RoundedRectangle(cornerRadius: 15, style: .continuous))
                    .overlay(RoundedRectangle(cornerRadius: 15, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.8))
                }
            } else {
                HStack(spacing: 9) {
                    Image(systemName: "timer")
                        .font(.system(size: 13, weight: .medium))
                        .foregroundStyle(NomiTheme.ink3)
                    TextField("Objectif du focus", text: $objective)
                        .textFieldStyle(.plain)
                        .font(.subheadline)
                        .submitLabel(.go)
                        .onSubmit(start)
                    Button("Démarrer", action: start)
                        .buttonStyle(.nomiPrimary)
                        .disabled(objective.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
                .padding(.horizontal, 11)
                .frame(height: 48)
                .background(NomiTheme.surface.opacity(0.94), in: RoundedRectangle(cornerRadius: 13, style: .continuous))
                .overlay(RoundedRectangle(cornerRadius: 13, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.8))
            }
        }
    }

    private var suggestions: some View {
        let tasks = Array(state.openTasks.prefix(3))
        return Group {
            if state.focusSession == nil && !tasks.isEmpty {
                VStack(alignment: .leading, spacing: 6) {
                    sectionLabel("Depuis le tableau", detail: "\(tasks.count)")
                    VStack(spacing: 0) {
                        ForEach(Array(tasks.enumerated()), id: \.element.id) { index, task in
                            if index > 0 { Divider().overlay(NomiTheme.line.opacity(0.6)).padding(.leading, 39) }
                            Button {
                                state.startFocus(objective: task.title)
                            } label: {
                                HStack(spacing: 9) {
                                    Image(systemName: "play")
                                        .font(.system(size: 10, weight: .bold))
                                        .foregroundStyle(NomiTheme.accent)
                                        .frame(width: 22, height: 22)
                                        .background(NomiTheme.accentSoft, in: RoundedRectangle(cornerRadius: 7, style: .continuous))
                                    Text(task.title)
                                        .font(.subheadline.weight(.medium))
                                        .foregroundStyle(NomiTheme.ink)
                                        .lineLimit(1)
                                    Spacer()
                                    Text(task.estimateMinutes.map { "\($0)m" } ?? "—")
                                        .font(.caption2.monospaced())
                                        .foregroundStyle(NomiTheme.ink3)
                                }
                                .padding(.horizontal, 10)
                                .frame(minHeight: 42)
                            }
                            .buttonStyle(.plain)
                        }
                    }
                    .background(NomiTheme.surface.opacity(0.9), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                    .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.78), lineWidth: 0.7))
                }
            }
        }
    }

    private var history: some View {
        VStack(alignment: .leading, spacing: 6) {
            sectionLabel("Historique", detail: "\(state.focusHistory.count)")
            VStack(spacing: 0) {
                ForEach(Array(state.focusHistory.prefix(5).enumerated()), id: \.element.id) { index, session in
                    if index > 0 { Divider().overlay(NomiTheme.line.opacity(0.6)).padding(.leading, 39) }
                    HStack(spacing: 9) {
                        Image(systemName: "checkmark")
                            .font(.system(size: 10, weight: .bold))
                            .foregroundStyle(NomiTheme.success)
                            .frame(width: 22, height: 22)
                            .background(NomiTheme.successSoft, in: RoundedRectangle(cornerRadius: 7, style: .continuous))
                        VStack(alignment: .leading, spacing: 2) {
                            Text(session.objective)
                                .font(.subheadline.weight(.medium))
                                .foregroundStyle(NomiTheme.ink)
                                .lineLimit(1)
                            if !session.notes.isEmpty {
                                Text(session.notes)
                                    .font(.caption2)
                                    .foregroundStyle(NomiTheme.ink3)
                                    .lineLimit(1)
                            }
                        }
                        Spacer()
                        Text(session.endedAt?.formatted(date: .omitted, time: .shortened) ?? "—")
                            .font(.caption2.monospaced())
                            .foregroundStyle(NomiTheme.ink3)
                    }
                    .padding(.horizontal, 10)
                    .frame(minHeight: 42)
                }
                if state.focusHistory.isEmpty {
                    Text("Aucune session terminée. Le prochain focus apparaîtra ici.")
                        .font(.caption)
                        .foregroundStyle(NomiTheme.ink3)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(12)
                }
            }
            .background(NomiTheme.surface.opacity(0.9), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.78), lineWidth: 0.7))
        }
    }

    private func sectionLabel(_ title: String, detail: String) -> some View {
        HStack(alignment: .firstTextBaseline) {
            Text(title.uppercased())
                .font(.caption2.weight(.bold))
                .tracking(1.1)
                .foregroundStyle(NomiTheme.ink3)
            Spacer()
            Text(detail)
                .font(.caption2.monospaced())
                .foregroundStyle(NomiTheme.ink3)
        }
        .padding(.horizontal, 1)
    }

    private func start() {
        let cleaned = objective.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleaned.isEmpty else { return }
        state.startFocus(objective: cleaned)
        objective = ""
    }

    private func duration(_ session: FocusSession, at date: Date) -> String {
        let activePause = session.status == .paused ? date.timeIntervalSince(session.pausedAt ?? date) : 0
        let seconds = max(0, Int(date.timeIntervalSince(session.startedAt) - session.pausedSeconds - activePause))
        if seconds < 3600 { return String(format: "%02d:%02d", seconds / 60, seconds % 60) }
        return String(format: "%02d:%02d:%02d", seconds / 3600, seconds % 3600 / 60, seconds % 60)
    }
}
