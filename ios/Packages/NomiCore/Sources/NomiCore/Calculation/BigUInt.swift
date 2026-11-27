import Foundation

// Entier non signé de précision arbitraire — interne au noyau de calcul.
// Implémentation sans dépendance : membres de 64 bits, poids faibles en tête.
// Référence de conformité : calc-spec/SPEC.md section 1.
struct BigUInt: Equatable, Comparable, Sendable {
    private(set) var limbs: [UInt64] // little-endian, sans zéros de tête ; vide = zéro

    init() { limbs = [] }

    init(_ value: UInt64) {
        limbs = value == 0 ? [] : [value]
    }

    init(limbsNormalized limbs: [UInt64]) {
        var copy = limbs
        while copy.last == 0 { copy.removeLast() }
        self.limbs = copy
    }

    var isZero: Bool { limbs.isEmpty }

    // MARK: Parsing décimal exact

    init?(decimalDigits text: String) {
        guard !text.isEmpty, text.allSatisfy(\.isNumber) else { return nil }
        var result = BigUInt()
        let ten = BigUInt(10)
        for character in text {
            guard let digit = character.wholeNumberValue else { return nil }
            result = result * ten + BigUInt(UInt64(digit))
        }
        self = result
    }

    var decimalString: String {
        if isZero { return "0" }
        var digits: [Character] = []
        var current = self
        let ten = BigUInt(10)
        while !current.isZero {
            let (quotient, remainder) = current.quotientAndRemainder(dividingBy: ten)
            digits.append(Character(String(remainder.limbs.first ?? 0)))
            current = quotient
        }
        return String(digits.reversed())
    }

    var decimalDigitCount: Int {
        decimalString.count
    }

    // MARK: Comparaison

    static func < (lhs: BigUInt, rhs: BigUInt) -> Bool {
        if lhs.limbs.count != rhs.limbs.count { return lhs.limbs.count < rhs.limbs.count }
        for index in stride(from: lhs.limbs.count - 1, through: 0, by: -1) where lhs.limbs.count > 0 {
            if lhs.limbs[index] != rhs.limbs[index] { return lhs.limbs[index] < rhs.limbs[index] }
        }
        return false
    }

    // MARK: Addition / soustraction

    static func + (lhs: BigUInt, rhs: BigUInt) -> BigUInt {
        let count = max(lhs.limbs.count, rhs.limbs.count)
        var result = [UInt64]()
        result.reserveCapacity(count + 1)
        var carry: UInt64 = 0
        for index in 0..<count {
            let a = index < lhs.limbs.count ? lhs.limbs[index] : 0
            let b = index < rhs.limbs.count ? rhs.limbs[index] : 0
            let (sum1, overflow1) = a.addingReportingOverflow(b)
            let (sum2, overflow2) = sum1.addingReportingOverflow(carry)
            result.append(sum2)
            carry = (overflow1 ? 1 : 0) + (overflow2 ? 1 : 0)
        }
        if carry > 0 { result.append(carry) }
        return BigUInt(limbsNormalized: result)
    }

    /// lhs - rhs ; précondition lhs >= rhs.
    static func - (lhs: BigUInt, rhs: BigUInt) -> BigUInt {
        precondition(lhs >= rhs, "soustraction BigUInt négative")
        var result = [UInt64]()
        result.reserveCapacity(lhs.limbs.count)
        var borrow: UInt64 = 0
        for index in 0..<lhs.limbs.count {
            let a = lhs.limbs[index]
            let b = index < rhs.limbs.count ? rhs.limbs[index] : 0
            let (diff1, overflow1) = a.subtractingReportingOverflow(b)
            let (diff2, overflow2) = diff1.subtractingReportingOverflow(borrow)
            result.append(diff2)
            borrow = (overflow1 ? 1 : 0) + (overflow2 ? 1 : 0)
        }
        precondition(borrow == 0, "retenue finale dans la soustraction BigUInt")
        return BigUInt(limbsNormalized: result)
    }

    // MARK: Multiplication

