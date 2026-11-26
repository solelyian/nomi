import SwiftUI
import NomiCore

struct RoutinesView: View {
    @EnvironmentObject private var state: AppState
    @State private var phrase = ""
    @State private var editingRoutine: NomiRoutine?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                header
                composer
                if let ready = state.readyRoutine { readyRow(ready) }
                routineList
            }
            .padding(.horizontal, 18)
            .padding(.top, 14)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
        .nomiChildNavigation()
        .navigationTitle("")
        .sheet(item: $editingRoutine) { routine in
            RoutineDetailView(routine: routine) { updated in state.updateRoutine(updated) }
        }
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Routines")
                .font(.system(size: 32, weight: .semibold, design: .serif))
                .tracking(-0.7)
                .foregroundStyle(NomiTheme.ink)
            Text("Automatisations visibles, modifiables et contrôlées")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
        }
    }

    private var composer: some View {
        HStack(spacing: 9) {
            Image(systemName: "repeat")
                .font(.system(size: 13, weight: .medium))
                .foregroundStyle(NomiTheme.ink3)
            TextField("Ex. Chaque matin, priorités", text: $phrase)
                .textFieldStyle(.plain)
                .font(.subheadline)
                .submitLabel(.done)
                .onSubmit(add)
            Button("Créer", action: add)
                .buttonStyle(.nomiPrimary)
                .disabled(phrase.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
        }
        .padding(.horizontal, 11)
        .frame(height: 48)
        .background(NomiTheme.surface.opacity(0.94), in: RoundedRectangle(cornerRadius: 13, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 13, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.8))
    }

    private func readyRow(_ routine: NomiRoutine) -> some View {
        VStack(alignment: .leading, spacing: 9) {
            Text("ROUTINE PRÊTE")
                .font(.caption2.weight(.bold))
                .tracking(1.0)
                .foregroundStyle(NomiTheme.accent)
            Text(routine.name)
                .font(.subheadline.weight(.semibold))
                .foregroundStyle(NomiTheme.ink)
            Text("Aucune exécution silencieuse : choisissez explicitement la suite.")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
            HStack(spacing: 7) {
                Button("Exécuter") { state.runRoutine(routine) }.buttonStyle(.nomiPrimary)
                Button("Plus tard") { state.snoozeRoutine(routine) }.buttonStyle(.nomiSecondary)
                Button("Ignorer") { state.skipRoutine(routine) }.buttonStyle(.nomiSecondary)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(13)
        .background(NomiTheme.surface.opacity(0.94), in: RoundedRectangle(cornerRadius: 15, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 15, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.8))
    }

    private var routineList: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .firstTextBaseline) {
                Text("TOUTES")
                    .font(.caption2.weight(.bold))
                    .tracking(1.1)
                    .foregroundStyle(NomiTheme.ink3)
                Spacer()
                Text("\(state.routines.count)")
                    .font(.caption2.monospaced())
                    .foregroundStyle(NomiTheme.ink3)
            }
            .padding(.horizontal, 1)
            VStack(spacing: 0) {
                ForEach(Array(state.routines.enumerated()), id: \.element.id) { index, routine in
                    if index > 0 { Divider().overlay(NomiTheme.line.opacity(0.6)).padding(.leading, 42) }
                    routineRow(routine)
                }
                if state.routines.isEmpty {
                    Text("Aucune routine. Créez une règle simple en langage naturel.")
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

    private func routineRow(_ routine: NomiRoutine) -> some View {
        HStack(spacing: 9) {
            Toggle("Activer \(routine.name)", isOn: Binding(get: { routine.enabled }, set: { value in
                if let index = state.routines.firstIndex(where: { $0.id == routine.id }) {
                    state.routines[index].enabled = value
                }
            }))
            .labelsHidden()
            .tint(NomiTheme.accent)
            .accessibilityLabel("Activer la routine \(routine.name)")
            VStack(alignment: .leading, spacing: 2) {
                Text(routine.name)
                    .font(.subheadline.weight(.medium))
                    .foregroundStyle(NomiTheme.ink)
                    .lineLimit(1)
                Text(metadata(routine))
                    .font(.caption2)
                    .foregroundStyle(NomiTheme.ink3)
                    .lineLimit(1)
            }
            Spacer(minLength: 8)
            Menu {
                Button("Modifier", systemImage: "pencil") { editingRoutine = routine }
                Button("Exécuter", systemImage: "play") { state.runRoutine(routine) }
                Button(routine.documentTitle == nil ? "Lier un document" : "Retirer le document", systemImage: "doc") {
                    if let index = state.routines.firstIndex(where: { $0.id == routine.id }) {
                        state.routines[index].documentTitle = routine.documentTitle == nil ? "Document de démonstration" : nil
                    }
                }
                Button("Supprimer", systemImage: "trash", role: .destructive) {
                    state.routines.removeAll { $0.id == routine.id }
                }
            } label: {
                Image(systemName: "ellipsis")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(NomiTheme.ink2)
                    .frame(width: 30, height: 30)
            }
            .accessibilityLabel("Actions pour \(routine.name)")
        }
        .padding(.horizontal, 10)
        .frame(minHeight: 48)
    }

    private func metadata(_ routine: NomiRoutine) -> String {
        var parts = [routine.trigger.frenchTitle, String(format: "%02d:%02d", routine.hour, routine.minute)]
        if let document = routine.documentTitle { parts.append(document) }
        return parts.joined(separator: " · ")
    }

    private func add() {
        let cleaned = phrase.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleaned.isEmpty else { return }
        state.addRoutine(from: cleaned)
        phrase = ""
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
