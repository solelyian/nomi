import SwiftUI
import NomiCore

struct HomeView: View {
    @EnvironmentObject private var state: AppState
    @State private var commandPresented = false
    @State private var showRescueConfirmation = false
    @State private var rescueSummary = ""

    var body: some View {
        GeometryReader { geometry in
            ZStack {
                NomiAmbientBackground()
                ScrollView {
                    VStack(alignment: .leading, spacing: 0) {
                        header
                        commandBar
                            .padding(.top, 14)
                        if geometry.size.width >= 680 {
                            LazyVGrid(columns: [GridItem(.flexible(), spacing: 14), GridItem(.flexible(), spacing: 14)], spacing: 14) {
                                nextAction.gridCellColumns(2)
                                prioritySection
                                trajectorySection
                                focusSection.gridCellColumns(2)
                            }
                            .padding(.top, 18)
                        } else {
                            nextAction.padding(.top, 18)
                            prioritySection.padding(.top, 18)
                            trajectorySection.padding(.top, 16)
                            focusSection.padding(.top, 16)
                        }
                    }
                    .frame(maxWidth: geometry.size.width >= 680 ? 1040 : .infinity, alignment: .leading)
                    .frame(maxWidth: .infinity)
                    .padding(.horizontal, geometry.size.width >= 680 ? 24 : 18)
                    .padding(.top, 12)
                    .padding(.bottom, 34)
                }
                .disabled(commandPresented)

                if commandPresented {
                    NomiCommandPalette(isPresented: $commandPresented)
                        .transition(.opacity.combined(with: .move(edge: .top)))
                        .zIndex(2)
                }
            }
            .animation(.spring(response: 0.24, dampingFraction: 0.86), value: commandPresented)
        }
        .confirmationDialog("Sauver ma journée ?", isPresented: $showRescueConfirmation, titleVisibility: .visible) {
            Button("Reporter les tâches normales", role: .destructive) {
                rescueSummary = state.rescueDay()
            }
            Button("Annuler", role: .cancel) {}
        } message: {
            Text("Les tâches normales sont reportées à demain ; les priorités hautes restent visibles.")
        }
    }

    private var greeting: String {
        let hour = Calendar.current.component(.hour, from: .now)
        if hour < 12 { return "Bonjour." }
        if hour < 18 { return "Bel après-midi." }
        return "Bonsoir."
    }

    private var rankedTasks: [WorkTask] {
        state.openTasks.sorted(by: { $0.priority > $1.priority })
    }

    private var header: some View {
        HStack(alignment: .center) {
            VStack(alignment: .leading, spacing: 4) {
                Text(Date.now.formatted(date: .complete, time: .omitted).uppercased())
                    .font(.caption2.weight(.bold))
                    .tracking(1.1)
                    .foregroundStyle(NomiTheme.ink3)
                Text(greeting)
                    .font(.system(size: 34, weight: .semibold, design: .serif))
                    .tracking(-0.8)
                    .foregroundStyle(NomiTheme.ink)
                Text("\(state.openTasks.count) intention(s) ouverte(s) · \(state.focusHistory.count) focus terminé(s)")
                    .font(.caption)
                    .foregroundStyle(NomiTheme.ink2)
            }
            Spacer()
            Text("n.")
                .font(.system(size: 18, weight: .bold, design: .serif))
                .foregroundStyle(.white)
                .frame(width: 31, height: 31)
                .background(NomiTheme.accent, in: RoundedRectangle(cornerRadius: 9, style: .continuous))
        }
    }

    private var commandBar: some View {
        Button { commandPresented = true } label: {
            HStack(spacing: 9) {
                Image(systemName: "magnifyingglass")
                    .font(.system(size: 14, weight: .medium))
                    .foregroundStyle(NomiTheme.ink2)
                Text("Créer, chercher ou demander…")
                    .font(.subheadline)
                    .foregroundStyle(NomiTheme.ink3)
                Spacer()
                Text("⌘K")
                    .font(.caption2.monospaced())
                    .foregroundStyle(NomiTheme.ink2)
                    .padding(.horizontal, 5)
                    .padding(.vertical, 3)
                    .background(NomiTheme.surface2.opacity(0.62), in: RoundedRectangle(cornerRadius: 5, style: .continuous))
                    .overlay(RoundedRectangle(cornerRadius: 5, style: .continuous).stroke(NomiTheme.line.opacity(0.7), lineWidth: 0.7))
            }
            .padding(.horizontal, 11)
            .frame(height: 46)
            .background(NomiTheme.surface.opacity(0.96), in: RoundedRectangle(cornerRadius: 13, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 13, style: .continuous).stroke(NomiTheme.line.opacity(0.88), lineWidth: 0.8))
        }
        .buttonStyle(.plain)
        .accessibilityHint("Ouvre la commande universelle")
    }

