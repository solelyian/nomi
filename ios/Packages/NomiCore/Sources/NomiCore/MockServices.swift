import Foundation

public struct MockCodeReviewService: CodeReviewService, AIServiceStatusProviding {
    public let implementationDescription = "Simulation locale déterministe — remplaçable par un service distant ou un modèle Apple Foundation Models lorsque disponible."
    public let leavesDevice = false
    public let fallbackDescription = "Les règles locales continuent de détecter les erreurs simples si le réseau ou le modèle est indisponible."

    public init() {}

    public func review(code: String, language: String, focus: String?) async throws -> [CodeDiagnostic] {
        try await Task.sleep(nanoseconds: 350_000_000)
        var diagnostics: [CodeDiagnostic] = []
        let lines = code.components(separatedBy: .newlines)
        for (index, line) in lines.enumerated() {
            let lineNumber = index + 1
            if line.contains("== null") || line.contains("!= null") {
                diagnostics.append(CodeDiagnostic(
                    line: lineNumber,
                    severity: .suggestion,
                    title: "Comparaison à nil peu idiomatique",
                    problem: "La comparaison explicite à nil alourdit le code Swift.",
                    explanation: "Swift offre optional binding et coalescence nil, qui réduisent les chemins d'erreur.",
                    proposedCorrection: "Remplacer par `if let value = value` ou `value ?? defaultValue`.",
                    educationalNote: "En Swift, une optionnelle exprime l'absence de valeur dans le type. La traiter explicitement évite les force unwrap."
                ))
            }
            if line.contains("!") && !line.contains("//") && language.lowercased().contains("swift") {
                diagnostics.append(CodeDiagnostic(
                    line: lineNumber,
                    severity: .warning,
                    title: "Force unwrap ou force try potentiel",
                    problem: "Un opérateur forcé peut provoquer un crash à l'exécution.",
                    explanation: "La valeur n'est pas garantie au moment de l'exécution. Le compilateur ne peut pas vérifier l'invariant métier.",
                    proposedCorrection: "Utiliser `guard let`, `if let` ou propager l'erreur avec `throws`.",
                    educationalNote: "Une correction sûre rend le cas absent visible et testable au lieu de le transformer en plantage."
                ))
            }
            if line.contains("eval(") || line.lowercased().contains("password") || line.lowercased().contains("secret") {
                diagnostics.append(CodeDiagnostic(
                    line: lineNumber,
                    severity: .critical,
                    title: "Risque de sécurité",
                    problem: "La ligne semble exécuter une entrée ou manipuler un secret.",
                    explanation: "L'exécution dynamique et les secrets en clair sont des causes fréquentes de vulnérabilité.",
                    proposedCorrection: "Supprimer l'exécution dynamique et stocker les secrets dans le trousseau ou côté serveur.",
                    educationalNote: "Une clé API ne doit jamais être embarquée dans une application distribuée : elle peut être extraite du binaire."
                ))
            }
            if line.contains("TODO") || line.contains("FIXME") {
                diagnostics.append(CodeDiagnostic(
                    line: lineNumber,
                    severity: .info,
                    title: "Travail non terminé",
                    problem: "Un marqueur TODO/FIXME subsiste dans la portion relue.",
                    explanation: "Le marqueur peut être légitime, mais il doit être tracé avant une livraison.",
                    proposedCorrection: "Créer une tâche explicite ou terminer l'implémentation.",
                    educationalNote: "Les relectures mobiles sont efficaces pour transformer ces signaux en actions courtes."
                ))
            }
        }
        if diagnostics.isEmpty {
            diagnostics.append(CodeDiagnostic(
                line: 1,
                severity: .info,
                title: "Aucun problème évident détecté",
                problem: "Les règles de démonstration n'ont pas trouvé de motif risqué.",
                explanation: "Cela ne prouve pas l'absence de bug. Une revue IA réelle analyserait aussi le contexte du projet et les types.",
                proposedCorrection: "Relire les cas limites et ajouter des tests unitaires.",
                educationalNote: "Les diagnostics statiques sont des indices, pas une preuve de correction."
            ))
        }
        return diagnostics.sorted { lhs, rhs in
            if lhs.severity == rhs.severity { return lhs.line < rhs.line }
            return lhs.severity > rhs.severity
        }
    }

    public func answerQuestion(_ question: String, about code: String) async throws -> String {
        try await Task.sleep(nanoseconds: 250_000_000)
        if question.lowercased().contains("pourquoi") {
            return "Parce que cette portion crée un chemin où l'erreur n'est pas visible par l'appelant. Dans le prototype, cette explication est simulée ; une implémentation réelle citerait les lignes, les types et le contexte du projet."
        }
        return "La relecture conversationnelle est simulée ici : commencez par isoler la donnée d'entrée, vérifiez ses invariants, puis remplacez les branches implicites par des erreurs explicites. Le service réel pourrait utiliser un modèle distant ou sur appareil."
    }
}

