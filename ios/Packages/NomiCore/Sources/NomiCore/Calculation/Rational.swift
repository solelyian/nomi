import Foundation

// Rationnel exact : numérateur signé / dénominateur strictement positif,
// toujours réduit (pgcd = 1). Zéro est 0/1. Aucun arrondi n'existe ici —
// l'arrondi n'intervient qu'à l'affichage. Référence : calc-spec/SPEC.md section 1.
public struct Rational: Equatable, Comparable, Sendable {
    private(set) var numerator: BigInt
    private(set) var denominator: BigUInt // toujours >= 1

    static let digitLimit = 10_000

    init(numerator: BigInt, denominator: BigUInt) {
        precondition(!denominator.isZero, "dénominateur nul")
        if numerator.isZero {
            self.numerator = BigInt()
            self.denominator = BigUInt(1)
            return
        }
        let divisor = BigUInt.gcd(numerator.magnitude, denominator)
        self.numerator = BigInt(magnitude: numerator.magnitude / divisor, negative: numerator.isNegative)
        self.denominator = denominator / divisor
    }

    init(integer value: BigInt) {
        numerator = value
        denominator = BigUInt(1)
    }

    /// Lit un littéral décimal canonique (point décimal, pas de milliers) en fraction exacte.
    init?(decimalLiteral text: String) {
        var negative = false
        var body = text
        if body.hasPrefix("-") { negative = true; body.removeFirst() }
        if body.hasPrefix("+") { body.removeFirst() }
        guard !body.isEmpty else { return nil }
        let parts = body.split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count <= 2, parts.allSatisfy({ !$0.isEmpty || parts.count == 1 }) else { return nil }
        let digits = parts.joined()
        guard !digits.isEmpty, digits.allSatisfy({ $0 >= "0" && $0 <= "9" }) else { return nil }
        guard let magnitude = BigUInt(decimalDigits: String(digits)) else { return nil }
        var denominator = BigUInt(1)
        if parts.count == 2 {
            denominator = BigUInt.pow(BigUInt(10), parts[1].count)
        }
        self.init(numerator: BigInt(magnitude: magnitude, negative: negative), denominator: denominator)
    }

    var isZero: Bool { numerator.isZero }

    /// Valeur absolue.
    var magnitude: Rational {
        Rational(numerator: BigInt(magnitude: numerator.magnitude, negative: false), denominator: denominator)
    }

    /// Dépassement de la borne de ressources (SPEC 1.2).
    /// Première magnitude à `digitLimit + 1` chiffres : le dépassement est une
    /// comparaison directe — jamais de conversion décimale complète (coûteuse).
    private static let overflowMagnitude = BigUInt.pow(BigUInt(10), Rational.digitLimit)

    var exceedsLimits: Bool {
        numerator.magnitude >= Rational.overflowMagnitude || denominator >= Rational.overflowMagnitude
    }

    static func + (lhs: Rational, rhs: Rational) -> Rational {
        let left = lhs.numerator * BigInt(magnitude: rhs.denominator, negative: false)
        let right = rhs.numerator * BigInt(magnitude: lhs.denominator, negative: false)
        return Rational(numerator: left + right, denominator: lhs.denominator * rhs.denominator)
    }

    static func - (lhs: Rational, rhs: Rational) -> Rational {
        lhs + (-rhs)
    }

    static prefix func - (value: Rational) -> Rational {
        Rational(numerator: -value.numerator, denominator: value.denominator)
    }

    static func * (lhs: Rational, rhs: Rational) -> Rational {
        Rational(
            numerator: lhs.numerator * rhs.numerator,
            denominator: lhs.denominator * rhs.denominator
        )
    }

    /// Division exacte ; la division par zéro est gérée par l'appelant (erreur typée).
    static func / (lhs: Rational, rhs: Rational) -> Rational {
        precondition(!rhs.isZero, "division rationnelle par zéro")
        let numerator = lhs.numerator * BigInt(magnitude: rhs.denominator, negative: false)
        var denominatorMagnitude = lhs.denominator * rhs.numerator.magnitude
        var negativeDenominator = false
        if rhs.numerator.isNegative { negativeDenominator = true }
        // Le dénominateur reste strictement positif : le signe remonte au numérateur.
        let signedNumerator = negativeDenominator ? -numerator : numerator
        return Rational(numerator: signedNumerator, denominator: denominatorMagnitude)
    }

