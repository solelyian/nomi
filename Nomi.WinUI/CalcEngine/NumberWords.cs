namespace Nomi.CalcEngine;

/// <summary>
/// Lecture des nombres en toutes lettres — couverture bornée au français usuel
/// (0 à 999 999), deterministic_calculation_system.md §7.2 et §10.4. Exacte et
/// exhaustive sur sa couverture ; une séquence non reconnue n'est pas masquable.
/// </summary>
public static class NumberWords
{
    private static readonly Dictionary<string, int> Units = new()
    {
        ["zéro"] = 0, ["un"] = 1, ["une"] = 1, ["deux"] = 2, ["trois"] = 3, ["quatre"] = 4,
        ["cinq"] = 5, ["six"] = 6, ["sept"] = 7, ["huit"] = 8, ["neuf"] = 9, ["dix"] = 10,
        ["onze"] = 11, ["douze"] = 12, ["treize"] = 13, ["quatorze"] = 14, ["quinze"] = 15, ["seize"] = 16,
    };

    private static readonly Dictionary<string, int> Tens = new()
    {
        ["vingt"] = 20, ["trente"] = 30, ["quarante"] = 40, ["cinquante"] = 50,
        ["soixante"] = 60, ["quatre-vingt"] = 80, ["quatre-vingts"] = 80,
    };

    /// <summary>Analyse un mot simple ou composé avec traits d'union — nil hors couverture.</summary>
    public static int? Parse(string word)
    {
        var lowered = word.ToLowerInvariant();
        if (Units.TryGetValue(lowered, out var simple)) return simple;
        if (Tens.TryGetValue(lowered, out var ten)) return ten;
        var parts = lowered.Split('-');
        if (parts.Length < 2) return null;
        // Formes bornées : vingt…soixante (+ et un/deux…neuf) ; soixante-dix…seize
        // (70–76) ; soixante-dix-sept…neuf (77–79) ; quatre-vingt(s) (-un…-dix-neuf).
        // « quatre-vingt » est lui-même composé : la tête peut couvrir deux parties.
        int total;
        var index = 1;
        if (parts.Length >= 2 && Tens.TryGetValue(parts[0] + "-" + parts[1], out var compound))
        {
            total = compound;
            index = 2;
        }
        else if (!Tens.TryGetValue(parts[0], out total)) return null;
        if (index < parts.Length && parts[index] == "et") index++;
        if (index >= parts.Length) return total; // forme canonique (« quatre-vingt »)
        if (Units.TryGetValue(parts[index], out var head))
        {
            if (head < 10)
            {
                if (index != parts.Length - 1) return null;
                return total + head;
            }
            // soixante-dix…seize — seul « soixante » compose avec 10–16
            if (total is not (60 or 80) || head is < 10 or > 16) return null;
            total += head;
            index++;
            if (index == parts.Length) return total;
            // soixante-dix-sept…neuf
            if (!Units.TryGetValue(parts[index], out var tail) || tail >= 10 || index != parts.Length - 1) return null;
            return total + tail;
        }
        return null;
    }

    /// <summary>Analyse une séquence de mots adjacents — « deux cent quarante-deux »,
    /// « mille deux cents ». Renvoie la valeur et le nombre de mots consommés.</summary>
    public static (int Value, int Consumed)? ParseSequence(IReadOnlyList<string> words)
    {
        var total = 0;
        var index = 0;
        var consumedSomething = false;

        int? TakeSimple()
        {
            if (index < words.Count && Parse(words[index]) is { } value) { index++; return value; }
            return null;
        }

        var thousands = 0;
        var mark = index;
        if (index < words.Count && words[index].ToLowerInvariant() == "mille") { thousands = 1; index++; }
        else if (TakeSimple() is { } head && index < words.Count && words[index].ToLowerInvariant() == "mille")
        {
            thousands = head;
            index++;
        }
        else index = mark;
        if (thousands > 0) { total += thousands * 1000; consumedSomething = true; }

        var hundreds = 0;
        var centMark = index;
        if (index < words.Count && words[index].ToLowerInvariant() is "cent" or "cents") { hundreds = 1; index++; }
        else if (index < words.Count && Parse(words[index]) is { } centHead && centHead < 10
            && index + 1 < words.Count && words[index + 1].ToLowerInvariant() is "cent" or "cents")
        {
            hundreds = centHead;
            index += 2;
        }
        else index = centMark;
        if (hundreds > 0) { total += hundreds * 100; consumedSomething = true; }

        if (TakeSimple() is { } rest) { total += rest; consumedSomething = true; }
        if (!consumedSomething || total is < 0 or > 999_999) return null;
        return (total, index);
    }
}
