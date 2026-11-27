import SwiftUI
import NomiCore

struct BoardView: View {
    @EnvironmentObject private var state: AppState
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
    @State private var phrase = ""
    @State private var selectedColumn: WorkColumn = .todo
    @State private var editingTask: WorkTask?

    private var visibleTasks: [WorkTask] {
        state.tasks.filter { $0.column == selectedColumn }
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 14) {
                header
                composer
                columnPicker
                taskList
            }
            .padding(.horizontal, 18)
            .padding(.top, 14)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
        .sheet(item: $editingTask) { task in
            TaskDetailView(task: task) { updated in state.updateTask(updated) }
        }
    }

    private var header: some View {
        HStack(alignment: .bottom) {
            VStack(alignment: .leading, spacing: 4) {
                Text("Tableau")
                    .font(.system(.largeTitle, design: .serif).weight(.semibold))
                    .tracking(-0.7)
                    .foregroundStyle(NomiTheme.ink)
                Text("\(state.openTasks.count) ouverte(s) · tri par colonne")
                    .font(.nomi(.caption))
                    .foregroundStyle(NomiTheme.ink2)
            }
            Spacer()
            Button { add() } label: {
                Image(systemName: "plus")
                    .font(.system(size: 16, weight: .semibold))
                    .foregroundStyle(NomiTheme.canvas)
                    .frame(width: 34, height: 34)
                    .background(NomiTheme.ink, in: RoundedRectangle(cornerRadius: 10, style: .continuous))
            }
            .buttonStyle(.plain)
            .disabled(phrase.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            .opacity(phrase.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? 0.45 : 1)
            .accessibilityLabel("Ajouter la tâche")
        }
    }

    private var composer: some View {
        HStack(spacing: 9) {
            Image(systemName: "plus")
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(NomiTheme.ink3)
            TextField(dynamicTypeSize.isAccessibilitySize ? "Nouvelle tâche…" : "Ex. Envoyer le compte rendu demain 30 min important", text: $phrase)
                .textFieldStyle(.plain)
                .font(.nomi(.subheadline))
                .foregroundStyle(NomiTheme.ink)
                .submitLabel(.done)
                .onSubmit(add)
        }
        .padding(.horizontal, 11)
        .frame(height: 44)
        .background(NomiTheme.surface.opacity(0.94), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.8))
    }

    private var columnPicker: some View {
        Picker("Colonne", selection: $selectedColumn) {
            ForEach(WorkColumn.allCases, id: \.self) { column in
                Text(column.frenchTitle).tag(column)
            }
        }
        .pickerStyle(.segmented)
        .accessibilityLabel("Colonne du tableau")
    }

    private var taskList: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .firstTextBaseline) {
                Text(selectedColumn.frenchTitle.uppercased())
                    .font(.nomi(.caption2, weight: .bold))
                    .tracking(1.1)
                    .foregroundStyle(NomiTheme.ink3)
                Spacer()
                Text("\(visibleTasks.count)")
                    .font(.caption2.monospaced())
                    .foregroundStyle(NomiTheme.ink3)
            }
            .padding(.horizontal, 1)

            VStack(spacing: 0) {
                ForEach(Array(visibleTasks.enumerated()), id: \.element.id) { index, task in
                    if index > 0 { Divider().overlay(NomiTheme.line.opacity(0.6)).padding(.leading, 42) }
                    BoardTaskRow(
                        task: task,
                        toggle: { toggle(task) },
                        focus: { state.startFocus(objective: task.title) },
                        edit: { editingTask = task },
                        move: { column in state.moveTask(task, to: column) },
                        split: { state.splitTask(task) },
                        delete: { state.deleteTask(task) }
                    )
                }
                if visibleTasks.isEmpty {
                    VStack(alignment: .leading, spacing: 5) {
                        Text("Aucune tâche")
                            .font(.nomi(.subheadline, weight: .semibold))
                            .foregroundStyle(NomiTheme.ink)
                        Text("Les tâches \(selectedColumn.frenchTitle.lowercased()) apparaîtront ici.")
                            .font(.nomi(.caption))
                            .foregroundStyle(NomiTheme.ink3)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(13)
                }
            }
            .background(NomiTheme.surface.opacity(0.93), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.7))
        }
    }

    private func add() {
        let cleaned = phrase.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleaned.isEmpty else { return }
        state.addTask(from: cleaned)
        phrase = ""
    }

    private func toggle(_ task: WorkTask) {
        if task.column == .done {
            state.moveTask(task, to: .todo)
        } else {
            state.moveTask(task, to: .done)
        }
    }
}

