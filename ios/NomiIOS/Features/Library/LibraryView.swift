import SwiftUI
import NomiCore

struct LibraryView: View {
    @EnvironmentObject private var state: AppState
    @State private var selectedSheet: RevisionSheet?
    @State private var creatingSheet = false

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                header
                creationPanel
                sheetsList
                honestyPanel
            }
            .padding(.horizontal, 18)
            .padding(.top, 14)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
        .nomiChildNavigation()
        .navigationTitle("")
        .sheet(isPresented: $creatingSheet) { NewRevisionSourceView() }
        .sheet(item: $selectedSheet) { sheet in
            NavigationStack { StoredSheetDetail(sheet: sheet) }
        }
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Fiches et révisions")
                .font(.system(.largeTitle, design: .serif).weight(.semibold))
                .tracking(-0.7)
                .foregroundStyle(NomiTheme.ink)
            Text("Sources transformées en supports révisables")
                .font(.nomi(.caption))
                .foregroundStyle(NomiTheme.ink2)
        }
    }

    private var creationPanel: some View {
        HStack(spacing: 9) {
            Image(systemName: "doc.text.magnifyingglass")
                .font(.system(size: 13, weight: .medium))
                .foregroundStyle(NomiTheme.accent)
                .frame(width: 25, height: 25)
                .background(NomiTheme.accentSoft, in: RoundedRectangle(cornerRadius: 8, style: .continuous))
            VStack(alignment: .leading, spacing: 2) {
                Text("Créer une fiche")
                    .font(.nomi(.subheadline, weight: .medium))
                    .foregroundStyle(NomiTheme.ink)
                Text("Collez une source ; Gemma local si installé")
                    .font(.nomi(.caption2))
                    .foregroundStyle(NomiTheme.ink3)
                    .lineLimit(1)
            }
            Spacer()
            Button("Nouvelle") { creatingSheet = true }
                .buttonStyle(.nomiPrimary)
        }
        .padding(.horizontal, 10)
        .frame(minHeight: 54)
        .background(NomiTheme.surface.opacity(0.94), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.8))
    }

    private var sheetsList: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .firstTextBaseline) {
                Text("SUPPORTS")
                    .font(.nomi(.caption2, weight: .bold))
                    .tracking(1.1)
                    .foregroundStyle(NomiTheme.ink3)
                Spacer()
                Text("\(state.revisionSheets.count)")
                    .font(.caption2.monospaced())
                    .foregroundStyle(NomiTheme.ink3)
            }
            .padding(.horizontal, 1)
            VStack(spacing: 0) {
                ForEach(Array(state.revisionSheets.enumerated()), id: \.element.id) { index, sheet in
                    if index > 0 { Divider().overlay(NomiTheme.line.opacity(0.6)).padding(.leading, 42) }
                    Button { selectedSheet = sheet } label: { sheetRow(sheet) }
                    .buttonStyle(.plain)
                    .contextMenu {
                        Button("Supprimer", systemImage: "trash", role: .destructive) {
                            state.revisionSheets.removeAll { $0.id == sheet.id }
                        }
                    }
                    .accessibilityAction(named: "Supprimer") {
                        state.revisionSheets.removeAll { $0.id == sheet.id }
                    }
                }
                if state.revisionSheets.isEmpty {
                    Text("Aucune fiche. La première peut être créée depuis une transcription ou un texte source.")
                        .font(.nomi(.caption))
                        .foregroundStyle(NomiTheme.ink3)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(12)
                }
            }
            .background(NomiTheme.surface.opacity(0.93), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.7))
        }
    }

    private func sheetRow(_ sheet: RevisionSheet) -> some View {
        HStack(spacing: 9) {
            Image(systemName: "rectangle.on.rectangle")
                .font(.system(size: 12, weight: .medium))
                .foregroundStyle(NomiTheme.accent)
                .frame(width: 24, height: 24)
                .background(NomiTheme.accentSoft, in: RoundedRectangle(cornerRadius: 7, style: .continuous))
            VStack(alignment: .leading, spacing: 2) {
                Text(sheet.title)
                    .font(.nomi(.subheadline, weight: .medium))
                    .foregroundStyle(NomiTheme.ink)
                    .lineLimit(1)
                Text("\(sheet.format.frenchTitle) · \(sheet.level.frenchTitle) · \(sheet.createdAt.formatted(date: .abbreviated, time: .omitted))")
                    .font(.nomi(.caption2))
                    .foregroundStyle(NomiTheme.ink3)
                    .lineLimit(1)
            }
            Spacer(minLength: 8)
            Image(systemName: "chevron.right")
                .font(.system(size: 10, weight: .semibold))
                .foregroundStyle(NomiTheme.ink3)
        }
        .padding(.horizontal, 10)
        .frame(minHeight: 48)
    }

    private var honestyPanel: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("ÉTAT DE GÉNÉRATION")
                .font(.nomi(.caption2, weight: .bold))
                .tracking(1.1)
                .foregroundStyle(NomiTheme.ink3)
            Label(state.gemmaManager.state(for: state.gemmaManager.selectedModel) == .downloaded ? "Gemma 4 local actif pour les nouvelles fiches" : "Repli extractif hors ligne tant que Gemma n’est pas installé", systemImage: state.gemmaManager.state(for: state.gemmaManager.selectedModel) == .downloaded ? "checkmark.circle" : "info.circle")
                .font(.nomi(.caption))
                .foregroundStyle(state.gemmaManager.state(for: state.gemmaManager.selectedModel) == .downloaded ? NomiTheme.success : NomiTheme.ink2)
            Label("Flashcards et quiz consultables hors ligne", systemImage: "checkmark.circle")
                .font(.nomi(.caption))
                .foregroundStyle(NomiTheme.ink2)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(12)
        .background(NomiTheme.surface.opacity(0.72), in: RoundedRectangle(cornerRadius: 13, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 13, style: .continuous).stroke(NomiTheme.line.opacity(0.68), lineWidth: 0.7))
    }
}

