import Combine
import Foundation
import NomiCore

private struct ProductivitySnapshot: Codable {
    var revisionSheets: [RevisionSheet]
    var tasks: [WorkTask]
    var agenda: [AgendaEvent]
    var routines: [NomiRoutine]
    var focusSession: FocusSession?
    var focusHistory: [FocusSession]
    var threads: [ChatThread]
    var activeThreadID: UUID?
    var preferredDetailLevel: RevisionDetailLevel
    var preferredFormat: RevisionFormat
    var allowRemoteAI: Bool
    var transcriptionLanguage: String
}

enum GemmaAvailabilityError: LocalizedError {
    case modelNotInstalled

    var errorDescription: String? {
        switch self {
        case .modelNotInstalled:
            "Gemma 4 n'est pas encore installé sur cet appareil. Ouvrez Réglages › Gemma 4 local pour télécharger le modèle (2,6 Go recommandé). Le téléchargement utilise le réseau une fois ; les conversations sont ensuite traitées localement."
        }
    }
}

@MainActor
final class AppState: ObservableObject {
    @Published var revisionSheets: [RevisionSheet] { didSet { persist() } }
    @Published var preferredDetailLevel: RevisionDetailLevel = .standard { didSet { persist() } }
    @Published var preferredFormat: RevisionFormat = .sheet { didSet { persist() } }
    @Published var allowRemoteAI = false { didSet { persist() } }
    @Published var transcriptionLanguage = "fr-FR" { didSet { persist() } }

    @Published var tasks: [WorkTask] { didSet { persist() } }
    @Published var agenda: [AgendaEvent] { didSet { persist() } }
    @Published var routines: [NomiRoutine] { didSet { persist() } }
    @Published var focusSession: FocusSession? { didSet { persist() } }
    @Published var focusHistory: [FocusSession] = [] { didSet { persist() } }
    @Published var threads: [ChatThread] { didSet { persist() } }
    @Published var activeThreadID: ChatThread.ID? { didSet { persist() } }
    @Published var dayStartHour = 8
    @Published var dayEndHour = 18
    @Published var isGeneratingReply = false

    let revisionService: any RevisionService
    let demoTranscriptionService: any LiveTranscriptionService
    let gemmaManager: GemmaModelManager
    let gemmaRuntime: GemmaRuntime
    private var cancellables: Set<AnyCancellable> = []
    private var suppressPersistence = false

    init(
        revisionService: any RevisionService = MockRevisionService(),
        demoTranscriptionService: any LiveTranscriptionService = MockLiveTranscriptionService(),
        gemmaManager: GemmaModelManager? = nil,
        gemmaRuntime: GemmaRuntime = GemmaRuntime()
    ) {
        self.revisionService = revisionService
        self.demoTranscriptionService = demoTranscriptionService
        self.gemmaManager = gemmaManager ?? GemmaModelManager()
        self.gemmaRuntime = gemmaRuntime
        let stored = Self.loadSnapshot()
        self.revisionSheets = stored?.revisionSheets ?? []

        let calendar = Calendar.current
        let today = calendar.startOfDay(for: .now)
        self.tasks = stored?.tasks ?? [
            WorkTask(title: "Préparer le point produit", priority: .high, dueDate: today, estimateMinutes: 45),
            WorkTask(title: "Répondre aux messages importants", priority: .normal, estimateMinutes: 25),
            WorkTask(title: "Relire la fiche de révision", priority: .normal, estimateMinutes: 20)
        ]
        self.agenda = stored?.agenda ?? [
            AgendaEvent(title: "Cours de probabilités", start: today.addingTimeInterval(10 * 3600), end: today.addingTimeInterval(11.5 * 3600), location: "Amphi B"),
            AgendaEvent(title: "Révision espacée", start: today.addingTimeInterval(17 * 3600), end: today.addingTimeInterval(17.5 * 3600))
        ]
        self.routines = stored?.routines ?? [
            NomiRoutine(name: "Revue du matin", action: "Créer la liste des priorités du jour", hour: 8, minute: 30),
            NomiRoutine(name: "Rappel de révision", action: "Rouvrir la dernière fiche et proposer un quiz", hour: 17, minute: 30)
        ]
        let thread = ChatThread(title: "Nouvelle conversation", messages: [
            ChatMessage(role: .assistant, text: "Bonjour, je peux planifier, vérifier, apprendre et résumer. Installez Gemma 4 local dans Réglages pour activer la génération IA sur appareil ; aucun contenu ne quitte alors votre iPhone.")
        ])
        if let storedThreads = stored?.threads, !storedThreads.isEmpty {
            self.threads = storedThreads
        } else {
            self.threads = [thread]
        }
        self.activeThreadID = stored?.activeThreadID ?? thread.id
        self.focusSession = stored?.focusSession
        self.focusHistory = stored?.focusHistory ?? []
        self.preferredDetailLevel = stored?.preferredDetailLevel ?? .standard
        self.preferredFormat = stored?.preferredFormat ?? .sheet
        self.allowRemoteAI = stored?.allowRemoteAI ?? false
        self.transcriptionLanguage = stored?.transcriptionLanguage ?? "fr-FR"
        self.gemmaManager.objectWillChange
            .sink { [weak self] _ in
                Task { @MainActor in self?.objectWillChange.send() }
            }
            .store(in: &cancellables)
    }

