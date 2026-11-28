using System.Numerics;
using System.Text.RegularExpressions;

namespace Nomi.CalcEngine;

/// <summary>
/// Vérificateur indépendant du noyau — calc-spec/SPEC.md section 4.
/// Machine à pile (shunting-yard), fractions non réduites en cours de calcul :
/// chemin structurellement différent de la descente récursive du noyau.
/// </summary>
public sealed partial class VerifierEngine
{
    private enum SymbolKind { Value, Operator, LeftParen }

    private readonly record struct Symbol(SymbolKind Kind, Measured Value, char Operator);

    private enum ScanKind { Number, Identifier, Operator, LeftParen, RightParen, Currency }

    private readonly record struct Scanned(ScanKind Kind, string Text, string Currency = "");

    /// <summary>Tokenise indépendamment du noyau — aucune fonction partagée.</summary>
    private static List<Scanned> Scan(string input)
    {
        var text = input
            .Replace('×', '*').Replace('÷', '/').Replace('−', '-')
            .Replace("\u202F", "").Replace("\u00A0", "");
        string previous;
        do
        {
            previous = text;
            text = Grouping().Replace(text, "");
        } while (text != previous);
        if (text.Length > Kernel.MaxLength) throw new CalculationException("too-long");

        var tokens = new List<Scanned>();
        var index = 0;
        while (index < text.Length)
        {
            var character = text[index];
            if (char.IsWhiteSpace(character)) { index++; continue; }
            if (character >= '0' && character <= '9')
            {
                var start = index;
                while (index < text.Length && ((text[index] >= '0' && text[index] <= '9') || text[index] is '.' or ',')) index++;
                tokens.Add(new Scanned(ScanKind.Number, text[start..index]));
                continue;
            }
            if ((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z') || character == '_')
            {
                var start = index;
                while (index < text.Length && ((text[index] >= 'a' && text[index] <= 'z') || (text[index] >= 'A' && text[index] <= 'Z') || (text[index] >= '0' && text[index] <= '9') || text[index] == '_')) index++;
                tokens.Add(new Scanned(ScanKind.Identifier, text[start..index]));
                continue;
            }
            if (character is '€' or '$' or '£')
            {
                tokens.Add(new Scanned(ScanKind.Currency, character.ToString()));
                index++;
                continue;
            }
            switch (character)
            {
                case '(': tokens.Add(new Scanned(ScanKind.LeftParen, "(")); break;
                case ')': tokens.Add(new Scanned(ScanKind.RightParen, ")")); break;
                case '+' or '-' or '*' or '/' or '^' or '%':
                    tokens.Add(new Scanned(ScanKind.Operator, character.ToString()));
                    break;
                default: throw new CalculationException("parse-error");
            }
            index++;
        }
        // Attache les symboles monétaires au nombre adjacent (SPEC 3.2) —
        // écrit indépendamment du noyau : symbole entre deux nombres = unsupported,
        // symbole isolé = parse-error.
        var attached = new List<Scanned>();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind != ScanKind.Currency) { attached.Add(token); continue; }
            var before = attached.Count > 0 && attached[^1].Kind == ScanKind.Number;
            var after = i + 1 < tokens.Count && tokens[i + 1].Kind == ScanKind.Number;
            if (before && after) throw new CalculationException("unsupported");
            if (!before && !after) throw new CalculationException("parse-error");
            var code = Dimension.CurrencyOf(token.Text[0]).Code;
            if (before)
            {
                if (attached[^1].Currency != "") throw new CalculationException("parse-error");
                attached[^1] = attached[^1] with { Currency = code };
            }
            else
            {
                if (tokens[i + 1].Currency != "") throw new CalculationException("parse-error");
                tokens[i + 1] = tokens[i + 1] with { Currency = code };
            }
        }
        return attached;
    }

    /// <summary>Évalue par machine à pile. Mêmes erreurs typées que la spec.</summary>
    public Rational Evaluate(string input, IReadOnlyDictionary<string, Rational>? names = null)
    {
        var lifted = names?.ToDictionary(pair => pair.Key, pair => Measured.Plain(pair.Value));
        return EvaluateDimensional(input, lifted).Value;
    }