private struct NewRevisionSourceView: View {
    @Environment(\.dismiss) private var dismiss
    @State private var sourceTitle = ""
    @State private var sourceText = ""

    private var canContinue: Bool {
        !sourceTitle.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty &&
        sourceText.trimmingCharacters(in: .whitespacesAndNewlines).count >= 80
    }

    var body: some View {
        NavigationStack {
            Form {
                Section("Source") {
                    TextField("Titre du cours ou document", text: $sourceTitle)
                    TextEditor(text: $sourceText)
                        .frame(minHeight: 220)
                        .accessibilityLabel("Texte source")
                }
                Section {
                    NavigationLink {
                        RevisionSheetView(
                            sourceText: sourceText.trimmingCharacters(in: .whitespacesAndNewlines),
                            sourceTitle: sourceTitle.trimmingCharacters(in: .whitespacesAndNewlines)
                        )
                    } label: {
                        Label("Préparer la fiche", systemImage: "sparkles")
                    }
                    .disabled(!canContinue)
                    Text("Collez au moins 80 caractères. La génération utilise Gemma 4 localement quand le modèle est installé.")
                        .font(.nomi(.caption))
                        .foregroundStyle(.secondary)
                }
            }
            .navigationTitle("Nouvelle fiche")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Fermer") { dismiss() }
                }
            }
        }
    }
}

