import Foundation

// Entier signé de précision arbitraire — signe + magnitude.
struct BigInt: Equatable, Comparable, Sendable {
    enum Sign: Sendable { case positive, zero, negative }

    private(set) var magnitude: BigUInt
    private(set) var sign: Sign

    init() {
        magnitude = BigUInt()
        sign = .zero
    }

    init(magnitude: BigUInt, negative: Bool) {
        if magnitude.isZero {
            self.magnitude = BigUInt()
            sign = .zero
        } else {
            self.magnitude = magnitude
            sign = negative ? .negative : .positive
        }
    }

    var isZero: Bool { sign == .zero }
    var isNegative: Bool { sign == .negative }

    static prefix func - (value: BigInt) -> BigInt {
        if value.isZero { return value }
        return BigInt(magnitude: value.magnitude, negative: !value.isNegative)
    }

    static func + (lhs: BigInt, rhs: BigInt) -> BigInt {
        if lhs.isZero { return rhs }
        if rhs.isZero { return lhs }
        if lhs.sign == rhs.sign {
            return BigInt(magnitude: lhs.magnitude + rhs.magnitude, negative: lhs.isNegative)
        }
        if lhs.magnitude == rhs.magnitude { return BigInt() }
        if lhs.magnitude > rhs.magnitude {
            return BigInt(magnitude: lhs.magnitude - rhs.magnitude, negative: lhs.isNegative)
        }
        return BigInt(magnitude: rhs.magnitude - lhs.magnitude, negative: rhs.isNegative)
    }

    static func - (lhs: BigInt, rhs: BigInt) -> BigInt {
        lhs + (-rhs)
    }

    static func * (lhs: BigInt, rhs: BigInt) -> BigInt {
        if lhs.isZero || rhs.isZero { return BigInt() }
        return BigInt(magnitude: lhs.magnitude * rhs.magnitude, negative: lhs.isNegative != rhs.isNegative)
    }

    static func < (lhs: BigInt, rhs: BigInt) -> Bool {
        if lhs.sign != rhs.sign {
            let order: [Sign: Int] = [.negative: 0, .zero: 1, .positive: 2]
            return order[lhs.sign]! < order[rhs.sign]!
        }
        switch lhs.sign {
        case .zero: return false
        case .positive: return lhs.magnitude < rhs.magnitude
        case .negative: return rhs.magnitude < lhs.magnitude
        }
    }
}