    /// <summary>Évalue en valeur dimensionnée — SPEC 3.1, machine à pile.</summary>
    public Measured EvaluateDimensional(string input, IReadOnlyDictionary<string, Measured>? names = null)
    {
        names ??= new Dictionary<string, Measured>();
        var tokens = Scan(input);
        // SPEC 2.1 : l'équilibre des parenthèses est structurel — vérifié avant
        // toute évaluation, comme dans le noyau (`(1/0` est `parenthesis`).
        var parenthesisBalance = 0;
        foreach (var scanned in tokens)
        {
            if (scanned.Kind == ScanKind.LeftParen) parenthesisBalance++;
            if (scanned.Kind == ScanKind.RightParen)
            {
                parenthesisBalance--;
                if (parenthesisBalance < 0) throw new CalculationException("parenthesis");
            }
        }
        if (parenthesisBalance > 0) throw new CalculationException("parenthesis");
        var output = new Stack<Symbol>();
        var operators = new Stack<char>();
        var expectValue = true;
        var depth = 0;

        static int Precedence(char op) => op switch
        {
            '+' or '-' => 1,
            '*' or '/' => 2,
            '~' => 3,
            '^' => 4,
            _ => 0,
        };

        void ApplyTop()
        {
            if (operators.Count == 0) throw new CalculationException("parse-error");
            var op = operators.Pop();
            if (op is '~' or '%')
            {
                if (output.Count == 0 || output.Pop() is not { Kind: SymbolKind.Value } top) throw new CalculationException("parse-error");
                var unaryResult = op == '~'
                    ? new Measured(-top.Value.Value, top.Value.Dim)
                    : new Measured(top.Value.Value / new Rational(100, 1), top.Value.Dim);
                if (unaryResult.Value.ExceedsLimits) throw new CalculationException("overflow");
                output.Push(new Symbol(SymbolKind.Value, unaryResult, '\0'));
                return;
            }
            if (output.Count < 2) throw new CalculationException("parse-error");
            var rightSymbol = output.Pop();
            var leftSymbol = output.Pop();
            if (rightSymbol.Kind != SymbolKind.Value || leftSymbol.Kind != SymbolKind.Value) throw new CalculationException("parse-error");
            var left = leftSymbol.Value;
            var right = rightSymbol.Value;
            Measured result;
            switch (op)
            {
                case '+': result = Measured.Add(left, right, true); break;
                case '-': result = Measured.Add(left, right, false); break;
                case '*': result = Measured.Multiply(left, right); break;
                case '/': result = Measured.Divide(left, right); break;
                case '^':
                    if (!left.Dim.IsNone || !right.Dim.IsNone) throw new CalculationException("dimension-mismatch");
                    if (right.Value.Denominator != System.Numerics.BigInteger.One
                        || right.Value.Numerator > Kernel.MaxExponent
                        || right.Value.Numerator < -Kernel.MaxExponent)
                        throw new CalculationException("exponent");
                    result = Measured.Plain(left.Value.RaisedTo((int)right.Value.Numerator));
                    break;
                default: throw new CalculationException("parse-error");
            }
            if (result.Value.ExceedsLimits) throw new CalculationException("overflow");
            output.Push(new Symbol(SymbolKind.Value, result, '\0'));
        }

        foreach (var token in tokens)
        {
            switch (token.Kind)
            {
                case ScanKind.Number:
                {
                    if (!expectValue) throw new CalculationException("trailing");
                    var normalized = token.Text.Replace(',', '.');
                    var parsed = Rational.ParseLiteral(normalized) ?? throw new CalculationException("parse-error");
                    var dim = token.Currency == "" ? Dimension.None : new Dimension(DimKind.Currency, token.Currency);
                    output.Push(new Symbol(SymbolKind.Value, new Measured(parsed, dim), '\0'));
                    expectValue = false;
                    break;
                }
                case ScanKind.Identifier:
                    if (!expectValue) throw new CalculationException("trailing");
                    if (token.Text.Length > 32 || token.Text[0] is < 'a' or > 'z' && token.Text[0] is < 'A' or > 'Z')
                        throw new CalculationException("parse-error");
                    if (!names.TryGetValue(token.Text, out var named)) throw new CalculationException("unknown-identifier");
                    output.Push(new Symbol(SymbolKind.Value, named, '\0'));
                    expectValue = false;
                    break;
                case ScanKind.LeftParen:
                    if (!expectValue) throw new CalculationException("trailing");
                    depth++;
                    if (depth > Kernel.MaxDepth) throw new CalculationException("too-deep");
                    operators.Push('(');
                    expectValue = true;
                    break;
                case ScanKind.RightParen:
                    depth--;
                    // SPEC 2.1 : une fermante sans ouvrante est `parenthesis` — vérifié
                    // avant d'appliquer les opérateurs en attente (`2+)` reste `parenthesis`).
                    if (!operators.Contains('(')) throw new CalculationException("parenthesis");
                    while (operators.Peek() != '(') ApplyTop();
                    operators.Pop();
                    expectValue = false;
                    break;
                case ScanKind.Operator:
                {
                    var op = token.Text[0];
                    if (op == '%')
                    {
                        operators.Push('%');
                        ApplyTop();
                        expectValue = false;
                        continue;
                    }
                    if (expectValue)
                    {
                        if (op == '-')
                        {
                            // Moins unaire préfixe : poussé directement, sans appliquer
                            // les opérateurs en attente — il porte sur l'opérande suivant.
                            operators.Push('~');
                            expectValue = true;
                            continue;
                        }
                        if (op == '+') continue;
                        throw new CalculationException("parse-error");
                    }
                    var rightAssociative = op == '^';
                    while (operators.Count > 0 && operators.Peek() != '(')
                    {
                        var topPrecedence = Precedence(operators.Peek());
                        var currentPrecedence = Precedence(op);
                        if (topPrecedence > currentPrecedence || (topPrecedence == currentPrecedence && !rightAssociative)) ApplyTop();
                        else break;
                    }
                    operators.Push(op);
                    expectValue = true;
                    break;
                }
            }
        }
        while (operators.Count > 0)
        {
            if (operators.Peek() == '(') throw new CalculationException("parenthesis");
            ApplyTop();
        }
        if (output.Count != 1 || output.Peek().Kind != SymbolKind.Value) throw new CalculationException("parse-error");
        return output.Pop().Value;
    }

