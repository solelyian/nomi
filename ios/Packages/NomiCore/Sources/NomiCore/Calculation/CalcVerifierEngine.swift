import Foundation

// Vérificateur indépendant du noyau — calc-spec/SPEC.md section 4.
// Partage la spécification mais pas le code du noyau : évaluation par machine
// à pile (shunting-yard) sur des fractions NON réduites en cours de calcul,
// la réduction n'intervenant qu'à la comparaison finale — chemin de calcul
// structurellement différent de la descente récursive du noyau, qui réduit
// à chaque étape. L'accord exigé est l'égalité exacte des fractions réduites.
struct CalcVerifierEngine: Sendable {
    init() {}

    private enum Symbol: Equatable {
        case value(Measured)
        case op(Character)
        case leftParen
    }

    private enum Token: Equatable {
        case number(String, currency: String = "")
        case identifier(String)
        case op(Character)
        case leftParen
        case rightParen
        case currencySymbol(Character)
    }

    /// Tokenise indépendamment du noyau : pas de fonction partagée.
    private func scan(_ input: String) throws -> [Token] {
        var text = input
            .replacingOccurrences(of: "×", with: "*")
            .replacingOccurrences(of: "÷", with: "/")
            .replacingOccurrences(of: "−", with: "-")
            .replacingOccurrences(of: "\u{202F}", with: "")
            .replacingOccurrences(of: "\u{00A0}", with: "")
        // Milliers : un espace suivi d'exactement 3 chiffres puis d'un non-chiffre.
        while let range = text.range(of: #"(?<=\d) (?=\d{3}(?!\d))"#, options: .regularExpression) {
            text.removeSubrange(range)
        }
        guard text.count <= CalcKernel.maxLength else { throw CalcError.tooLong }

        var tokens: [Token] = []
        var index = text.startIndex
        while index < text.endIndex {
            let character = text[index]
            if character.isWhitespace { index = text.index(after: index); continue }
            if character >= "0" && character <= "9" {
                var end = index
                while end < text.endIndex, (text[end] >= "0" && text[end] <= "9") || text[end] == "." || text[end] == "," {
                    end = text.index(after: end)
                }
                tokens.append(.number(String(text[index..<end])))
                index = end
                continue
            }
            if (character >= "a" && character <= "z") || (character >= "A" && character <= "Z") || character == "_" {
                var end = index
                while end < text.endIndex, (text[end] >= "a" && text[end] <= "z") || (text[end] >= "A" && text[end] <= "Z") || (text[end] >= "0" && text[end] <= "9") || text[end] == "_" {
                    end = text.index(after: end)
                }
                tokens.append(.identifier(String(text[index..<end])))
                index = end
                continue
            }
            if character == "€" || character == "$" || character == "£" {
                tokens.append(.currencySymbol(character))
                index = text.index(after: index)
                continue
            }
            switch character {
            case "(": tokens.append(.leftParen)
            case ")": tokens.append(.rightParen)
            case "+", "-", "*", "/", "^", "%": tokens.append(.op(character))
            default: throw CalcError.parseError(position: text.distance(from: text.startIndex, to: index))
            }
            index = text.index(after: index)
        }
        // Attache les symboles monétaires au nombre adjacent (SPEC 3.2) —
        // écrit indépendamment du noyau : symbole entre deux nombres =
        // unsupported, symbole isolé = parse-error.
        var attached: [Token] = []
        var ahead = tokens
        for index in tokens.indices {
            let token = ahead[index]
            guard case .currencySymbol(let symbol) = token else { attached.append(token); continue }
            var before = false
            if let last = attached.last, case .number(_, _) = last { before = true }
            var after = false
            if index + 1 < ahead.count, case .number(_, _) = ahead[index + 1] { after = true }
            if before && after { throw CalcError.unsupported(position: 0) }
            if !before && !after { throw CalcError.parseError(position: 0) }
            let code = try CalcDimension.currency(of: symbol).code
            if before {
                guard case .number(let raw, let existing) = attached[attached.count - 1], existing.isEmpty else {
                    throw CalcError.parseError(position: 0)
                }
                attached[attached.count - 1] = .number(raw, currency: code)
            } else {
                guard case .number(let raw, let existing) = ahead[index + 1], existing.isEmpty else {
                    throw CalcError.parseError(position: 0)
                }
                ahead[index + 1] = .number(raw, currency: code)
            }
        }
        return attached
    }

