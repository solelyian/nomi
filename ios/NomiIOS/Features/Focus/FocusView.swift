import SwiftUI
import NomiCore

struct FocusView: View {
    @EnvironmentObject private var state: AppState
    @State private var objective = ""
    @State private var notes = ""

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                NomiScreenTitle("Focus", subtitle: "Une bulle de travail explicite. iOS ne permet pas l'observation continue des autres apps comme Windows.")
                activeCard
                history
            }
            .padding(.horizontal, 20)
            .padding(.top, 18)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
    }

    private var activeCard: some View {
        VStack(spacing: 18) {
            if let session = state.focusSession {
                TimelineView(.periodic(from: .now, by: 1)) { context in
                    focusOrb(session: session, date: context.date)
                }
                Text(session.objective)
                    .font(.system(.title3, design: .rounded).weight(.bold))
                    .multilineTextAlignment(.center)
                NomiBadge(session.status.frenchTitle, tint: session.status == .running ? NomiTheme.success : NomiTheme.warning)
                TextField("Notes de contexte à conserver", text: $notes, axis: .vertical)
                    .textFieldStyle(.plain)
                    .nomiInput()
                HStack(spacing: 10) {
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
            } else {
                VStack(alignment: .leading, spacing: 12) {
                    Label("Créer une bulle de travail", systemImage: "timer")
                        .font(.headline)
                        .foregroundStyle(NomiTheme.focus)
                    TextField("Objectif du focus", text: $objective)
                        .textFieldStyle(.plain)
                        .nomiInput()
                    Button("Démarrer", systemImage: "play.fill") {
                        state.startFocus(objective: objective)
                        objective = ""
                    }
                    .buttonStyle(.nomiPrimary)
                    .controlSize(.large)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            }
        }
        .frame(maxWidth: .infinity)
        .nomiCard(tint: NomiTheme.focus)
    }

    private func focusOrb(session: FocusSession, date: Date) -> some View {
        let rotation = session.status == .running ? date.timeIntervalSince1970.truncatingRemainder(dividingBy: 45) / 45 * 360 : 0
        return ZStack {
            Circle()
                .stroke(NomiTheme.ink.opacity(0.08), lineWidth: 18)
            Circle()
                .stroke(NomiTheme.focusGradient, style: StrokeStyle(lineWidth: 18, lineCap: .round))
                .rotationEffect(.degrees(rotation))
                .shadow(color: NomiTheme.focus.opacity(0.24), radius: 18)
            Circle()
                .fill(.thinMaterial)
                .padding(24)
                .overlay {
                    Circle()
                        .stroke(NomiTheme.glassStroke.opacity(0.66), lineWidth: 1)
                        .padding(24)
                }
            VStack(spacing: 6) {
                Text(duration(session, at: date))
                    .font(.system(size: 45, weight: .bold, design: .rounded))
                    .monospacedDigit()
                    .foregroundStyle(NomiTheme.ink)
                Text(session.status == .running ? "respiration calme" : "en pause")
                    .font(.caption.weight(.bold))
                    .foregroundStyle(session.status == .running ? NomiTheme.focus : NomiTheme.warning)
            }
        }
        .frame(width: 252, height: 252)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Minuteur de focus")
        .accessibilityValue(duration(session, at: date))
    }

    private func duration(_ session: FocusSession, at date: Date) -> String {
        let activePause = session.status == .paused ? date.timeIntervalSince(session.pausedAt ?? date) : 0
        let seconds = max(0, Int(date.timeIntervalSince(session.startedAt) - session.pausedSeconds - activePause))
        if seconds < 3600 {
            return String(format: "%02d:%02d", seconds / 60, seconds % 60)
        }
        return String(format: "%02d:%02d:%02d", seconds / 3600, seconds % 3600 / 60, seconds % 60)
    }

    private var history: some View {
        VStack(alignment: .leading, spacing: 12) {
            NomiSectionTitle("Historique", subtitle: "Sessions terminées conservées localement dans ce prototype.")
            if state.focusHistory.isEmpty {
                Text("Aucune session terminée pour le moment. Votre prochain focus apparaîtra ici.")
                    .font(.footnote)
                    .foregroundStyle(NomiTheme.ink2)
            }
            ForEach(state.focusHistory) { session in
                HStack(alignment: .top, spacing: 12) {
                    Image(systemName: "checkmark.circle.fill")
                        .foregroundStyle(NomiTheme.success)
                        .frame(width: 28)
                    VStack(alignment: .leading, spacing: 4) {
                        Text(session.objective).font(.headline)
                        Text("Terminé à \(session.endedAt?.formatted(date: .omitted, time: .shortened) ?? "—")")
                            .font(.caption)
                            .foregroundStyle(NomiTheme.ink2)
                        if !session.notes.isEmpty {
                            Text(session.notes)
                                .font(.footnote)
                                .foregroundStyle(NomiTheme.ink2)
                                .fixedSize(horizontal: false, vertical: true)
                        }
                    }
                }
                .padding(13)
                .background(NomiTheme.surface.opacity(0.46), in: RoundedRectangle(cornerRadius: 20, style: .continuous))
                .overlay(RoundedRectangle(cornerRadius: 20, style: .continuous).strokeBorder(NomiTheme.glassStroke.opacity(0.52), lineWidth: 1))
            }
        }
    }
}
