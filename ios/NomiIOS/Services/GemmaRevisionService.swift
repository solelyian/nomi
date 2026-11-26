import Foundation
import NomiCore

struct GemmaRevisionService {
    let runtime: GemmaRuntime
    let modelURL: URL

    private struct Payload: Decodable {
        struct Card: Decodable { var front: String; var back: String }
        struct Question: Decodable {
            var question: String
            var choices: [String]
            var correctIndex: Int
            var explanation: String
        }
        var objective: String
        var keyIdeas: [String]
        var definitions: [String]
        var flashcards: [Card]
        var quiz: [Question]
    }

    func makeRevision(from source: String, sourceTitle: String, level: RevisionDetailLevel, format: RevisionFormat) async throws -> RevisionSheet {
        let trimmedSource = String(source.prefix(9_000))
        let ideaCount = level == .condensed ? 3 : level == .standard ? 5 : 8
        let prompt = """
        Crée une fiche de révision en français à partir de la source ci-dessous. Reste strictement fidèle à la source, signale les incertitudes plutôt qu'inventer, et adapte la densité au niveau \(level.frenchTitle). Retourne uniquement le JSON demandé.

        Exigences :
        - objective : une phrase précise ;
        - keyIdeas : \(ideaCount) idées maximum, chacune en une phrase ;
        - definitions : uniquement les définitions réellement présentes ou directement déductibles ;
        - flashcards : 4 à 6 cartes question/réponse courtes ;
        - quiz : 3 questions avec exactement 3 choix chacune, correctIndex entre 0 et 2 ;
        - aucun contenu hors source sauf formulation pédagogique prudente.

        Source intitulée « \(sourceTitle) » :
        \(trimmedSource)
        """
        let schema: [String: Any] = [
            "type": "object",
            "properties": [
                "objective": ["type": "string"],
                "keyIdeas": ["type": "array", "items": ["type": "string"]],
                "definitions": ["type": "array", "items": ["type": "string"]],
                "flashcards": [
                    "type": "array",
                    "items": [
                        "type": "object",
                        "properties": ["front": ["type": "string"], "back": ["type": "string"]],
                        "required": ["front", "back"]
                    ]
                ],
                "quiz": [
                    "type": "array",
                    "items": [
                        "type": "object",
                        "properties": [
                            "question": ["type": "string"],
                            "choices": ["type": "array", "items": ["type": "string"]],
                            "correctIndex": ["type": "integer"],
                            "explanation": ["type": "string"]
                        ],
                        "required": ["question", "choices", "correctIndex", "explanation"]
                    ]
                ]
            ],
            "required": ["objective", "keyIdeas", "definitions", "flashcards", "quiz"]
        ]
        let data = try await runtime.generateStructuredJSON(prompt: prompt, schema: schema, modelURL: modelURL)
        let payload = try JSONDecoder().decode(Payload.self, from: data)
        let cards = payload.flashcards.prefix(6).map { Flashcard(front: $0.front, back: $0.back) }
        let questions = payload.quiz.prefix(4).compactMap { question -> QuizQuestion? in
            guard question.choices.count == 3, question.correctIndex >= 0, question.correctIndex < 3 else { return nil }
            return QuizQuestion(question: question.question, choices: question.choices, correctIndex: question.correctIndex, explanation: question.explanation)
        }
        guard !payload.objective.isEmpty, !payload.keyIdeas.isEmpty else { throw GemmaRuntimeError.emptyResponse }
        let sections = [
            RevisionSection(title: "Objectif du cours", body: payload.objective, fidelity: .generatedExplanation),
            RevisionSection(title: "Idées essentielles", body: payload.keyIdeas.enumerated().map { "\($0.offset + 1). \($0.element)" }.joined(separator: "\n"), fidelity: .generatedExplanation),
            RevisionSection(title: "Définitions", body: payload.definitions.isEmpty ? "Aucune définition fiable n'a été identifiée dans la source." : payload.definitions.joined(separator: "\n"), fidelity: .generatedExplanation),
            RevisionSection(title: "Moteur", body: "Fiche générée localement par Gemma 4. Les formulations doivent être comparées à la source avant révision intensive.", fidelity: .generatedExplanation)
        ]
        return RevisionSheet(
            title: "Fiche — \(sourceTitle)",
            level: level,
            format: format,
            sourceTitle: sourceTitle,
            sections: sections,
            flashcards: Array(cards),
            quiz: Array(questions)
        )
    }
}
