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
                .font(.system(size: 32, weight: .semibold, design: .serif))
                .tracking(-0.7)
                .foregroundStyle(NomiTheme.ink)
            Text("Sources transformées en supports révisables")
                .font(.caption)
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
                    .font(.subheadline.weight(.medium))
                    .foregroundStyle(NomiTheme.ink)
                Text("Collez une source ; Gemma local si installé")
                    .font(.caption2)
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
                    .font(.caption2.weight(.bold))
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
                        .font(.caption)
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
                    .font(.subheadline.weight(.medium))
                    .foregroundStyle(NomiTheme.ink)
                    .lineLimit(1)
                Text("\(sheet.format.frenchTitle) · \(sheet.level.frenchTitle) · \(sheet.createdAt.formatted(date: .abbreviated, time: .omitted))")
                    .font(.caption2)
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
                .font(.caption2.weight(.bold))
                .tracking(1.1)
                .foregroundStyle(NomiTheme.ink3)
            Label(state.gemmaManager.state(for: state.gemmaManager.selectedModel) == .downloaded ? "Gemma 4 local actif pour les nouvelles fiches" : "Repli extractif hors ligne tant que Gemma n’est pas installé", systemImage: state.gemmaManager.state(for: state.gemmaManager.selectedModel) == .downloaded ? "checkmark.circle" : "info.circle")
                .font(.caption)
                .foregroundStyle(state.gemmaManager.state(for: state.gemmaManager.selectedModel) == .downloaded ? NomiTheme.success : NomiTheme.ink2)
            Label("Flashcards et quiz consultables hors ligne", systemImage: "checkmark.circle")
                .font(.caption)
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
                        .font(.caption)
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
            VStack(alignment: .leading, spacing: 18) {
                NomiScreenTitle(sheet.title, subtitle: "\(sheet.level.frenchTitle) · \(sheet.format.frenchTitle) · \(sheet.createdAt.formatted(date: .abbreviated, time: .shortened))")

                ForEach(sheet.sections) { section in
                    VStack(alignment: .leading, spacing: 10) {
                        HStack(alignment: .top) {
                            Text(section.title)
                                .font(.system(.headline, design: .rounded).weight(.bold))
                                .foregroundStyle(NomiTheme.ink)
                            Spacer()
                            NomiBadge(section.fidelity.frenchTitle, tint: section.fidelity == .source ? NomiTheme.success : NomiTheme.focus)
                        }
                        Text(section.body)
                            .font(.subheadline)
                            .foregroundStyle(NomiTheme.ink2)
                            .fixedSize(horizontal: false, vertical: true)
                            .textSelection(.enabled)
                    }
                    .nomiCard(tint: section.fidelity == .source ? NomiTheme.success : NomiTheme.focus)
                }

                if !sheet.flashcards.isEmpty {
                    VStack(alignment: .leading, spacing: 13) {
                        Label("Flashcards", systemImage: "rectangle.on.rectangle.fill")
                            .font(.system(.headline, design: .rounded).weight(.bold))
                            .foregroundStyle(NomiTheme.focus)
                        ForEach(sheet.flashcards) { card in
                            VStack(alignment: .leading, spacing: 7) {
                                Text(card.front)
                                    .font(.body.weight(.semibold))
                                    .foregroundStyle(NomiTheme.ink)
                                Text(card.back)
                                    .font(.subheadline)
                                    .foregroundStyle(NomiTheme.ink2)
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(13)
                            .background(NomiTheme.focus.opacity(0.09), in: RoundedRectangle(cornerRadius: 16, style: .continuous))
                        }
                    }
                    .nomiCard(tint: NomiTheme.focus)
                }

                if !sheet.quiz.isEmpty {
                    VStack(alignment: .leading, spacing: 14) {
                        Label("Quiz", systemImage: "checkmark.circle.fill")
                            .font(.system(.headline, design: .rounded).weight(.bold))
                            .foregroundStyle(NomiTheme.course)
                        ForEach(sheet.quiz) { question in
                            VStack(alignment: .leading, spacing: 8) {
                                Text(question.question)
                                    .font(.body.weight(.semibold))
                                    .foregroundStyle(NomiTheme.ink)
                                ForEach(Array(question.choices.enumerated()), id: \.offset) { index, choice in
                                    HStack(alignment: .top, spacing: 8) {
                                        Image(systemName: index == question.correctIndex ? "checkmark.circle.fill" : "circle")
                                            .foregroundStyle(index == question.correctIndex ? NomiTheme.success : NomiTheme.ink3)
                                        Text(choice)
                                            .font(.subheadline)
                                            .foregroundStyle(index == question.correctIndex ? NomiTheme.ink : NomiTheme.ink2)
                                    }
                                }
                                Text(question.explanation)
                                    .font(.caption)
                                    .foregroundStyle(NomiTheme.ink2)
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(13)
                            .background(NomiTheme.course.opacity(0.08), in: RoundedRectangle(cornerRadius: 16, style: .continuous))
                        }
                    }
                    .nomiCard(tint: NomiTheme.course)
                }
            }
            .padding(.horizontal, 20)
            .padding(.top, 18)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
        .nomiChildNavigation()
        .navigationTitle("")
        .navigationBarTitleDisplayMode(.inline)
    }
}