struct StoredSheetDetail: View {
    var sheet: RevisionSheet

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                header
                sections
                if !sheet.flashcards.isEmpty { flashcards }
                if !sheet.quiz.isEmpty { quiz }
            }
            .padding(.horizontal, 18)
            .padding(.top, 14)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
        .nomiChildNavigation()
        .navigationTitle("")
        .navigationBarTitleDisplayMode(.inline)
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 5) {
            Text(sheet.title)
                .font(.system(.title, design: .serif).weight(.semibold))
                .tracking(-0.6)
                .foregroundStyle(NomiTheme.ink)
                .fixedSize(horizontal: false, vertical: true)
            Text("\(sheet.level.frenchTitle) · \(sheet.format.frenchTitle) · \(sheet.createdAt.formatted(date: .abbreviated, time: .omitted))")
                .font(.nomi(.caption))
                .foregroundStyle(NomiTheme.ink2)
        }
    }

    private var sections: some View {
        VStack(alignment: .leading, spacing: 6) {
            sectionTitle("SECTIONS", count: sheet.sections.count)
            VStack(spacing: 0) {
                ForEach(Array(sheet.sections.enumerated()), id: \.element.id) { index, section in
                    if index > 0 { Divider().overlay(NomiTheme.line.opacity(0.6)) }
                    VStack(alignment: .leading, spacing: 6) {
                        HStack(alignment: .firstTextBaseline) {
                            Text(section.title)
                                .font(.nomi(.subheadline, weight: .semibold))
                                .foregroundStyle(NomiTheme.ink)
                            Spacer()
                            fidelityLabel(section.fidelity)
                        }
                        Text(section.body)
                            .font(.nomi(.subheadline))
                            .foregroundStyle(NomiTheme.ink2)
                            .fixedSize(horizontal: false, vertical: true)
                            .textSelection(.enabled)
                    }
                    .padding(.horizontal, 10)
                    .padding(.vertical, 9)
                }
            }
            .background(NomiTheme.surface.opacity(0.93), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.7))
        }
    }

    private var flashcards: some View {
        VStack(alignment: .leading, spacing: 6) {
            sectionTitle("FLASHCARDS", count: sheet.flashcards.count)
            VStack(spacing: 0) {
                ForEach(Array(sheet.flashcards.enumerated()), id: \.element.id) { index, card in
                    if index > 0 { Divider().overlay(NomiTheme.line.opacity(0.6)).padding(.leading, 10) }
                    VStack(alignment: .leading, spacing: 5) {
                        Text(card.front)
                            .font(.nomi(.subheadline, weight: .medium))
                            .foregroundStyle(NomiTheme.ink)
                            .fixedSize(horizontal: false, vertical: true)
                        Text(card.back)
                            .font(.nomi(.caption))
                            .foregroundStyle(NomiTheme.ink2)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.horizontal, 10)
                    .padding(.vertical, 9)
                }
            }
            .background(NomiTheme.surface.opacity(0.93), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.7))
        }
    }

    private var quiz: some View {
        VStack(alignment: .leading, spacing: 6) {
            sectionTitle("QUIZ", count: sheet.quiz.count)
            VStack(spacing: 0) {
                ForEach(Array(sheet.quiz.enumerated()), id: \.element.id) { index, question in
                    if index > 0 { Divider().overlay(NomiTheme.line.opacity(0.6)).padding(.leading, 10) }
                    VStack(alignment: .leading, spacing: 7) {
                        Text(question.question)
                            .font(.nomi(.subheadline, weight: .semibold))
                            .foregroundStyle(NomiTheme.ink)
                            .fixedSize(horizontal: false, vertical: true)
                        ForEach(Array(question.choices.enumerated()), id: \.offset) { choiceIndex, choice in
                            HStack(alignment: .top, spacing: 7) {
                                Image(systemName: choiceIndex == question.correctIndex ? "checkmark.circle.fill" : "circle")
                                    .font(.system(size: 12, weight: .medium))
                                    .foregroundStyle(choiceIndex == question.correctIndex ? NomiTheme.success : NomiTheme.ink3)
                                Text(choice)
                                    .font(.nomi(.caption))
                                    .foregroundStyle(choiceIndex == question.correctIndex ? NomiTheme.ink : NomiTheme.ink2)
                                    .fixedSize(horizontal: false, vertical: true)
                            }
                        }
                        Text(question.explanation)
                            .font(.nomi(.caption))
                            .foregroundStyle(NomiTheme.ink2)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.horizontal, 10)
                    .padding(.vertical, 9)
                }
            }
            .background(NomiTheme.surface.opacity(0.93), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.7))
        }
    }

    private func sectionTitle(_ title: String, count: Int) -> some View {
        HStack(alignment: .firstTextBaseline) {
            Text(title)
                .font(.nomi(.caption2, weight: .bold))
                .tracking(1.1)
                .foregroundStyle(NomiTheme.ink3)
            Spacer()
            Text("\(count)")
                .font(.caption2.monospaced())
                .foregroundStyle(NomiTheme.ink3)
        }
        .padding(.horizontal, 1)
    }

    private func fidelityLabel(_ fidelity: FidelityKind) -> some View {
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
}
