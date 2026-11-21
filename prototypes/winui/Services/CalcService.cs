using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Nomi.Prototype.WinUI.Services;

/// <summary>
/// Moteur de calcul DÉTERMINISTE — réel, pas simulé.
/// Évalue des expressions arithmétiques simples (+ - * / %, parenthèses,
/// virgule décimale, séparateur de milliers) sans passer par l'IA.
/// C'est le pendant prototype de Calc.cs dans Nomi.WinUI.
/// </summary>
public static class CalcService
{
    private sealed record Tok(string Kind, string Text);

    /// <summary>Évalue une expression. Lève FormatException si invalide.</summary>
    public static double Eval(string expression)
    {
        var tokens = Tokenize(expression);
        var pos = 0;
        var value = ParseExpr(tokens, ref pos);
        if (pos != tokens.Count)
            throw new FormatException($"Caractères en trop après « {tokens[pos].Text} »");
        return value;
    }

    /// <summary>Vérifie une série de lignes « libellé / expression / valeur lue ».</summary>
    public static IReadOnlyList<CalcLine> Verify(IEnumerable<(string Label, string Expr, double Expected)> lines)
    {
        var result = new List<CalcLine>();
        foreach (var (label, expr, expected) in lines)
        {
            try
            {
                var computed = Eval(expr);
                result.Add(new CalcLine(label, expr, expected, computed,
                    Match: System.Math.Abs(computed - expected) < 0.005));
            }
            catch (FormatException)
            {
                result.Add(new CalcLine(label, expr, expected, double.NaN, Match: false));
            }
        }
        return result;
    }

    private static List<Tok> Tokenize(string expr)
    {
        var tokens = new List<Tok>();
        int i = 0;
        while (i < expr.Length)
        {
            char c = expr[i];
            if (char.IsWhiteSpace(c) || c == '€' || c == '$') { i++; continue; }
            if (char.IsDigit(c) || c == ',' || c == '.')
            {
                int start = i;
                while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == ',' || expr[i] == '.' || char.IsWhiteSpace(expr[i])))
                    i++;
                var raw = expr.Substring(start, i - start).Trim();
                tokens.Add(new Tok("num", NormalizeNumber(raw)));
                continue;
            }
            if ("+-*/%()".Contains(c)) { tokens.Add(new Tok("op", c.ToString())); i++; continue; }
            throw new FormatException($"Caractère inattendu « {c} »");
        }
        return tokens;
    }

    /// <summary>
    /// « 1 200,50 » / « 14 900 » → « 1200.5 ».
    /// Locale FR : virgule unique = séparateur décimal (« 0,125 » = 0.125) ;
    /// les milliers se notent par espaces. « 1,234,567.89 » reste lu à l'anglaise.
    /// </summary>
    private static string NormalizeNumber(string raw)
    {
        raw = new string(raw.Where(c => !char.IsWhiteSpace(c)).ToArray());
        int commas = raw.Count(c => c == ',');
        int dots = raw.Count(c => c == '.');
        if (commas == 1 && dots == 0) return raw.Replace(',', '.');
        if (commas > 1) return raw.Replace(",", "");
        if (commas == 1 && dots >= 1) return raw.Replace(",", ""); // 1,200.50 → 1200.50
        return raw;
    }

    private static double ParseExpr(List<Tok> t, ref int pos)
    {
        double v = ParseTerm(t, ref pos);
        while (pos < t.Count && t[pos].Text is "+" or "-")
        {
            var op = t[pos++].Text;
            double r = ParseTerm(t, ref pos);
            v = op == "+" ? v + r : v - r;
        }
        return v;
    }

    private static double ParseTerm(List<Tok> t, ref int pos)
    {
        double v = ParseFactor(t, ref pos);
        while (pos < t.Count && t[pos].Text is "*" or "/")
        {
            var op = t[pos++].Text;
            double r = ParseFactor(t, ref pos);
            v = op == "*" ? v * r : v / r;
        }
        return v;
    }

    private static double ParseFactor(List<Tok> t, ref int pos)
    {
        double v;
        if (pos < t.Count && t[pos].Text == "-") { pos++; v = -ParseFactor(t, ref pos); }
        else if (pos < t.Count && t[pos].Text == "(")
        {
            pos++;
            v = ParseExpr(t, ref pos);
            if (pos >= t.Count || t[pos].Text != ")") throw new FormatException("Parenthèse non fermée");
            pos++;
        }
        else if (pos < t.Count && t[pos].Kind == "num")
            v = double.Parse(t[pos++].Text, CultureInfo.InvariantCulture);
        else
            throw new FormatException("Expression incomplète");
        // % postfixe : « 20 % » = 0,20 — vaut aussi après « ) » : « (10+10)% » = 0,20
        while (pos < t.Count && t[pos].Text == "%") { pos++; v /= 100.0; }
        return v;
    }

    /// <summary>Formatage affichage : 14632 → « 14 632 », 3.6 → « 3,6 ».</summary>
    public static string Format(double value)
    {
        if (double.IsNaN(value)) return "—";
        var s = value.ToString("#,##0.##", CultureInfo.InvariantCulture).Replace(",", " ");
        return s.Replace('.', ',');
    }
}
