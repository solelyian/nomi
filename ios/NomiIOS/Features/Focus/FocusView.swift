import SwiftUI
import NomiCore

struct FocusView: View {
    @EnvironmentObject private var state: AppState
    @State private var objective = ""
    @State private var notes = ""

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                NomiSectionTitle("Focus", subtitle: "Version iOS adaptée : minuteur, objectif et notes explicites. iOS ne permet pas l'observation continue des autres apps comme Windows.")
                activeCard
                history
            }
            .padding()
        }
        .nomiScreenBackground()
        .navigationTitle("Focus")
    }

    private var activeCard: some View {
        VStack(alignment: .leading, spacing: 14) {
            if let session = state.focusSession {
                TimelineView(.periodic(from: .now, by: 1)) { context in
                    Text(duration(session, at: context.date))
                        .font(.system(size: 52, weight: .bold, design: .rounded))
                        .monospacedDigit()
                }
                Text(session.objective).font(.title3.weight(.semibold))
                NomiBadge(session.status.frenchTitle, tint: session.status == .running ? NomiTheme.success : NomiTheme.warning)
                TextField("Notes de contexte à conserver", text: $notes, axis: .vertical)
                    .textFieldStyle(.roundedBorder)
                HStack {
                    if session.status == .running {
                        Button("Pause") { state.pauseFocus() }.buttonStyle(.bordered)
                    } else {
                        Button("Reprendre") { state.resumeFocus() }.buttonStyle(.bordered)
                    }
                    Button("Terminer") { state.finishFocus(notes: notes); notes = "" }
                        .buttonStyle(.borderedProminent)
                }
            } else {
                TextField("Objectif du focus", text: $objective)
                    .textFieldStyle(.roundedBorder)
                Button("Démarrer", systemImage: "play.fill") {
                    state.startFocus(objective: objective)
                    objective = ""
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
            }
        }
        .nomiCard(tint: NomiTheme.code)
    }

    private func duration(_ session: FocusSession, at date: Date) -> String {
        let activePause = session.status == .paused ? date.timeIntervalSince(session.pausedAt ?? date) : 0
        let seconds = max(0, Int(date.timeIntervalSince(session.startedAt) - session.pausedSeconds - activePause))
        return String(format: "%02d:%02d:%02d", seconds / 3600, seconds % 3600 / 60, seconds % 60)
    }

    private var history: some View {
        VStack(alignment: .leading, spacing: 10) {
            NomiSectionTitle("Historique", subtitle: "Sessions terminées dans cette exécution du prototype.")
            if state.focusHistory.isEmpty {
                Text("Aucune session terminée pour le moment. L'historique est conservé localement dans ce prototype.")
                    .font(.footnote)
                    .foregroundStyle(NomiTheme.ink2)
            }
            ForEach(state.focusHistory) { session in
                VStack(alignment: .leading, spacing: 4) {
                    Text(session.objective).font(.headline)
                    Text("Terminé à \(session.endedAt?.formatted(date: .omitted, time: .shortened) ?? "—")")
                        .font(.caption)
                        .foregroundStyle(NomiTheme.ink2)
                    if !session.notes.isEmpty {
                        Text(session.notes).font(.footnote).foregroundStyle(NomiTheme.ink2)
                    }
                }
                .padding(10)
                .background(NomiTheme.surface2, in: RoundedRectangle(cornerRadius: 12, style: .continuous))
            }
        }
    }
}
