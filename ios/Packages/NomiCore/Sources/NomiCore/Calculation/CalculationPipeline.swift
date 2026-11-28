import Foundation

/// Une étape de calcul proposée par le modèle — jamais calculée par lui.
public struct CalcPlanStep: Equatable, Sendable {
    public let id: String
    public let expression: String
    public let unit: String
}

/// Résultat d'une étape, calculé par le noyau et recoupé par le vérificateur.
public struct CalcStepResult: Equatable, Sendable {
    public let id: String
    public let expression: String
    public let value: Rational?
    public let unit: String
    public let verified: Bool
    /// Code monnaie si la valeur est dimensionnée (EUR/USD/GBP) — vide sinon.
    public let dimension: String

    public init(id: String, expression: String, value: Rational?, unit: String, verified: Bool, dimension: String = "") {
        self.id = id
        self.expression = expression
        self.value = value
        self.unit = unit
        self.verified = verified
        self.dimension = dimension
    }
}

/// Certificat de calcul — structure vérifiable, jamais du texte libre (SPEC 7.3).
public struct CalcCertificate: Equatable, Sendable, Codable {
    public let engine: String
    public let steps: [CertificateStep]
    public let grounding: String
    public let verifier: String
    public let status: String

    public struct CertificateStep: Equatable, Sendable, Codable {
        public let id: String
        public let expression: String
        public let value: String
        public let unit: String
        public let dimension: String
    }

    /// Forme JSON consignée — stable, triée, sans texte libre.
    public func jsonString() -> String? {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.sortedKeys]
        guard let data = try? encoder.encode(self) else { return nil }
        return String(data: data, encoding: .utf8)
    }
}

/// Pipeline de calcul déterministe — calc-spec/SPEC.md et deterministic_calculation_system.md.
/// Le modèle propose un plan JSON ; toute la chaîne numérique est ensuite mécanique :
/// ancrage des opérandes dans la demande, calcul exact recoupé, composition des
/// chiffres par substitution de marqueurs, masquage de tout chiffre intraçable.
public enum CalcPipeline {
    public static let maxSteps = 12

