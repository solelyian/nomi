namespace Nomi.CalcEngine;

/// <summary>Erreurs du catalogue de schémas — deterministic_calculation_system.md §8.</summary>
public sealed class SchemaException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

/// <summary>Résultat d'un schéma appliqué : valeurs exactes et détail de validation.</summary>
public sealed record SchemaOutcome(string Schema, IReadOnlyDictionary<string, Rational> Values, bool InvariantsHeld, bool InverseVerified);

/// <summary>
/// Catalogue fermé de schémas de calcul — deterministic_calculation_system.md §8.2.
/// Le modèle ne fait que classifier la demande et remplir les slots ; le calcul
/// est entièrement mécanique. Chaque schéma porte ses invariants de domaine et sa
/// vérification inverse — deux chemins indépendants qui doivent converger exactement.
/// </summary>
public static class Schemas
{
    public static readonly string[] Catalog = ["remise", "tva", "heures_travail", "repartition", "regle_de_trois", "moyenne", "variation"];

    private static readonly Rational Zero = new(0, 1);
    private static readonly Rational One = new(1, 1);

    private static Rational Require(IReadOnlyDictionary<string, Rational> slots, string name) =>
        slots.TryGetValue(name, out var value) ? value : throw new SchemaException($"missing-slot:{name}");

    private static Rational Rate(Rational numerator, Rational denominator) =>
        denominator.IsZero ? throw new SchemaException("division-by-zero") : numerator / denominator;

    public static SchemaOutcome Apply(string schema, IReadOnlyDictionary<string, Rational> slots) => schema switch
    {
        "remise" => Remise(slots),
        "tva" => Tva(slots),
        "heures_travail" => Heures(slots),
        "repartition" => Repartition(slots),
        "regle_de_trois" => RegleDeTrois(slots),
        "moyenne" => Moyenne(slots),
        "variation" => Variation(slots),
        _ => throw new SchemaException($"unknown-schema:{schema}"),
    };

    private static SchemaOutcome Remise(IReadOnlyDictionary<string, Rational> slots)
    {
        var prix = Require(slots, "prix");
        var taux = Require(slots, "taux");
        if (taux < Zero || taux > One) throw new SchemaException("invariant:taux ∈ [0,1]");
        var remise = prix * taux;
        var final = prix * (One - taux);
        if (final < Zero || final > prix) throw new SchemaException("invariant:0 ≤ final ≤ prix");
        if (!prix.IsZero)
        {
            if (Rate(final, prix) != One - taux) throw new SchemaException("inverse:final ÷ prix ≠ 1 − taux");
            if (Rate(remise, prix) != taux) throw new SchemaException("inverse:remise ÷ prix ≠ taux");
        }
        return new SchemaOutcome("remise", new Dictionary<string, Rational> { ["remise"] = remise, ["final"] = final }, true, true);
    }

    private static SchemaOutcome Tva(IReadOnlyDictionary<string, Rational> slots)
    {
        var montant = Require(slots, "montant");
        var taux = Require(slots, "taux");
        slots.TryGetValue("sens", out var sens);
        if (taux < Zero || taux > One) throw new SchemaException("invariant:taux ∈ [0,1]");
        var facteur = One + taux;
        if (sens == One)
        {
            var ttc = montant * facteur;
            if (Rate(ttc, facteur) != montant) throw new SchemaException("inverse:ttc ÷ (1+taux) ≠ ht");
            return new SchemaOutcome("tva", new Dictionary<string, Rational> { ["ht"] = montant, ["ttc"] = ttc }, true, true);
        }
        if (sens == Zero)
        {
            var ht = Rate(montant, facteur);
            // En arithmétique rationnelle exacte la division est toujours inversible
            // exactement — le recoupement inverse signale un défaut d'implémentation,
            // jamais une approximation.
            if (ht * facteur != montant) throw new SchemaException("inverse:ht × (1+taux) ≠ ttc");
            return new SchemaOutcome("tva", new Dictionary<string, Rational> { ["ht"] = ht, ["ttc"] = montant }, true, true);
        }
        throw new SchemaException("invalid-slot:sens ∈ {0,1}");
    }

