import SwiftUI
import NomiCore

struct BoardView: View {
    @EnvironmentObject private var state: AppState
    @State private var phrase = ""
    @State private var selectedColumn: WorkColumn = .todo
    @State private var editingTask: WorkTask?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                NomiScreenTitle("Tableau", subtitle: "Ajoutez une intention, puis faites-la progresser sans perdre le contexte.")
                composer
                columnPicker
                let visibleTasks = state.tasks.filter { $0.column == selectedColumn }
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 400), spacing: 14)], spacing: 14) {
                    ForEach(visibleTasks) { task in
                        taskCard(task)
                    }
                }
                if visibleTasks.isEmpty {
                    ContentUnavailableView("Aucune tâche", systemImage: "checklist", description: Text("Les tâches \(selectedColumn.frenchTitle.lowercased()) apparaîtront ici."))
                        .frame(maxWidth: .infinity)
                        .padding(.top, 36)
                }
            }
            .padding(.horizontal, 20)
            .padding(.top, 18)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
        .sheet(item: $editingTask) { task in
            TaskDetailView(task: task) { updated in
                state.updateTask(updated)
            }
        }
    }

    private var composer: some View {
        VStack(alignment: .leading, spacing: 12) {
            Label("Nouvelle intention", systemImage: "plus.circle.fill")
                .font(.headline)
                .foregroundStyle(NomiTheme.accent)
            TextField("Ex. Envoyer le compte rendu demain 30 min important", text: $phrase, axis: .vertical)
                .textFieldStyle(.plain)
                .nomiInput()
                .submitLabel(.done)
                .onSubmit { add() }
            HStack(alignment: .center, spacing: 12) {
                Button("Ajouter", systemImage: "plus") { add() }
                    .buttonStyle(.nomiPrimary)
                    .disabled(phrase.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                Text("Comprend demain, aujourd'hui, urgent/important et une durée simple.")
                    .font(.caption)
                    .foregroundStyle(NomiTheme.ink2)
            }
        }
        .nomiCard(tint: NomiTheme.accent)
    }

    private var columnPicker: some View {
        HStack(spacing: 4) {
            ForEach(WorkColumn.allCases, id: \.self) { column in
                Button {
                    withAnimation(NomiTheme.morphAnimation) { selectedColumn = column }
                } label: {
                    Text(column.frenchTitle)
                        .font(.subheadline.weight(.bold))
                        .foregroundStyle(selectedColumn == column ? NomiTheme.ink : NomiTheme.ink2)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 10)
                        .background(selectedColumn == column ? NomiTheme.surface.opacity(0.84) : Color.clear, in: Capsule())
                        .shadow(color: selectedColumn == column ? .black.opacity(0.06) : .clear, radius: 8, y: 3)
                }
                .buttonStyle(.plain)
                .accessibilityAddTraits(selectedColumn == column ? .isSelected : [])
            }
        }
        .padding(4)
        .background(NomiTheme.surface.opacity(0.38), in: Capsule())
        .overlay(Capsule().strokeBorder(NomiTheme.glassStroke.opacity(0.55), lineWidth: 1))
    }

    private func add() {
        state.addTask(from: phrase)
        phrase = ""
    }

    private func taskCard(_ task: WorkTask) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(alignment: .top, spacing: 10) {
                VStack(alignment: .leading, spacing: 7) {
                    Text(task.title)
                        .font(.system(.headline, design: .rounded).weight(.bold))
                        .fixedSize(horizontal: false, vertical: true)
                    HStack(spacing: 7) {
                        NomiBadge(task.priority.frenchTitle, tint: task.priority >= .high ? NomiTheme.warning : NomiTheme.focus)
                        if let estimate = task.estimateMinutes {
                            Label("\(estimate) min", systemImage: "clock")
                        }
                        if let dueDate = task.dueDate {
                            Label(dueDate.formatted(date: .abbreviated, time: .omitted), systemImage: "calendar")
                        }
                    }
                    .font(.caption)
                    .foregroundStyle(NomiTheme.ink2)
                }
                Spacer(minLength: 4)
                Menu {
                    Button("Modifier") { editingTask = task }
                    ForEach(WorkColumn.allCases, id: \.self) { column in
                        Button("Déplacer vers \(column.frenchTitle)") { state.moveTask(task, to: column) }
                    }
                    Button("Diviser en étapes") { state.splitTask(task) }
                    Button("Démarrer un focus") { state.startFocus(objective: task.title) }
                    Button("Supprimer", role: .destructive) { state.deleteTask(task) }
                } label: {
                    Image(systemName: "ellipsis")
                        .font(.headline)
                        .foregroundStyle(NomiTheme.ink2)
                        .frame(width: 40, height: 40)
                        .background(NomiTheme.surface.opacity(0.52), in: Circle())
                }
                .accessibilityLabel("Actions pour \(task.title)")
            }
            if !task.notes.isEmpty {
                Text(task.notes)
                    .font(.footnote)
                    .foregroundStyle(NomiTheme.ink2)
                    .fixedSize(horizontal: false, vertical: true)
            }
            HStack(spacing: 10) {
                if task.column != .done {
                    Button(task.column == .todo ? "Commencer" : "Terminer") {
                        state.moveTask(task, to: task.column == .todo ? .doing : .done)
                    }
                    .buttonStyle(.nomiPrimary)
                } else {
                    Button("Rouvrir") { state.moveTask(task, to: .todo) }
                        .buttonStyle(.nomiSecondary)
                }
                if task.column != .done {
                    Button("Focus") { state.startFocus(objective: task.title) }
                        .buttonStyle(.nomiSecondary)
                }
            }
        }
        .nomiCard(tint: task.priority >= .high ? NomiTheme.warning : NomiTheme.accent)
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