private struct BoardTaskRow: View {
    let task: WorkTask
    let toggle: () -> Void
    let focus: () -> Void
    let edit: () -> Void
    let move: (WorkColumn) -> Void
    let split: () -> Void
    let delete: () -> Void

    var body: some View {
        HStack(spacing: 9) {
            Button(action: toggle) {
                Image(systemName: task.column == .done ? "checkmark.circle.fill" : "circle")
                    .font(.system(size: 13, weight: .medium))
                    .foregroundStyle(task.priority >= .high ? NomiTheme.accent : NomiTheme.ink2)
                    .frame(width: 24, height: 24)
                    .background((task.priority >= .high ? NomiTheme.accentSoft : NomiTheme.surface2.opacity(0.55)), in: RoundedRectangle(cornerRadius: 7, style: .continuous))
            }
            .buttonStyle(.plain)
            .accessibilityLabel(task.column == .done ? "Rouvrir \(task.title)" : "Terminer \(task.title)")

            VStack(alignment: .leading, spacing: 2) {
                Text(task.title)
                    .font(.nomi(.subheadline, weight: .medium))
                    .foregroundStyle(NomiTheme.ink)
                    .lineLimit(1)
                Text(metadata)
                    .font(.nomi(.caption2))
                    .foregroundStyle(NomiTheme.ink3)
                    .lineLimit(1)
            }
            Spacer(minLength: 8)
            Text(task.priority >= .high ? "P1" : "P2")
                .font(.caption2.monospaced().weight(task.priority >= .high ? .bold : .regular))
                .foregroundStyle(task.priority >= .high ? NomiTheme.accent : NomiTheme.ink3)
            Menu {
                Button("Modifier", systemImage: "pencil", action: edit)
                Button("Démarrer un focus", systemImage: "timer", action: focus)
                Button("Diviser en étapes", systemImage: "arrow.branch", action: split)
                ForEach(WorkColumn.allCases, id: \.self) { column in
                    Button("Déplacer vers \(column.frenchTitle)") { move(column) }
                }
                Button("Supprimer", systemImage: "trash", role: .destructive, action: delete)
            } label: {
                Image(systemName: "ellipsis")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(NomiTheme.ink2)
                    .frame(width: 30, height: 30)
            }
            .accessibilityLabel("Actions pour \(task.title)")
        }
        .padding(.horizontal, 10)
        .frame(minHeight: 46)
    }

    private var metadata: String {
        var parts: [String] = []
        if let estimate = task.estimateMinutes { parts.append("\(estimate) min") }
        if let dueDate = task.dueDate { parts.append(dueDate.formatted(date: .abbreviated, time: .omitted)) }
        if !task.notes.isEmpty { parts.append("notes") }
        return parts.isEmpty ? task.priority.frenchTitle : parts.joined(separator: " · ")
    }
}

struct TaskDetailView: View {
    @Environment(\.dismiss) private var dismiss
    @State private var task: WorkTask
    @State private var hasDueDate: Bool
    let onSave: (WorkTask) -> Void

    init(task: WorkTask, onSave: @escaping (WorkTask) -> Void) {
        _task = State(initialValue: task)
        _hasDueDate = State(initialValue: task.dueDate != nil)
        self.onSave = onSave
    }

    var body: some View {
        NavigationStack {
            Form {
                Section("Tâche") {
                    TextField("Titre", text: $task.title, axis: .vertical)
                    Picker("Priorité", selection: $task.priority) {
                        ForEach(WorkPriority.allCases, id: \.self) { priority in
                            Text(priority.frenchTitle).tag(priority)
                        }
                    }
                    Stepper("Estimation : \(task.estimateMinutes ?? 0) min", value: Binding(
                        get: { task.estimateMinutes ?? 0 },
                        set: { task.estimateMinutes = $0 }
                    ), in: 0...480, step: 5)
                }
                Section("Échéance") {
                    Toggle("Date d'échéance", isOn: $hasDueDate)
                    if hasDueDate {
                        DatePicker("Date", selection: Binding(
                            get: { task.dueDate ?? .now },
                            set: { task.dueDate = $0 }
                        ), displayedComponents: [.date])
                    }
                }
                Section("Notes") {
                    TextEditor(text: $task.notes)
                        .frame(minHeight: 120)
                }
            }
            .navigationTitle("Détail")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button("Annuler") { dismiss() } }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Enregistrer") {
                        if hasDueDate && task.dueDate == nil {
                            task.dueDate = Calendar.current.startOfDay(for: .now)
                        } else if !hasDueDate {
                            task.dueDate = nil
                        }
                        onSave(task)
                        dismiss()
                    }
                    .disabled(task.title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
            }
        }
    }
}
