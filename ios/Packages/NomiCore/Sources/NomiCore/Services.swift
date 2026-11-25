import Foundation

public protocol CodeReviewService: Sendable {
    func review(code: String, language: String, focus: String?) async throws -> [CodeDiagnostic]
    func answerQuestion(_ question: String, about code: String) async throws -> String
}

public protocol RevisionService: Sendable {
    func makeRevision(from source: String, sourceTitle: String, level: RevisionDetailLevel, format: RevisionFormat) async throws -> RevisionSheet
}

public protocol LiveTranscriptionService: Sendable {
    /// Implementations may be simulated, backed by Apple Speech, or by a server.
    /// This protocol intentionally does not require diarization: Apple Speech does not expose it natively.
    func transcribeDemoCourse() -> AsyncStream<TranscriptSegment>
}

public protocol AIServiceStatusProviding: Sendable {
    var implementationDescription: String { get }
    var leavesDevice: Bool { get }
    var fallbackDescription: String { get }
}

public enum ServiceImplementation: String, Codable, Sendable {
    case mock
    case appleOnDevice
    case remoteAPI
    case hybrid
}
