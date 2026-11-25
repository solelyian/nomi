import SwiftUI
import NomiCore

struct HomeView: View {
    @EnvironmentObject private var state: AppState
    @State private var showRescueConfirmation = false
    @State private var rescueSummary = ""

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                header
                nextAction
                agendaCard
                priorities
                if let routine = state.readyRoutine { routineCard(routine) }
                focusCard
            }
            .padding()
        }
        .nomiScreenBackground()
        .navigationTitle("Aujourd'hui")
        .confirmationDialog("Sauver ma journée ?", isPresented: $showRescueConfirmation, titleVisibility: .visible) {
            Button("Reporter les tâches normales", role: .destructive) {
                rescueSummary = state.rescueDay()
            }
            Button("Annuler", role: .cancel) {}
        } message: {
            Text("Action déterministe : les tâches normales sont reportées à demain, les priorités hautes et urgentes restent visibles. Vous pouvez modifier chaque tâche ensuite.")
        }
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(Date.now.formatted(date: .complete, time: .omitted))
                .font(.subheadline)
                .foregroundStyle(NomiTheme.ink2)
            HStack {
                VStack(alignment: .leading) {
                    Text("Votre journée")
                        .font(.largeTitle.weight(.bold))
                    Text("\(state.openTasks.count) tâches ouvertes · \(state.focusHistory.count) focus terminés")
                        .foregroundStyle(NomiTheme.ink2)
                }
                Spacer()
                ProgressView(value: dayProgress)
                    .tint(NomiTheme.accent)
                    .frame(width: 82)
            }
        }
    }

    private var dayProgress: Double {
        let total = max(1, state.dayEndHour - state.dayStartHour)
        let elapsed = min(max(0, Calendar.current.component(.hour, from: .now) - state.dayStartHour), total)
        return Double(elapsed) / Double(total)
    }

    private var nextAction: some View {
        VStack(alignment: .leading, spacing: 10) {
            Label("Prochaine action recommandée", systemImage: "sparkles")
                .font(.headline)
            if let task = state.openTasks.sorted(by: { $0.priority > $1.priority }).first {
                Text(task.title).font(.title3.weight(.semibold))
                Text("Priorité \(task.priority.frenchTitle.lowercased()) · \(task.estimateMinutes.map { "\($0) min" } ?? "durée libre")")
                    .font(.subheadline)
                    .foregroundStyle(NomiTheme.ink2)
                HStack {
                    Button("Démarrer un focus") { state.startFocus(objective: task.title) }
                        .buttonStyle(.borderedProminent)
                    Button("Terminer") { state.moveTask(task, to: .done) }
                        .buttonStyle(.bordered)
                }
                Button("Sauver ma journée", systemImage: "lifepreserver") { showRescueConfirmation = true }
                    .buttonStyle(.bordered)
                if !rescueSummary.isEmpty {
                    Text(rescueSummary)
                        .font(.caption)
                        .foregroundStyle(NomiTheme.ink2)
                }
            } else {
                Text("Aucune tâche ouverte. Ajoutez une intention dans le tableau.")
                    .foregroundStyle(NomiTheme.ink2)
            }
        }
        .nomiCard(tint: NomiTheme.accent)
    }

    private var agendaCard: some View {
        VStack(alignment: .leading, spacing: 8) {
            Label("Agenda", systemImage: "calendar")
                .font(.headline)
            if let event = state.nextAgendaEvent {
                Text(event.title).font(.body.weight(.semibold))
                Text("\(event.start.formatted(date: .omitted, time: .shortened))–\(event.end.formatted(date: .omitted, time: .shortened)) · \(event.location ?? "sans lieu")")
                    .font(.subheadline)
                    .foregroundStyle(NomiTheme.ink2)
            } else {
                Text("Aucun événement à venir aujourd'hui.")
                    .foregroundStyle(NomiTheme.ink2)
            }
        }
        .nomiCard(tint: NomiTheme.course)
    }

    private var priorities: some View {
        VStack(alignment: .leading, spacing: 10) {
            NomiSectionTitle("Priorités", subtitle: "Les trois tâches les plus importantes ouvertes.")
            ForEach(Array(state.openTasks.sorted(by: { $0.priority > $1.priority }).prefix(3))) { task in
                HStack {
                    Image(systemName: task.priority == .urgent ? "exclamationmark.circle.fill" : "circle")
                        .foregroundStyle(task.priority == .urgent ? NomiTheme.warning : NomiTheme.ink3)
                    VStack(alignment: .leading) {
                        Text(task.title)
                        Text(task.estimateMinutes.map { "\($0) min" } ?? "Durée libre")
                            .font(.caption)
                            .foregroundStyle(NomiTheme.ink2)
                    }
                    Spacer()
                }
                .padding(10)
                .background(NomiTheme.surface2, in: RoundedRectangle(cornerRadius: 12, style: .continuous))
            }
        }
    }

    private func routineCard(_ routine: NomiRoutine) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            Label("Routine prête", systemImage: "repeat.circle.fill")
                .font(.headline)
            Text(routine.name).font(.title3.weight(.semibold))
            Text(routine.action).foregroundStyle(NomiTheme.ink2)
            HStack {
                Button("Démarrer") { state.runRoutine(routine) }.buttonStyle(.borderedProminent)
                Button("Plus tard") { state.snoozeRoutine(routine) }.buttonStyle(.bordered)
                Button("Ignorer") { state.skipRoutine(routine) }.buttonStyle(.borderless)
            }
        }
        .nomiCard(tint: NomiTheme.success)
    }

    private var focusCard: some View {
        VStack(alignment: .leading, spacing: 8) {
            Label("Focus", systemImage: "timer")
                .font(.headline)
            if let session = state.focusSession {
                Text(session.objective)
                Text("Session \(session.status.frenchTitle.lowercased()) depuis \(session.startedAt.formatted(date: .omitted, time: .shortened))")
                    .font(.subheadline)
                    .foregroundStyle(NomiTheme.ink2)
            } else {
                Text("Aucun focus actif. Démarrez depuis la prochaine action ou l'écran Focus.")
                    .foregroundStyle(NomiTheme.ink2)
            }
        }
        .nomiCard(tint: NomiTheme.code)
    }
}
