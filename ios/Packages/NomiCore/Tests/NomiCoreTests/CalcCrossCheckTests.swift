import XCTest
@testable import NomiCore

/// Recoupement noyau/vérificateur sur le corpus complet + propriétés différentielles.
final class CalcCrossCheckTests: XCTestCase {
    private func corpusVectors() throws -> [CalcConformanceTests.Vector] {
        let thisFile = URL(fileURLWithPath: #filePath)
        let repoRoot = thisFile
            .deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
            .deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        let url = repoRoot.appendingPathComponent("calc-spec/vectors.json")
        return try JSONDecoder().decode(CalcConformanceTests.Corpus.self, from: Data(contentsOf: url)).vectors
    }

    func testKernelAndVerifierAgreeOnEntireCorpus() throws {
        var failures: [String] = []
        for vector in try corpusVectors() {
            var names: [String: Rational] = [:]
            for (key, raw) in vector.names ?? [:] {
                if let value = Rational(decimalLiteral: raw) { names[key] = value }
            }
            switch CalcCrossCheck.compare(vector.input, names: names) {
            case .agree, .bothReject:
                break
            case .disagree(let kernel, let verifier):
                failures.append("\(vector.id): désaccord noyau=\(kernel) vérificateur=\(verifier)")
            case .kernelOnly:
                failures.append("\(vector.id): le vérificateur accepte ce que le noyau refuse")
            case .verifierOnly:
                failures.append("\(vector.id): le noyau accepte ce que le vérificateur refuse")
            }
        }
        XCTAssertTrue(failures.isEmpty, failures.joined(separator: "\n"))
    }

    /// Propriété différentielle : expressions générées aléatoirement (graine fixe),
    /// égalité exacte exigée entre noyau et vérificateur.
    func testDifferentialRandomExpressions() {
        var generator = SeededGenerator(seed: 2_026_11_27)
        var disagreements = 0
        var evaluated = 0
        for _ in 0..<500 {
            let expression = randomExpression(depth: 0, using: &generator)
            switch CalcCrossCheck.compare(expression) {
            case .agree, .bothReject:
                evaluated += 1
            default:
                disagreements += 1
            }
        }
        XCTAssertGreaterThan(evaluated, 100, "trop peu d'expressions exploitables, le test ne prouve rien")
        XCTAssertEqual(disagreements, 0)
    }

    // MARK: Générateur d'expressions déterministe

    struct SeededGenerator: RandomNumberGenerator {
        var state: UInt64
        init(seed: UInt64) { state = seed &* 0x9E3779B97F4A7C15 }
        mutating func next() -> UInt64 {
            state = state &* 6364136223846793005 &+ 1442695040888963407
            return state >> 11
        }
    }

    private func randomExpression(depth: Int, using generator: inout SeededGenerator) -> String {
        if depth > 3 || Int.random(in: 0..<4, using: &generator) == 0 {
            let value = Int.random(in: 0..<100, using: &generator)
            let fraction = Int.random(in: 0..<4, using: &generator)
            return fraction == 0 ? "\(value),\(Int.random(in: 0..<100, using: &generator))" : "\(value)"
        }
        let left = randomExpression(depth: depth + 1, using: &generator)
        let right = randomExpression(depth: depth + 1, using: &generator)
        let op = ["+", "-", "*", "/", "^"][Int.random(in: 0..<5, using: &generator)]
        let maybePercent = Int.random(in: 0..<5, using: &generator) == 0 ? "%" : ""
        return "(\(left) \(op) \(right))\(maybePercent)"
    }
}
