import XCTest
@testable import NomiCore

/// Exécute le corpus de conformité partagé calc-spec/vectors.json — même corpus
/// sur Windows et iOS. Tout écart est un échec de build.
final class CalcConformanceTests: XCTestCase {
    struct Vector: Decodable {
        let id: String
        let category: String
        let input: String
        let names: [String: String]?
        let expect: Expect
    }

    struct Expect: Decodable {
        let status: String
        let rational: String?
        let display_fr: String?
        let display_places: Int?
        let error: String?
        let dimension: DimensionExpect?

        enum DimensionExpect: Decodable, Equatable {
            case none
            case currency(String)

            init(from decoder: Decoder) throws {
                let container = try decoder.singleValueContainer()
                if let text = try? container.decode(String.self) {
                    self = text == "none" ? .none : .none
                    return
                }
                let object = try container.decode([String: String].self)
                if let code = object["currency"] { self = .currency(code); return }
                self = .none
            }
        }
    }

    struct Corpus: Decodable {
        let vectors: [Vector]
    }

    private func loadCorpus() throws -> Corpus {
        // calc-spec/vectors.json à la racine du dépôt — chemin relatif depuis ce fichier.
        let thisFile = URL(fileURLWithPath: #filePath)
        let repoRoot = thisFile
            .deletingLastPathComponent() // NomiCoreTests
            .deletingLastPathComponent() // Tests
            .deletingLastPathComponent() // NomiCore
            .deletingLastPathComponent() // Packages
            .deletingLastPathComponent() // ios
            .deletingLastPathComponent() // racine du dépôt
        let url = repoRoot.appendingPathComponent("calc-spec/vectors.json")
        let data = try Data(contentsOf: url)
        return try JSONDecoder().decode(Corpus.self, from: data)
    }

    func testConformanceCorpus() throws {
        let corpus = try loadCorpus()
        XCTAssertGreaterThanOrEqual(corpus.vectors.count, 78, "le corpus ne doit pas rétrécir")
        let kernel = CalcKernel()
        var failures: [String] = []
        for vector in corpus.vectors {
            var names: [String: Rational] = [:]
            for (key, raw) in vector.names ?? [:] {
                if let value = Rational(decimalLiteral: raw) { names[key] = value }
            }
            do {
                let measured = try kernel.evaluateDimensional(vector.input, names: names.mapValues { Measured.plain($0) })
                let result = measured.value
                // Vecteurs de dimension : la dimension attendue est comparée exactement.
                if let wantDim = vector.expect.dimension {
                    let dimOk: Bool
                    switch wantDim {
                    case .none: dimOk = measured.dim.kind == .none
                    case .currency(let code): dimOk = measured.dim.kind == .currency && measured.dim.code == code
                    }
                    if !dimOk {
                        failures.append("\(vector.id): dimension \(measured.dim.kind)/\(measured.dim.code) inattendue")
                        continue
                    }
                }
                if vector.expect.status == "error" {
                    failures.append("\(vector.id): attendait une erreur \(vector.expect.error ?? "?"), obtient \(result.displayFrench())")
                    continue
                }
                // Fraction réduite exacte attendue.
                if let expected = vector.expect.rational {
                    let actual = rationalDescription(result)
                    if actual != expected {
                        failures.append("\(vector.id): rationnel \(actual) != \(expected)")
                        continue
                    }
                }
                if let expectedDisplay = vector.expect.display_fr {
                    let places = vector.expect.display_places ?? 4
                    let actualDisplay = result.displayFrench(places: places)
                    if actualDisplay != expectedDisplay {
                        failures.append("\(vector.id): affichage \(actualDisplay.debugDescription) != \(expectedDisplay.debugDescription)")
                    }
                }
            } catch let error as CalcError {
                if vector.expect.status != "error" {
                    failures.append("\(vector.id): erreur inattendue \(error.code)")
                } else if let expectedCode = vector.expect.error, error.code != expectedCode {
                    failures.append("\(vector.id): erreur \(error.code) != \(expectedCode)")
                }
            } catch {
                failures.append("\(vector.id): erreur non typée \(error)")
            }
        }
        XCTAssertTrue(failures.isEmpty, failures.joined(separator: "\n"))
    }

    private func rationalDescription(_ value: Rational) -> String {
        let numerator = value.numerator.magnitude.decimalString
        let signed = value.numerator.isNegative ? "-" + numerator : numerator
        return "\(signed)/\(value.denominator.decimalString)"
    }
}
