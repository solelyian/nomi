import Foundation

/// Déterministic arithmetic checker for visible calculations.
/// It intentionally supports a small, explicit grammar instead of guessing:
/// decimal numbers, parentheses, + - × * / ÷ and postfix percent.
public struct CalculationVerifier: Sendable {
    public init() {}

    public func verify(_ source: String) -> CalculationVerification {
        let currencySymbols = source.filter { "€$£".contains($0) }
        let distinctCurrencies = Set(currencySymbols)
        guard distinctCurrencies.count <= 1 else {
            return CalculationVerification(expression: source, expected: nil, computed: nil, status: .invalid, message: "Plusieurs monnaies sont mélangées ; conversion et vérification sont refusées.")
        }
        if Self.hasMalformedCurrencyPlacement(source) {
            return CalculationVerification(expression: source, expected: nil, computed: nil, status: .unsupported, message: "Le symbole monétaire est placé à l'intérieur d'un nombre ; utilisez un séparateur décimal explicite.")
        }
        let cleanedSource = distinctCurrencies.count == 1 ? source.replacingOccurrences(of: String(distinctCurrencies.first!), with: "") : source
        guard let equals = cleanedSource.lastIndex(of: "=") else {
            return CalculationVerification(expression: source, expected: nil, computed: nil, status: .invalid, message: "Aucun signe égal détecté.")
        }
        let left = String(cleanedSource[..<equals]).trimmingCharacters(in: .whitespacesAndNewlines)
        let rightText = String(cleanedSource[cleanedSource.index(after: equals)...]).trimmingCharacters(in: .whitespacesAndNewlines)
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

    static func hasMalformedCurrencyPlacement(_ source: String) -> Bool {
        let characters = Array(source)
        for (index, character) in characters.enumerated() where "€$£".contains(character) {
            var before = index - 1
            while before >= 0, characters[before].isWhitespace { before -= 1 }
            var after = index + 1
            while after < characters.count, characters[after].isWhitespace { after += 1 }
            let hasDigitBefore = before >= 0 && characters[before].isNumber
            let hasDigitAfter = after < characters.count && characters[after].isNumber
            if hasDigitBefore == hasDigitAfter { return true }
        }
        return false
    }

    static func parseNumber(_ input: String) -> Decimal? {
        let trimmed = input.trimmingCharacters(in: .whitespacesAndNewlines)
        let strictNumberPattern = #"^[+-]?(?:[0-9]+|[0-9]{1,3}(?: [0-9]{3})+)(?:[.,][0-9]+)?$"#
        guard trimmed.range(of: strictNumberPattern, options: .regularExpression) != nil else { return nil }
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
            while index < scalars.count, scalars[index].isWhitespace {
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
            while index < scalars.count {
                var probe = index
                var whitespaceCount = 0
                while probe < scalars.count, scalars[probe].isWhitespace {
                    whitespaceCount += 1
                    probe += 1
                }
                var groupEnd = probe
                var groupedDigits = 0
                while groupEnd < scalars.count, scalars[groupEnd].isNumber {
                    groupedDigits += 1
                    groupEnd += 1
                }
                guard whitespaceCount > 0, groupedDigits == 3 else { break }
                if groupEnd < scalars.count, scalars[groupEnd].isNumber || scalars[groupEnd] == "." || scalars[groupEnd] == "," { break }
                index = groupEnd
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