    [GeneratedRegex(@"(?<=\d) (?=\d{3}(?!\d))")]
    private static partial Regex Grouping();
}

/// <summary>Recoupement : égalité exacte des fractions réduites exigée.</summary>
public static class CrossCheck
{
    public enum OutcomeKind { Agree, Disagree, BothReject, KernelOnly, VerifierOnly }

    public sealed record Outcome(OutcomeKind Kind, string Detail)
    {
        public bool Consistent => Kind is OutcomeKind.Agree or OutcomeKind.BothReject;
    }

    public static Outcome Compare(string input, IReadOnlyDictionary<string, Rational>? names = null)
    {
        var kernel = new Kernel();
        var verifier = new VerifierEngine();
        Rational? kernelValue = null, verifierValue = null;
        string? kernelError = null, verifierError = null;
        try { kernelValue = kernel.Evaluate(input, names); }
        catch (CalculationException error) { kernelError = error.Code; }
        try { verifierValue = verifier.Evaluate(input, names); }
        catch (CalculationException error) { verifierError = error.Code; }

        if (kernelValue is { } k && verifierValue is { } v)
            return k == v
                ? new Outcome(OutcomeKind.Agree, k.ToString())
                : new Outcome(OutcomeKind.Disagree, $"kernel {k} != verifier {v}");
        if (kernelError is not null && verifierError is not null)
            return kernelError == verifierError
                ? new Outcome(OutcomeKind.BothReject, kernelError)
                : new Outcome(OutcomeKind.Disagree, $"kernel error {kernelError} != verifier error {verifierError}");
        return kernelValue is not null
            ? new Outcome(OutcomeKind.VerifierOnly, "kernel accepts, verifier rejects")
            : new Outcome(OutcomeKind.KernelOnly, "verifier accepts, kernel rejects");
    }
}
