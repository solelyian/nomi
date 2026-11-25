import SwiftUI
import NomiCore

struct RoutinesView: View {
    @EnvironmentObject private var state: AppState
    @State private var phrase = ""
    @State private var editingRoutine: NomiRoutine?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                NomiSectionTitle("Routines", subtitle: "Automatisations explicites, jamais silencieuses. La création en langage naturel est simplifiée dans ce prototype.")
                composer
                if let ready = state.readyRoutine { readyCard(ready) }
                ForEach(state.routines) { routine in
                    routineCard(routine)
                }
            }
            .padding()
            .padding(.bottom, 72)
        }
        .nomiScreenBackground()
        .navigationTitle("Routines")
        .sheet(item: $editingRoutine) { routine in
            RoutineDetailView(routine: routine) { updated in state.updateRoutine(updated) }
        }
    }

    private var composer: some View {
        VStack(alignment: .leading, spacing: 10) {
            TextField("Ex. Chaque matin préparer mes priorités", text: $phrase)
                .textFieldStyle(.roundedBorder)
            Button("Créer la routine", systemImage: "plus") {
                state.addRoutine(from: phrase)
                phrase = ""
            }
            .buttonStyle(.borderedProminent)
            .disabled(phrase.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            Text("Le parseur local comprend matin/soir, chaque jour, week-end, un jour précis et une heure simple ; les déclencheurs contextuels complets de Windows restent à implémenter.")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
        }
        .nomiCard(tint: NomiTheme.success)
    }

    private func readyCard(_ routine: NomiRoutine) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            Label("Routine prête maintenant", systemImage: "bell.badge")
                .font(.headline)
            Text(routine.name).font(.title3.weight(.semibold))
            HStack {
                Button("Démarrer") { state.runRoutine(routine) }.buttonStyle(.borderedProminent)
                Button("Plus tard") { state.snoozeRoutine(routine) }.buttonStyle(.bordered)
                Button("Ignorer") { state.skipRoutine(routine) }.buttonStyle(.borderless)
            }
        }
        .nomiCard(tint: NomiTheme.accent)
    }

    private func routineCard(_ routine: NomiRoutine) -> some View {
        VStack(alignment: .leading, spacing: 9) {
            HStack {
                VStack(alignment: .leading, spacing: 4) {
                    Text(routine.name).font(.headline)
                    Text(routine.action).font(.subheadline).foregroundStyle(NomiTheme.ink2)
                    Text("\(routine.trigger.frenchTitle) · \(String(format: "%02d:%02d", routine.hour, routine.minute))")
                        .font(.caption)
                        .foregroundStyle(NomiTheme.ink3)
                }
                Spacer()
                Toggle("Activer \(routine.name)", isOn: Binding(get: { routine.enabled }, set: { value in
                    if let index = state.routines.firstIndex(where: { $0.id == routine.id }) {
                        state.routines[index].enabled = value
                    }
                }))
                .labelsHidden()
                .accessibilityLabel("Activer la routine \(routine.name)")
            }
            if let document = routine.documentTitle {
                Label("Document lié : \(document)", systemImage: "doc.fill")
                    .font(.caption)
                    .foregroundStyle(NomiTheme.code)
            }
            LazyVGrid(columns: [GridItem(.adaptive(minimum: 108), spacing: 8)], spacing: 8) {
                Button("Modifier") { editingRoutine = routine }
                    .buttonStyle(.bordered)
                    .frame(maxWidth: .infinity)
                Button("Exécuter") { state.runRoutine(routine) }
                    .buttonStyle(.bordered)
                    .frame(maxWidth: .infinity)
                Button(routine.documentTitle == nil ? "Lier" : "Retirer", systemImage: "doc") {
                    if let index = state.routines.firstIndex(where: { $0.id == routine.id }) {
                        state.routines[index].documentTitle = routine.documentTitle == nil ? "Document de démonstration" : nil
                    }
                }
                .buttonStyle(.bordered)
                .frame(maxWidth: .infinity)
                Button("Supprimer", role: .destructive) {
                    state.routines.removeAll { $0.id == routine.id }
                }
                .buttonStyle(.borderless)
                .frame(maxWidth: .infinity)
            }
        }
        .padding(12)
        .background(NomiTheme.surface, in: RoundedRectangle(cornerRadius: 16, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 16, style: .continuous).stroke(NomiTheme.line))
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