    public static func < (lhs: Rational, rhs: Rational) -> Bool {
        let left = lhs.numerator * BigInt(magnitude: rhs.denominator, negative: false)
        let right = rhs.numerator * BigInt(magnitude: lhs.denominator, negative: false)
        return left < right
    }

    /// Puissance exacte, exposant entier borné par le noyau avant appel.
    /// Puissance exacte — la coupure de capacité est déléguée à `powLimited`,
    /// qui ne rejette que les dépassements CERTAINS : aucun faux positif près
    /// de la borne, aucun calcul sans fin sur puissances imbriquées.
    func raised(to exponent: Int) throws -> Rational {
        if exponent >= 0 {
            return Rational(
                numerator: BigInt(magnitude: try BigUInt.powLimited(numerator.magnitude, exponent), negative: numerator.isNegative && exponent % 2 == 1),
                denominator: try BigUInt.powLimited(denominator, exponent)
            )
        }
        precondition(!isZero, "zéro à exposant négatif")
        let positive = -exponent
        return Rational(
            numerator: BigInt(magnitude: try BigUInt.powLimited(denominator, positive), negative: numerator.isNegative && positive % 2 == 1),
            denominator: try BigUInt.powLimited(numerator.magnitude, positive)
        )
    }

    /// Arrondi half-up (away from zero) exact à `places` décimales — SPEC 1.3.
    func roundedHalfUp(places: Int) -> Rational {
        let scale = BigUInt.pow(BigUInt(10), places)
        // scaled = self * scale ; floor de la valeur absolue puis signe.
        let scaledNumerator = numerator.magnitude * scale
        let quotient = scaledNumerator / denominator
        let remainder = scaledNumerator % denominator
        // arrondi si remainder/denominator >= 1/2 ⇔ 2*remainder >= denominator
        var roundedMagnitude = quotient
        if remainder * BigUInt(2) >= denominator {
            roundedMagnitude = roundedMagnitude + BigUInt(1)
        }
        return Rational(
            numerator: BigInt(magnitude: roundedMagnitude, negative: numerator.isNegative),
            denominator: scale
        )
    }

    /// Affichage français conforme SPEC 1.3/1.4 : half-up à `places` décimales,
    /// zéros terminaux supprimés, virgule décimale, milliers groupés U+202F.
    public func displayFrench(places: Int = 4) -> String {
        let rounded = roundedHalfUp(places: places)
        let scale = BigUInt.pow(BigUInt(10), places)
        // Expansion décimale exacte sur `places` chiffres.
        let scaledMagnitude = rounded.numerator.magnitude * scale / rounded.denominator
        var digits = scaledMagnitude.decimalString
        if digits.count <= places {
            digits = String(repeating: "0", count: places + 1 - digits.count) + digits
        }
        let splitIndex = digits.index(digits.endIndex, offsetBy: -places)
        var intPart = String(digits[..<splitIndex])
        var fracPart = String(digits[splitIndex...])
        while fracPart.hasSuffix("0") { fracPart.removeLast() }
        intPart = Rational.groupThousands(intPart)
        let sign = rounded.numerator.isNegative ? "-" : ""
        return fracPart.isEmpty ? sign + intPart : sign + intPart + "," + fracPart
    }

    /// Groupement par trois avec espace fine insécable U+202F dès 4 chiffres — SPEC 1.4.
    static func groupThousands(_ intPart: String) -> String {
        guard intPart.count > 3 else { return intPart }
        var remaining = intPart
        var groups: [String] = []
        while !remaining.isEmpty {
            groups.insert(String(remaining.suffix(3)), at: 0)
            remaining = String(remaining.dropLast(3))
        }
        return groups.joined(separator: "\u{202F}")
    }
}
