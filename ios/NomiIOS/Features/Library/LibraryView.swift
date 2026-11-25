import SwiftUI
import NomiCore

struct LibraryView: View {
    @EnvironmentObject private var state: AppState

    var body: some View {
        List {
            Section {
                if state.revisionSheets.isEmpty {
                    ContentUnavailableView("Bibliothèque vide", systemImage: "books.vertical", description: Text("Les fiches générées depuis une transcription ou un document apparaîtront ici."))
                        .frame(maxWidth: .infinity)
                } else {
                    ForEach(state.revisionSheets) { sheet in
                        NavigationLink {
                            StoredSheetDetail(sheet: sheet)
                        } label: {
                            VStack(alignment: .leading, spacing: 5) {
                                Text(sheet.title)
                                    .font(.headline)
                                Text("\(sheet.format.frenchTitle) · \(sheet.level.frenchTitle) · \(sheet.createdAt.formatted(date: .abbreviated, time: .shortened))")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                            }
                            .padding(.vertical, 4)
                        }
                        .swipeActions(edge: .trailing) {
                            Button(role: .destructive) {
                                state.revisionSheets.removeAll { $0.id == sheet.id }
                            } label: {
                                Label("Supprimer", systemImage: "trash")
                            }
                        }
                    }
                }
            } header: {
                Text("Cours et fiches")
            } footer: {
                Text("Le prototype conserve les fiches en mémoire. La version production doit utiliser SwiftData ou fichiers protégés par Data Protection, avec synchronisation iCloud opt-in.")
            }

            Section("Imports prévus") {
                Label("Fichiers, PDF, scans et partage iOS", systemImage: "folder")
                Label("Recherche Spotlight et rappels de révision", systemImage: "magnifyingglass")
                Label("Synchronisation iCloud chiffrée, désactivée par défaut", systemImage: "icloud")
            }
            .foregroundStyle(.secondary)
        }
        .scrollContentBackground(.hidden)
        .nomiScreenBackground()
        .navigationTitle("Bibliothèque")
    }
}

private struct StoredSheetDetail: View {
    var sheet: RevisionSheet

    var body: some View {
        List {
            ForEach(sheet.sections) { section in
                Section(section.title) {
                    Text(section.body)
                        .textSelection(.enabled)
                    Text("Fidélité : \(section.fidelity.frenchTitle)")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
            if !sheet.flashcards.isEmpty {
                Section("Flashcards") {
                    ForEach(sheet.flashcards) { card in
                        VStack(alignment: .leading, spacing: 5) {
                            Text(card.front).font(.headline)
                            Text(card.back).font(.subheadline).foregroundStyle(.secondary)
                        }
                        .padding(.vertical, 3)
                    }
                }
            }
            if !sheet.quiz.isEmpty {
                Section("Quiz") {
                    ForEach(sheet.quiz) { question in
                        VStack(alignment: .leading, spacing: 5) {
                            Text(question.question).font(.headline)
                            ForEach(Array(question.choices.enumerated()), id: \.offset) { index, choice in
                                Text("\(index + 1). \(choice)")
                                    .font(.subheadline)
                            }
                            Text("Réponse \(question.correctIndex + 1) — \(question.explanation)")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                        }
                        .padding(.vertical, 3)
                    }
                }
            }
        }
        .navigationTitle(sheet.title)
        .navigationBarTitleDisplayMode(.inline)
    }
}
