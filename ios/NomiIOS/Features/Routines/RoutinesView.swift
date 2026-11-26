import SwiftUI
import NomiCore

struct RoutinesView: View {
    @EnvironmentObject private var state: AppState
    @State private var phrase = ""
    @State private var editingRoutine: NomiRoutine?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                NomiScreenTitle("Routines", subtitle: "Automatisations explicites, jamais silencieuses. Elles restent visibles et modifiables.")
                composer
                if let ready = state.readyRoutine { readyCard(ready) }
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 320), spacing: 14)], spacing: 14) {
                    ForEach(state.routines) { routine in
                        routineCard(routine)
                    }
                }
            }
            .padding(.horizontal, 20)
            .padding(.top, 18)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
        .nomiChildNavigation()
        .navigationTitle("")
        .sheet(item: $editingRoutine) { routine in
            RoutineDetailView(routine: routine) { updated in state.updateRoutine(updated) }
        }
    }

    private var composer: some View {
        VStack(alignment: .leading, spacing: 12) {
            Label("Créer une routine", systemImage: "repeat.circle.fill")
                .font(.headline)
                .foregroundStyle(NomiTheme.success)
            TextField("Ex. Chaque matin, priorités", text: $phrase)
                .textFieldStyle(.plain)
                .nomiInput()
            HStack(alignment: .center, spacing: 12) {
                Button("Créer", systemImage: "plus") {
                    state.addRoutine(from: phrase)
                    phrase = ""
                }
                .buttonStyle(.nomiPrimary)
                .disabled(phrase.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                Text("Comprend matin/soir, jours, week-end et heures simples.")
                    .font(.caption)
                    .foregroundStyle(NomiTheme.ink2)
            }
        }
        .nomiCard(tint: NomiTheme.success)
    }

    private func readyCard(_ routine: NomiRoutine) -> some View {
        VStack(alignment: .leading, spacing: 11) {
            HStack {
                Label("Prête maintenant", systemImage: "bell.badge.fill")
                    .font(.headline)
                Spacer()
                NomiBadge("à décider", tint: NomiTheme.success)
            }
            Text(routine.name).font(.system(.title3, design: .rounded).weight(.bold))
            HStack(spacing: 10) {
                Button("Démarrer") { state.runRoutine(routine) }.buttonStyle(.nomiPrimary)
                Button("Plus tard") { state.snoozeRoutine(routine) }.buttonStyle(.nomiSecondary)
                Button("Ignorer") { state.skipRoutine(routine) }.buttonStyle(.nomiSecondary)
            }
        }
        .nomiCard(tint: NomiTheme.success)
    }

    private func routineCard(_ routine: NomiRoutine) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(alignment: .top, spacing: 12) {
                Image(systemName: routine.trigger == .time ? "clock.fill" : "bolt.fill")
                    .foregroundStyle(routine.enabled ? NomiTheme.success : NomiTheme.ink3)
                    .frame(width: 42, height: 42)
                    .background((routine.enabled ? NomiTheme.success : NomiTheme.ink3).opacity(0.12), in: RoundedRectangle(cornerRadius: 15, style: .continuous))
                VStack(alignment: .leading, spacing: 4) {
                    Text(routine.name)
                        .font(.system(.headline, design: .rounded).weight(.bold))
                    Text(routine.action)
                        .font(.subheadline)
                        .foregroundStyle(NomiTheme.ink2)
                        .fixedSize(horizontal: false, vertical: true)
                    Text("\(routine.trigger.frenchTitle) · \(String(format: "%02d:%02d", routine.hour, routine.minute))")
                        .font(.caption)
                        .foregroundStyle(NomiTheme.ink3)
                }
                Spacer(minLength: 4)
                Toggle("Activer \(routine.name)", isOn: Binding(get: { routine.enabled }, set: { value in
                    if let index = state.routines.firstIndex(where: { $0.id == routine.id }) {
                        state.routines[index].enabled = value
                    }
                }))
                .labelsHidden()
                .tint(NomiTheme.success)
                .accessibilityLabel("Activer la routine \(routine.name)")
            }
            if let document = routine.documentTitle {
                Label("Document lié : \(document)", systemImage: "doc.fill")
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(NomiTheme.focus)
                    .padding(.horizontal, 10)
                    .padding(.vertical, 7)
                    .background(NomiTheme.focus.opacity(0.11), in: Capsule())
            }
            LazyVGrid(columns: [GridItem(.adaptive(minimum: 108), spacing: 8)], spacing: 8) {
                Button("Modifier") { editingRoutine = routine }
                    .buttonStyle(.nomiSecondary)
                    .frame(maxWidth: .infinity)
                Button("Exécuter") { state.runRoutine(routine) }
                    .buttonStyle(.nomiPrimary)
                    .frame(maxWidth: .infinity)
                Button(routine.documentTitle == nil ? "Lier" : "Retirer") {
                    if let index = state.routines.firstIndex(where: { $0.id == routine.id }) {
                        state.routines[index].documentTitle = routine.documentTitle == nil ? "Document de démonstration" : nil
                    }
                }
                .buttonStyle(.nomiSecondary)
                .frame(maxWidth: .infinity)
                Button("Supprimer", role: .destructive) {
                    state.routines.removeAll { $0.id == routine.id }
                }
                .buttonStyle(.nomiSecondary)
                .tint(.red)
                .frame(maxWidth: .infinity)
            }
        }
        .nomiCard(tint: routine.enabled ? NomiTheme.success : NomiTheme.ink3)
    }
}


struct RoutineDetailView: View {
    @Environment(\.dismiss) private var dismiss
    @State private var routine: NomiRoutine
    let onSave: (NomiRoutine) -> Void

    init(routine: NomiRoutine, onSave: @escaping (NomiRoutine) -> Void) {
        _routine = State(initialValue: routine)
        self.onSave = onSave
    }

    var body: some View {
        NavigationStack {
            Form {
                Section("Routine") {
                    TextField("Nom", text: $routine.name)
                    TextField("Action", text: $routine.action, axis: .vertical)
                    Picker("Déclencheur", selection: $routine.trigger) {
                        ForEach(RoutineTrigger.allCases, id: \.self) { trigger in
                            Text(trigger.frenchTitle).tag(trigger)
                        }
                    }
                }
                if routine.trigger == .time {
                    Section("Heure et jours") {
                        DatePicker("Heure", selection: Binding(
                            get: {
                                Calendar.current.date(bySettingHour: routine.hour, minute: routine.minute, second: 0, of: .now) ?? .now
                            },
                            set: { date in
                                routine.hour = Calendar.current.component(.hour, from: date)
                                routine.minute = Calendar.current.component(.minute, from: date)
                            }
                        ), displayedComponents: .hourAndMinute)
                        ForEach(1...7, id: \.self) { weekday in
                            Toggle(weekdayName(weekday), isOn: Binding(
                                get: { routine.weekdays.contains(weekday) },
                                set: { enabled in
                                    if enabled { routine.weekdays.insert(weekday) } else { routine.weekdays.remove(weekday) }
                                }
                            ))
                        }
                    }
                }
                Section("Document") {
                    TextField("Document lié", text: Binding(
                        get: { routine.documentTitle ?? "" },
                        set: { routine.documentTitle = $0.isEmpty ? nil : $0 }
                    ))
                }
            }
            .navigationTitle("Routine")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button("Annuler") { dismiss() } }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Enregistrer") {
                        onSave(routine)
                        dismiss()
                    }
                    .disabled(routine.name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
            }
        }
    }

    private func weekdayName(_ weekday: Int) -> String {
        ["", "Dimanche", "Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi"][weekday]
    }
}
