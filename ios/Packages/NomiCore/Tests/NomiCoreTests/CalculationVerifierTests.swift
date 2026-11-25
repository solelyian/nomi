import XCTest
@testable import NomiCore

final class CalculationVerifierTests: XCTestCase {
    func testVerifiesPercentAndSubtractionChain() {
        let verifier = CalculationVerifier()
        let result = verifier.verify("80 × 15 % = 12 €")
        XCTAssertEqual(result.status, .verified)
        let final = verifier.verify("80 - 12 = 68 €")
        XCTAssertEqual(final.status, .verified)
    }

    func testDetectsMismatch() {
        let result = CalculationVerifier().verify("80 - 12 = 70 €")
        XCTAssertEqual(result.status, .mismatch)
        XCTAssertEqual(result.computed, 68)
    }

    func testRespectsOperatorPrecedence() {
        let result = CalculationVerifier().verify("2 + 3 × 4 = 14")
        XCTAssertEqual(result.status, .verified)
    }

    func testRejectsAmbiguousInput() {
        let result = CalculationVerifier().verify("2 heures + 30 minutes = 2,5")
        XCTAssertNotEqual(result.status, .verified)
    }
}