    private var nextAction: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("PROCHAINE ACTION")
                .font(.caption2.weight(.bold))
                .tracking(1.0)
                .foregroundStyle(NomiTheme.accent)
            if let task = rankedTasks.first {
                Text(task.title)
                    .font(.subheadline.weight(.semibold))
                    .foregroundStyle(NomiTheme.ink)
                    .fixedSize(horizontal: false, vertical: true)
                Text("Priorité \(task.priority.frenchTitle.lowercased()) · \(task.estimateMinutes.map { "\($0) min" } ?? "durée libre")")
                    .font(.caption)
                    .foregroundStyle(NomiTheme.ink2)
                HStack(spacing: 7) {
                    Button("Démarrer") { state.startFocus(objective: task.title) }
                        .buttonStyle(.nomiPrimary)
                    Button("Terminer") { state.moveTask(task, to: .done) }
                        .buttonStyle(.nomiSecondary)
                    Button("Sauver") { showRescueConfirmation = true }
                        .buttonStyle(.nomiSecondary)
                }
                if !rescueSummary.isEmpty {
                    Text(rescueSummary)
                        .font(.caption2)
                        .foregroundStyle(NomiTheme.ink2)
                }
            } else {
                Text("Aucune tâche ouverte")
                    .font(.subheadline.weight(.semibold))
                Text("Utilisez la commande ou le tableau pour créer une intention concrète.")
                    .font(.caption)
                    .foregroundStyle(NomiTheme.ink2)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .nomiCard(tint: NomiTheme.accent)
    }