    private static var snapshotURL: URL {
        URL.applicationSupportDirectory.appendingPathComponent("Nomi/productivity-state.json")
    }

    private static func loadSnapshot() -> ProductivitySnapshot? {
        guard let data = try? Data(contentsOf: snapshotURL) else { return nil }
        return try? JSONDecoder().decode(ProductivitySnapshot.self, from: data)
    }

    private func persist() {
        guard !suppressPersistence else { return }
        let snapshot = ProductivitySnapshot(
            revisionSheets: revisionSheets,
            tasks: tasks,
            agenda: agenda,
            routines: routines,
            focusSession: focusSession,
            focusHistory: focusHistory,
            threads: threads,
            activeThreadID: activeThreadID,
            preferredDetailLevel: preferredDetailLevel,
            preferredFormat: preferredFormat,
            allowRemoteAI: allowRemoteAI,
            transcriptionLanguage: transcriptionLanguage
        )
        guard let data = try? JSONEncoder().encode(snapshot) else { return }
        let url = Self.snapshotURL
        try? FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        try? data.write(to: url, options: [.atomic, .completeFileProtection])
    }

    var openTasks: [WorkTask] { tasks.filter { $0.column != .done } }
    var todayTasks: [WorkTask] {
        let calendar = Calendar.current
        return openTasks.filter { task in
            guard let dueDate = task.dueDate else { return false }
            return calendar.isDateInToday(dueDate) || dueDate < .now
        }
    }
    var nextAgendaEvent: AgendaEvent? { agenda.filter { $0.end > .now }.sorted { $0.start < $1.start }.first }
    var activeThread: ChatThread? { threads.first { $0.id == activeThreadID } ?? threads.first }
    var readyRoutine: NomiRoutine? { routines.first { $0.isDue(at: .now) } }


    func makeRevision(from source: String, sourceTitle: String, level: RevisionDetailLevel, format: RevisionFormat) async throws -> RevisionSheet {
        if let modelURL = gemmaManager.selectedModelURL {
            do {
                return try await GemmaRevisionService(runtime: gemmaRuntime, modelURL: modelURL).makeRevision(from: source, sourceTitle: sourceTitle, level: level, format: format)
            } catch {
                gemmaManager.runtimeNotice = "Gemma 4 n'a pas pu générer cette fiche ; le mode extractif local a été utilisé."
            }
        }
        return try await revisionService.makeRevision(from: source, sourceTitle: sourceTitle, level: level, format: format)
    }

    func addSheet(_ sheet: RevisionSheet) {
        revisionSheets.insert(sheet, at: 0)
    }

