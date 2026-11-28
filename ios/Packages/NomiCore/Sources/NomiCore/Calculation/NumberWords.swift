import Foundation

/// Lecture des nombres en toutes lettres — couverture bornée au français usuel
/// (0 à 999 999), deterministic_calculation_system.md §7.2 et §10.4.
/// Le masquage ne peut pas laisser passer un chiffre écrit en lettres quand il
/// est intraçable : cette lecture est exacte et exhaustive sur sa couverture —
/// une séquence non reconnue n'est pas un nombre masquable, par construction.
enum NumberWords {
    private static let units: [String: Int] = [
        "zéro": 0, "un": 1, "une": 1, "deux": 2, "trois": 3, "quatre": 4, "cinq": 5,
        "six": 6, "sept": 7, "huit": 8, "neuf": 9, "dix": 10, "onze": 11, "douze": 12,
        "treize": 13, "quatorze": 14, "quinze": 15, "seize": 16,
    ]
    private static let tens: [String: Int] = [
        "vingt": 20, "trente": 30, "quarante": 40, "cinquante": 50,
        "soixante": 60, "quatre-vingt": 80, "quatre-vingts": 80,
    ]

    /// Analyse un mot simple ou composé avec traits d'union (« soixante-huit »,
    /// « quatre-vingt-douze »). Renvoie nil hors couverture — jamais de devinette.
    static func parse(_ word: String) -> Int? {
        let lowered = word.lowercased()
        if let value = units[lowered] ?? tens[lowered] { return value }
        let parts = lowered.split(separator: "-").map(String.init)
        guard parts.count > 1 else { return nil }
        // Formes : soixante-dix, soixante et onze, quatre-vingt-douze, cent-vingt…
        // Formes bornées : vingt…soixante (+ et un/Deux…neuf) ; soixante-dix…seize
        // (70–76) ; soixante-dix-sept…neuf (77–79) ; quatre-vingt(s) (-un…-dix-neuf).
        // « quatre-vingt » est lui-même composé : la tête peut couvrir deux parties.
        var total = 0
        var index = 1
        if parts.count >= 2, let compound = tens[parts[0] + "-" + parts[1]] {
            total = compound
            index = 2
        } else if let base = tens[parts[0]] {
            total = base
        } else {
            return nil
        }
        if index < parts.count, parts[index] == "et" { index += 1 }
        guard index < parts.count else { return total } // forme canonique (« quatre-vingt »)
        if let head = units[parts[index]] {
            if head < 10 {
                guard index == parts.count - 1 else { return nil }
                return total + head
            }
            // soixante-dix…seize — seul « soixante » compose avec 10–16
            guard total == 60 || total == 80, head >= 10, head <= 16 else { return nil }
            total += head
            index += 1
            if index == parts.count { return total }
            // soixante-dix-sept…neuf
            guard let tail = units[parts[index]], tail < 10, index == parts.count - 1 else { return nil }
            return total + tail
        }
        return nil
    }

    /// Analyse une séquence de mots adjacents éventuellement composée de
    /// « cent(s) » et « mille » : « deux cent quarante-deux », « mille deux cents ».
    /// Renvoie la valeur et le nombre de mots consommés, ou nil.
    static func parseSequence(_ words: [String]) -> (value: Int, consumed: Int)? {
        var total = 0
        var index = 0
        var consumedSomething = false

        func takeSimple() -> Int? {
            guard index < words.count, let value = parse(words[index]) else { return nil }
            index += 1
            return value
        }

        // Millions exclus de la couverture v1 — bornée.
        // mille : « mille », « deux mille »
        var thousands = 0
        let mark = index
        if index < words.count, words[index].lowercased() == "mille" {
            thousands = 1
            index += 1
        } else if let head = takeSimple(), index < words.count, words[index].lowercased() == "mille" {
            thousands = head
            index += 1
        } else {
            index = mark
        }
        if thousands > 0 { total += thousands * 1000; consumedSomething = true }

        // cent(s) : « cent », « deux cents », « cent vingt »
        var hundreds = 0
        let centMark = index
        if index < words.count, ["cent", "cents"].contains(words[index].lowercased()) {
            hundreds = 1
            index += 1
        } else if let head = index < words.count ? parse(words[index]) : nil, head < 10,
                  index + 1 < words.count, ["cent", "cents"].contains(words[index + 1].lowercased()) {
            hundreds = head
            index += 2
        } else {
            index = centMark
        }
        if hundreds > 0 { total += hundreds * 100; consumedSomething = true }

        if let rest = takeSimple() {
            total += rest
            consumedSomething = true
        }
        guard consumedSomething, total >= 0, total <= 999_999 else { return nil }
        return (total, index)
    }
}
