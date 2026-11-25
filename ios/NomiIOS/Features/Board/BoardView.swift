import SwiftUI
import NomiCore

struct BoardView: View {
    @EnvironmentObject private var state: AppState
    @State private var phrase = ""
    @State private var selectedColumn: WorkColumn = .todo
    @State private var editingTask: WorkTask?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                NomiSectionTitle("Tableau", subtitle: "Ajoutez une intention en langage naturel, puis faites-la progresser.")
                composer
                Picker("Colonne", selection: $selectedColumn) {
                    ForEach(WorkColumn.allCases, id: \.self) { column in
                        Text(column.frenchTitle).tag(column)
                    }
                }
                .pickerStyle(.segmented)
                ForEach(state.tasks.filter { $0.column == selectedColumn }) { task in
                    taskCard(task)
                }
                if state.tasks.filter({ $0.column == selectedColumn }).isEmpty {
                    ContentUnavailableView("Aucune tâche", systemImage: "checklist", description: Text("Les tâches \(selectedColumn.frenchTitle.lowercased()) apparaîtront ici."))
                }
            }
            .padding()
        }
        .nomiScreenBackground()
        .navigationTitle("Tableau")
        .sheet(item: $editingTask) { task in
            TaskDetailView(task: task) { updated in
                state.updateTask(updated)
            }
        }
    }

    private var composer: some View {
        VStack(alignment: .leading, spacing: 10) {
            TextField("Ex. Envoyer le compte rendu demain 30 min important", text: $phrase, axis: .vertical)
                .textFieldStyle(.roundedBorder)
                .submitLabel(.done)
                .onSubmit { add() }
            HStack {
                Button("Ajouter", systemImage: "plus") { add() }
                    .buttonStyle(.borderedProminent)
                    .disabled(phrase.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                Text("Le prototype comprend demain, aujourd'hui, urgent/important et une durée simple.")
                    .font(.caption)
                    .foregroundStyle(NomiTheme.ink2)
            }
        }
        .nomiCard(tint: NomiTheme.accent)
    }

    private func add() {
        state.addTask(from: phrase)
        phrase = ""
    }

    private func taskCard(_ task: WorkTask) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 4) {
                    Text(task.title).font(.headline)
                    HStack(spacing: 8) {
                        NomiBadge(task.priority.frenchTitle, tint: task.priority >= .high ? NomiTheme.warning : NomiTheme.code)
                        if let estimate = task.estimateMinutes { Text("\(estimate) min") }
                        if let dueDate = task.dueDate { Text(dueDate.formatted(date: .abbreviated, time: .omitted)) }
                    }
                    .font(.caption)
                    .foregroundStyle(NomiTheme.ink2)
                }
                Spacer()
                Menu {
                    Button("Modifier") { editingTask = task }
                    ForEach(WorkColumn.allCases, id: \.self) { column in
                        Button("Déplacer vers \(column.frenchTitle)") { state.moveTask(task, to: column) }
                    }
                    Button("Diviser en étapes") { state.splitTask(task) }
                    Button("Démarrer un focus") { state.startFocus(objective: task.title) }
                    Button("Supprimer", role: .destructive) { state.deleteTask(task) }
                } label: {
                    Image(systemName: "ellipsis.circle")
                        .frame(width: 44, height: 44)
                }
            }
            if !task.notes.isEmpty {
                Text(task.notes).font(.footnote).foregroundStyle(NomiTheme.ink2)
            }
            HStack {
                if task.column != .done {
                    Button(task.column == .todo ? "Commencer" : "Terminer") {
                        state.moveTask(task, to: task.column == .todo ? .doing : .done)
                    }
                    .buttonStyle(.borderedProminent)
                } else {
                    Button("Rouvrir") { state.moveTask(task, to: .todo) }
                        .buttonStyle(.bordered)
                }
            }
        }
        .padding(12)
        .background(NomiTheme.surface, in: RoundedRectangle(cornerRadius: 16, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 16, style: .continuous).stroke(NomiTheme.line))
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
                ToolbarItem(placement: .cancellationAction) {
                    Button("Annuler") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Enregistrer") {
                        if !hasDueDate { task.dueDate = nil }
                        onSave(task)
                        dismiss()
                    }
                    .disabled(task.title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
            }
        }
    }
}
