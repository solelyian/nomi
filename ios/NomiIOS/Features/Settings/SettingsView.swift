import SwiftUI

struct SettingsView: View {
    @EnvironmentObject private var state: AppState
    @AppStorage("nomi.reduceMotionPreference") private var reduceMotionPreference = false
    @AppStorage("nomi.highContrastPreference") private var highContrastPreference = false

    var body: some View {
        Form {
            Section {
                NomiScreenTitle("Réglages", subtitle: "Modèle local, confidentialité, accessibilité et intégrations.")
                    .padding(.vertical, 4)
                    .listRowInsets(EdgeInsets(top: 0, leading: 0, bottom: 0, trailing: 0))
                    .listRowBackground(Color.clear)
            }

            Section("Gemma 4 local") {
                NavigationLink(value: NomiDetailDestination.gemma) {
                    HStack {
                        Label("Modèle IA sur appareil", systemImage: "cpu")
                        Spacer()
                        Text(state.gemmaManager.state(for: state.gemmaManager.selectedModel).label)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
                Text("Gemma 4 E2B est téléchargé à la demande (2,6 Go). Après installation, Nomi génère les réponses localement avec LiteRT-LM.")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
            }

            Section("IA et confidentialité") {
                Toggle("Autoriser l'IA distante", isOn: $state.allowRemoteAI)
                Text("Désactivé par défaut et inutile pour Gemma 4 local. Aucune clé API n'est embarquée. Toute future IA distante devra passer par un serveur intermédiaire, demander consentement et indiquer quelles données quittent l'appareil.")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
                Picker("Langue de transcription", selection: $state.transcriptionLanguage) {
                    Text("Français").tag("fr-FR")
                    Text("English").tag("en-US")
                }
            }

            Section("Lecture et accessibilité") {
                Toggle("Réduire les animations", isOn: $reduceMotionPreference)
                Toggle("Augmenter le contraste", isOn: $highContrastPreference)
                Label("Dynamic Type, VoiceOver et zones tactiles ≥ 44 pt sont prévus dans le design system.", systemImage: "accessibility")
            }

            Section("Intégrations iOS classées") {
                integrationRow("1", "Notifications locales", "routines et rappels de focus")
                integrationRow("2", "Document Picker / Fichiers", "documents liés aux routines et pièces jointes")
                integrationRow("3", "App Intents / Raccourcis", "ajouter une tâche, démarrer un focus ou une transcription")
                integrationRow("4", "Widgets", "révision du jour et tâches courtes")
                integrationRow("5", "Spotlight", "fiches et cours indexés localement")
            }

            Section("État fonctionnel") {
                Label("Tâches, Focus, calculs et persistance locale : réels", systemImage: "checkmark.circle")
                Label("Reconnaissance vocale Apple : semi-réelle sur appareil", systemImage: "checkmark.circle")
                Label("Réponses IA conversationnelles : réelles avec Gemma 4 après installation", systemImage: "checkmark.circle")
                Label("Génération de fiches : Gemma 4 local, avec repli extractif hors-ligne", systemImage: "circle.dashed")
                Label("Notifications, iCloud, Documents et extension de partage : non implémentés", systemImage: "xmark.circle")
            }
        }
        .scrollContentBackground(.hidden)
        .nomiScreenBackground()
        .nomiChildNavigation()
        .navigationTitle("Réglages")
    }

    private func integrationRow(_ rank: String, _ title: String, _ detail: String) -> some View {
        HStack(alignment: .top, spacing: 10) {
            Text(rank)
                .font(.caption.weight(.bold))
                .frame(width: 24, height: 24)
                .background(NomiTheme.accentSoft, in: Circle())
            VStack(alignment: .leading) {
                Text(title)
                Text(detail)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
    }
}