    /// Constantes utilisables sans provenance : comptages usuels et unités de temps — SPEC.
    static let constants: [Rational] = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 24, 30, 31, 52, 60, 100, 365, 1000]
        .map { Rational(integer: BigInt(magnitude: BigUInt(UInt64($0)), negative: false)) }

    // MARK: Lecture du plan JSON

    /// Extraction tolérante du plan : premier `{` au dernier `}`, jamais d'erreur —
    /// un plan illisible signifie simplement « pas de calcul ».
    public static func parsePlan(_ text: String) -> (calculates: Bool, steps: [CalcPlanStep], answer: String) {
        guard let start = text.firstIndex(of: "{"), let end = text.lastIndex(of: "}"), end > start,
              let data = String(text[start...end]).data(using: .utf8),
              let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return (false, [], "")
        }
        var steps: [CalcPlanStep] = []
        if let rawSteps = object["steps"] as? [[String: Any]] {
            for raw in rawSteps.prefix(maxSteps) {
                let id = (raw["id"] as? String) ?? ""
                let expression = ((raw["expr"] as? String) ?? "")
                    .replacingOccurrences(of: "{", with: "").replacingOccurrences(of: "}", with: "")
                let unit = String(((raw["unit"] as? String) ?? "").prefix(12))
                guard isValidIdentifier(id), !expression.isEmpty, expression.count <= CalcKernel.maxLength else { continue }
                steps.append(CalcPlanStep(id: id, expression: expression, unit: unit))
            }
        }
        let answer = ((object["answer"] as? String) ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        let calculates = steps.isEmpty == false
        return (calculates, steps, answer)
    }

    static func isValidIdentifier(_ id: String) -> Bool {
        guard id.count <= 32, let first = id.first,
              (first >= "a" && first <= "z") || (first >= "A" && first <= "Z") else { return false }
        return id.allSatisfy {
            ($0 >= "a" && $0 <= "z") || ($0 >= "A" && $0 <= "Z") || ($0 >= "0" && $0 <= "9") || $0 == "_"
        }
    }

    // MARK: Extraction de nombres dans un texte libre

    private static let numberPattern = try! NSRegularExpression(
        pattern: #"(?<![\w.,])\d{1,3}(?:[\x{202F}\x{00A0} ]\d{3})+(?:[.,]\d+)?|(?<![\w.,])\d+(?:[.,]\d+)*"#)
    private static let datePattern = try! NSRegularExpression(
        pattern: #"\b\d{1,2}/\d{1,2}(?:/\d{2,4})?\b|\b\d{4}-\d{2}-\d{2}\b|\b\d{1,2} ?[:hH] ?\d{2}\b"#)

    /// Lectures rationnelles exactes d'un fragment numérique : forme à virgule
    /// et forme à point quand les deux sont plausibles, groupement de milliers retiré.
    static func readings(_ fragment: String) -> [Rational] {
        var clean = fragment
            .replacingOccurrences(of: "\u{202F}", with: "")
            .replacingOccurrences(of: "\u{00A0}", with: "")
        // Groupement par espace simple : validé comme dans le noyau.
        while let range = clean.range(of: #"(?<=\d) (?=\d{3}(?!\d))"#, options: .regularExpression) {
            clean.removeSubrange(range)
        }
        clean = clean.replacingOccurrences(of: " ", with: "")
        let commas = clean.filter { $0 == "," }.count
        let dots = clean.filter { $0 == "." }.count
        var forms: [String] = []
        if commas + dots == 0 { forms = [clean] }
        else if commas == 1, dots == 0 { forms = [clean.replacingOccurrences(of: ",", with: ".")] }
        else if dots == 1, commas == 0 { forms = [clean] }
        else {
            // Formes ambiguës : les deux lectures candidates.
            forms = [clean.replacingOccurrences(of: ",", with: "."), clean.replacingOccurrences(of: ".", with: "")]
        }
        var result: [Rational] = []
        for form in forms {
            guard form.filter({ $0 == "." }).count <= 1,
                  !form.hasPrefix("."), !form.hasSuffix("."),
                  form.allSatisfy({ ($0 >= "0" && $0 <= "9") || $0 == "." }),
                  let value = Rational(decimalLiteral: form) else { continue }
            if !result.contains(value) { result.append(value) }
        }
        return result
    }

    /// Nombres présents dans un texte libre (demande utilisateur, contexte).
    public static func extractNumbers(_ text: String) -> [Rational] {
        let nsrange = NSRange(text.startIndex..., in: text)
        var numbers: [Rational] = []
        for match in numberPattern.matches(in: text, range: nsrange) {
            guard let range = Range(match.range, in: text) else { continue }
            for value in readings(String(text[range])) where !numbers.contains(value) {
                numbers.append(value)
            }
        }
        return numbers
    }

    // MARK: Ancrage

    /// Une étape n'est vérifiable que si chaque nombre de son expression provient
    /// de la demande/du contexte, d'une constante admise, ou d'une variante de taux.
    public static func isGrounded(_ expression: String, known: [Rational]) -> Bool {
        var values = known.map { $0.magnitude } + constants
        values += values.flatMap { rate in
            let hundred = Rational(integer: BigInt(magnitude: BigUInt(100), negative: false))
            let one = Rational(integer: BigInt(magnitude: BigUInt(1), negative: false))
            return [rate / hundred, one + rate / hundred, one - rate / hundred]
        }
        let nsrange = NSRange(expression.startIndex..., in: expression)
        for match in numberPattern.matches(in: expression, range: nsrange) {
            guard let range = Range(match.range, in: expression) else { continue }
            let fragment = String(expression[range])
            guard fragment.first?.isNumber == true || fragment.first == "." else { continue }
            let candidates = readings(fragment)
            // Chaque fragment doit avoir AU MOINS une lecture ancrée — sinon il est inventé.
            guard candidates.contains(where: { candidate in values.contains(candidate) }) else { return false }
        }
        return true
    }

    // MARK: Calcul recoupé

    /// Évalue les étapes en chaîne — chaque identificateur ne peut se résoudre
    /// que dans une étape antérieure (SPEC 2). Le vérificateur indépendant doit
    /// produire la fraction exacte identique : tout désaccord invalide l'étape.
    public static func run(_ steps: [CalcPlanStep], known: [Rational]) -> [CalcStepResult] {
        let kernel = CalcKernel()
        let verifier = CalcVerifierEngine()
        // SPEC 3.1 : les dimensions se propagent par les identificateurs —
        // une étape référencée porte sa dimension monétaire dans les suivantes.
        var bindings: [String: Measured] = [:]
        var results: [CalcStepResult] = []
        for step in steps.prefix(maxSteps) {
            var verified = false
            var value: Rational?
            var dimension = ""
            if isGrounded(step.expression, known: known + bindings.values.map(\.value)) {
                do {
                    let kernelValue = try kernel.evaluateDimensional(step.expression, names: bindings)
                    let verifierValue = try verifier.evaluateDimensional(step.expression, names: bindings)
                    if kernelValue == verifierValue {
                        value = kernelValue.value
                        verified = true
                        bindings[step.id] = kernelValue
                        if kernelValue.dim.kind == .currency { dimension = kernelValue.dim.code }
                    }
                } catch {
                    // expression invalide, contresens dimensionnel ou borne dépassée :
                    // étape non vérifiée, jamais de repli
                }
            }
            results.append(CalcStepResult(id: step.id, expression: step.expression, value: value, unit: step.unit, verified: verified, dimension: dimension))
        }
        return results
    }

    /// Assemble le certificat de calcul d'une réponse — la structure affichée et
    /// consignée, que le texte généré ne peut pas imiter (SPEC 7.3).
    public static func certify(_ results: [CalcStepResult], grounded: Bool, engineVersion: String = "nomi-calc/1.1") -> CalcCertificate {
        let verifiedSteps = results.filter(\.verified).map { result in
            CalcCertificate.CertificateStep(
                id: result.id, expression: result.expression,
                value: result.value?.displayFrench() ?? "", unit: result.unit,
                dimension: result.dimension)
        }
        let status = !results.isEmpty && results.allSatisfy(\.verified) && grounded ? "verified" : "ungrounded"
        return CalcCertificate(engine: engineVersion, steps: verifiedSteps, grounding: grounded ? "anchored" : "unanchored", verifier: "agree", status: status)
    }

    // MARK: Composition mécanique

    /// Substitue les marqueurs `{id}` par les chiffres du moteur — jamais par le modèle.
    /// nil si le modèle cite une étape absente ou non vérifiée.
    public static func compose(_ template: String, results: [CalcStepResult]) -> String? {
        var byId: [String: Rational] = [:]
        for result in results where result.verified { byId[result.id] = result.value }
        var missing = false
        var output = template
        let pattern = try! NSRegularExpression(pattern: #"[\{]([A-Za-z][A-Za-z0-9_]{0,31})[\}]"#)
        let matches = pattern.matches(in: template, range: NSRange(template.startIndex..., in: template))
        for match in matches.reversed() {
            guard let range = Range(match.range, in: template),
                  let idRange = Range(match.range(at: 1), in: template) else { continue }
            let id = String(template[idRange])
            if let value = byId[id] {
                output.replaceSubrange(range, with: value.displayFrench())
            } else {
                missing = true
            }
        }
        return missing || output.isEmpty ? nil : output
    }

    // MARK: Masquage

    /// Remplace par `…` tout nombre du texte qui n'est ni un résultat vérifié,
    /// ni un nombre de la demande/du contexte, ni un petit entier structural,
    /// ni une date/heure. Retourne le texte masqué et le nombre de chiffres masqués.
    public static func mask(_ answer: String, allowed: [Rational]) -> (text: String, hidden: Int) {
        let nsrange = NSRange(answer.startIndex..., in: answer)
        let dateRanges = datePattern.matches(in: answer, range: nsrange).compactMap { Range($0.range, in: answer) }
        var blocked: [Range<String.Index>] = []
        for match in numberPattern.matches(in: answer, range: nsrange) {
            guard let range = Range(match.range, in: answer) else { continue }
            if dateRanges.contains(where: { $0.overlaps(range) }) { continue }
            let fragment = String(answer[range])
            let candidates = readings(fragment)
            guard !candidates.isEmpty else { continue }
            // Petits entiers structuraux (comptages, listes) — jamais des résultats.
            if fragment.allSatisfy({ $0 >= "0" && $0 <= "9" }),
               let first = candidates.first, first <= constants[10] { continue }
            let traceable = candidates.contains { candidate in
                allowed.contains { value in sameAsDisplayed(value, candidate) }
            }
            if !traceable { blocked.append(range) }
        }
        var text = answer
        for range in blocked.reversed() { text.replaceSubrange(range, with: "…") }
        return (text, blocked.count)
    }

    /// Le texte affiche des valeurs arrondies : la comparaison se fait à l'échelle
    /// du fragment, ou en cent fois plus grand (taux écrit en pourcent ou en fraction).
    private static func sameAsDisplayed(_ value: Rational, _ candidate: Rational) -> Bool {
        if value == candidate { return true }
        if value.magnitude == candidate.magnitude { return true }
        // Arrondi du résultat à l'échelle du candidat.
        let scale = decimalPlaces(of: candidate)
        if value.roundedHalfUp(places: scale) == candidate { return true }
        if (value * Rational(integer: BigInt(magnitude: BigUInt(100), negative: false))).roundedHalfUp(places: scale) == candidate { return true }
        return false
    }

    private static func decimalPlaces(of value: Rational) -> Int {
        var denominator = value.denominator
        var places = 0
        let ten = BigUInt(10)
        while (denominator % ten).isZero, places < 12 {
            denominator = denominator / ten
            places += 1
        }
        return denominator == BigUInt(1) ? places : 4
    }

    /// Vérification directe de l'expression saisie par l'utilisateur (`80*15/100` ou
    /// `80*15/100 =`) : le calcul est évalué par le noyau, recoupé par le vérificateur,
    /// et la réponse est entièrement mécanique. nil si l'entrée n'est pas une expression.
    public static func verifyUserExpression(_ text: String) -> (expression: String, display: String, verified: Bool)? {
        var expression = text.trimmingCharacters(in: .whitespacesAndNewlines)
        if expression.hasSuffix("=") { expression = String(expression.dropLast()).trimmingCharacters(in: .whitespaces) }
        guard !expression.isEmpty, expression.count <= CalcKernel.maxLength,
              expression.contains(where: { $0 >= "0" && $0 <= "9" }),
              expression.contains(where: { "+-*/^%()".contains($0) }) else { return nil }
        let kernel = CalcKernel()
        let verifier = CalcVerifierEngine()
        do {
            let kernelValue = try kernel.evaluate(expression)
            let verifierValue = try verifier.evaluate(expression)
            guard kernelValue == verifierValue else {
                return (expression, "", false)
            }
            return (expression, kernelValue.displayFrench(), true)
        } catch {
            return (expression, "", false)
        }
    }

    // MARK: Calcul posé (audit utilisateur)

    /// Le calcul posé, affichable sous la réponse : une ligne par étape vérifiée.
    public static func posed(_ results: [CalcStepResult]) -> [String] {
        results.filter(\.verified).compactMap { result in
            guard let value = result.value else { return nil }
            return "\(result.id) = \(result.expression) = \(value.displayFrench()) \(result.unit)"
                .trimmingCharacters(in: .whitespaces)
        }
    }
}
