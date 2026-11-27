import Foundation

// Noyau de calcul déterministe — implémentation de référence côté iOS de
// calc-spec/SPEC.md v1.0. Arithmétique rationnelle exacte ; aucune dépendance
// vers l'IA, le réseau, l'horloge ou le hasard. Pur et sans état.
enum CalcError: Error, Equatable, Sendable {
    case parseError(position: Int)
    case parenthesis(position: Int)
    case trailing(position: Int)
    case divisionByZero
    case exponent
    case unknownIdentifier(String)
    case tooLong
    case tooDeep
    case overflow

    var code: String {
        switch self {
        case .parseError: return "parse-error"
        case .parenthesis: return "parenthesis"
        case .trailing: return "trailing"
        case .divisionByZero: return "division-by-zero"
        case .exponent: return "exponent"
        case .unknownIdentifier: return "unknown-identifier"
        case .tooLong: return "too-long"
        case .tooDeep: return "too-deep"
        case .overflow: return "overflow"
        }
    }
}

struct CalcKernel: Sendable {
    static let maxLength = 200
    static let maxDepth = 32
    static let maxExponent = 64

    init() {}

    private enum Token: Equatable {
        case number(String, position: Int)
        case identifier(String, position: Int)
        case `operator`(Character, position: Int)
        case end(position: Int)
    }

    /// Normalisation de la frontière de lecture — SPEC 3.3 : alias d'opérateurs,
    /// espaces insécables supprimées, groupes de milliers validés puis supprimés.
    private static func normalize(_ expression: String) -> String {
        var text = expression
            .replacingOccurrences(of: "×", with: "*")
            .replacingOccurrences(of: "÷", with: "/")
            .replacingOccurrences(of: "−", with: "-")
            .replacingOccurrences(of: "\u{202F}", with: "")
            .replacingOccurrences(of: "\u{00A0}", with: "")
        // Un espace simple entre un chiffre et un groupe d'exactement 3 chiffres
        // est un séparateur de milliers ; itératif pour les chaînes longues.
        while true {
            var changed = false
            var result = ""
            let characters = Array(text)
            var index = 0
            while index < characters.count {
                if characters[index] == " ",
                   index > 0, characters[index - 1].isNumber,
                   index + 3 < characters.count || index + 3 == characters.count {
                    let groupEnd = index + 3
                    if groupEnd < characters.count,
                       characters[index + 1].isNumber, characters[index + 2].isNumber, characters[index + 3].isNumber,
                       groupEnd + 1 >= characters.count || !characters[groupEnd + 1].isNumber {
                        changed = true
                        index += 1
                        continue
                    }
                }
                result.append(characters[index])
                index += 1
            }
            if !changed { break }
            text = result
        }
        return text
    }

    private static func tokenize(_ expression: String) throws -> [Token] {
        guard expression.count <= maxLength else { throw CalcError.tooLong }
        let characters = Array(expression)
        var tokens: [Token] = []
        var index = 0
        while index < characters.count {
            let character = characters[index]
            if character.isWhitespace { index += 1; continue }
            if character >= "0" && character <= "9" {
                let start = index
                while index < characters.count,
                      (characters[index] >= "0" && characters[index] <= "9") || characters[index] == "." || characters[index] == "," {
                    index += 1
                }
                tokens.append(.number(String(characters[start..<index]), position: start))
                continue
            }
            if (character >= "a" && character <= "z") || (character >= "A" && character <= "Z") || character == "_" {
                let start = index
                while index < characters.count, (characters[index] >= "a" && characters[index] <= "z") || (characters[index] >= "A" && characters[index] <= "Z") || (characters[index] >= "0" && characters[index] <= "9") || characters[index] == "_" {
                    index += 1
                }
                tokens.append(.identifier(String(characters[start..<index]), position: start))
                continue
            }
            if "+-*/^()%".contains(character) {
                tokens.append(.operator(character, position: index))
                index += 1
                continue
            }
            throw CalcError.parseError(position: index)
        }
        tokens.append(.end(position: characters.count))
        return tokens
    }

    private static func parseNumber(_ raw: String, position: Int) throws -> Rational {
        // À la frontière du noyau, la virgule est lue comme séparateur décimal
        // français (SPEC 3.3) ; un seul séparateur par littéral.
        let normalized = raw.replacingOccurrences(of: ",", with: ".")
        guard normalized.filter({ $0 == "." }).count <= 1 else {
            throw CalcError.parseError(position: position)
        }
        guard let value = Rational(decimalLiteral: normalized) else {
            throw CalcError.parseError(position: position)
        }
        return value
    }

    // MARK: Évaluation — descente récursive sur l'arbre grammatical (SPEC 2)

    private struct Parser {
        let tokens: [Token]
        var at = 0
        let names: [String: Rational]

        var peek: Token { tokens[min(at, tokens.count - 1)] }