    @discardableResult
    func rescueDay() -> String {
        let calendar = Calendar.current
        guard let tomorrow = calendar.date(byAdding: .day, value: 1, to: calendar.startOfDay(for: .now)) else { return "Impossible de calculer la date de report." }
        var deferred = 0
        for index in tasks.indices where tasks[index].column != .done {
            let priority = tasks[index].priority
            let dueDate = tasks[index].dueDate
            let occupiesToday = dueDate.map { $0 < tomorrow } ?? true
            if priority <= .normal && occupiesToday {
                tasks[index].dueDate = tomorrow
                if !tasks[index].notes.contains("Reporté par Sauver ma journée.") {
                    tasks[index].notes += "\nReporté par Sauver ma journée."
                }
                deferred += 1
            }
        }
        let kept = openTasks.filter { task in
            guard let dueDate = task.dueDate else { return task.priority >= .high }
            return dueDate < tomorrow || task.priority >= .high
        }.count
        return "\(deferred) tâche(s) normales reportées à demain ; \(kept) priorité(s) conservées aujourd'hui."
    }

    func addTask(from phrase: String) {
        tasks.insert(TaskPhraseParser.parse(phrase), at: 0)
    }

    func moveTask(_ task: WorkTask, to column: WorkColumn) {
        guard let index = tasks.firstIndex(where: { $0.id == task.id }) else { return }
        let wasDone = tasks[index].column == .done
        tasks[index].column = column
        if column == .done {
            if !wasDone {
                tasks[index].completedAt = .now
                runEventRoutines(.taskDone)
            }
        } else {
            tasks[index].completedAt = nil
        }
    }

    func updateTask(_ task: WorkTask) {
        guard let index = tasks.firstIndex(where: { $0.id == task.id }) else { return }
        tasks[index] = task
    }

    func deleteTask(_ task: WorkTask) {
        tasks.removeAll { $0.id == task.id }
    }

    func splitTask(_ task: WorkTask) {
        guard let index = tasks.firstIndex(where: { $0.id == task.id }) else { return }
        tasks[index].notes += "\nDécoupé manuellement dans le prototype."
        let first = WorkTask(title: "\(task.title) — étape 1", priority: task.priority, dueDate: task.dueDate, estimateMinutes: max(10, (task.estimateMinutes ?? 30) / 2), origin: task.origin)
        let second = WorkTask(title: "\(task.title) — étape 2", priority: task.priority, estimateMinutes: max(10, (task.estimateMinutes ?? 30) / 2), origin: task.origin)
        tasks.insert(contentsOf: [first, second], at: index + 1)
    }

    func startFocus(objective: String) {
        guard focusSession == nil else { return }
        focusSession = FocusSession(objective: objective.isEmpty ? "Focus sans objectif" : objective)
    }

    func pauseFocus() {
        guard focusSession?.status == .running else { return }
        focusSession?.pausedAt = .now
        focusSession?.status = .paused
    }

    func resumeFocus() {
        guard focusSession?.status == .paused else { return }
        if let pausedAt = focusSession?.pausedAt {
            focusSession?.pausedSeconds += Date().timeIntervalSince(pausedAt)
        }
        focusSession?.pausedAt = nil
        focusSession?.status = .running
    }

    func finishFocus(notes: String = "") {
        guard var session = focusSession else { return }
        if let pausedAt = session.pausedAt {
            session.pausedSeconds += Date().timeIntervalSince(pausedAt)
            session.pausedAt = nil
        }
        session.status = .finished
        session.endedAt = .now
        session.notes = notes
        focusHistory.insert(session, at: 0)
        focusSession = nil
        runEventRoutines(.focusEnd)
    }

    func addRoutine(from phrase: String) {
        let cleaned = phrase.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleaned.isEmpty else { return }
        routines.insert(RoutinePhraseParser.parse(cleaned), at: 0)
    }

    private func runEventRoutines(_ trigger: RoutineTrigger) {
        for routine in routines where routine.enabled && routine.trigger == trigger {
            tasks.insert(WorkTask(title: routine.action, priority: .normal, dueDate: .now, origin: "routine"), at: 0)
        }
    }

    func updateRoutine(_ routine: NomiRoutine) {
        guard let index = routines.firstIndex(where: { $0.id == routine.id }) else { return }
        routines[index] = routine
    }

    func runRoutine(_ routine: NomiRoutine) {
        guard let index = routines.firstIndex(where: { $0.id == routine.id }) else { return }
        routines[index].lastRun = .now
        tasks.insert(WorkTask(title: routine.action, priority: .high, dueDate: .now, origin: "routine"), at: 0)
    }