    /// Évalue par machine à pile. Lève les mêmes erreurs typées que la spec.
    func evaluate(_ input: String, names: [String: Rational] = [:]) throws -> Rational {
        let lifted = names.mapValues { Measured.plain($0) }
        return try evaluateDimensional(input, names: lifted).value
    }

    /// Évalue en valeur dimensionnée — SPEC 3.1, machine à pile.
    func evaluateDimensional(_ input: String, names: [String: Measured] = [:]) throws -> Measured {
        let tokens = try scan(input)
        // SPEC 2.1 : l'équilibre des parenthèses est structurel — vérifié avant
        // toute évaluation, comme dans le noyau (`(1/0` est `parenthesis`).
        var parenthesisBalance = 0
        for token in tokens {
            if case .leftParen = token { parenthesisBalance += 1 }
            if case .rightParen = token {
                parenthesisBalance -= 1
                if parenthesisBalance < 0 { throw CalcError.parenthesis(position: 0) }
            }
        }
        if parenthesisBalance > 0 { throw CalcError.parenthesis(position: 0) }
        var output: [Symbol] = []
        var operators: [Character] = []
        var expectValue = true
        var depth = 0

        func precedence(_ op: Character) -> Int {
            switch op {
            case "+", "-": return 1
            case "*", "/": return 2
            case "~": return 3 // moins unaire
            case "^": return 4
            default: return 0
            }
        }

        func applyTop() throws {
            guard let op = operators.popLast() else { throw CalcError.parseError(position: 0) }
            if op == "~" {
                guard case .value(let top) = output.popLast() else { throw CalcError.parseError(position: 0) }
                output.append(.value(Measured(value: -top.value, dim: top.dim)))
                return
            }
            if op == "%" {
                guard case .value(let top) = output.popLast() else { throw CalcError.parseError(position: 0) }
                let percentResult = top.value / Rational(integer: BigInt(magnitude: BigUInt(100), negative: false))
                if percentResult.exceedsLimits { throw CalcError.overflow }
                // SPEC v1.1 : % est une mise à l'échelle sans dimension — dim conservée.
                output.append(.value(Measured(value: percentResult, dim: top.dim)))
                return
            }
            guard case .value(let right) = output.popLast(),
                  case .value(let left) = output.popLast() else {
                throw CalcError.parseError(position: 0)
            }
            let result: Measured
            switch op {
            case "+": result = try Measured.add(left, right, adding: true)
            case "-": result = try Measured.add(left, right, adding: false)
            case "*": result = try Measured.multiply(left, right)
            case "/": result = try Measured.divide(left, right)
            case "^":
                guard left.dim.isNone, right.dim.isNone else { throw CalcError.dimensionMismatch }
                guard right.value.denominator == BigUInt(1),
                      right.value.numerator.magnitude.decimalDigitCount <= 2,
                      let magnitude = Int(right.value.numerator.magnitude.decimalString),
                      magnitude <= CalcKernel.maxExponent else {
                    throw CalcError.exponent
                }
                let exponent = right.value.numerator.isNegative ? -magnitude : magnitude
                guard abs(exponent) <= CalcKernel.maxExponent else { throw CalcError.exponent }
                if exponent < 0, left.value.isZero { throw CalcError.divisionByZero }
                result = Measured.plain(try left.value.raised(to: exponent))
            default: throw CalcError.parseError(position: 0)
            }
            if result.value.exceedsLimits { throw CalcError.overflow }
            output.append(.value(result))
        }

        for token in tokens {
            switch token {
            case .currencySymbol:
                // Tous les symboles monétaires sont fusionnés au scan — aucun ne
                // doit subsister ici ; un reliquat est une entrée hors grammaire.
                throw CalcError.parseError(position: 0)
            case .number(let raw, let currency):
                // Juxtaposition : une valeur complète suivie d'un nombre est
                // `trailing` — SPEC grammaire, avant toute évaluation.
                if !expectValue { throw CalcError.trailing(position: 0) }
                let normalized = raw.replacingOccurrences(of: ",", with: ".")
                guard normalized.filter({ $0 == "." }).count <= 1,
                      let value = Rational(decimalLiteral: normalized) else {
                    throw CalcError.parseError(position: 0)
                }
                let dim = currency.isEmpty ? CalcDimension.none : CalcDimension(kind: .currency, code: currency)
                output.append(.value(Measured(value: value, dim: dim)))
                expectValue = false
            case .identifier(let name):
                if !expectValue { throw CalcError.trailing(position: 0) }
                if name.count > 32 || name.first == "_" { throw CalcError.parseError(position: 0) }
                guard let value = names[name] else { throw CalcError.unknownIdentifier(name) }
                output.append(.value(value))
                expectValue = false
            case .leftParen:
                if !expectValue { throw CalcError.trailing(position: 0) }
                depth += 1
                if depth > CalcKernel.maxDepth { throw CalcError.tooDeep }
                operators.append("(")
                expectValue = true
            case .rightParen:
                depth -= 1
                // SPEC 2.1 : fermante sans ouvrante — `parenthesis` avant toute application
                // d'opérateur en attente (`2+)` reste `parenthesis`).
                if !operators.contains("(") { throw CalcError.parenthesis(position: 0) }
                while let top = operators.last, top != "(" {
                    try applyTop()
                }
                guard operators.popLast() == "(" else { throw CalcError.parenthesis(position: 0) }
                // le pourcentage postfixe peut suivre une parenthèse fermante — il est
                // déjà traité par la boucle des opérateurs ; rien à faire ici.
                expectValue = false
            case .op(var op):
                if op == "%" {
                    // postfixe : s'applique immédiatement au sommet de pile.
                    operators.append("%")
                    try applyTop()
                    expectValue = false
                    continue
                }
                if expectValue {
                    if op == "-" {
                        // Moins unaire préfixe : il porte sur l'opérande SUIVANT,
                        // jamais sur ce qui précède — poussé directement, sans
                        // appliquer les opérateurs en attente (`2^-2` = 2^(−2),
                        // `-2^2` = −(2²) car ^ est appliqué avant ~ en fin de pile).
                        operators.append("~")
                        expectValue = true
                        continue
                    }
                    if op == "+" { continue }
                    throw CalcError.parseError(position: 0)
                }
                let rightAssociative = op == "^" || op == "~"
                while let top = operators.last, top != "(" {
                    let topPrecedence = precedence(top)
                    let currentPrecedence = precedence(op)
                    if topPrecedence > currentPrecedence || (topPrecedence == currentPrecedence && !rightAssociative) {
                        try applyTop()
                    } else {
                        break
                    }
                }
                operators.append(op)
                expectValue = true
            }
        }
        while !operators.isEmpty {
            if operators.last == "(" { throw CalcError.parenthesis(position: 0) }
            try applyTop()
        }
        guard output.count == 1, case .value(let result) = output.first else {
            throw CalcError.parseError(position: 0)
        }
        return result
    }
}

