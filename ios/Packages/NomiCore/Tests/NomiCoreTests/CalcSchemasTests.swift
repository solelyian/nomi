import XCTest
@testable import NomiCore

/// Catalogue fermé de schémas — deterministic_calculation_system.md §8.
/// Invariants de domaine et vérification inverse, en rationnels exacts.
final class CalcSchemasTests: XCTestCase {
    private func rat(_ value: Int) -> Rational {
        Rational(integer: BigInt(magnitude: BigUInt(UInt64(abs(value))), negative: value < 0))
    }

    private func rat(_ num: Int, _ den: Int) -> Rational {
        Rational(numerator: BigInt(magnitude: BigUInt(UInt64(abs(num))), negative: num < 0),
                 denominator: BigUInt(UInt64(den)))
    }

    private func slots(_ pairs: [(String, Rational)]) -> [String: Rational] {
        Dictionary(uniqueKeysWithValues: pairs)
    }

    func testRemise() throws {
        let outcome = try CalcSchemas.apply("remise", slots: slots([("prix", rat(80)), ("taux", rat(15, 100))]))
        XCTAssertEqual(outcome.values["final"], rat(68))
        XCTAssertEqual(outcome.values["remise"], rat(12))
        XCTAssertTrue(outcome.invariantsHeld)
        XCTAssertTrue(outcome.inverseVerified)
    }

    func testRemiseInvariantTauxHorsBornes() {
        XCTAssertThrowsError(try CalcSchemas.apply("remise", slots: slots([("prix", rat(80)), ("taux", rat(150, 100))]))) { error in
            XCTAssertEqual(error as? SchemaError, .invariantViolated("taux ∈ [0,1]"))
        }
        XCTAssertThrowsError(try CalcSchemas.apply("remise", slots: slots([("prix", rat(80)), ("taux", rat(-1, 10))])))
    }

    func testTvaAjouterEtRetirer() throws {
        let ajouter = try CalcSchemas.apply("tva", slots: slots([("montant", rat(100)), ("taux", rat(20, 100)), ("sens", rat(1))]))
        XCTAssertEqual(ajouter.values["ttc"], rat(120))
        // Retirer : inverse exact de ajouter — la cohérence HT/TTC est vérifiée à rebours.
        let retirer = try CalcSchemas.apply("tva", slots: slots([("montant", rat(120)), ("taux", rat(20, 100)), ("sens", rat(0))]))
        XCTAssertEqual(retirer.values["ht"], rat(100))
        XCTAssertTrue(retirer.inverseVerified)
    }

    func testTvaSensInvalide() {
        XCTAssertThrowsError(try CalcSchemas.apply("tva", slots: slots([("montant", rat(100)), ("taux", rat(20, 100)), ("sens", rat(2))]))) { error in
            XCTAssertEqual(error as? SchemaError, .invalidSlot("sens ∈ {0,1}"))
        }
    }

    func testHeuresTravail() throws {
        let outcome = try CalcSchemas.apply("heures_travail", slots: slots([("durees_total", rat(35)), ("taux_horaire", rat(15))]))
        XCTAssertEqual(outcome.values["total"], rat(525))
        XCTAssertTrue(outcome.inverseVerified)
    }

    func testRepartitionSommeExacte() throws {
        // 100 réparti en 1:1:1 — chaque part est 100/3 exactement, la somme est exactement 100.
        let outcome = try CalcSchemas.apply("repartition", slots: slots([
            ("total", rat(100)), ("part_a", rat(1)), ("part_b", rat(1)), ("part_c", rat(1)),
        ]))
        XCTAssertEqual(outcome.values["a"], rat(100, 3))
        let somme = outcome.values["a"]! + outcome.values["b"]! + outcome.values["c"]!
        XCTAssertEqual(somme, rat(100), "Σparts = total exactement en rationnels")
        XCTAssertTrue(outcome.inverseVerified)
    }

    func testRepartitionRatioZeroTotal() {
        XCTAssertThrowsError(try CalcSchemas.apply("repartition", slots: slots([
            ("total", rat(100)), ("part_a", rat(0)), ("part_b", rat(0)),
        ]))) { error in
            XCTAssertEqual(error as? SchemaError, .invariantViolated("Σratios ≠ 0"))
        }
    }

    func testRegleDeTrois() throws {
        let outcome = try CalcSchemas.apply("regle_de_trois", slots: slots([("a", rat(4)), ("b", rat(10)), ("c", rat(7))]))
        XCTAssertEqual(outcome.values["x"], rat(35, 2))
        XCTAssertTrue(outcome.inverseVerified)
    }