    private var prioritySection: some View {
        VStack(alignment: .leading, spacing: 6) {
            sectionHeader("Priorités", detail: "\(min(3, rankedTasks.count)) visibles")
            VStack(spacing: 0) {
                ForEach(Array(rankedTasks.prefix(3).enumerated()), id: \.element.id) { index, task in
                    if index > 0 { Divider().overlay(NomiTheme.line.opacity(0.6)).padding(.leading, 42) }
                    HomeTaskRow(task: task) {
                        state.moveTask(task, to: .done)
                    }
                }
                if rankedTasks.isEmpty {
                    Text("Aucune priorité ouverte.")
                        .font(.caption)
                        .foregroundStyle(NomiTheme.ink3)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(12)
                }
            }
            .background(NomiTheme.surface.opacity(0.93), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.7))
        }
    }

    private var trajectorySection: some View {
        let events = state.agenda.filter { $0.end > .now }.sorted { $0.start < $1.start }.prefix(2)
        return VStack(alignment: .leading, spacing: 6) {
            sectionHeader("Trajectoire", detail: "\(events.count) événement(s)")
            VStack(spacing: 0) {
                ForEach(Array(events.enumerated()), id: \.element.id) { index, event in
                    if index > 0 { Divider().overlay(NomiTheme.line.opacity(0.6)).padding(.leading, 42) }
                    HStack(spacing: 9) {
                        Image(systemName: "clock")
                            .font(.system(size: 11, weight: .medium))
                            .foregroundStyle(NomiTheme.ink2)
                            .frame(width: 23, height: 23)
                            .background(NomiTheme.surface2.opacity(0.55), in: RoundedRectangle(cornerRadius: 7, style: .continuous))
                        VStack(alignment: .leading, spacing: 2) {
                            Text(event.title).font(.subheadline.weight(.medium)).lineLimit(1)
                            Text("\(event.start.formatted(date: .omitted, time: .shortened))–\(event.end.formatted(date: .omitted, time: .shortened)) · \(event.location ?? "sans lieu")")
                                .font(.caption2)
                                .foregroundStyle(NomiTheme.ink3)
                                .lineLimit(1)
                        }
                        Spacer(minLength: 8)
                        Text(event.start.formatted(date: .omitted, time: .shortened))
                            .font(.caption2.monospaced())
                            .foregroundStyle(NomiTheme.ink3)
                    }
                    .padding(.horizontal, 10)
                    .frame(minHeight: 40)
                }
                if events.isEmpty {
                    Text("Aucun événement à venir aujourd’hui.")
                        .font(.caption)
                        .foregroundStyle(NomiTheme.ink3)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(12)
                }
            }
            .background(NomiTheme.surface.opacity(0.93), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.7))
        }
    }

    private var focusSection: some View {
        VStack(alignment: .leading, spacing: 6) {
            sectionHeader("Focus actif", detail: state.focusSession?.status.frenchTitle.uppercased() ?? "INACTIF")
            if let session = state.focusSession {
                TimelineView(.periodic(from: .now, by: 1)) { context in
                    HStack(spacing: 13) {
                        VStack(alignment: .leading, spacing: 5) {
                            Text(duration(session, at: context.date))
                                .font(.system(size: 29, weight: .light, design: .monospaced))
                                .tracking(-1.2)
                                .foregroundStyle(NomiTheme.ink)
                            Text(session.objective)
                                .font(.caption)
                                .foregroundStyle(NomiTheme.ink2)
                                .lineLimit(1)
                        }
                        Spacer()
                        Button {
                            session.status == .running ? state.pauseFocus() : state.resumeFocus()
                        } label: {
                            Image(systemName: session.status == .running ? "pause.fill" : "play.fill")
                                .font(.system(size: 13, weight: .semibold))
                                .foregroundStyle(NomiTheme.ink)
                                .frame(width: 38, height: 38)
                                .background(NomiTheme.surface2.opacity(0.65), in: Circle())
                                .overlay(Circle().stroke(NomiTheme.line.opacity(0.75), lineWidth: 0.8))
                        }
                        .buttonStyle(.plain)
                    }
                    .padding(12)
                    .background(NomiTheme.surface.opacity(0.93), in: RoundedRectangle(cornerRadius: 15, style: .continuous))
                    .overlay(RoundedRectangle(cornerRadius: 15, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.8))
                }
            } else {
                Text("Aucun focus actif. Démarrez depuis la prochaine action ou la commande.")
                    .font(.caption)
                    .foregroundStyle(NomiTheme.ink3)
                    .padding(12)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(NomiTheme.surface.opacity(0.82), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                    .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.68), lineWidth: 0.7))
            }
        }
    }

    private func sectionHeader(_ title: String, detail: String) -> some View {
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

    private func duration(_ session: FocusSession, at date: Date) -> String {
        let activePause = session.status == .paused ? date.timeIntervalSince(session.pausedAt ?? date) : 0
        let seconds = max(0, Int(date.timeIntervalSince(session.startedAt) - session.pausedSeconds - activePause))
        if seconds < 3600 { return String(format: "%02d:%02d", seconds / 60, seconds % 60) }
        return String(format: "%02d:%02d:%02d", seconds / 3600, seconds % 3600 / 60, seconds % 60)
    }
}

private struct HomeTaskRow: View {
    let task: WorkTask
    let complete: () -> Void

    var body: some View {
        HStack(spacing: 9) {
            Button(action: complete) {
                Image(systemName: "circle")
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(task.priority >= .high ? NomiTheme.accent : NomiTheme.ink2)
                    .frame(width: 23, height: 23)
                    .background((task.priority >= .high ? NomiTheme.accentSoft : NomiTheme.surface2.opacity(0.55)), in: RoundedRectangle(cornerRadius: 7, style: .continuous))
            }
            .buttonStyle(.plain)
            .accessibilityLabel("Terminer \(task.title)")
            VStack(alignment: .leading, spacing: 2) {
                Text(task.title)
                    .font(.subheadline.weight(.medium))
                    .foregroundStyle(NomiTheme.ink)
                    .lineLimit(1)
                Text(task.estimateMinutes.map { "\($0) min" } ?? "Durée libre")
                    .font(.caption2)
                    .foregroundStyle(NomiTheme.ink3)
                    .lineLimit(1)
            }
            Spacer(minLength: 8)
            Text(task.priority >= .high ? "P1" : "P2")
                .font(.caption2.monospaced().weight(task.priority >= .high ? .bold : .regular))
                .foregroundStyle(task.priority >= .high ? NomiTheme.accent : NomiTheme.ink3)
        }
        .padding(.horizontal, 10)
        .frame(minHeight: 42)
    }
}