    func snoozeRoutine(_ routine: NomiRoutine) {
        guard let index = routines.firstIndex(where: { $0.id == routine.id }) else { return }
        routines[index].snoozedUntil = Calendar.current.date(byAdding: .hour, value: 1, to: .now)
    }

    func skipRoutine(_ routine: NomiRoutine) {
        guard let index = routines.firstIndex(where: { $0.id == routine.id }) else { return }
        routines[index].lastRun = .now
    }

    func sendMessage(_ text: String) {
        guard var thread = activeThread else { return }
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, !isGeneratingReply else { return }
        let priorMessages = thread.messages
        thread.messages.append(ChatMessage(role: .user, text: trimmed))
        let verification = trimmed.contains("=") ? CalculationVerifier().verify(trimmed) : nil
        if thread.title == "Nouvelle conversation" {
            thread.title = String(trimmed.prefix(42))
        }

        if let verification {
            let answer = "\(verification.message)\n\nCette réponse est produite localement par le vérificateur déterministe, sans IA générative."
            thread.messages.append(ChatMessage(role: .assistant, text: answer, verification: verification))
            replaceThread(thread)
            return
        }

        let assistantID = UUID()
        thread.messages.append(ChatMessage(id: assistantID, role: .assistant, text: "Gemma 4 prépare une réponse locale…"))
        replaceThread(thread)
        isGeneratingReply = true
        let threadID = thread.id
        let context = productivityContext()
        Task { [weak self] in
            guard let self else { return }
            defer { self.isGeneratingReply = false }
            do {
                guard let modelURL = self.gemmaManager.selectedModelURL else {
                    throw GemmaAvailabilityError.modelNotInstalled
                }
                let answer = try await self.gemmaRuntime.generateResponse(
                    userText: trimmed,
                    history: priorMessages,
                    productivityContext: context,
                    modelURL: modelURL
                ) { partial in
                    self.updateAssistantMessage(partial, id: assistantID, threadID: threadID, persistUpdate: false)
                }
                self.updateAssistantMessage(answer, id: assistantID, threadID: threadID)
            } catch {
                let explanation: String
                if let availability = error as? GemmaAvailabilityError {
                    explanation = availability.errorDescription ?? "Gemma 4 est indisponible."
                } else {
                    explanation = "Gemma 4 n'a pas pu produire de réponse sur cet appareil : \(error.localizedDescription)"
                }
                self.updateAssistantMessage(explanation, id: assistantID, threadID: threadID)
            }
        }
    }

    private func replaceThread(_ thread: ChatThread) {
        guard let index = threads.firstIndex(where: { $0.id == thread.id }) else { return }
        threads[index] = thread
        activeThreadID = thread.id
    }

    private func updateAssistantMessage(_ text: String, id: UUID, threadID: UUID, persistUpdate: Bool = true) {
        guard let threadIndex = threads.firstIndex(where: { $0.id == threadID }),
              let messageIndex = threads[threadIndex].messages.firstIndex(where: { $0.id == id }) else { return }
        if !persistUpdate { suppressPersistence = true }
        threads[threadIndex].messages[messageIndex].text = text
        suppressPersistence = false
    }

    private func productivityContext() -> String {
        let importantTasks = openTasks.prefix(5).map { task in
            "- \(task.title) [\(task.column.frenchTitle), priorité \(task.priority.frenchTitle)]"
        }.joined(separator: "\n")
        let routineNames = routines.filter(\.enabled).prefix(4).map(\.name).joined(separator: ", ")
        return "Tâches ouvertes:\n\(importantTasks.isEmpty ? "aucune" : importantTasks)\nRoutines actives: \(routineNames.isEmpty ? "aucune" : routineNames). Nomi peut conseiller mais ne modifie les données que lorsque l'utilisateur utilise explicitement les écrans prévus."
    }

    func newThread() {
        let thread = ChatThread(title: "Nouvelle conversation")
        threads.insert(thread, at: 0)
        activeThreadID = thread.id
    }
}