    static func * (lhs: BigUInt, rhs: BigUInt) -> BigUInt {
        if lhs.isZero || rhs.isZero { return BigUInt() }
        var result = [UInt64](repeating: 0, count: lhs.limbs.count + rhs.limbs.count + 1)
        for (i, a) in lhs.limbs.enumerated() {
            var carry: UInt64 = 0
            for (j, b) in rhs.limbs.enumerated() {
                let index = i + j
                let (high, low) = a.multipliedFullWidth(by: b)
                let (sum1, overflow1) = result[index].addingReportingOverflow(low)
                let (sum2, overflow2) = sum1.addingReportingOverflow(carry)
                result[index] = sum2
                carry = high + (overflow1 ? 1 : 0) + (overflow2 ? 1 : 0)
            }
            var index = i + rhs.limbs.count
            while carry > 0 {
                let (sum, overflow) = result[index].addingReportingOverflow(carry)
                result[index] = sum
                carry = overflow ? 1 : 0
                index += 1
            }
        }
        return BigUInt(limbsNormalized: result)
    }

    // MARK: Division — longue division binaire, exacte

    func quotientAndRemainder(dividingBy divisor: BigUInt) -> (quotient: BigUInt, remainder: BigUInt) {
        precondition(!divisor.isZero, "division par zéro dans BigUInt")
        if self < divisor { return (BigUInt(), self) }
        if divisor.limbs.count == 1, divisor.limbs[0] == 1 { return (self, BigUInt()) }

        var quotient = [UInt64](repeating: 0, count: limbs.count)
        var remainder = BigUInt()
        let bitCount = bitLength
        for bitIndex in stride(from: bitCount - 1, through: 0, by: -1) {
            remainder = remainder.shiftedLeftByOne()
            if bit(bitIndex) { remainder = remainder + BigUInt(1) }
            if remainder >= divisor {
                remainder = remainder - divisor
                quotient[bitIndex / 64] |= UInt64(1) << UInt64(bitIndex % 64)
            }
        }
        return (BigUInt(limbsNormalized: quotient), remainder)
    }

    static func / (lhs: BigUInt, rhs: BigUInt) -> BigUInt {
        lhs.quotientAndRemainder(dividingBy: rhs).quotient
    }

    static func % (lhs: BigUInt, rhs: BigUInt) -> BigUInt {
        lhs.quotientAndRemainder(dividingBy: rhs).remainder
    }

    // MARK: Bits

    var bitLength: Int {
        guard let last = limbs.last else { return 0 }
        return (limbs.count - 1) * 64 + (64 - last.leadingZeroBitCount)
    }

    func bit(_ index: Int) -> Bool {
        let limb = index / 64
        guard limb < limbs.count else { return false }
        return (limbs[limb] >> UInt64(index % 64)) & 1 == 1
    }

    func shiftedLeftByOne() -> BigUInt {
        var result = [UInt64]()
        result.reserveCapacity(limbs.count + 1)
        var carry: UInt64 = 0
        for limb in limbs {
            result.append((limb << 1) | carry)
            carry = limb >> 63
        }
        if carry > 0 { result.append(carry) }
        return BigUInt(limbsNormalized: result)
    }

    // MARK: PGCD — Euclide binaire simple, exact

    static func gcd(_ a: BigUInt, _ b: BigUInt) -> BigUInt {
        var x = a, y = b
        while !y.isZero {
            (x, y) = (y, x % y)
        }
        return x
    }

    static func pow(_ base: BigUInt, _ exponent: Int) -> BigUInt {
        precondition(exponent >= 0)
        // L'exactitude est exigée ici : le dépassement de capacité est précontrôlé
        // AVANT l'appel (Rational.powerExceedsLimits) — jamais de troncature.
        var result = BigUInt(1)
        var factor = base
        var remaining = exponent
        while remaining > 0 {
            if remaining % 2 == 1 { result = result * factor }
            remaining /= 2
            if remaining > 0 { factor = factor * factor }
        }
        return result
    }
}
