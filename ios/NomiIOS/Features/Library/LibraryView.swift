import SwiftUI
import NomiCore

struct LibraryView: View {
    @EnvironmentObject private var state: AppState
    @State private var showingNewSheet = false

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                HStack(alignment: .top) {
                    NomiScreenTitle("Fiches et révisions", subtitle: "Flashcards, quiz et exports générés localement depuis vos cours.")
                    Spacer()
                    Button("Nouvelle fiche", systemImage: "plus") { showingNewSheet = true }
                        .buttonStyle(.nomiSecondary)
                }
                if state.revisionSheets.isEmpty {
                    VStack(alignment: .leading, spacing: 12) {
                        Image(systemName: "books.vertical.fill")
                            .font(.title2)
                            .foregroundStyle(NomiTheme.focus)
                        Text("Bibliothèque vide")
                            .font(.system(.title3, design: .rounded).weight(.bold))
                        Text("Créez une fiche depuis une transcription ou un texte collé. Gemma 4 la génère localement lorsqu’il est installé ; sinon un mode extractif hors-ligne prend le relais.")
                            .foregroundStyle(NomiTheme.ink2)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .nomiCard(tint: NomiTheme.focus)
                } else {
                    if state.revisionSheets.count == 1, let sheet = state.revisionSheets.first {
                        NavigationLink(value: NomiDetailDestination.revisionSheet(sheet.id)) {
                            sheetCard(sheet)
                        }
                        .buttonStyle(.plain)
                        .frame(maxWidth: 560, alignment: .leading)
                        .contextMenu {
                            Button("Supprimer", role: .destructive) {
                                state.revisionSheets.removeAll { $0.id == sheet.id }
                            }
                        }
                    } else {
                        LazyVGrid(columns: [GridItem(.adaptive(minimum: 300, maximum: 520), spacing: 14)], spacing: 14) {
                            ForEach(state.revisionSheets) { sheet in
                                NavigationLink(value: NomiDetailDestination.revisionSheet(sheet.id)) {
                                    sheetCard(sheet)
                                }
                                .buttonStyle(.plain)
                                .contextMenu {
                                    Button("Supprimer", role: .destructive) {
                                        state.revisionSheets.removeAll { $0.id == sheet.id }
                                    }
                                }
                            }
                        }
                    }
                }
                VStack(alignment: .leading, spacing: 12) {
                    Label("Imports prévus", systemImage: "folder.fill")
                        .font(.headline)
                        .foregroundStyle(NomiTheme.course)
                    plannedRow("Fichiers, PDF, scans et partage iOS", icon: "doc.fill")
                    plannedRow("Recherche Spotlight et rappels de révision", icon: "magnifyingglass")
                    plannedRow("Synchronisation iCloud chiffrée, désactivée par défaut", icon: "icloud.fill")
                    Text("La version production devra utiliser SwiftData ou fichiers protégés par Data Protection, avec synchronisation opt-in.")
                        .font(.caption)
                        .foregroundStyle(NomiTheme.ink2)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .nomiCard(tint: NomiTheme.course)
            }
            .padding(.horizontal, 20)
            .padding(.top, 18)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
        .nomiChildNavigation()
        .sheet(isPresented: $showingNewSheet) {
            NewRevisionSourceView()
                .environmentObject(state)
        }
    }

    private func sheetCard(_ sheet: RevisionSheet) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(alignment: .top) {
                Image(systemName: sheet.format == .flashcards ? "rectangle.on.rectangle.fill" : "doc.text.fill")
                    .font(.title3)
                    .foregroundStyle(NomiTheme.focus)
                    .frame(width: 42, height: 42)
                    .background(NomiTheme.focus.opacity(0.12), in: RoundedRectangle(cornerRadius: 15, style: .continuous))
                Spacer()
                Image(systemName: "arrow.right")
                    .font(.caption.weight(.bold))
                    .foregroundStyle(NomiTheme.focus)
            }
            Text(sheet.title)
                .font(.system(.headline, design: .rounded).weight(.bold))
                .foregroundStyle(NomiTheme.ink)
                .fixedSize(horizontal: false, vertical: true)
            HStack(spacing: 7) {
                NomiBadge(sheet.format.frenchTitle, tint: NomiTheme.focus)
                NomiBadge(sheet.level.frenchTitle, tint: NomiTheme.course)
            }
            Text(sheet.createdAt.formatted(date: .abbreviated, time: .shortened))
                .font(.caption)
                .foregroundStyle(NomiTheme.ink3)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .nomiCard(tint: NomiTheme.focus)
    }

    private func plannedRow(_ title: String, icon: String) -> some View {
        Label(title, systemImage: icon)
            .font(.subheadline)
            .foregroundStyle(NomiTheme.ink2)
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
