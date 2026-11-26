import SwiftUI
import NomiCore

struct HomeView: View {
    @EnvironmentObject private var state: AppState
    @Environment(\.horizontalSizeClass) private var horizontalSizeClass
    @State private var showRescueConfirmation = false
    @State private var rescueSummary = ""

    var body: some View {
        GeometryReader { geometry in
            let useTwoColumns = horizontalSizeClass == .regular && geometry.size.width >= 680
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    NomiScreenTitle(greeting, kicker: Date.now.formatted(date: .complete, time: .omitted), subtitle: "\(state.openTasks.count) tâche(s) ouverte(s) · \(state.focusHistory.count) focus terminé(s)")
                    if useTwoColumns {
                        LazyVGrid(columns: [GridItem(.flexible(), spacing: 16), GridItem(.flexible(), spacing: 16)], spacing: 16) {
                            dayHero.gridCellColumns(2)
                            nextAction.gridCellColumns(2)
                            agendaCard
                            focusCard
                            priorities
                            if let routine = state.readyRoutine {
                                routineCard(routine)
                            }
                        }
                    } else {
                        dayHero
                        nextAction
                        agendaCard
                        priorities
                        if let routine = state.readyRoutine { routineCard(routine) }
                        focusCard
                    }
                }
                .frame(maxWidth: useTwoColumns ? 1040 : .infinity)
                .frame(maxWidth: .infinity)
                .padding(.horizontal, 20)
                .padding(.top, 18)
                .padding(.bottom, 34)
            }
        }
        .nomiScreenBackground()
        .confirmationDialog("Sauver ma journée ?", isPresented: $showRescueConfirmation, titleVisibility: .visible) {
            Button("Reporter les tâches normales", role: .destructive) {
                rescueSummary = state.rescueDay()
            }
            Button("Annuler", role: .cancel) {}
        } message: {
            Text("Action déterministe : les tâches normales sont reportées à demain, les priorités hautes et urgentes restent visibles. Vous pouvez modifier chaque tâche ensuite.")
        }
    }

    private var greeting: String {
        let hour = Calendar.current.component(.hour, from: .now)
        if hour < 12 { return "Bonjour." }
        if hour < 18 { return "Bel après-midi." }
        return "Bonsoir."
    }

    private var dayProgress: Double {
        let total = max(1, state.dayEndHour - state.dayStartHour)
        let elapsed = min(max(0, Calendar.current.component(.hour, from: .now) - state.dayStartHour), total)
        return Double(elapsed) / Double(total)
    }

    private var dayHero: some View {
        HStack(spacing: 18) {
            VStack(alignment: .leading, spacing: 7) {
                Text("Votre journée avance")
                    .font(.subheadline.weight(.semibold))
                    .foregroundStyle(NomiTheme.ink2)
                Text(dayProgress < 1 ? "en douceur" : "presque terminée")
                    .font(.system(.title2, design: .rounded).weight(.bold))
                    .foregroundStyle(NomiTheme.ink)
                Text("Le rythme reste une indication, pas une injonction.")
                    .font(.footnote)
                    .foregroundStyle(NomiTheme.ink2)
            }
            Spacer(minLength: 8)
            NomiProgressRing(progress: dayProgress, tint: NomiTheme.accent, size: 82)
        }
        .nomiCard(tint: NomiTheme.accent)
    }

    private var nextAction: some View {
        VStack(alignment: .leading, spacing: 13) {
            HStack {
                NomiBadge("Prochaine action", tint: NomiTheme.accent)
                Spacer()
                Image(systemName: "sparkles")
                    .foregroundStyle(NomiTheme.accent)
            }
            if let task = state.openTasks.sorted(by: { $0.priority > $1.priority }).first {
                Text(task.title)
                    .font(.system(.title3, design: .rounded).weight(.bold))
                    .fixedSize(horizontal: false, vertical: true)
                Text("Priorité \(task.priority.frenchTitle.lowercased()) · \(task.estimateMinutes.map { "\($0) min" } ?? "durée libre")")
                    .font(.subheadline)
                    .foregroundStyle(NomiTheme.ink2)
                HStack(spacing: 10) {
                    Button("Démarrer un focus") { state.startFocus(objective: task.title) }
                        .buttonStyle(.nomiPrimary)
                    Button("Terminer") { state.moveTask(task, to: .done) }
                        .buttonStyle(.nomiSecondary)
                }
                Button("Sauver ma journée", systemImage: "lifepreserver") { showRescueConfirmation = true }
                    .buttonStyle(.nomiSecondary)
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
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Label("Moment suivant", systemImage: "calendar")
                    .font(.headline)
                Spacer()
                Image(systemName: "chevron.right")
                    .font(.caption.weight(.bold))
                    .foregroundStyle(NomiTheme.ink3)
            }
            if let event = state.nextAgendaEvent {
                HStack(alignment: .center, spacing: 12) {
                    Text(event.start.formatted(date: .omitted, time: .shortened))
                        .font(.caption.weight(.bold))
                        .foregroundStyle(NomiTheme.course)
                        .padding(.horizontal, 10)
                        .padding(.vertical, 7)
                        .background(NomiTheme.course.opacity(0.12), in: Capsule())
                    VStack(alignment: .leading, spacing: 3) {
                        Text(event.title).font(.body.weight(.semibold))
                        Text("\(event.end.formatted(date: .omitted, time: .shortened)) · \(event.location ?? "sans lieu")")
                            .font(.subheadline)
                            .foregroundStyle(NomiTheme.ink2)
                    }
                }
            } else {
                Text("Aucun événement à venir aujourd'hui.")
                    .foregroundStyle(NomiTheme.ink2)
            }
        }
        .nomiCard(tint: NomiTheme.course)
    }

    private var priorities: some View {
        VStack(alignment: .leading, spacing: 11) {
            NomiSectionTitle("Priorités", subtitle: "Les trois tâches importantes qui méritent votre attention.")
            ForEach(Array(state.openTasks.sorted(by: { $0.priority > $1.priority }).prefix(3).enumerated()), id: \.element.id) { index, task in
                HStack(spacing: 12) {
                    Text("\(index + 1)")
                        .font(.headline.weight(.bold))
                        .foregroundStyle(task.priority >= .high ? NomiTheme.accent : NomiTheme.ink3)
                        .frame(width: 34, height: 34)
                        .background((task.priority >= .high ? NomiTheme.accent : NomiTheme.ink3).opacity(0.11), in: Circle())
                    VStack(alignment: .leading, spacing: 3) {
                        Text(task.title).font(.body.weight(.semibold))
                        Text(task.estimateMinutes.map { "\($0) min" } ?? "Durée libre")
                            .font(.caption)
                            .foregroundStyle(NomiTheme.ink2)
                    }
                    Spacer()
                    NomiBadge(task.priority.frenchTitle, tint: task.priority >= .high ? NomiTheme.warning : NomiTheme.focus)
                }
                .padding(13)
                .background(NomiTheme.surface.opacity(0.46), in: RoundedRectangle(cornerRadius: 20, style: .continuous))
                .overlay(RoundedRectangle(cornerRadius: 20, style: .continuous).strokeBorder(NomiTheme.glassStroke.opacity(0.52), lineWidth: 1))
            }
        }
    }

    private func routineCard(_ routine: NomiRoutine) -> some View {
        VStack(alignment: .leading, spacing: 11) {
            HStack {
                Label("Routine prête", systemImage: "repeat.circle.fill")
                    .font(.headline)
                Spacer()
                NomiBadge("maintenant", tint: NomiTheme.success)
            }
            Text(routine.name).font(.title3.weight(.bold))
            Text(routine.action).foregroundStyle(NomiTheme.ink2)
            HStack(spacing: 10) {
                Button("Démarrer") { state.runRoutine(routine) }.buttonStyle(.nomiPrimary)
                Button("Plus tard") { state.snoozeRoutine(routine) }.buttonStyle(.nomiSecondary)
                Button("Ignorer") { state.skipRoutine(routine) }.buttonStyle(.nomiSecondary)
            }
        }
        .nomiCard(tint: NomiTheme.success)
    }

    private var focusCard: some View {
        HStack(spacing: 14) {
            ZStack {
                Circle()
                    .fill(NomiTheme.focus.opacity(0.13))
                    .frame(width: 48, height: 48)
                Image(systemName: "timer")
                    .foregroundStyle(NomiTheme.focus)
            }
            VStack(alignment: .leading, spacing: 4) {
                Text("Focus").font(.headline)
                if let session = state.focusSession {
                    Text(session.objective).font(.subheadline.weight(.semibold))
                    Text("Session \(session.status.frenchTitle.lowercased()) depuis \(session.startedAt.formatted(date: .omitted, time: .shortened))")
                        .font(.caption)
                        .foregroundStyle(NomiTheme.ink2)
                } else {
                    Text("Aucun focus actif. Choisissez une intention pour créer une bulle de travail.")
                        .font(.subheadline)
                        .foregroundStyle(NomiTheme.ink2)
                }
            }
            Spacer()
        }
        .nomiCard(tint: NomiTheme.focus)
    }
}