/// Recoupement : l'accord exigé est l'égalité exacte des fractions réduites.
/// Un désaccord est un défaut d'implémentation — bloquant, jamais toléré.
enum CalcCrossCheck {
    /// Résultat du recoupement noyau/vérificateur.
    enum Outcome: Equatable {
        case agree(Rational)
        case disagree(kernel: String, verifier: String)
        case bothReject(String)
        case kernelOnly
        case verifierOnly
    }

    static func compare(_ input: String, names: [String: Rational] = [:]) -> Outcome {
        let kernel = CalcKernel()
        let verifier = CalcVerifierEngine()
        let kernelResult = Result { try kernel.evaluate(input, names: names) }
        let verifierResult = Result { try verifier.evaluate(input, names: names) }
        switch (kernelResult, verifierResult) {
        case (.success(let k), .success(let v)):
            if k == v { return .agree(k) }
            return .disagree(kernel: describe(k), verifier: describe(v))
        case (.failure(let ke), .failure(let ve)):
            let kc = (ke as? CalcError)?.code ?? "?"
            let vc = (ve as? CalcError)?.code ?? "?"
            return kc == vc ? .bothReject(kc) : .disagree(kernel: "error \(kc)", verifier: "error \(vc)")
        case (.success, .failure):
            return .verifierOnly
        case (.failure, .success):
            return .kernelOnly
        }
    }

    private static func describe(_ value: Rational) -> String {
        let num = value.numerator.magnitude.decimalString
        let signed = value.numerator.isNegative ? "-" + num : num
        return "\(signed)/\(value.denominator.decimalString)"
    }
}
