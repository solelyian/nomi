using System.Numerics;
using System.Text;

namespace Nomi.CalcEngine;

/// <summary>
/// Rationnel exact : numérateur signé / dénominateur strictement positif, toujours
/// réduit. Aucun arrondi en dehors de l'affichage. Référence : calc-spec/SPEC.md section 1.
/// </summary>
public readonly struct Rational : IEquatable<Rational>, IComparable<Rational>
{
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; } // strictement positif

    public const int DigitLimit = 10_000;

    public Rational(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero) throw new CalculationException("division-by-zero");
        if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
        if (numerator.IsZero) { Numerator = BigInteger.Zero; Denominator = BigInteger.One; return; }
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        Numerator = numerator / gcd;
        Denominator = denominator / gcd;
    }

    public static Rational Zero => new(BigInteger.Zero, BigInteger.One);

    /// <summary>Lit un littéral décimal canonique en fraction exacte.</summary>
    public static Rational? ParseLiteral(string text)
    {
        var body = text;
        var negative = false;
        if (body.StartsWith('-')) { negative = true; body = body[1..]; }
        if (body.StartsWith('+')) body = body[1..];
        if (body.Length == 0) return null;
        var dot = body.IndexOf('.');
        if (dot != body.LastIndexOf('.')) return null;
        var digits = body.Replace(".", "");
        if (digits.Length == 0 || !digits.All(char.IsDigit)) return null;
        var numerator = BigInteger.Parse(digits);
        if (negative) numerator = -numerator;
        var denominator = BigInteger.One;
        if (dot >= 0) denominator = BigInteger.Pow(10, body.Length - dot - 1);
        return new Rational(numerator, denominator);
    }

    public bool IsZero => Numerator.IsZero;

    public bool ExceedsLimits =>
        DigitCount(BigInteger.Abs(Numerator)) > DigitLimit || DigitCount(BigInteger.Abs(Denominator)) > DigitLimit;

    private static int DigitCount(BigInteger value) => value.IsZero ? 1 : value.ToString().Length;

    public static Rational operator +(Rational a, Rational b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);

    public static Rational operator -(Rational a, Rational b) =>
        new(a.Numerator * b.Denominator - b.Numerator * a.Denominator, a.Denominator * b.Denominator);

    public static Rational operator -(Rational a) => new(-a.Numerator, a.Denominator);

    public static Rational operator *(Rational a, Rational b) =>
        new(a.Numerator * b.Numerator, a.Denominator * b.Denominator);

    public static Rational operator /(Rational a, Rational b)
    {
        if (b.IsZero) throw new CalculationException("division-by-zero");
        return new Rational(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
    }

    public int CompareTo(Rational other) =>
        (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);

    public bool Equals(Rational other) => Numerator == other.Numerator && Denominator == other.Denominator;
    public override bool Equals(object? obj) => obj is Rational other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);
    public static bool operator ==(Rational a, Rational b) => a.Equals(b);
    public static bool operator !=(Rational a, Rational b) => !a.Equals(b);

    /// <summary>Puissance exacte, exposant entier borné par le noyau avant appel.</summary>
    public Rational RaisedTo(int exponent)
    {
        if (exponent >= 0)
            return new Rational(BigInteger.Pow(Numerator, exponent), BigInteger.Pow(Denominator, exponent));
        if (IsZero) throw new CalculationException("division-by-zero");
        var positive = -exponent;
        return new Rational(BigInteger.Pow(Denominator, positive), BigInteger.Pow(Numerator, positive));
    }

    /// <summary>Arrondi half-up (away from zero) exact à <paramref name="places"/> décimales — SPEC 1.3.</summary>
    public Rational RoundedHalfUp(int places)
    {
        var scale = BigInteger.Pow(10, places);
        var scaled = BigInteger.Abs(Numerator) * scale;
        var quotient = scaled / Denominator;
        var remainder = scaled % Denominator;
        if (remainder * 2 >= Denominator) quotient += 1;
        if (Numerator.Sign < 0) quotient = -quotient;
        return new Rational(quotient, scale);
    }

    /// <summary>Affichage français conforme SPEC 1.3/1.4.</summary>
    public string DisplayFrench(int places = 4)
    {
        var rounded = RoundedHalfUp(places);
        var scale = BigInteger.Pow(10, places);
        var scaledMagnitude = BigInteger.Abs(rounded.Numerator) * scale / rounded.Denominator;
        var digits = scaledMagnitude.ToString();
        if (digits.Length <= places) digits = new string('0', places + 1 - digits.Length) + digits;
        var intPart = digits[..^places];
        var fracPart = digits[^places..].TrimEnd('0');
        intPart = GroupThousands(intPart);
        var sign = rounded.Numerator.Sign < 0 ? "-" : "";
        return fracPart.Length == 0 ? sign + intPart : sign + intPart + "," + fracPart;
    }

    /// <summary>Groupement par trois U+202F dès 4 chiffres — SPEC 1.4.</summary>
    public static string GroupThousands(string intPart)
    {
        if (intPart.Length <= 3) return intPart;
        var groups = new List<string>();
        var remaining = intPart;
        while (remaining.Length > 0)
        {
            var take = Math.Min(3, remaining.Length);
            groups.Insert(0, remaining[^take..]);
            remaining = remaining[..^take];
        }
        return string.Join("\u202F", groups);
    }

    public override string ToString() => $"{Numerator}/{Denominator}";
}

public sealed class CalculationException(string code) : Exception(code)
{
    public string Code => Message;
}
