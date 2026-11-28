import XCTest
@testable import NomiCore

/// Analyse dimensionnelle — SPEC 3.1/3.2 (v1.1). Chaque cas est exigé
/// identique entre noyau et vérificateur : valeur exacte, dimension, erreur.
final class DimensionTests: XCTestCase {
    private func check(_ expression: String, rational: String? = nil, dim: DimKind? = nil, code: String = "", error: String? = nil, file: StaticString = #file, line: UInt = #line) {
        let kernel = CalcKernel()
        let verifier = CalcVerifierEngine()
        let kernelResult = Result { try kernel.evaluateDimensional(expression) }
        let verifierResult = Result { try verifier.evaluateDimensional(expression) }

        switch (kernelResult, verifierResult) {
        case (.success(let k), .success(let v)):
            XCTAssertEqual(k, v, "désaccord noyau/vérificateur sur \(expression)", file: file, line: line)
            if let error {
                XCTFail("\(expression) : erreur \(error) attendue, valeur obtenue", file: file, line: line)
                return
            }
            if let rational {
                let parts = rational.split(separator: "/")
                let expected = "\(k.value.numerator.magnitude.decimalString)/\(k.value.denominator.decimalString)"
                XCTAssertEqual(expected, "\(parts[0])/\(parts[1])", "\(expression)", file: file, line: line)
            }
            if let dim {
                XCTAssertEqual(k.dim.kind, dim, "\(expression) dimension", file: file, line: line)
                if !code.isEmpty { XCTAssertEqual(k.dim.code, code, "\(expression) code", file: file, line: line) }
            }
        case (.failure(let ke), .failure(let ve)):
            let kc = (ke as? CalcError)?.code ?? "?"
            let vc = (ve as? CalcError)?.code ?? "?"
            XCTAssertEqual(kc, vc, "erreurs divergentes sur \(expression)", file: file, line: line)
            if let error {
                XCTAssertEqual(kc, error, "\(expression)", file: file, line: line)
            } else {
                XCTFail("\(expression) : erreur inattendue \(kc)", file: file, line: line)
            }
        default:
            XCTFail("\(expression) : un seul moteur rejette — \(kernelResult) vs \(verifierResult)", file: file, line: line)
        }
    }

    private static func negative(_ rational: String) -> String { rational }

    func testCurrencyMultipliedByPercentKeepsCurrency() {
        check("80€ * 15%", rational: "12/1", dim: .currency, code: "EUR")
        check("80 € * 15%", rational: "12/1", dim: .currency, code: "EUR")
        check("€80 * 15%", rational: "12/1", dim: .currency, code: "EUR")
    }

    func testSameCurrencyAdds() {
        check("$10 + $5", rational: "15/1", dim: .currency, code: "USD")
        check("10€ + 5€", rational: "15/1", dim: .currency, code: "EUR")
    }

    func testCurrencyMixingRejected() {
        check("10€ + 5$", error: "currency-mixing")
    }

    func testDimensionMismatchRejected() {
        check("10€ + 5", error: "dimension-mismatch")
        check("100€ + 20%", error: "dimension-mismatch")
        check("10€ * 2€", error: "dimension-mismatch")
        check("5 / 2€", error: "dimension-mismatch")
    }

    func testCurrencyRatioIsDimensionless() {
        check("10€ / 2€", rational: "5/1", dim: .none)
        check("10€ / 2", rational: "5/1", dim: .currency, code: "EUR")
    }

    func testSymbolInsideNumberUnsupported() {
        check("12€50", error: "unsupported")
    }

    func testDimensionedExponentRejected() {
        check("10^2€", error: "dimension-mismatch")
        check("(10€)^2", error: "dimension-mismatch")
    }

    func testPlainExponentUnaffected() {
        check("2^3", rational: "8/1", dim: .none)
    }

    func testPercentIsDimensionlessScaling() {
        check("1 + 20%", rational: "6/5", dim: .none)
        check("100 + 20%", rational: "501/5", dim: .none)
        check("80 * (1 + 20%)", rational: "96/1", dim: .none)
        check("80€ * (1 + 20%)", rational: "96/1", dim: .currency, code: "EUR")
        check("15% + 5%", rational: "1/5", dim: .none)
    }

    func testLoneSymbolIsParseError() {
        check("€", error: "parse-error")
    }

    func testDiscountScenario() {
        check("80€ - 80€ * 15%", rational: "68/1", dim: .currency, code: "EUR")
    }

    // MARK: Propagation à travers le pipeline et certificat

    private func known(_ values: [UInt64]) -> [Rational] {
        values.map { Rational(integer: BigInt(magnitude: BigUInt($0), negative: false)) }
    }

    func testPipelinePropagatesDimensionsAcrossSteps() {
        let steps = [
            CalcPlanStep(id: "remise", expression: "80€ * 15%", unit: "€"),
            CalcPlanStep(id: "net", expression: "80€ - remise", unit: "€"),
        ]
        let results = CalcPipeline.run(steps, known: known([80, 15]))
        XCTAssertTrue(results[0].verified)
        XCTAssertEqual(results[0].dimension, "EUR")
        XCTAssertTrue(results[1].verified)
        XCTAssertEqual(results[1].dimension, "EUR")
        XCTAssertEqual(results[1].value?.displayFrench(), "68")
    }

    func testPipelineRejectsCrossStepDimensionMismatch() {
        let steps = [
            CalcPlanStep(id: "remise", expression: "80€ * 15%", unit: "€"),
            CalcPlanStep(id: "bad", expression: "remise + 5", unit: "€"),
        ]
        let results = CalcPipeline.run(steps, known: known([80, 15]))
        XCTAssertTrue(results[0].verified)
        XCTAssertFalse(results[1].verified, "remise (EUR) + 5 sans dimension doit être rejeté")
    }

    func testPipelineRejectsCrossStepCurrencyMixing() {
        let steps = [
            CalcPlanStep(id: "a", expression: "10€", unit: "€"),
            CalcPlanStep(id: "b", expression: "a + 5$", unit: ""),
        ]
        let results = CalcPipeline.run(steps, known: known([10, 5]))
        XCTAssertTrue(results[0].verified)
        XCTAssertFalse(results[1].verified)
    }

    func testCertificateShape() {
        let steps = [CalcPlanStep(id: "remise", expression: "80€ * 15%", unit: "€")]
        let results = CalcPipeline.run(steps, known: known([80, 15]))
        let certificate = CalcPipeline.certify(results, grounded: true)
        XCTAssertEqual(certificate.engine, "nomi-calc/1.1")
        XCTAssertEqual(certificate.status, "interpreted")
        XCTAssertEqual(certificate.verifier, "agree")
        XCTAssertEqual(certificate.grounding, "anchored")
        XCTAssertEqual(certificate.steps.count, 1)
        XCTAssertEqual(certificate.steps[0].dimension, "EUR")

        let ungrounded = CalcPipeline.certify(results, grounded: false)
        XCTAssertEqual(ungrounded.status, "ungrounded")
    }
}
