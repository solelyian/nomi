import Foundation

/// Catalogue fermé de schémas de calcul — deterministic_calculation_system.md §8.2.
/// Le modèle ne fait que classifier la demande et remplir les slots ; le calcul
/// est entièrement mécanique. Chaque schéma porte ses invariants de domaine et sa
/// vérification inverse — deux chemins de calcul indépendants qui doivent
/// converger exactement, comme le recoupement arithmétique.
public enum SchemaError: Error, Equatable, Sendable {
    case unknownSchema(String)
    case missingSlot(String)
    case invalidSlot(String)
    case invariantViolated(String)
    case inverseMismatch(String)
    case divisionByZero
}

/// Résultat d'un schéma appliqué : les valeurs calculées exactement, avec le
/// détail de la validation pour le certificat.
public struct SchemaOutcome: Equatable, Sendable {
    public let schema: String
    public let values: [String: Rational]
    public let invariantsHeld: Bool
    public let inverseVerified: Bool
}

public enum CalcSchemas {
    public static let catalog: [String] = ["remise", "tva", "heures_travail", "repartition", "regle_de_trois", "moyenne", "variation"]

    private static func require(_ slots: [String: Rational], _ name: String) throws -> Rational {
        guard let value = slots[name] else { throw SchemaError.missingSlot(name) }
        return value
    }

    private static var zero: Rational { Rational(integer: BigInt(magnitude: BigUInt(0), negative: false)) }
    private static var one: Rational { Rational(integer: BigInt(magnitude: BigUInt(1), negative: false)) }
    private static func rational(_ value: UInt64) -> Rational { Rational(integer: BigInt(magnitude: BigUInt(value), negative: false)) }
    private static func rate(_ numerator: Rational, _ denominator: Rational) throws -> Rational {
        guard !denominator.isZero else { throw SchemaError.divisionByZero }
        return numerator / denominator
    }

    /// Applique un schéma du catalogue : calcul exact de la formule, vérification
    /// des invariants de domaine, puis vérification inverse — tout écart est bloquant.
    public static func apply(_ schema: String, slots: [String: Rational]) throws -> SchemaOutcome {
        switch schema {
        case "remise": return try applyRemise(slots)
        case "tva": return try applyTva(slots)
        case "heures_travail": return try applyHeures(slots)
        case "repartition": return try applyRepartition(slots)
        case "regle_de_trois": return try applyRegleDeTrois(slots)
        case "moyenne": return try applyMoyenne(slots)
        case "variation": return try applyVariation(slots)
        default: throw SchemaError.unknownSchema(schema)
        }
    }

    // MARK: remise — final = prix × (1 − taux) ; inverse : final ÷ prix = 1 − taux

    private static func applyRemise(_ slots: [String: Rational]) throws -> SchemaOutcome {
        let prix = try require(slots, "prix")
        let taux = try require(slots, "taux")
        guard taux >= zero, taux <= one else { throw SchemaError.invariantViolated("taux ∈ [0,1]") }
        let remise = prix * taux
        let final = prix * (one - taux)
        guard final >= zero, final <= prix else { throw SchemaError.invariantViolated("0 ≤ final ≤ prix") }
        if !prix.isZero {
            guard try rate(final, prix) == one - taux else { throw SchemaError.inverseMismatch("final ÷ prix ≠ 1 − taux") }
            guard try rate(remise, prix) == taux else { throw SchemaError.inverseMismatch("remise ÷ prix ≠ taux") }
        }
        return SchemaOutcome(schema: "remise", values: ["remise": remise, "final": final], invariantsHeld: true, inverseVerified: true)
    }

    // MARK: tva — ajouter : ttc = ht × (1 + taux) ; retirer : ht = ttc ÷ (1 + taux)

    private static func applyTva(_ slots: [String: Rational]) throws -> SchemaOutcome {
        let montant = try require(slots, "montant")
        let taux = try require(slots, "taux")
        let sens = slots["sens"] // 1 = ajouter, 0 = retirer — codage mécanique, pas de texte libre
        guard taux >= zero, taux <= one else { throw SchemaError.invariantViolated("taux ∈ [0,1]") }
        let facteur = one + taux
        if sens == one {
            let ttc = montant * facteur
            guard try rate(ttc, facteur) == montant else { throw SchemaError.inverseMismatch("ttc ÷ (1+taux) ≠ ht") }
            return SchemaOutcome(schema: "tva", values: ["ht": montant, "ttc": ttc], invariantsHeld: true, inverseVerified: true)
        }
        if sens == zero {
            let ht = try rate(montant, facteur)
            // En arithmétique rationnelle exacte la division est toujours inversible
            // exactement — la vérification inverse est un recoupement de chemin,
            // comme le vérificateur arithmétique : une divergence signale un défaut
            // d'implémentation, jamais une approximation.
            guard ht * facteur == montant else { throw SchemaError.inverseMismatch("ht × (1+taux) ≠ ttc") }
            return SchemaOutcome(schema: "tva", values: ["ht": ht, "ttc": montant], invariantsHeld: true, inverseVerified: true)
        }
        throw SchemaError.invalidSlot("sens ∈ {0,1}")
    }

