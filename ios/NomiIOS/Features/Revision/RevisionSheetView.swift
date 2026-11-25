import SwiftUI
import NomiCore

struct RevisionSheetView: View {
    @EnvironmentObject private var state: AppState
    var sourceText: String
    var sourceTitle: String

    @State private var sheet: RevisionSheet?
    @State private var isGenerating = false
    @State private var errorMessage: String?
    @State private var showingSource = false
    @State private var flippedCards: Set<UUID> = []
    @State private var savedSheetIDs: Set<UUID> = []

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                NomiSectionTitle("Fiche de révision", subtitle: "Le contenu issu du cours est distingué des explications et hypothèses générées.")

                VStack(alignment: .leading, spacing: 12) {
                    HStack {
                        NomiBadge(sourceTitle, tint: NomiTheme.success)
                        Spacer()
                        Button("Source") { showingSource.toggle() }
                            .buttonStyle(.bordered)
                    }
                    Picker("Niveau", selection: $state.preferredDetailLevel) {
                        ForEach(RevisionDetailLevel.allCases, id: \.self) { level in
                            Text(level.frenchTitle).tag(level)
                        }
                    }
                    .pickerStyle(.segmented)
                    Picker("Format", selection: $state.preferredFormat) {
                        ForEach(RevisionFormat.allCases, id: \.self) { format in
                            Text(format.frenchTitle).tag(format)
                        }
                    }
                    .pickerStyle(.menu)
                    Button {
                        Task { await generate() }
                    } label: {
                        Label(isGenerating ? "Génération…" : "Générer", systemImage: "sparkles")
                            .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.borderedProminent)
                    .disabled(isGenerating)
                    if let errorMessage {
                        Label(errorMessage, systemImage: "exclamationmark.triangle")
                            .font(.footnote)
                            .foregroundStyle(.orange)
                    }
                }
                .nomiCard(tint: NomiTheme.success)

                if showingSource {
                    VStack(alignment: .leading, spacing: 8) {
                        Label("Source importée", systemImage: "doc.plaintext")
                            .font(.headline)
                        Text(sourceText)
                            .font(.footnote)
                            .textSelection(.enabled)
                            .foregroundStyle(NomiTheme.ink2)
                    }
                    .nomiCard(tint: NomiTheme.ink3)
                }

                if let sheet {
                    sheetContent(sheet)
                } else {
                    ContentUnavailableView("Aucune fiche", systemImage: "rectangle.stack", description: Text("Choisissez un niveau et un format, puis générez une fiche vérifiable."))
                }
            }
            .padding()
        }
        .nomiScreenBackground()
        .navigationTitle("Révision")
    }

    @ViewBuilder
    private func sheetContent(_ sheet: RevisionSheet) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 4) {
                    Text(sheet.title)
                        .font(.title3.weight(.bold))
                        .foregroundStyle(NomiTheme.ink)
                    Text("\(sheet.level.frenchTitle) · \(sheet.format.frenchTitle)")
                        .font(.subheadline)
                        .foregroundStyle(NomiTheme.ink2)
                }
                Spacer()
                ShareLink(item: exportText(sheet)) {
                    Image(systemName: "square.and.arrow.up")
                        .frame(width: 44, height: 44)
                }
                .buttonStyle(.bordered)
            }

            if sheet.format == .flashcards {
                flashcards(sheet)
            } else if sheet.format == .quiz {
                quiz(sheet)
            } else {
                ForEach(sheet.sections) { section in
                    VStack(alignment: .leading, spacing: 8) {
                        HStack {
                            Text(section.title)
                                .font(.headline)
                            Spacer()
                            fidelityBadge(section.fidelity)
                        }
                        Text(section.body)
                            .font(.subheadline)
                            .foregroundStyle(NomiTheme.ink2)
                            .fixedSize(horizontal: false, vertical: true)
                            .textSelection(.enabled)
                    }
                    .padding(12)
                    .background(section.highlighted ? NomiTheme.accentSoft : NomiTheme.surface2, in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                    .contextMenu {
                        Button("Mettre en évidence") {}
                        Button("Réorganiser (simulé)") {}
                        Button("Corriger (simulé)") {}
                    }
                }
            }

            Button(savedSheetIDs.contains(sheet.id) ? "Enregistrée dans la bibliothèque" : "Enregistrer dans la bibliothèque") {
                state.addSheet(sheet)
                savedSheetIDs.insert(sheet.id)
            }
            .buttonStyle(.borderedProminent)
            .disabled(savedSheetIDs.contains(sheet.id))
        }
        .nomiCard(tint: NomiTheme.success)
    }

    @ViewBuilder
    private func flashcards(_ sheet: RevisionSheet) -> some View {
        ForEach(sheet.flashcards) { card in
            Button {
                if flippedCards.contains(card.id) { flippedCards.remove(card.id) } else { flippedCards.insert(card.id) }
            } label: {
                VStack(alignment: .leading, spacing: 8) {
                    Text(flippedCards.contains(card.id) ? "Réponse" : "Question")
                        .font(.caption.weight(.bold))
                        .foregroundStyle(NomiTheme.success)
                    Text(flippedCards.contains(card.id) ? card.back : card.front)
                        .font(.body)
                        .foregroundStyle(NomiTheme.ink)
                        .frame(maxWidth: .infinity, alignment: .leading)
                }
                .padding(14)
                .background(NomiTheme.successSoft, in: RoundedRectangle(cornerRadius: 16, style: .continuous))
            }
            .buttonStyle(.plain)
        }
    }

    @ViewBuilder
    private func quiz(_ sheet: RevisionSheet) -> some View {
        ForEach(sheet.quiz) { question in
            VStack(alignment: .leading, spacing: 8) {
                Text(question.question)
                    .font(.headline)
                ForEach(Array(question.choices.enumerated()), id: \.offset) { index, choice in
                    HStack {
                        Image(systemName: index == question.correctIndex ? "checkmark.circle" : "circle")
                            .foregroundStyle(index == question.correctIndex ? NomiTheme.success : NomiTheme.ink3)
                        Text(choice)
                            .font(.subheadline)
                    }
                }
                Text(question.explanation)
                    .font(.footnote)
                    .foregroundStyle(NomiTheme.ink2)
            }
            .padding(12)
            .background(NomiTheme.surface2, in: RoundedRectangle(cornerRadius: 14, style: .continuous))
        }
    }

    private func fidelityBadge(_ fidelity: FidelityKind) -> some View {
        switch fidelity {
        case .source:
            NomiBadge("source", tint: NomiTheme.success)
        case .generatedExplanation:
            NomiBadge("explication IA", tint: NomiTheme.code)
        case .hypothesis:
            NomiBadge("hypothèse", tint: NomiTheme.warning)
        }
    }

    private func generate() async {
        isGenerating = true
        errorMessage = nil
        do {
            sheet = try await state.revisionService.makeRevision(
                from: sourceText,
                sourceTitle: sourceTitle,
                level: state.preferredDetailLevel,
                format: state.preferredFormat
            )
        } catch {
            errorMessage = "La génération simulée a échoué. En production, le fallback hors-ligne conserverait un résumé extractif."
        }
        isGenerating = false
    }

    private func exportText(_ sheet: RevisionSheet) -> String {
        var output = "# \(sheet.title)\n\nSource : \(sheet.sourceTitle)\n\n"
        for section in sheet.sections {
            output += "## \(section.title) [\(section.fidelity.frenchTitle)]\n\(section.body)\n\n"
        }
        return output
    }
}
