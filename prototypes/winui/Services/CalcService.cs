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
                while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == ',' || expr[i] == '.' || expr[i] == ' '))
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
    /// « 1 200,50 » / « 1,200 » (milliers US) / « 14 900 » → « 1200.5 ».
    /// La virgule est décimale quand elle n'est suivie que de ≤2 groupes de 1-2 chiffres,
    /// ou quand un point existe déjà ; sinon elle est millier.
    /// </summary>
    private static string NormalizeNumber(string raw)
    {
        raw = raw.Replace(" ", "").Replace(" ", "");
        int commas = raw.Count(c => c == ',');
        int dots = raw.Count(c => c == '.');
        if (commas == 1 && dots == 0)
        {
            int decDigits = raw.Length - raw.IndexOf(',') - 1;
            if (decDigits == 3)
            {
                // « 1,200 » : millier US si 3 chiffres et pas de point décimal
                var head = raw.Substring(0, raw.IndexOf(','));
                if (head.Length <= 3) return raw.Replace(",", "");
            }
            return raw.Replace(',', '.');
        }
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
        if (pos < t.Count && t[pos].Text == "-") { pos++; return -ParseFactor(t, ref pos); }
        if (pos < t.Count && t[pos].Text == "(")
        {
            pos++;
            double inner = ParseExpr(t, ref pos);
            if (pos >= t.Count || t[pos].Text != ")") throw new FormatException("Parenthèse non fermée");
            pos++;
            return inner;
        }
        double v;
        if (pos < t.Count && t[pos].Kind == "num")
            v = double.Parse(t[pos++].Text, CultureInfo.InvariantCulture);
        else
            throw new FormatException("Expression incomplète");
        // % postfixe : « 20 % » = 0,20
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
