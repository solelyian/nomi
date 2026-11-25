import SwiftUI
import NomiCore

struct CodeReviewView: View {
    @EnvironmentObject private var state: AppState

    var body: some View {
        CodeReviewContent(reviewService: state.codeReviewService)
    }
}

private struct CodeReviewContent: View {
    @StateObject private var model: CodeReviewViewModel
    @State private var selectedDiagnostic: CodeDiagnostic?

    init(reviewService: any CodeReviewService) {
        _model = StateObject(wrappedValue: CodeReviewViewModel(reviewService: reviewService))
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                NomiSectionTitle("Relecture de code", subtitle: "Import, collage ou diff — pensé pour relire en mobilité, pas pour remplacer un IDE desktop.")

                VStack(alignment: .leading, spacing: 10) {
                    HStack {
                        NomiBadge("Swift · exemple réaliste", tint: NomiTheme.code)
                        Spacer()
                        Menu("Importer") {
                            Button("Depuis Fichiers (simulé)") {}
                            Button("Depuis le Partage (simulé)") {}
                            Button("Diff de pull request (extension)") {}
                        }
                        .font(.subheadline.weight(.semibold))
                    }
                    TextEditor(text: Binding(get: { model.code }, set: { model.code = $0 }))
                        .font(.system(.footnote, design: .monospaced))
                        .frame(minHeight: 210)
                        .padding(8)
                        .scrollContentBackground(.hidden)
                        .background(NomiTheme.surface2, in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                        .accessibilityLabel("Code à relire")
                    HStack {
                        Button {
                            Task { await model.runReview() }
                        } label: {
                            Label(model.isReviewing ? "Relecture…" : "Relire", systemImage: "checkmark.seal")
                                .frame(maxWidth: .infinity)
                        }
                        .buttonStyle(.borderedProminent)
                        .disabled(model.isReviewing)

                        Button("Explication", systemImage: "graduationcap") {
                            if let first = model.diagnostics.first { selectedDiagnostic = first }
                        }
                        .buttonStyle(.bordered)
                        .disabled(model.diagnostics.isEmpty)
                    }
                }
                .nomiCard(tint: NomiTheme.code)

                if let error = model.errorMessage {
                    Label(error, systemImage: "exclamationmark.triangle")
                        .font(.footnote)
                        .foregroundStyle(NomiTheme.warning)
                }

                VStack(alignment: .leading, spacing: 12) {
                    HStack {
                        NomiSectionTitle("Diagnostics", subtitle: model.diagnostics.isEmpty ? "Lancez une relecture pour voir les problèmes." : "\(model.diagnostics.count) diagnostic(s), triés par sévérité.")
                        Spacer()
                    }
                    if model.diagnostics.isEmpty {
                        ContentUnavailableView("Aucune relecture", systemImage: "doc.text.magnifyingglass", description: Text("La version iOS cible les fichiers, sélections et diffs ; l'exécution de projets reste réservée au desktop."))
                            .frame(maxWidth: .infinity)
                    } else {
                        ForEach(model.diagnostics) { diagnostic in
                            Button {
                                selectedDiagnostic = diagnostic
                            } label: {
                                DiagnosticRow(diagnostic: diagnostic)
                            }
                            .buttonStyle(.plain)
                            .swipeActions(edge: .trailing) {
                                Button("Corriger") { model.apply(diagnostic) }
                                    .tint(NomiTheme.success)
                            }
                        }
                    }
                }
                .nomiCard(tint: NomiTheme.code)

                VStack(alignment: .leading, spacing: 12) {
                    NomiSectionTitle("Vérification de calcul", subtitle: "Moteur local déterministe : le chiffre n'est jamais régénéré par l'IA.")
                    TextField("Ex. 80 × 15 % = 12 €", text: Binding(get: { model.calculationInput }, set: { model.calculationInput = $0 }))
                        .textFieldStyle(.roundedBorder)
                        .keyboardType(.numbersAndPunctuation)
                    Button("Vérifier localement") { model.verifyCalculation() }
                        .buttonStyle(.borderedProminent)
                    if let result = model.calculationResult {
                        CalculationResultView(result: result)
                    }
                }
                .nomiCard(tint: NomiTheme.success)

                VStack(alignment: .leading, spacing: 12) {
                    NomiSectionTitle("Question sur le code", subtitle: "Revue conversationnelle simulée dans ce prototype.")
                    TextField("Pourquoi ce force unwrap est-il risqué ?", text: Binding(get: { model.question }, set: { model.question = $0 }), axis: .vertical)
                        .textFieldStyle(.roundedBorder)
                        .lineLimit(2...4)
                    Button("Poser la question") {
                        Task { await model.askQuestion() }
                    }
                    .buttonStyle(.bordered)
                    if !model.conversationAnswer.isEmpty {
                        Text(model.conversationAnswer)
                            .font(.subheadline)
                            .foregroundStyle(NomiTheme.ink2)
                            .textSelection(.enabled)
                    }
                }
                .nomiCard(tint: NomiTheme.course)
            }
            .padding()
        }
        .nomiScreenBackground()
        .navigationTitle("Code")
        .sheet(item: $selectedDiagnostic) { diagnostic in
            DiagnosticDetailSheet(diagnostic: diagnostic) {
                model.apply(diagnostic)
                selectedDiagnostic = nil
            }
            .presentationDetents([.medium, .large])
        }
    }
}

