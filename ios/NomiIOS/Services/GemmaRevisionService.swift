import Foundation
import NomiCore

struct GemmaRevisionService {
    let runtime: GemmaRuntime
    let modelURL: URL

    private struct FactsPayload: Decodable {
        var facts: [String]
    }

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


    private func chunkedSource(_ source: String, limit: Int) -> [String] {
        var chunks: [String] = []
        var current = ""
        for paragraph in source.components(separatedBy: "\n") {
            if paragraph.count > limit {
                if !current.isEmpty {
                    chunks.append(current)
                    current = ""
                }
                var start = paragraph.startIndex
                while start < paragraph.endIndex {
                    let end = paragraph.index(start, offsetBy: limit, limitedBy: paragraph.endIndex) ?? paragraph.endIndex
                    chunks.append(String(paragraph[start..<end]))
                    start = end
                }
            } else if current.count + paragraph.count + 1 > limit, !current.isEmpty {
                chunks.append(current)
                current = paragraph
            } else {
                if !current.isEmpty { current += "\n" }
                current += paragraph
            }
        }
        if !current.isEmpty { chunks.append(current) }
        return chunks
    }

    private var factsSchema: [String: Any] {
        [
            "type": "object",
            "properties": [
                "facts": ["type": "array", "items": ["type": "string"]]
            ],
            "required": ["facts"]
        ]
    }

    private func extractFacts(from text: String, instruction: String, modelURL: URL) async throws -> [String] {
        let prompt = """
        \(instruction)
        Retourne uniquement le JSON demandé. Chaque fait doit être court, fidèle et exploitable pour construire une fiche de révision. N'invente rien.

        Texte :
        \(text)
        """
        let data = try await runtime.generateStructuredJSON(prompt: prompt, schema: factsSchema, modelURL: modelURL, maxOutputTokens: 700)
        let payload = try JSONDecoder().decode(FactsPayload.self, from: data)
        return payload.facts
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
    }

    private func fullSourceSynthesis(from source: String, modelURL: URL) async throws -> (text: String, coverageNote: String) {
        let chunks = chunkedSource(source, limit: 4_500)
        var facts: [String] = []
        for (index, chunk) in chunks.enumerated() {
            let extracted = try await extractFacts(
                from: chunk,
                instruction: "Segment \(index + 1) sur \(chunks.count). Extrais les définitions, formules, exemples, étapes, limites et idées essentielles de ce segment.",
                modelURL: modelURL
            )
            facts.append(contentsOf: extracted)
        }

        var uniqueFacts: [String] = []
        var seen: Set<String> = []
        for fact in facts {
            if seen.insert(fact).inserted { uniqueFacts.append(fact) }
        }

        var iteration = 0
        while uniqueFacts.joined(separator: "\n").count > 8_500, iteration < 6 {
            iteration += 1
            var condensed: [String] = []
            let batches = chunkedSource(uniqueFacts.enumerated().map { "\($0.offset + 1). \($0.element)" }.joined(separator: "\n"), limit: 4_500)
            for batch in batches {
                condensed.append(contentsOf: try await extractFacts(
                    from: batch,
                    instruction: "Condense cette liste exhaustive de faits sans perdre de notion distincte. Fusionne seulement les doublons ou reformulations.",
                    modelURL: modelURL
                ))
            }
            uniqueFacts = condensed
        }
        guard uniqueFacts.joined(separator: "\n").count <= 8_500 else { throw GemmaRuntimeError.emptyResponse }
        let text = uniqueFacts.enumerated().map { "\($0.offset + 1). \($0.element)" }.joined(separator: "\n")
        guard !text.isEmpty else { throw GemmaRuntimeError.emptyResponse }
        return (text, "Source longue analysée en \(chunks.count) segments successifs ; la fiche s'appuie sur une synthèse factuelle de l'ensemble, pas seulement sur le début.")
    }

    func makeRevision(from source: String, sourceTitle: String, level: RevisionDetailLevel, format: RevisionFormat) async throws -> RevisionSheet {
        let directLimit = 9_000
        var revisionSource = String(source.prefix(directLimit))
        var coverageNote = "Source courte analysée directement dans son intégralité."
        if source.count > directLimit {
            let synthesis = try await fullSourceSynthesis(from: source, modelURL: modelURL)
            revisionSource = synthesis.text
            coverageNote = synthesis.coverageNote
        }
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

        Matière fidèle issue de la source intitulée « \(sourceTitle) » :
        \(revisionSource)
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
        if format == .flashcards, cards.isEmpty { throw GemmaRuntimeError.emptyResponse }
        if format == .quiz, questions.isEmpty { throw GemmaRuntimeError.emptyResponse }
        let sections = [
            RevisionSection(title: "Objectif du cours", body: payload.objective, fidelity: .generatedExplanation),
            RevisionSection(title: "Idées essentielles", body: payload.keyIdeas.enumerated().map { "\($0.offset + 1). \($0.element)" }.joined(separator: "\n"), fidelity: .generatedExplanation),
            RevisionSection(title: "Définitions", body: payload.definitions.isEmpty ? "Aucune définition fiable n'a été identifiée dans la source." : payload.definitions.joined(separator: "\n"), fidelity: .generatedExplanation),
            RevisionSection(title: "Moteur", body: "Fiche générée localement par Gemma 4. \(coverageNote) Les formulations doivent être comparées à la source avant révision intensive.", fidelity: .generatedExplanation)
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
