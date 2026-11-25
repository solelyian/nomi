import Foundation
import NomiCore

@MainActor
final class CodeReviewViewModel: ObservableObject {
    @Published var code = DemoContent.swiftCode
    @Published var diagnostics: [CodeDiagnostic] = []
    @Published var calculationInput = "80 × 15 % = 12 €"
    @Published var calculationResult: CalculationVerification?
    @Published var question = ""
    @Published var conversationAnswer = ""
    @Published var isReviewing = false
    @Published var errorMessage: String?

    private let reviewService: any CodeReviewService
    private let verifier = CalculationVerifier()

    init(reviewService: any CodeReviewService) {
        self.reviewService = reviewService
    }

    func runReview() async {
        isReviewing = true
        errorMessage = nil
        do {
            diagnostics = try await reviewService.review(code: code, language: "swift", focus: nil)
        } catch {
            errorMessage = "La relecture simulée a échoué : \(error.localizedDescription)"
        }
        isReviewing = false
    }

    func verifyCalculation() {
        calculationResult = verifier.verify(calculationInput)
    }

    func askQuestion() async {
        let trimmed = question.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        do {
            conversationAnswer = try await reviewService.answerQuestion(trimmed, about: code)
        } catch {
            conversationAnswer = "Impossible de répondre pour l'instant. En mode hors-ligne, seuls les diagnostics locaux restent disponibles."
        }
    }

    func apply(_ diagnostic: CodeDiagnostic) {
        // Le prototype montre le geste d'application sans réécrire l'AST.
        // Une implémentation réelle doit proposer un diff ligne à ligne avant application.
        var lines = code.components(separatedBy: .newlines)
        guard diagnostic.line > 0, diagnostic.line <= lines.count else { return }
        lines[diagnostic.line - 1] += "  // Correctif proposé : \(diagnostic.proposedCorrection)"
        code = lines.joined(separator: "\n")
    }
}
