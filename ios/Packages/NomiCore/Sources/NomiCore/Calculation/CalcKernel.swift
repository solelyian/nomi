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
    case dimensionMismatch
    case currencyMixing
    case unsupported(position: Int)

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
        case .dimensionMismatch: return "dimension-mismatch"
        case .currencyMixing: return "currency-mixing"
        case .unsupported: return "unsupported"
        }
    }
}

struct CalcKernel: Sendable {
    static let maxLength = 200
    static let maxDepth = 32
    static let maxExponent = 64

    init() {}

    private enum Token: Equatable {
        case number(String, position: Int, currency: String = "")
        case identifier(String, position: Int)
        case `operator`(Character, position: Int)
        case currencySymbol(Character, position: Int)
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
            if character == "€" || character == "$" || character == "£" {
                tokens.append(.currencySymbol(character, position: index))
                index += 1
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
        return try mergeCurrency(tokens)
    }

    /// Attache les symboles monétaires au nombre adjacent — SPEC 3.2 : préfixe
    /// ou suffixe, avec ou sans espace. Un symbole entre deux nombres (`12€50`)
    /// est `unsupported` ; un symbole sans nombre adjacent est `parse-error`.
    private static func mergeCurrency(_ tokens: [Token]) throws -> [Token] {
        var merged: [Token] = []
        var ahead = tokens
        for index in tokens.indices {
            let token = ahead[index]
            guard case .currencySymbol(let symbol, let position) = token else { merged.append(token); continue }
            var before = false
            if let last = merged.last, case .number(_, _, _) = last { before = true }
            var after = false
            if index + 1 < ahead.count, case .number(_, _, _) = ahead[index + 1] { after = true }
            if before && after { throw CalcError.unsupported(position: position) }
            if !before && !after { throw CalcError.parseError(position: position) }
            let code = try CalcDimension.currency(of: symbol).code
            if before {
                guard case .number(let raw, let numberPosition, let existing) = merged[merged.count - 1], existing.isEmpty else {
                    throw CalcError.parseError(position: position)
                }
                merged[merged.count - 1] = .number(raw, position: numberPosition, currency: code)
            } else {
                guard case .number(let raw, let numberPosition, let existing) = ahead[index + 1], existing.isEmpty else {
                    throw CalcError.parseError(position: position)
                }
                ahead[index + 1] = .number(raw, position: numberPosition, currency: code)
            }
        }
        return merged
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
        let names: [String: Measured]

        var peek: Token { tokens[min(at, tokens.count - 1)] }

        mutating func parse() throws -> Measured {
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
            case .number(_, let p, _), .identifier(_, let p), .operator(_, let p), .currencySymbol(_, let p), .end(let p): return p
            }
        }

        mutating func expression(depth: Int) throws -> Measured {
            var value = try term(depth: depth)
            while case .operator(let op, _) = peek, op == "+" || op == "-" {
                at += 1
                let right = try term(depth: depth)
                value = try Measured.add(value, right, adding: op == "+")
                try checkOverflow(value.value)
            }
            return value
        }

        mutating func term(depth: Int) throws -> Measured {
            var value = try factor(depth: depth)
            while case .operator(let op, _) = peek, op == "*" || op == "/" {
                at += 1
                let right = try factor(depth: depth)
                value = op == "*" ? try Measured.multiply(value, right) : try Measured.divide(value, right)
                try checkOverflow(value.value)
            }
            return value
        }

        mutating func factor(depth: Int) throws -> Measured {
            if case .operator(let op, _) = peek, op == "-" {
                at += 1
                let negated = try factor(depth: depth)
                return Measured(value: -negated.value, dim: negated.dim)
            }
            if case .operator(let op, _) = peek, op == "+" {
                at += 1
                return try factor(depth: depth)
            }
            let base = try postfix(depth: depth)
            if case .operator("^", _) = peek {
                at += 1
                let exponentValue = try factor(depth: depth) // associatif à droite
                // SPEC 3.1 : l'exponentiation d'une valeur dimensionnée est un
                // contresens dimensionnel (€² n'a pas de représentation en v1).
                guard base.dim.isNone, exponentValue.dim.isNone else { throw CalcError.dimensionMismatch }
                let exponentRational = exponentValue.value
                guard exponentRational.denominator == BigUInt(1),
                      exponentRational.numerator.magnitude.decimalDigitCount <= 2 else {
                    throw CalcError.exponent
                }
                let exponentText = exponentRational.numerator.magnitude.decimalString
                guard let magnitude = Int(exponentText), magnitude <= maxExponent else {
                    throw CalcError.exponent
                }
                let exponent = exponentRational.numerator.isNegative ? -magnitude : magnitude
                guard abs(exponent) <= maxExponent else { throw CalcError.exponent }
                if exponent < 0, base.value.isZero { throw CalcError.divisionByZero }
                let result = try base.value.raised(to: exponent)
                try checkOverflow(result)
                return Measured(value: result, dim: .none)
            }
            return base
        }

        mutating func postfix(depth: Int) throws -> Measured {
            var value = try atom(depth: depth)
            while case .operator("%", _) = peek {
                at += 1
                // SPEC v1.1 : % est une mise à l'échelle sans dimension —
                // la dimension de la valeur est conservée.
                value = Measured(value: value.value / Rational(integer: BigInt(magnitude: BigUInt(100), negative: false)), dim: value.dim)
                try checkOverflow(value.value)
            }
            return value
        }

        mutating func atom(depth: Int) throws -> Measured {
            guard depth <= maxDepth else { throw CalcError.tooDeep }
            let token = peek
            at += 1
            switch token {
            case .number(let raw, let position, let currency):
                let parsed = try CalcKernel.parseNumber(raw, position: position)
                let dim = currency.isEmpty ? CalcDimension.none : CalcDimension(kind: .currency, code: currency)
                return Measured(value: parsed, dim: dim)
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
                if case .number(_, let juxtapositionPosition, _) = peek { throw CalcError.trailing(position: juxtapositionPosition) }
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

    /// Évalue une expression en rationnel exact. Les liaisons sont sans
    /// dimension ; les règles dimensionnelles des littéraux monétaires restent
    /// appliquées pendant l'évaluation.
    func evaluate(_ expression: String, names: [String: Rational] = [:]) throws -> Rational {
        let lifted = names.mapValues { Measured.plain($0) }
        return try evaluateDimensional(expression, names: lifted).value
    }

    /// Évalue une expression en valeur dimensionnée — SPEC 3.1.
    func evaluateDimensional(_ expression: String, names: [String: Measured] = [:]) throws -> Measured {
        let tokens = try CalcKernel.tokenize(CalcKernel.normalize(expression))
        var parser = Parser(tokens: tokens, names: names)
        return try parser.parse()
    }
}