    private static SchemaOutcome Heures(IReadOnlyDictionary<string, Rational> slots)
    {
        var durees = Require(slots, "durees_total");
        var taux = Require(slots, "taux_horaire");
        if (durees < Zero) throw new SchemaException("invariant:durées ≥ 0");
        var total = durees * taux;
        if (total < Zero) throw new SchemaException("invariant:total ≥ 0");
        if (!taux.IsZero && Rate(total, taux) != durees) throw new SchemaException("inverse:total ÷ taux ≠ Σdurées");
        return new SchemaOutcome("heures_travail", new Dictionary<string, Rational> { ["total"] = total }, true, true);
    }

    private static SchemaOutcome Repartition(IReadOnlyDictionary<string, Rational> slots)
    {
        var total = Require(slots, "total");
        var ratios = slots.Where(pair => pair.Key.StartsWith("part_") && pair.Key.Length > 5).ToList();
        if (ratios.Count == 0) throw new SchemaException("missing-slot:part_*");
        var sommeRatios = Zero;
        foreach (var (_, ratio) in ratios)
        {
            if (ratio < Zero) throw new SchemaException("invariant:ratios ≥ 0");
            sommeRatios += ratio;
        }
        if (sommeRatios.IsZero) throw new SchemaException("invariant:Σratios ≠ 0");
        var values = new Dictionary<string, Rational>();
        var sommeParts = Zero;
        foreach (var (name, ratio) in ratios)
        {
            var part = Rate(total * ratio, sommeRatios);
            values[name[5..]] = part;
            sommeParts += part;
        }
        // Invariant exact : la somme des parts est exactement le total — toujours vrai
        // en arithmétique rationnelle, vérifié quand même.
        if (sommeParts != total) throw new SchemaException("invariant:Σparts ≠ total");
        foreach (var (name, ratio) in ratios)
            if (values[name[5..]] * sommeRatios != total * ratio) throw new SchemaException("inverse:part × Σratios ≠ total × ratio");
        return new SchemaOutcome("repartition", values, true, true);
    }

    private static SchemaOutcome RegleDeTrois(IReadOnlyDictionary<string, Rational> slots)
    {
        var a = Require(slots, "a");
        var b = Require(slots, "b");
        var c = Require(slots, "c");
        if (a.IsZero) throw new SchemaException("invariant:a ≠ 0");
        var x = Rate(b * c, a);
        if (x * a != b * c) throw new SchemaException("inverse:x × a ≠ b × c");
        return new SchemaOutcome("regle_de_trois", new Dictionary<string, Rational> { ["x"] = x }, true, true);
    }

    private static SchemaOutcome Moyenne(IReadOnlyDictionary<string, Rational> slots)
    {
        var somme = Require(slots, "somme");
        var compte = Require(slots, "n");
        if (compte <= Zero) throw new SchemaException("invariant:n > 0");
        var moyenne = Rate(somme, compte);
        if (slots.TryGetValue("min", out var min) && slots.TryGetValue("max", out var max)
            && (moyenne < min || moyenne > max)) throw new SchemaException("invariant:min ≤ moyenne ≤ max");
        if (moyenne * compte != somme) throw new SchemaException("inverse:moyenne × n ≠ Σ");
        return new SchemaOutcome("moyenne", new Dictionary<string, Rational> { ["moyenne"] = moyenne }, true, true);
    }

    private static SchemaOutcome Variation(IReadOnlyDictionary<string, Rational> slots)
    {
        var avant = Require(slots, "avant");
        var apres = Require(slots, "apres");
        if (avant.IsZero) throw new SchemaException("invariant:avant ≠ 0");
        var variation = Rate(apres - avant, avant);
        if (avant * (One + variation) != apres) throw new SchemaException("inverse:avant × (1 + variation) ≠ après");
        return new SchemaOutcome("variation", new Dictionary<string, Rational> { ["variation"] = variation }, true, true);
    }
}