private struct DiagnosticRow: View {
    var diagnostic: CodeDiagnostic

    var color: Color {
        switch diagnostic.severity {
        case .critical: return .red
        case .warning: return .orange
        case .suggestion: return NomiTheme.code
        case .info: return NomiTheme.success
        }
    }

    var body: some View {
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: diagnostic.severity == .critical ? "exclamationmark.octagon.fill" : "lightbulb.fill")
                .foregroundStyle(color)
                .frame(width: 28)
            VStack(alignment: .leading, spacing: 4) {
                Text(diagnostic.title)
                    .font(.headline)
                    .foregroundStyle(NomiTheme.ink)
                Text("Ligne \(diagnostic.line) · \(diagnostic.problem)")
                    .font(.subheadline)
                    .foregroundStyle(NomiTheme.ink2)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Spacer()
            NomiBadge(diagnostic.severity.rawValue, tint: color)
        }
        .padding(.vertical, 6)
        .contentShape(Rectangle())
    }
}

private struct DiagnosticDetailSheet: View {
    var diagnostic: CodeDiagnostic
    var apply: () -> Void

    var body: some View {
        NavigationStack {
            List {
                Section("Problème") {
                    Text(diagnostic.problem)
                    Text("Ligne \(diagnostic.line) · sévérité \(diagnostic.severity.rawValue)")
                        .foregroundStyle(.secondary)
                }
                Section("Explication pédagogique") {
                    Text(diagnostic.explanation)
                    Text(diagnostic.educationalNote)
                        .foregroundStyle(.secondary)
                }
                Section("Correction proposée") {
                    Text(diagnostic.proposedCorrection)
                    Button("Appliquer le correctif", action: apply)
                        .buttonStyle(.borderedProminent)
                }
                Section {
                    Label("Prototype : le correctif ajoute une annotation, sans analyse AST ni diff automatique.", systemImage: "info.circle")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                }
            }
            .navigationTitle(diagnostic.title)
            .navigationBarTitleDisplayMode(.inline)
        }
    }
}

private struct CalculationResultView: View {
    var result: CalculationVerification

    var tint: Color {
        switch result.status {
        case .verified: return NomiTheme.success
        case .mismatch: return .red
        case .unsupported, .invalid: return NomiTheme.warning
        }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 7) {
            Label(result.message, systemImage: result.status == .verified ? "checkmark.circle.fill" : "exclamationmark.triangle.fill")
                .foregroundStyle(tint)
                .font(.subheadline.weight(.semibold))
            if let computed = result.computed {
                Text("Recalcul local : \(NSDecimalNumber(decimal: computed).stringValue)")
                    .font(.footnote.monospaced())
                    .foregroundStyle(NomiTheme.ink2)
            }
        }
        .padding(10)
        .background(tint.opacity(0.1), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
    }
}
