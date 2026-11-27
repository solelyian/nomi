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
    @State private var quizAnswers: [UUID: Int] = [:]
    @State private var submittedQuizIDs: Set<UUID> = []

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                header
                generationPanel
                if showingSource { sourcePanel }
                if let sheet {
                    sheetContent(sheet)
                } else {
                    emptyState
                }
            }
            .padding(.horizontal, 18)
            .padding(.top, 14)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
        .navigationTitle("Révision")
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Fiche de révision")
                .font(.system(.largeTitle, design: .serif).weight(.semibold))
                .tracking(-0.7)
                .foregroundStyle(NomiTheme.ink)
            Text("Source, explication et hypothèse explicitement distinguées")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
        }
    }

    private var generationPanel: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(spacing: 9) {
                Image(systemName: "doc.text")
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(NomiTheme.accent)
                    .frame(width: 24, height: 24)
                    .background(NomiTheme.accentSoft, in: RoundedRectangle(cornerRadius: 7, style: .continuous))
                VStack(alignment: .leading, spacing: 2) {
                    Text(sourceTitle)
                        .font(.subheadline.weight(.medium))
                        .foregroundStyle(NomiTheme.ink)
                        .lineLimit(1)
                    Text("\(sourceText.count) caractères")
                        .font(.caption2.monospaced())
                        .foregroundStyle(NomiTheme.ink3)
                }
                Spacer()
                Button(showingSource ? "Masquer" : "Source") { showingSource.toggle() }
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(NomiTheme.ink2)
                    .frame(minWidth: 44, minHeight: 32)
            }

            Divider().overlay(NomiTheme.line.opacity(0.65))

            VStack(alignment: .leading, spacing: 8) {
                Text("NIVEAU")
                    .font(.caption2.weight(.bold))
                    .tracking(1.0)
                    .foregroundStyle(NomiTheme.ink3)
                Picker("Niveau", selection: $state.preferredDetailLevel) {
                    ForEach(RevisionDetailLevel.allCases, id: \.self) { level in
                        Text(level.frenchTitle).tag(level)
                    }
                }
                .pickerStyle(.segmented)
            }

            HStack(spacing: 10) {
                VStack(alignment: .leading, spacing: 4) {
                    Text("FORMAT")
                        .font(.caption2.weight(.bold))
                        .tracking(1.0)
                        .foregroundStyle(NomiTheme.ink3)
                    Picker("Format", selection: $state.preferredFormat) {
                        ForEach(RevisionFormat.allCases, id: \.self) { format in
                            Text(format.frenchTitle).tag(format)
                        }
                    }
                    .pickerStyle(.menu)
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
                Spacer()
                Button {
                    Task { await generate() }
                } label: {
                    Label(isGenerating ? "Génération…" : "Générer", systemImage: "sparkles")
                }
                .buttonStyle(.nomiPrimary)
                .disabled(isGenerating)
                .accessibilityHint("Génère la fiche localement quand Gemma est installé")
            }

            if let errorMessage {
                Label(errorMessage, systemImage: "exclamationmark.triangle")
                    .font(.caption)
                    .foregroundStyle(.orange)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
        .padding(12)
        .background(NomiTheme.surface.opacity(0.94), in: RoundedRectangle(cornerRadius: 15, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 15, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.8))
    }

    private var sourcePanel: some View {
        VStack(alignment: .leading, spacing: 7) {
            Text("SOURCE IMPORTÉE")
                .font(.caption2.weight(.bold))
                .tracking(1.0)
                .foregroundStyle(NomiTheme.ink3)
            Text(sourceText)
                .font(.footnote)
                .foregroundStyle(NomiTheme.ink2)
                .textSelection(.enabled)
                .fixedSize(horizontal: false, vertical: true)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(12)
        .background(NomiTheme.surface.opacity(0.72), in: RoundedRectangle(cornerRadius: 13, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 13, style: .continuous).stroke(NomiTheme.line.opacity(0.68), lineWidth: 0.7))
    }

    private var emptyState: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("Aucune fiche générée")
                .font(.subheadline.weight(.medium))
                .foregroundStyle(NomiTheme.ink)
            Text("Choisissez le niveau et le format, puis lancez la génération. Sans Gemma installé, Nomi utilise un repli extractif hors ligne.")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink3)
                .fixedSize(horizontal: false, vertical: true)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(12)
        .background(NomiTheme.surface.opacity(0.64), in: RoundedRectangle(cornerRadius: 13, style: .continuous))
    }

    @ViewBuilder
    private func sheetContent(_ sheet: RevisionSheet) -> some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(alignment: .top, spacing: 10) {
                VStack(alignment: .leading, spacing: 4) {
                    Text(sheet.title)
                        .font(.system(.title2, design: .serif).weight(.semibold))
                        .tracking(-0.4)
                        .foregroundStyle(NomiTheme.ink)
                    Text("\(sheet.level.frenchTitle) · \(sheet.format.frenchTitle)")
                        .font(.caption)
                        .foregroundStyle(NomiTheme.ink2)
                }
                Spacer()
                ShareLink(item: exportText(sheet)) {
                    Image(systemName: "square.and.arrow.up")
                        .font(.system(size: 14, weight: .medium))
                        .foregroundStyle(NomiTheme.ink2)
                        .frame(width: 44, height: 44)
                        .background(NomiTheme.surface2.opacity(0.52), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
                }
                .buttonStyle(.plain)
                .accessibilityLabel("Partager la fiche")
            }

            if sheet.format == .flashcards {
                flashcards(sheet)
            } else if sheet.format == .quiz {
                quiz(sheet)
            } else {
                sectionsList(sheet)
            }

            if !sheet.flashcards.isEmpty, sheet.format != .flashcards {
                supplementaryTitle("FLASHCARDS", count: sheet.flashcards.count)
                flashcards(sheet)
            }
            if !sheet.quiz.isEmpty, sheet.format != .quiz {
                supplementaryTitle("QUIZ", count: sheet.quiz.count)
                quiz(sheet)
            }

            Button(savedSheetIDs.contains(sheet.id) ? "Enregistrée" : "Enregistrer dans la bibliothèque") {
                state.addSheet(sheet)
                savedSheetIDs.insert(sheet.id)
            }
            .buttonStyle(.nomiPrimary)
            .disabled(savedSheetIDs.contains(sheet.id))
        }
        .padding(13)
        .background(NomiTheme.surface.opacity(0.94), in: RoundedRectangle(cornerRadius: 15, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 15, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.8))
    }

    private func supplementaryTitle(_ title: String, count: Int) -> some View {
        HStack(alignment: .firstTextBaseline) {
            Text(title)
                .font(.caption2.weight(.bold))
                .tracking(1.0)
                .foregroundStyle(NomiTheme.ink3)
            Spacer()
            Text("\(count)")
                .font(.caption2.monospaced())
                .foregroundStyle(NomiTheme.ink3)
        }
        .padding(.top, 4)
    }

    private func sectionsList(_ sheet: RevisionSheet) -> some View {
        VStack(spacing: 0) {
            ForEach(Array(sheet.sections.enumerated()), id: \.element.id) { index, section in
                if index > 0 { Divider().overlay(NomiTheme.line.opacity(0.6)) }
                VStack(alignment: .leading, spacing: 6) {
                    HStack(alignment: .firstTextBaseline) {
                        Text(section.title)
                            .font(.subheadline.weight(.semibold))
                            .foregroundStyle(NomiTheme.ink)
                        Spacer()
                        fidelityBadge(section.fidelity)
                    }
                    Text(section.body)
                        .font(.subheadline)
                        .foregroundStyle(NomiTheme.ink2)
                        .fixedSize(horizontal: false, vertical: true)
                        .textSelection(.enabled)
                }
                .padding(.vertical, 9)
                .padding(.horizontal, 2)
                .background(section.highlighted ? NomiTheme.accentSoft.opacity(0.34) : .clear)
            }
        }
        .background(NomiTheme.surface2.opacity(0.28), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
    }

    @ViewBuilder
    private func flashcards(_ sheet: RevisionSheet) -> some View {
        VStack(spacing: 7) {
            ForEach(sheet.flashcards) { card in
                Button {
                    if flippedCards.contains(card.id) { flippedCards.remove(card.id) } else { flippedCards.insert(card.id) }
                } label: {
                    VStack(alignment: .leading, spacing: 5) {
                        Text(flippedCards.contains(card.id) ? "RÉPONSE" : "QUESTION")
                            .font(.caption2.weight(.bold))
                            .tracking(0.8)
                            .foregroundStyle(flippedCards.contains(card.id) ? NomiTheme.success : NomiTheme.accent)
                        Text(flippedCards.contains(card.id) ? card.back : card.front)
                            .font(.subheadline)
                            .foregroundStyle(NomiTheme.ink)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                    .padding(10)
                    .background(NomiTheme.surface2.opacity(0.52), in: RoundedRectangle(cornerRadius: 11, style: .continuous))
                    .overlay(RoundedRectangle(cornerRadius: 11, style: .continuous).stroke(NomiTheme.line.opacity(0.58), lineWidth: 0.6))
                }
                .buttonStyle(.plain)
                .accessibilityHint("Retourne la flashcard")
            }
        }
    }

    @ViewBuilder
    private func quiz(_ sheet: RevisionSheet) -> some View {
        VStack(spacing: 8) {
            ForEach(sheet.quiz) { question in
                VStack(alignment: .leading, spacing: 8) {
                    Text(question.question)
                        .font(.subheadline.weight(.semibold))
                        .foregroundStyle(NomiTheme.ink)
                        .fixedSize(horizontal: false, vertical: true)
                    ForEach(Array(question.choices.enumerated()), id: \.offset) { index, choice in
                        Button {
                            if !submittedQuizIDs.contains(question.id) {
                                quizAnswers[question.id] = index
                            }
                        } label: {
                            HStack(alignment: .top, spacing: 8) {
                                Image(systemName: quizAnswers[question.id] == index ? "checkmark.circle.fill" : "circle")
                                    .font(.system(size: 13, weight: .medium))
                                    .foregroundStyle(quizAnswers[question.id] == index ? NomiTheme.accent : NomiTheme.ink3)
                                Text(choice)
                                    .font(.subheadline)
                                    .foregroundStyle(NomiTheme.ink)
                                    .multilineTextAlignment(.leading)
                                Spacer()
                                if submittedQuizIDs.contains(question.id), index == question.correctIndex {
                                    Image(systemName: "checkmark")
                                        .foregroundStyle(NomiTheme.success)
                                }
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .frame(minHeight: 32)
                            .contentShape(Rectangle())
                        }
                        .buttonStyle(.plain)
                    }
                    if submittedQuizIDs.contains(question.id) {
                        Text(quizAnswers[question.id] == question.correctIndex ? "Bonne réponse." : "Réponse à revoir.")
                            .font(.caption.weight(.semibold))
                            .foregroundStyle(quizAnswers[question.id] == question.correctIndex ? NomiTheme.success : .orange)
                        Text(question.explanation)
                            .font(.caption)
                            .foregroundStyle(NomiTheme.ink2)
                            .fixedSize(horizontal: false, vertical: true)
                    } else {
                        Button("Valider") {
                            if quizAnswers[question.id] != nil { submittedQuizIDs.insert(question.id) }
                        }
                        .buttonStyle(.nomiSecondary)
                        .disabled(quizAnswers[question.id] == nil)
                    }
                }
                .padding(10)
                .background(NomiTheme.surface2.opacity(0.52), in: RoundedRectangle(cornerRadius: 11, style: .continuous))
                .overlay(RoundedRectangle(cornerRadius: 11, style: .continuous).stroke(NomiTheme.line.opacity(0.58), lineWidth: 0.6))
            }
        }
    }

    private func fidelityBadge(_ fidelity: FidelityKind) -> some View {
        let label: String
        let tint: Color
        switch fidelity {
        case .source:
            label = "SOURCE"
            tint = NomiTheme.success
        case .generatedExplanation:
            label = "IA"
            tint = NomiTheme.accent
        case .hypothesis:
            label = "HYPOTHÈSE"
            tint = NomiTheme.warning
        }
        return Text(label)
            .font(.system(size: 8, weight: .bold, design: .monospaced))
            .tracking(0.6)
            .foregroundStyle(tint)
    }

    private func generate() async {
        isGenerating = true
        errorMessage = nil
        do {
            sheet = try await state.makeRevision(
                from: sourceText,
                sourceTitle: sourceTitle,
                level: state.preferredDetailLevel,
                format: state.preferredFormat
            )
        } catch {
            errorMessage = "La génération locale a échoué. Le mode extractif hors-ligne devrait normalement prendre le relais."
        }
        isGenerating = false
    }

    private func exportText(_ sheet: RevisionSheet) -> String {
        var output = "# \(sheet.title)\n\nSource : \(sheet.sourceTitle)\n\n"
        for section in sheet.sections {
            output += "## \(section.title) [\(section.fidelity.frenchTitle)]\n\(section.body)\n\n"
        }
        if sheet.format == .flashcards || !sheet.flashcards.isEmpty {
            output += "## Flashcards\n"
            for card in sheet.flashcards {
                output += "- Q : \(card.front)\n  R : \(card.back)\n"
            }
            output += "\n"
        }
        if sheet.format == .quiz || !sheet.quiz.isEmpty {
            output += "## Quiz\n"
            for question in sheet.quiz {
                output += "- \(question.question)\n"
                for (index, choice) in question.choices.enumerated() {
                    output += "  \(index + 1). \(choice)\n"
                }
                output += "  Réponse : \(question.correctIndex + 1). \(question.explanation)\n"
            }
        }
        return output
    }
}
