import Foundation

public struct MockRevisionService: RevisionService, AIServiceStatusProviding {
    public let implementationDescription = "Génération simulée à partir de règles locales — remplaçable par une API IA avec consentement explicite."
    public let leavesDevice = false
    public let fallbackDescription = "Le résumé extractif conserve les phrases du cours quand l'IA n'est pas disponible."

    public init() {}

    public func makeRevision(from source: String, sourceTitle: String, level: RevisionDetailLevel, format: RevisionFormat) async throws -> RevisionSheet {
        try await Task.sleep(nanoseconds: 450_000_000)
        let sentences = Self.splitSentences(source)
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { $0.count > 20 }
        let keyIdeas = Array(sentences.prefix(level == .condensed ? 2 : level == .standard ? 4 : 7))
        let objective = keyIdeas.first ?? "Le contenu source est trop court pour dégager un objectif fiable."
        let sections = [
            RevisionSection(title: "Objectif du cours", body: objective, fidelity: .source),
            RevisionSection(title: "Idées essentielles", body: keyIdeas.map { "• \($0)" }.joined(separator: "\n"), fidelity: .source),
            RevisionSection(title: "Définitions", body: "Les définitions exactes doivent être confirmées dans la transcription. Ce prototype n'invente pas de définition absente du cours.", fidelity: .generatedExplanation),
            RevisionSection(title: "Pièges fréquents", body: "Vérifier les unités, les hypothèses implicites et les exemples qui semblent contredire la règle générale.", fidelity: .hypothesis),
            RevisionSection(title: "Questions de révision", body: "• Quelle est l'idée centrale ?\n• Quel exemple du cours l'illustre ?\n• Quelle partie reste incertaine ?", fidelity: .generatedExplanation)
        ]
        let cards = keyIdeas.prefix(5).enumerated().map { index, idea in
            Flashcard(front: "À retenir : passage \(index + 1)", back: idea)
        }
        let quiz = keyIdeas.prefix(3).enumerated().map { index, idea in
            let choices = [idea, "Une affirmation absente du cours", "L'inverse de l'idée présentée"].shuffled()
            return QuizQuestion(
                question: "Quelle affirmation correspond au passage \(index + 1) du cours ?",
                choices: choices,
                correctIndex: choices.firstIndex(of: idea) ?? 0,
                explanation: "La bonne proposition reprend le passage source sans l'amplifier."
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

    /// Splits after sentence punctuation only when whitespace follows it, so
    /// decimal numbers such as 0.05 stay intact in source-labelled extracts.
    static func splitSentences(_ source: String) -> [String] {
        let pattern = #"(?<=[.!?])\s+|\n+"#
        guard let regex = try? NSRegularExpression(pattern: pattern) else { return [source] }
        let nsSource = source as NSString
        let matches = regex.matches(in: source, range: NSRange(location: 0, length: nsSource.length))
        var parts: [String] = []
        var cursor = 0
        for match in matches {
            guard match.range.location >= cursor else { continue }
            parts.append(nsSource.substring(with: NSRange(location: cursor, length: match.range.location - cursor)))
            cursor = match.range.location + match.range.length
        }
        if cursor < nsSource.length {
            parts.append(nsSource.substring(from: cursor))
        }
        return parts.filter { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
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
