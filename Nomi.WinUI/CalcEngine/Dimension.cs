namespace Nomi.CalcEngine;

/// <summary>Nature dimensionnelle d'une valeur — SPEC 3.1 (v1.1).</summary>
public enum DimKind { None, Currency, Duration }

/// <summary>
/// Dimension portée par une valeur calculée. Le pourcentage n'est PAS une
/// dimension : `%` est une mise à l'échelle sans dimension (SPEC v1.1) —
/// `currency × percent → currency` en découle naturellement.
/// </summary>
public readonly record struct Dimension(DimKind Kind, string Code)
{
    public static readonly Dimension None = new(DimKind.None, "");

    public static Dimension CurrencyOf(char symbol) => new(DimKind.Currency, symbol switch
    {
        '€' => "EUR",
        '$' => "USD",
        '£' => "GBP",
        _ => throw new CalculationException("parse-error"),
    });

    public bool IsNone => Kind == DimKind.None;
}

/// <summary>Valeur dimensionnée : le rationnel exact et sa dimension.</summary>
public readonly record struct Measured(Rational Value, Dimension Dim)
{
    public static Measured Plain(Rational value) => new(value, Dimension.None);

    /// <summary>Addition/soustraction — dimensions identiques exigées (SPEC 3.1).</summary>
    public static Measured Add(Measured left, Measured right, bool add)
    {
        CheckCompatible(left.Dim, right.Dim);
        return new Measured(add ? left.Value + right.Value : left.Value - right.Value, left.Dim);
    }

    /// <summary>Multiplication — la dimension unique survit ; deux dimensions
    /// non triviales sont un contresens (`€ × €`, `€ × h`).</summary>
    public static Measured Multiply(Measured left, Measured right)
    {
        var dim = left.Dim.IsNone ? right.Dim : right.Dim.IsNone ? left.Dim
            : throw new CalculationException("dimension-mismatch");
        return new Measured(left.Value * right.Value, dim);
    }

    /// <summary>Division — `d ÷ d` donne un ratio sans dimension ; `d ÷ none`
    /// conserve la dimension ; diviser un sans-dimension par une dimension est
    /// un contresens. La division par zéro est levée par le rationnel.</summary>
    public static Measured Divide(Measured left, Measured right)
    {
        if (left.Dim.IsNone && !right.Dim.IsNone) throw new CalculationException("dimension-mismatch");
        if (!left.Dim.IsNone && !right.Dim.IsNone)
        {
            CheckCompatible(left.Dim, right.Dim);
            return new Measured(left.Value / right.Value, Dimension.None);
        }
        return new Measured(left.Value / right.Value, left.Dim);
    }

    private static void CheckCompatible(Dimension left, Dimension right)
    {
        if (left.Kind != right.Kind) throw new CalculationException("dimension-mismatch");
        if (left.Kind == DimKind.Currency && left.Code != right.Code) throw new CalculationException("currency-mixing");
        if (left.Kind == DimKind.Duration && left.Code != right.Code) throw new CalculationException("dimension-mismatch");
    }
}
