import Foundation

/// Nature dimensionnelle d'une valeur — SPEC 3.1 (v1.1).
enum DimKind: Equatable, Sendable {
    case none, currency, duration
}

/// Dimension portée par une valeur calculée. Le pourcentage n'est PAS une
/// dimension : `%` est une mise à l'échelle sans dimension (SPEC v1.1) —
/// `currency × percent → currency` en découle naturellement.
struct CalcDimension: Equatable, Sendable {
    let kind: DimKind
    let code: String

    static let none = CalcDimension(kind: .none, code: "")

    static func currency(of symbol: Character) throws -> CalcDimension {
        switch symbol {
        case "€": return CalcDimension(kind: .currency, code: "EUR")
        case "$": return CalcDimension(kind: .currency, code: "USD")
        case "£": return CalcDimension(kind: .currency, code: "GBP")
        default: throw CalcError.parseError(position: 0)
        }
    }

    var isNone: Bool { kind == .none }
}

/// Valeur dimensionnée : le rationnel exact et sa dimension.
struct Measured: Equatable, Sendable {
    let value: Rational
    let dim: CalcDimension

    static func plain(_ value: Rational) -> Measured { Measured(value: value, dim: .none) }

    /// Addition/soustraction — dimensions identiques exigées (SPEC 3.1).
    static func add(_ left: Measured, _ right: Measured, adding: Bool) throws -> Measured {
        try checkCompatible(left.dim, right.dim)
        return Measured(value: adding ? left.value + right.value : left.value - right.value, dim: left.dim)
    }

    /// Multiplication — la dimension unique survit ; deux dimensions non
    /// triviales sont un contresens (`€ × €`, `€ × h`).
    static func multiply(_ left: Measured, _ right: Measured) throws -> Measured {
        let dim: CalcDimension
        if left.dim.isNone { dim = right.dim }
        else if right.dim.isNone { dim = left.dim }
        else { throw CalcError.dimensionMismatch }
        return Measured(value: left.value * right.value, dim: dim)
    }

    /// Division — `d ÷ d` donne un ratio sans dimension ; `d ÷ none` conserve la
    /// dimension ; diviser un sans-dimension par une dimension est un contresens.
    static func divide(_ left: Measured, _ right: Measured) throws -> Measured {
        if right.value.isZero { throw CalcError.divisionByZero }
        if left.dim.isNone, !right.dim.isNone { throw CalcError.dimensionMismatch }
        if !left.dim.isNone, !right.dim.isNone {
            try checkCompatible(left.dim, right.dim)
            return Measured(value: left.value / right.value, dim: .none)
        }
        return Measured(value: left.value / right.value, dim: left.dim)
    }

    private static func checkCompatible(_ left: CalcDimension, _ right: CalcDimension) throws {
        if left.kind != right.kind { throw CalcError.dimensionMismatch }
        if left.kind == .currency, left.code != right.code { throw CalcError.currencyMixing }
        if left.kind == .duration, left.code != right.code { throw CalcError.dimensionMismatch }
    }
}
