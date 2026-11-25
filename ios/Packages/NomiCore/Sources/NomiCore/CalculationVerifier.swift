import Foundation

/// Déterministic arithmetic checker for visible calculations.
/// It intentionally supports a small, explicit grammar instead of guessing:
/// decimal numbers, parentheses, + - × * / ÷ and postfix percent.
public struct CalculationVerifier: Sendable {
    public init() {}

    public func verify(_ source: String) -> CalculationVerification {
        guard let equals = source.lastIndex(of: "=") else {
            return CalculationVerification(expression: source, expected: nil, computed: nil, status: .invalid, message: "Aucun signe égal détecté.")
        }
        let left = String(source[..<equals]).trimmingCharacters(in: .whitespacesAndNewlines)
        let rightText = String(source[source.index(after: equals)...]).trimmingCharacters(in: .whitespacesAndNewlines)
        guard let expected = Self.parseNumber(rightText) else {
            return CalculationVerification(expression: source, expected: nil, computed: nil, status: .invalid, message: "Le résultat affiché n'est pas un nombre simple.")
        }
        do {
            var parser = Parser(left)
            let computed = try parser.parseExpression()
            parser.skipIgnored()
            guard parser.isAtEnd else {
                return CalculationVerification(expression: source, expected: expected, computed: nil, status: .unsupported, message: "Expression partiellement reconnue ; aucune correction automatique proposée.")
            }
            let delta = abs(computed - expected)
            let tolerance = Decimal(string: "0.000001")!
            if delta <= tolerance {
                return CalculationVerification(expression: source, expected: expected, computed: computed, status: .verified, message: "Calcul vérifié par le moteur déterministe.")
            }
            return CalculationVerification(expression: source, expected: expected, computed: computed, status: .mismatch, message: "Le résultat affiché ne correspond pas au recalcul local.")
        } catch {
            return CalculationVerification(expression: source, expected: expected, computed: nil, status: .unsupported, message: "Expression non prise en charge : parenthèses, unités ou notation ambiguë.")
        }
    }

    static func parseNumber(_ input: String) -> Decimal? {
        let trimmed = input.trimmingCharacters(in: .whitespacesAndNewlines)
            .replacingOccurrences(of: "€", with: "")
            .replacingOccurrences(of: "$", with: "")
            .replacingOccurrences(of: "£", with: "")
            .trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        let normalized = decimalNormalized(trimmed)
        return Decimal(string: normalized, locale: Locale(identifier: "en_US_POSIX"))
    }

    static func decimalNormalized(_ input: String) -> String {
        var value = input.replacingOccurrences(of: " ", with: "")
        if value.contains(",") && !value.contains(".") {
            value = value.replacingOccurrences(of: ",", with: ".")
        }
        return value
    }

    private struct Parser {
        private var scalars: [Character]
        private var index = 0

        init(_ input: String) {
            self.scalars = Array(input)
        }

        var isAtEnd: Bool { index >= scalars.count }

        mutating func skipIgnored() {
            while index < scalars.count, scalars[index].isWhitespace || scalars[index] == "€" || scalars[index] == "$" || scalars[index] == "£" {
                index += 1
            }
        }

        mutating func parseExpression() throws -> Decimal {
            var value = try parseTerm()
            while true {
                skipIgnored()
                if consume("+") {
                    value += try parseTerm()
                } else if consume("−") || consume("-") {
                    value -= try parseTerm()
                } else {
                    return value
                }
            }
        }

        mutating func parseTerm() throws -> Decimal {
            var value = try parseFactor()
            while true {
                skipIgnored()
                if consume("×") || consume("*") {
                    value *= try parseFactor()
                } else if consume("÷") || consume("/") {
                    let divisor = try parseFactor()
                    guard divisor != 0 else { throw ParserError.divisionByZero }
                    value /= divisor
                } else {
                    return value
                }
            }
        }

        mutating func parseFactor() throws -> Decimal {
            skipIgnored()
            if consume("+") { return try parseFactor() }
            if consume("−") || consume("-") { return -(try parseFactor()) }
            if consume("(") {
                let value = try parseExpression()
                guard consume(")") else { throw ParserError.unsupported }
                return try percentIfNeeded(value)
            }
            let number = try parseNumberToken()
            return try percentIfNeeded(number)
        }

        mutating func percentIfNeeded(_ value: Decimal) throws -> Decimal {
            skipIgnored()
            if consume("%") {
                return value / 100
            }
            return value
        }

        mutating func parseNumberToken() throws -> Decimal {
            skipIgnored()
            let start = index
            var sawDigit = false
            var sawSeparator = false
            while index < scalars.count {
                let c = scalars[index]
                if c.isNumber {
                    sawDigit = true
                    index += 1
                } else if (c == "," || c == ".") && !sawSeparator {
                    sawSeparator = true
                    index += 1
                } else {
                    break
                }
            }
            guard sawDigit else { throw ParserError.unsupported }
            let token = String(scalars[start..<index])
            guard let value = CalculationVerifier.parseNumber(token) else { throw ParserError.unsupported }
            return value
        }

        mutating func consume(_ token: Character) -> Bool {
            skipIgnored()
            guard index < scalars.count, scalars[index] == token else { return false }
            index += 1
            return true
        }
    }

    private enum ParserError: Error {
        case unsupported
        case divisionByZero
    }
}
