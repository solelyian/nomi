using System.Text.RegularExpressions;

namespace Nomi.CalcEngine;

/// <summary>
/// Noyau de calcul déterministe — implémentation Windows de calc-spec/SPEC.md v1.0.
/// Arithmétique rationnelle exacte ; aucune dépendance vers l'IA, le réseau,
/// l'horloge ou le hasard. Descente récursive sur l'arbre grammatical.
/// </summary>
public sealed partial class Kernel
{
    public const int MaxLength = 200;
    public const int MaxDepth = 32;
    public const int MaxExponent = 64;

    private enum TokenKind { Number, Identifier, Operator, End }

    private readonly record struct Token(TokenKind Kind, string Text, int Position);

    /// <summary>Normalisation de la frontière de lecture — SPEC 3.3.</summary>
    private static string Normalize(string expression)
    {
        var text = expression
            .Replace('×', '*').Replace('÷', '/').Replace('−', '-')
            .Replace("\u202F", "").Replace("\u00A0", "");
        // Espace simple entre un chiffre et un groupe d'exactement 3 chiffres = milliers.
        string previous;
        do
        {
            previous = text;
            text = ThousandsGrouping().Replace(text, "");
        } while (text != previous);
        return text;
    }

    private static List<Token> Tokenize(string expression)
    {
        if (expression.Length > MaxLength) throw new CalculationException("too-long");
        var tokens = new List<Token>();
        var index = 0;
        while (index < expression.Length)
        {
            var character = expression[index];
            if (char.IsWhiteSpace(character)) { index++; continue; }
            if (char.IsDigit(character) || character == '.')
            {
                var start = index;
                while (index < expression.Length && (char.IsDigit(expression[index]) || expression[index] is '.' or ',')) index++;
                tokens.Add(new Token(TokenKind.Number, expression[start..index], start));
                continue;
            }
            if (char.IsLetter(character) || character == '_')
            {
                var start = index;
                while (index < expression.Length && (char.IsLetterOrDigit(expression[index]) || expression[index] == '_')) index++;
                tokens.Add(new Token(TokenKind.Identifier, expression[start..index], start));
                continue;
            }
            if ("+-*/^()%".Contains(character))
            {
                tokens.Add(new Token(TokenKind.Operator, character.ToString(), index));
                index++;
                continue;
            }
            throw new CalculationException("parse-error");
        }
        tokens.Add(new Token(TokenKind.End, "", expression.Length));
        return tokens;
    }

    private static Rational ParseNumber(string raw)
    {
        // La virgule est lue comme séparateur décimal français — SPEC 3.3.
        var normalized = raw.Replace(',', '.');
        return Rational.ParseLiteral(normalized) ?? throw new CalculationException("parse-error");
    }

    private sealed class Parser(List<Token> tokens, IReadOnlyDictionary<string, Rational> names)
    {
        private int at;

        private Token Peek => tokens[Math.Min(at, tokens.Count - 1)];

        public Rational Parse()
        {
            var value = Expression(0);
            if (Peek.Kind != TokenKind.End)
            {
                // SPEC 2.1 : une fermante sans ouvrante est `parenthesis` ;
                // `trailing` ne concerne que les autres jetons résiduels.
                if (Peek is { Kind: TokenKind.Operator, Text: ")" }) throw new CalculationException("parenthesis");
                throw new CalculationException("trailing");
            }
            return value;
        }

        private Rational Expression(int depth)
        {
            var value = Term(depth);
            while (Peek is { Kind: TokenKind.Operator, Text: "+" or "-" })
            {
                var add = Peek.Text == "+";
                at++;
                var right = Term(depth);
                value = add ? value + right : value - right;
                CheckOverflow(value);
            }
            return value;
        }

        private Rational Term(int depth)
        {
            var value = Factor(depth);
            while (Peek is { Kind: TokenKind.Operator, Text: "*" or "/" })
            {
                var multiply = Peek.Text == "*";
                at++;
                var right = Factor(depth);
                value = multiply ? value * right : value / right; // division : Rational lève division-by-zero
                CheckOverflow(value);
            }
            return value;
        }

        private Rational Factor(int depth)
        {
            if (Peek is { Kind: TokenKind.Operator, Text: "-" }) { at++; return -Factor(depth); }
            if (Peek is { Kind: TokenKind.Operator, Text: "+" }) { at++; return Factor(depth); }
            var baseValue = Postfix(depth);
            if (Peek is { Kind: TokenKind.Operator, Text: "^" })
            {
                at++;
                var exponentValue = Factor(depth); // associatif à droite
                if (exponentValue.Denominator != System.Numerics.BigInteger.One
                    || exponentValue.Numerator > MaxExponent
                    || exponentValue.Numerator < -MaxExponent)
                    throw new CalculationException("exponent");
                var exponent = (int)exponentValue.Numerator;
                var result = baseValue.RaisedTo(exponent);
                CheckOverflow(result);
                return result;
            }
            return baseValue;
        }

        private Rational Postfix(int depth)
        {
            var value = Atom(depth);
            while (Peek is { Kind: TokenKind.Operator, Text: "%" })
            {
                at++;
                value /= new Rational(100, 1);
            }
            return value;
        }

        private Rational Atom(int depth)
        {
            if (depth > MaxDepth) throw new CalculationException("too-deep");
            var token = Peek;
            at++;
            switch (token.Kind)
            {
                case TokenKind.Number:
                    return ParseNumber(token.Text);
                case TokenKind.Identifier:
                    if (names.TryGetValue(token.Text, out var value)) return value;
                    throw new CalculationException("unknown-identifier");
                case TokenKind.Operator when token.Text == "(":
                {
                    var inner = Expression(depth + 1);
                    if (Peek is not { Kind: TokenKind.Operator, Text: ")" }) throw new CalculationException("parenthesis");
                    at++;
                    return inner;
                }
                default:
                    throw new CalculationException("parse-error");
            }
        }

        private static void CheckOverflow(Rational value)
        {
            if (value.ExceedsLimits) throw new CalculationException("overflow");
        }
    }

    /// <summary>Évalue une expression en rationnel exact.</summary>
    public Rational Evaluate(string expression, IReadOnlyDictionary<string, Rational>? names = null)
    {
        var tokens = Tokenize(Normalize(expression));
        return new Parser(tokens, names ?? new Dictionary<string, Rational>()).Parse();
    }

    [GeneratedRegex(@"(?<=\d) (?=\d{3}(?!\d))")]
    private static partial Regex ThousandsGrouping();
}
