import XCTest
@testable import NomiCore

/// Tests du pipeline déterministe — menaces du deterministic_calculation_system.md :
/// chiffres inventés, ancrage, composition mécanique, masquage des intraçables.
final class CalcPipelineTests: XCTestCase {

    private func rationals(_ texts: [String]) -> [Rational] {
        texts.compactMap { Rational(decimalLiteral: $0) }
    }

    // MARK: Lecture du plan

    func testParsesValidPlan() {
        let json = #"{"calculation": true, "steps": [{"id": "remise", "expr": "80*15/100", "unit": "€"}], "answer": "La remise est de {remise}."}"#
        let plan = CalcPipeline.parsePlan(json)
        XCTAssertTrue(plan.calculates)
        XCTAssertEqual(plan.steps.count, 1)
        XCTAssertEqual(plan.steps[0].id, "remise")
        XCTAssertEqual(plan.answer, "La remise est de {remise}.")
    }

    func testGarbageIsNotAPlan() {
        XCTAssertFalse(CalcPipeline.parsePlan("bonjour, pas de json ici").calculates)
        XCTAssertFalse(CalcPipeline.parsePlan("{invalid json").calculates)
        XCTAssertEqual(CalcPipeline.parsePlan(#"{"steps": "pas un tableau"}"#).steps.count, 0)
    }

    func testRejectsInvalidIdentifiers() {
        let json = #"{"steps": [{"id": "_mauvais", "expr": "1+1"}, {"id": "bon", "expr": "1+1"}], "answer": "x"}"#
        let plan = CalcPipeline.parsePlan(json)
        XCTAssertEqual(plan.steps.count, 1)
        XCTAssertEqual(plan.steps[0].id, "bon")
    }

    // MARK: Extraction et ancrage

    func testExtractsFrenchNumbers() {
        let numbers = CalcPipeline.extractNumbers("remise de 15% sur 80,50 € pour 1 250 articles")
        XCTAssertTrue(numbers.contains(Rational(decimalLiteral: "15")!))
        XCTAssertTrue(numbers.contains(Rational(decimalLiteral: "80.5")!))
        XCTAssertTrue(numbers.contains(Rational(decimalLiteral: "1250")!))
    }

    func testGroundingAcceptsRequestNumbersAndConstants() {
        let known = rationals(["80", "15"])
        XCTAssertTrue(CalcPipeline.isGrounded("80*15/100", known: known))
        XCTAssertTrue(CalcPipeline.isGrounded("80*0.15", known: known), "variante de taux 15/100")
        XCTAssertTrue(CalcPipeline.isGrounded("80*1.15", known: known), "variante 1+taux")
    }

    func testGroundingRejectsInventedNumbers() {
        let known = rationals(["80"])
        XCTAssertFalse(CalcPipeline.isGrounded("80*42", known: known), "42 n'est ni dans la demande ni admis")
        XCTAssertFalse(CalcPipeline.isGrounded("80*17/100", known: known), "17 inventé")
    }

    // MARK: Calcul recoupé

    func testRunVerifiesCorrectPlan() {
        let steps = [CalcPlanStep(id: "remise", expression: "80*15/100", unit: "€")]
        let results = CalcPipeline.run(steps, known: rationals(["80", "15"]))
        XCTAssertEqual(results.count, 1)
        XCTAssertTrue(results[0].verified)
        XCTAssertEqual(results[0].value, Rational(decimalLiteral: "12")!)
    }

    func testRunChainsStepsThroughIdentifiers() {
        let steps = [
            CalcPlanStep(id: "remise", expression: "80*15/100", unit: "€"),
            CalcPlanStep(id: "final", expression: "80-remise", unit: "€"),
        ]
        let results = CalcPipeline.run(steps, known: rationals(["80", "15"]))
        XCTAssertTrue(results.allSatisfy(\.verified))
        XCTAssertEqual(results[1].value, Rational(decimalLiteral: "68")!)
    }

    func testRunRejectsUngroundedAndBroken() {
        let invented = [CalcPlanStep(id: "x", expression: "80*42", unit: "")]
        XCTAssertFalse(CalcPipeline.run(invented, known: rationals(["80"]))[0].verified)
        let zeroDivision = [CalcPlanStep(id: "x", expression: "80/0", unit: "")]
        XCTAssertFalse(CalcPipeline.run(zeroDivision, known: rationals(["80"]))[0].verified)
    }

    // MARK: Composition mécanique

    func testComposeSubstitutesVerifiedValues() {
        let results = CalcPipeline.run(
            [CalcPlanStep(id: "remise", expression: "80*15/100", unit: "€")],
            known: rationals(["80", "15"]))
        let composed = CalcPipeline.compose("La remise est de {remise} €.", results: results)
        XCTAssertEqual(composed, "La remise est de 12 €.")
    }

    func testComposeFailsOnMissingStep() {
        let results = CalcPipeline.run(
            [CalcPlanStep(id: "remise", expression: "80*15/100", unit: "€")],
            known: rationals(["80", "15"]))
        XCTAssertNil(CalcPipeline.compose("total {inconnu}", results: results))
        XCTAssertNil(CalcPipeline.compose("", results: results))
    }

    // MARK: Masquage

    func testMaskHidesInventedNumbers() {
        let allowed = rationals(["68", "80", "15"])
        let masked = CalcPipeline.mask("Le total est 91 € pour 68 € remisés.", allowed: allowed)
        XCTAssertEqual(masked.hidden, 1)
        XCTAssertTrue(masked.text.contains("…"))
        XCTAssertTrue(masked.text.contains("68 €"))
    }

    func testMaskKeepsStructuralSmallIntsAndDates() {
        let masked = CalcPipeline.mask("En 3 étapes, rendez-vous le 12/05/2026 à 14h30.", allowed: [])
        XCTAssertEqual(masked.hidden, 0)
    }

    func testMaskAcceptsRoundedDisplay() {
        // 1/3 affiché arrondi 0,3333 : la comparaison à l'échelle du fragment doit passer.
        let third = Rational(numerator: BigInt(magnitude: BigUInt(1), negative: false), denominator: BigUInt(3))
        let masked = CalcPipeline.mask("Un tiers vaut environ 0,3333.", allowed: [third])
        XCTAssertEqual(masked.hidden, 0)
    }

    // MARK: Scénario bout en bout

    func testEndToEndRemise() {
        let question = "Quelle est la remise de 15% sur 80 € et le prix final ?"
        let plan = CalcPipeline.parsePlan(#"{"steps": [{"id": "remise", "expr": "80*15/100", "unit": "€"}, {"id": "final", "expr": "80-remise", "unit": "€"}], "answer": "La remise est de {remise} €, le prix final de {final} €."}"#)
        XCTAssertTrue(plan.calculates)
        let results = CalcPipeline.run(plan.steps, known: CalcPipeline.extractNumbers(question))
        XCTAssertTrue(results.allSatisfy(\.verified), "tout ancré dans la demande")
        let answer = CalcPipeline.compose(plan.answer, results: results)
        XCTAssertEqual(answer, "La remise est de 12 €, le prix final de 68 €.")
        let posed = CalcPipeline.posed(results)
        XCTAssertEqual(posed.count, 2)
        XCTAssertTrue(posed[0].contains("12"))
    }

    func testEndToEndInventedOperandsStayUnverified() {
        // Le modèle hallucine un taux de 20 % absent de la demande.
        let question = "Quelle est la remise de 15% sur 80 € ?"
        let plan = CalcPipeline.parsePlan(#"{"steps": [{"id": "remise", "expr": "80*20/100", "unit": "€"}], "answer": "{remise}"}"#)
        let results = CalcPipeline.run(plan.steps, known: CalcPipeline.extractNumbers(question))
        XCTAssertFalse(results[0].verified, "20 % est inventé : jamais vérifié")
        XCTAssertNil(CalcPipeline.compose(plan.answer, results: results))
    }
}

// MARK: — Masquage des nombres en toutes lettres (SPEC 7.2)
final class NumberWordsMaskTests: XCTestCase {
    private func rat(_ value: Int) -> Rational {
        Rational(integer: BigInt(magnitude: BigUInt(UInt64(value)), negative: false))
    }

    func testParseSimpleWords() {
        XCTAssertEqual(NumberWords.parse("soixante-huit"), 68)
        XCTAssertEqual(NumberWords.parse("quatre-vingt-douze"), 92)
        XCTAssertEqual(NumberWords.parse("vingt"), 20)
        XCTAssertEqual(NumberWords.parse("soixante-dix"), 70)
        XCTAssertEqual(NumberWords.parse("soixante-quinze"), 75)
        XCTAssertNil(NumberWords.parse("bonjour"))
    }

    func testParseSequences() {
        XCTAssertEqual(NumberWords.parseSequence(["deux", "cent", "quarante-deux"])?.value, 242)
        XCTAssertEqual(NumberWords.parseSequence(["mille", "deux", "cents"])?.value, 1200)
        XCTAssertEqual(NumberWords.parseSequence(["cent", "vingt"])?.value, 120)
    }

    func testMaskSpelledOutUntraceable() {
        let masked = CalcPipeline.mask("Le total est de soixante-huit euros.", allowed: [])
        XCTAssertTrue(masked.text.contains("…"), "le nombre en lettres intraçable est masqué : \(masked.text)")
        XCTAssertEqual(masked.hidden, 1)
    }

    func testMaskSpelledOutTraceablePasses() {
        let masked = CalcPipeline.mask("Le total est de soixante-huit euros.", allowed: [rat(68)])
        XCTAssertEqual(masked.hidden, 0)
        XCTAssertEqual(masked.text, "Le total est de soixante-huit euros.")
    }

    func testMaskSpelledOutConstantsPass() {
        // 0-10 sont des constantes structurelles — traçables sans provenance.
        let masked = CalcPipeline.mask("Il y a trois étapes.", allowed: [])
        XCTAssertEqual(masked.hidden, 0)
    }
}
