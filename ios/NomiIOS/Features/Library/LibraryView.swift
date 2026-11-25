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
        }
        .navigationTitle(sheet.title)
        .navigationBarTitleDisplayMode(.inline)
    }
}