    func testRegleDeTroisDiviseurNul() {
        XCTAssertThrowsError(try CalcSchemas.apply("regle_de_trois", slots: slots([("a", rat(0)), ("b", rat(10)), ("c", rat(7))])))
    }

    func testMoyenne() throws {
        let outcome = try CalcSchemas.apply("moyenne", slots: slots([
            ("somme", rat(30)), ("n", rat(4)), ("min", rat(5)), ("max", rat(12)),
        ]))
        XCTAssertEqual(outcome.values["moyenne"], rat(15, 2))
        XCTAssertTrue(outcome.inverseVerified)
    }

    func testMoyenneInvariantBorne() {
        // Somme incohérente avec min/max déclarés : l'invariant bloque — un plan mal posé ne passe pas.
        XCTAssertThrowsError(try CalcSchemas.apply("moyenne", slots: slots([
            ("somme", rat(100)), ("n", rat(4)), ("min", rat(5)), ("max", rat(12)),
        ]))) { error in
            XCTAssertEqual(error as? SchemaError, .invariantViolated("min ≤ moyenne ≤ max"))
        }
    }

    func testVariation() throws {
        let outcome = try CalcSchemas.apply("variation", slots: slots([("avant", rat(80)), ("apres", rat(68))]))
        XCTAssertEqual(outcome.values["variation"], rat(-15, 100))
        XCTAssertTrue(outcome.inverseVerified)
    }

    // MARK: Voie pipeline — parsing, ancrage, application

    func testSchemaPlanParsingEtApplication() throws {
        let text = #"{"schema":"remise","slots":{"prix":"80","taux":"0.15"},"answer":"Le prix final est {final} €."}"#
        let plan = CalcPipeline.parseSchemaPlan(text)
        XCTAssertNotNil(plan)
        XCTAssertEqual(plan?.schema, "remise")
        XCTAssertEqual(plan?.slots.count, 2)
        let outcome = CalcPipeline.runSchema(plan!, known: slots([("prix", rat(80)), ("taux", rat(15))]).values.map { $0 } + [rat(80), rat(15)])
        XCTAssertNotNil(outcome)
        XCTAssertEqual(outcome?.values["final"], rat(68))
    }

    func testSchemaPlanSlotNonAncreBloque() {
        let text = #"{"schema":"remise","slots":{"prix":"80","taux":"0.15"},"answer":"x"}"#
        let plan = CalcPipeline.parseSchemaPlan(text)!
        // 80 connu mais pas 15 ni ses variantes : tout le plan est bloqué.
        XCTAssertNil(CalcPipeline.runSchema(plan, known: [rat(80)]))
        // Variante de taux : 0,15 dérive de 15 dans la demande — ancré.
        XCTAssertNotNil(CalcPipeline.runSchema(plan, known: [rat(80), rat(15)]))
    }

    func testSchemaPlanInvalide() {
        XCTAssertNil(CalcPipeline.parseSchemaPlan("pas de json"))
        XCTAssertNil(CalcPipeline.parseSchemaPlan(#"{"schema":"remise","slots":{"prix":"abc"},"answer":""}"#))
        // Schéma hors catalogue : runSchema refuse même si le parsing passe.
        let horsCatalogue = CalcPipeline.parseSchemaPlan(#"{"schema":"invente","slots":{},"answer":""}"#)
        if let horsCatalogue {
            XCTAssertNil(CalcPipeline.runSchema(horsCatalogue, known: []))
        }
    }

    func testCertificatSchemaVerified() throws {
        let outcome = try CalcSchemas.apply("remise", slots: slots([("prix", rat(80)), ("taux", rat(15, 100))]))
        let certificate = CalcPipeline.certifySchema(outcome, slots: [:])
        XCTAssertEqual(certificate.status, "verified")
        XCTAssertEqual(certificate.verifier, "inverse")
        XCTAssertNotNil(certificate.jsonString())
    }

    func testSchemaInconnuEtSlotManquant() {
        XCTAssertThrowsError(try CalcSchemas.apply("inexistant", slots: [:])) { error in
            XCTAssertEqual(error as? SchemaError, .unknownSchema("inexistant"))
        }
        XCTAssertThrowsError(try CalcSchemas.apply("remise", slots: slots([("prix", rat(80))]))) { error in
            XCTAssertEqual(error as? SchemaError, .missingSlot("taux"))
        }
    }
}