        mutating func parse() throws -> Rational {
            // SPEC 2.1 : fermante sans ouvrante et ouvrante non fermée — `parenthesis`,
            // détectées structurellement avant l'évaluation.
            var balance = 0
            for token in tokens {
                if case .operator("(", _) = token { balance += 1 }
                if case .operator(")", let closePosition) = token {
                    balance -= 1
                    if balance < 0 { throw CalcError.parenthesis(position: closePosition) }
                }
            }
            if balance > 0 { throw CalcError.parenthesis(position: tokens.count - 1) }
            let value = try expression(depth: 0)
            if case .end = peek {} else {
                // SPEC 2.1 : une parenthèse fermante sans ouvrante est `parenthesis`,
                // `trailing` ne concerne que les autres jetons résiduels.
                if case .operator(")", let position) = peek {
                    throw CalcError.parenthesis(position: position)
                }
                throw CalcError.trailing(position: position(of: peek))
            }
            return value
        }

        func position(of token: Token) -> Int {
            switch token {
            case .number(_, let p), .identifier(_, let p), .operator(_, let p), .end(let p): return p
            }
        }

        mutating func expression(depth: Int) throws -> Rational {
            var value = try term(depth: depth)
            while case .operator(let op, _) = peek, op == "+" || op == "-" {
                at += 1
                let right = try term(depth: depth)
                value = op == "+" ? value + right : value - right
                try checkOverflow(value)
            }
            return value
        }

        mutating func term(depth: Int) throws -> Rational {
            var value = try factor(depth: depth)
            while case .operator(let op, _) = peek, op == "*" || op == "/" {
                at += 1
                let right = try factor(depth: depth)
                if op == "*" {
                    value = value * right
                } else {
                    guard !right.isZero else { throw CalcError.divisionByZero }
                    value = value / right
                }
                try checkOverflow(value)
            }
            return value
        }

        mutating func factor(depth: Int) throws -> Rational {
            if case .operator(let op, _) = peek, op == "-" {
                at += 1
                return try -factor(depth: depth)
            }
            if case .operator(let op, _) = peek, op == "+" {
                at += 1
                return try factor(depth: depth)
            }
            let base = try postfix(depth: depth)
            if case .operator("^", _) = peek {
                at += 1
                let exponentValue = try factor(depth: depth) // associatif à droite
                guard exponentValue.denominator == BigUInt(1),
                      exponentValue.numerator.magnitude.decimalDigitCount <= 2 else {
                    throw CalcError.exponent
                }
                let exponentText = exponentValue.numerator.magnitude.decimalString
                guard let magnitude = Int(exponentText), magnitude <= maxExponent else {
                    throw CalcError.exponent
                }
                let exponent = exponentValue.numerator.isNegative ? -magnitude : magnitude
                guard abs(exponent) <= maxExponent else { throw CalcError.exponent }
                if exponent < 0, base.isZero { throw CalcError.divisionByZero }
                if Rational.powerExceedsLimits(numeratorMagnitude: base.numerator.magnitude, denominator: base.denominator, exponent: exponent) {
                    throw CalcError.overflow
                }
                let result = base.raised(to: exponent)
                try checkOverflow(result)
                return result
            }
            return base
        }

        mutating func postfix(depth: Int) throws -> Rational {
            var value = try atom(depth: depth)
            while case .operator("%", _) = peek {
                at += 1
                value = value / Rational(integer: BigInt(magnitude: BigUInt(100), negative: false))
                try checkOverflow(value)
            }
            return value
        }

        mutating func atom(depth: Int) throws -> Rational {
            guard depth <= maxDepth else { throw CalcError.tooDeep }
            let token = peek
            at += 1
            switch token {
            case .number(let raw, let position):
                return try CalcKernel.parseNumber(raw, position: position)
            case .identifier(let name, let namePosition):
                // SPEC : identificateur = lettre (lettre|chiffre|"_")*, 32 caractères au plus.
                guard name.count <= 32, let first = name.first, first != "_" else {
                    throw CalcError.parseError(position: namePosition)
                }
                guard let value = names[name] else { throw CalcError.unknownIdentifier(name) }
                return value
            case .operator("(", let position):
                let value = try expression(depth: depth + 1)
                // Juxtaposition dans les parenthèses (`(3 4)`) : `trailing`, pas `parenthesis`.
                if case .number(_, let juxtapositionPosition) = peek { throw CalcError.trailing(position: juxtapositionPosition) }
                if case .identifier(_, let juxtapositionPosition) = peek { throw CalcError.trailing(position: juxtapositionPosition) }
                if case .operator("(", let juxtapositionPosition) = peek { throw CalcError.trailing(position: juxtapositionPosition) }
                guard case .operator(")", _) = peek else { throw CalcError.parenthesis(position: position) }
                at += 1
                return value
            case .end(let position):
                throw CalcError.parseError(position: position)
            default:
                throw CalcError.parseError(position: position(of: token))
            }
        }

        func checkOverflow(_ value: Rational) throws {
            if value.exceedsLimits { throw CalcError.overflow }
        }
    }

    /// Évalue une expression en rationnel exact. `names` lie les identificateurs
    /// des étapes antérieures — jamais postérieures (SPEC 2).
    func evaluate(_ expression: String, names: [String: Rational] = [:]) throws -> Rational {
        let tokens = try CalcKernel.tokenize(CalcKernel.normalize(expression))
        var parser = Parser(tokens: tokens, names: names)
        return try parser.parse()
    }
}