    // MARK: heures_travail — total = Σdurées × taux_horaire ; inverse : total ÷ taux = Σdurées

    private static func applyHeures(_ slots: [String: Rational]) throws -> SchemaOutcome {
        let durees = try require(slots, "durees_total") // somme mécanique assemblée en amont
        let taux = try require(slots, "taux_horaire")
        guard durees >= zero else { throw SchemaError.invariantViolated("durées ≥ 0") }
        let total = durees * taux
        guard total >= zero else { throw SchemaError.invariantViolated("total ≥ 0") }
        if !taux.isZero {
            guard try rate(total, taux) == durees else { throw SchemaError.inverseMismatch("total ÷ taux ≠ Σdurées") }
        }
        return SchemaOutcome(schema: "heures_travail", values: ["total": total], invariantsHeld: true, inverseVerified: true)
    }

    // MARK: repartition — part_i = total × ratio_i ÷ Σratios ; invariant : Σpart_i = total

    private static func applyRepartition(_ slots: [String: Rational]) throws -> SchemaOutcome {
        let total = try require(slots, "total")
        // parts nommées : slots "part_<nom>" = ratio_i — assemblage mécanique en amont
        let ratios = slots.filter { $0.key.hasPrefix("part_") && $0.key != "part_" }
        guard !ratios.isEmpty else { throw SchemaError.missingSlot("part_*") }
        var sommeRatios = zero
        for (_, ratio) in ratios {
            guard ratio >= zero else { throw SchemaError.invariantViolated("ratios ≥ 0") }
            sommeRatios = sommeRatios + ratio
        }
        guard !sommeRatios.isZero else { throw SchemaError.invariantViolated("Σratios ≠ 0") }
        var values: [String: Rational] = [:]
        var sommeParts = zero
        for (name, ratio) in ratios {
            let part = try rate(total * ratio, sommeRatios)
            values[String(name.dropFirst(5))] = part
            sommeParts = sommeParts + part
        }
        // Invariant exact : la somme des parts est exactement le total — toujours vrai
        // en arithmétique rationnelle (distributivité exacte), vérifié quand même.
        guard sommeParts == total else { throw SchemaError.invariantViolated("Σparts ≠ total") }
        // Inverse : part_i × Σratios = total × ratio_i pour chaque part.
        for (name, ratio) in ratios {
            let part = values[String(name.dropFirst(5))]!
            guard part * sommeRatios == total * ratio else { throw SchemaError.inverseMismatch("part × Σratios ≠ total × ratio") }
        }
        return SchemaOutcome(schema: "repartition", values: values, invariantsHeld: true, inverseVerified: true)
    }

    // MARK: regle_de_trois — x = b × c ÷ a ; inverse : x × a = b × c

    private static func applyRegleDeTrois(_ slots: [String: Rational]) throws -> SchemaOutcome {
        let a = try require(slots, "a")
        let b = try require(slots, "b")
        let c = try require(slots, "c")
        guard !a.isZero else { throw SchemaError.invariantViolated("a ≠ 0") }
        let x = try rate(b * c, a)
        guard x * a == b * c else { throw SchemaError.inverseMismatch("x × a ≠ b × c") }
        return SchemaOutcome(schema: "regle_de_trois", values: ["x": x], invariantsHeld: true, inverseVerified: true)
    }

    // MARK: moyenne — Σ ÷ n ; invariant : min ≤ moyenne ≤ max ; inverse : moyenne × n = Σ

    private static func applyMoyenne(_ slots: [String: Rational]) throws -> SchemaOutcome {
        let somme = try require(slots, "somme") // somme mécanique assemblée en amont
        let compte = try require(slots, "n")
        guard compte > zero else { throw SchemaError.invariantViolated("n > 0") }
        let moyenne = try rate(somme, compte)
        if let minValue = slots["min"], let maxValue = slots["max"] {
            guard moyenne >= minValue, moyenne <= maxValue else { throw SchemaError.invariantViolated("min ≤ moyenne ≤ max") }
        }
        guard moyenne * compte == somme else { throw SchemaError.inverseMismatch("moyenne × n ≠ Σ") }
        return SchemaOutcome(schema: "moyenne", values: ["moyenne": moyenne], invariantsHeld: true, inverseVerified: true)
    }

    // MARK: variation — (après − avant) ÷ avant ; inverse : avant × (1 + variation) = après

    private static func applyVariation(_ slots: [String: Rational]) throws -> SchemaOutcome {
        let avant = try require(slots, "avant")
        let apres = try require(slots, "apres")
        guard !avant.isZero else { throw SchemaError.invariantViolated("avant ≠ 0") }
        let variation = try rate(apres - avant, avant)
        guard avant * (one + variation) == apres else { throw SchemaError.inverseMismatch("avant × (1 + variation) ≠ après") }
        return SchemaOutcome(schema: "variation", values: ["variation": variation], invariantsHeld: true, inverseVerified: true)
    }
}