public struct MockRevisionService: RevisionService, AIServiceStatusProviding {
    public let implementationDescription = "Génération simulée à partir de règles locales — remplaçable par une API IA avec consentement explicite."
    public let leavesDevice = false
    public let fallbackDescription = "Le résumé extractif conserve les phrases du cours quand l'IA n'est pas disponible."

    public init() {}

    public func makeRevision(from source: String, sourceTitle: String, level: RevisionDetailLevel, format: RevisionFormat) async throws -> RevisionSheet {
        try await Task.sleep(nanoseconds: 450_000_000)
        let sentences = source
            .components(separatedBy: CharacterSet(charactersIn: ".!?\n"))
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { $0.count > 20 }
        let keyIdeas = Array(sentences.prefix(level == .condensed ? 2 : level == .standard ? 4 : 7))
        let objective = keyIdeas.first ?? "Le contenu source est trop court pour dégager un objectif fiable."
        let sections = [
            RevisionSection(title: "Objectif du cours", body: objective, fidelity: .source),
            RevisionSection(title: "Idées essentielles", body: keyIdeas.enumerated().map { "\($0.offset + 1). \($0.element)." }.joined(separator: "\n"), fidelity: .source),
            RevisionSection(title: "Définitions", body: "Les définitions exactes doivent être confirmées dans la transcription. Ce prototype n'invente pas de définition absente du cours.", fidelity: .generatedExplanation),
            RevisionSection(title: "Pièges fréquents", body: "Vérifier les unités, les hypothèses implicites et les exemples qui semblent contredire la règle générale.", fidelity: .hypothesis),
            RevisionSection(title: "Questions de révision", body: "• Quelle est l'idée centrale ?\n• Quel exemple du cours l'illustre ?\n• Quelle partie reste incertaine ?", fidelity: .generatedExplanation)
        ]
        let cards = keyIdeas.prefix(5).map {
            Flashcard(front: "À retenir : \($0.prefix(60))…", back: $0)
        }
        let quiz = keyIdeas.prefix(3).enumerated().map { index, idea in
            QuizQuestion(
                question: "Quelle affirmation correspond au passage \(index + 1) du cours ?",
                choices: [idea, "Une affirmation absente du cours", "L'inverse de l'idée présentée"],
                correctIndex: 0,
                explanation: "La première proposition reprend le passage source sans l'amplifier."
            )
        }
        return RevisionSheet(
            title: "Fiche — \(sourceTitle)",
            level: level,
            format: format,
            sourceTitle: sourceTitle,
            sections: sections,
            flashcards: Array(cards),
            quiz: Array(quiz)
        )
    }
}

public struct MockLiveTranscriptionService: LiveTranscriptionService, AIServiceStatusProviding {
    public let implementationDescription = "Cours de démonstration joué localement ; le service réel utilise Speech/SpeechAnalyzer sur appareil quand l'utilisateur l'autorise."
    public let leavesDevice = false
    public let fallbackDescription = "La démonstration reste disponible sans microphone ni permission."

    public init() {}

    public func transcribeDemoCourse() -> AsyncStream<TranscriptSegment> {
        AsyncStream { continuation in
            let lines = [
                "Bienvenue dans ce cours sur les probabilités conditionnelles.",
                "Une probabilité conditionnelle mesure la chance d'un événement sachant qu'un autre événement est réalisé.",
                "La formule centrale est P de A sachant B égal P de A inter B divisé par P de B.",
                "Attention au piège fréquent : intervertir A sachant B et B sachant A.",
                "Prenons un exemple médical simple avec une maladie rare et un test imparfait.",
                "Le résultat dépend fortement de la prévalence, pas seulement de la précision du test.",
                "Pour réviser, retenez la formule, les hypothèses et l'interprétation en langage courant."
            ]
            let task = Task {
                var cursor: TimeInterval = 0
                for line in lines {
                    if Task.isCancelled { break }
                    try? await Task.sleep(nanoseconds: 1_400_000_000)
                    let duration = TimeInterval(max(3, line.count / 18))
                    continuation.yield(TranscriptSegment(start: cursor, end: cursor + duration, speaker: "Enseignant", text: line))
                    cursor += duration + 0.7
                }
                continuation.finish()
            }
            continuation.onTermination = { _ in task.cancel() }
        }
    }
}

public enum DemoContent {
    public static let swiftCode = """
    import Foundation

    func priceAfterDiscount(price: Double, discount: Double) -> Double {
        let result = price! * (1 - discount / 100)
        return result
    }

    func loadUser(password: String) {
        // TODO: validate input
        print(password)
    }

    // Remise : 80 × 15 % = 12 €
    // Prix final : 80 - 12 = 68 €
    """

    public static let transcript = """
    Bienvenue dans ce cours sur les probabilités conditionnelles. Une probabilité conditionnelle mesure la chance d'un événement sachant qu'un autre événement est réalisé. La formule centrale est P(A|B) = P(A∩B) / P(B). Attention au piège fréquent : intervertir A sachant B et B sachant A. Le résultat dépend fortement de la prévalence, pas seulement de la précision du test.
    """
}
