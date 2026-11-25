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

    func testRejectsNonSimpleRightHandSide() {
        let result = CalculationVerifier().verify("2 + 2 = 4 + 1")
        XCTAssertEqual(result.status, .invalid)
    }

    func testRejectsMixedCurrencies() {
        let result = CalculationVerifier().verify("10 € + 10 $ = 20 €")
        XCTAssertEqual(result.status, .invalid)
    }

    func testSentenceSplitKeepsDecimalNumbersIntact() {
        let parts = MockRevisionService.splitSentences("Le taux est 0.05 dans cet exemple. Il faut le retenir.")
        XCTAssertEqual(parts.count, 2)
        XCTAssertTrue(parts[0].contains("0.05"))
    }
}
