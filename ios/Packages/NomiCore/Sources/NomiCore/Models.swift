import Foundation

public enum DiagnosticSeverity: String, Codable, CaseIterable, Sendable, Comparable {
    case info, suggestion, warning, critical

    public static func < (lhs: DiagnosticSeverity, rhs: DiagnosticSeverity) -> Bool {
        let order: [DiagnosticSeverity] = [.info, .suggestion, .warning, .critical]
        return order.firstIndex(of: lhs)! < order.firstIndex(of: rhs)!
    }
}

public struct CodeDiagnostic: Identifiable, Codable, Sendable, Equatable {
    public var id: UUID
    public var line: Int
    public var severity: DiagnosticSeverity
    public var title: String
    public var problem: String
    public var explanation: String
    public var proposedCorrection: String
    public var educationalNote: String

    public init(id: UUID = UUID(), line: Int, severity: DiagnosticSeverity, title: String, problem: String, explanation: String, proposedCorrection: String, educationalNote: String) {
        self.id = id
        self.line = line
        self.severity = severity
        self.title = title
        self.problem = problem
        self.explanation = explanation
        self.proposedCorrection = proposedCorrection
        self.educationalNote = educationalNote
    }
}

public struct CalculationVerification: Codable, Sendable, Equatable {
    public enum Status: String, Codable, Sendable {
        case verified, mismatch, unsupported, invalid, interpreted
    }

    public var expression: String
    public var expected: Decimal?
    public var computed: Decimal?
    public var status: Status
    public var message: String
    /// Certificat de calcul encodé en JSON — structure consignée, jamais imitable
    /// par le texte généré (SPEC 7.3). Vide pour les vérifications héritées.
    public var certificate: String?

    public init(expression: String, expected: Decimal?, computed: Decimal?, status: Status, message: String, certificate: String? = nil) {
        self.expression = expression
        self.expected = expected
        self.computed = computed
        self.status = status
        self.message = message
        self.certificate = certificate
    }
}

public struct TranscriptSegment: Identifiable, Codable, Sendable, Equatable {
    public var id: UUID
    public var start: TimeInterval
    public var end: TimeInterval
    public var speaker: String?
    public var text: String
    public var isFinal: Bool

    public init(id: UUID = UUID(), start: TimeInterval, end: TimeInterval, speaker: String? = nil, text: String, isFinal: Bool = true) {
        self.id = id
        self.start = start
        self.end = end
        self.speaker = speaker
        self.text = text
        self.isFinal = isFinal
    }
}

public struct CourseTranscript: Identifiable, Codable, Sendable, Equatable {
    public var id: UUID
    public var title: String
    public var language: String
    public var createdAt: Date
    public var segments: [TranscriptSegment]

    public init(id: UUID = UUID(), title: String, language: String = "fr-FR", createdAt: Date = .now, segments: [TranscriptSegment]) {
        self.id = id
        self.title = title
        self.language = language
        self.createdAt = createdAt
        self.segments = segments
    }

    public var plainText: String {
        segments.map(\.text).joined(separator: "\n")
    }
}

public enum RevisionDetailLevel: String, Codable, CaseIterable, Sendable {
    case condensed, standard, detailed

    public var frenchTitle: String {
        switch self {
        case .condensed: "Ultra-condensé"
        case .standard: "Standard"
        case .detailed: "Détaillé"
        }
    }
}

public enum RevisionFormat: String, Codable, CaseIterable, Sendable {
    case sheet, outline, flashcards, quiz, summary

    public var frenchTitle: String {
        switch self {
        case .sheet: "Fiche"
        case .outline: "Plan"
        case .flashcards: "Flashcards"
        case .quiz: "Quiz"
        case .summary: "Résumé"
        }
    }
}

public enum FidelityKind: String, Codable, Sendable {
    case source, generatedExplanation, hypothesis

    public var frenchTitle: String {
        switch self {
        case .source: return "source"
        case .generatedExplanation: return "explication IA"
        case .hypothesis: return "hypothèse"
        }
    }
}

public struct RevisionSection: Identifiable, Codable, Sendable, Equatable {
    public var id: UUID
    public var title: String
    public var body: String
    public var fidelity: FidelityKind
    public var highlighted: Bool

    public init(id: UUID = UUID(), title: String, body: String, fidelity: FidelityKind, highlighted: Bool = false) {
        self.id = id
        self.title = title
        self.body = body
        self.fidelity = fidelity
        self.highlighted = highlighted
    }
}

public struct Flashcard: Identifiable, Codable, Sendable, Equatable {
    public var id: UUID
    public var front: String
    public var back: String

    public init(id: UUID = UUID(), front: String, back: String) {
        self.id = id
        self.front = front
        self.back = back
    }
}

public struct QuizQuestion: Identifiable, Codable, Sendable, Equatable {
    public var id: UUID
    public var question: String
    public var choices: [String]
    public var correctIndex: Int
    public var explanation: String

    public init(id: UUID = UUID(), question: String, choices: [String], correctIndex: Int, explanation: String) {
        self.id = id
        self.question = question
        self.choices = choices
        self.correctIndex = correctIndex
        self.explanation = explanation
    }
}

public struct RevisionSheet: Identifiable, Codable, Sendable, Equatable {
    public var id: UUID
    public var title: String
    public var level: RevisionDetailLevel
    public var format: RevisionFormat
    public var createdAt: Date
    public var sourceTitle: String
    public var sections: [RevisionSection]
    public var flashcards: [Flashcard]
    public var quiz: [QuizQuestion]

    public init(id: UUID = UUID(), title: String, level: RevisionDetailLevel, format: RevisionFormat, createdAt: Date = .now, sourceTitle: String, sections: [RevisionSection], flashcards: [Flashcard] = [], quiz: [QuizQuestion] = []) {
        self.id = id
        self.title = title
        self.level = level
        self.format = format
        self.createdAt = createdAt
        self.sourceTitle = sourceTitle
        self.sections = sections
        self.flashcards = flashcards
        self.quiz = quiz
    }
}
