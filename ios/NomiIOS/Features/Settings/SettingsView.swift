import SwiftUI

struct SettingsView: View {
    @EnvironmentObject private var state: AppState
    @AppStorage("nomi.reduceMotionPreference") private var reduceMotionPreference = false
    @AppStorage("nomi.highContrastPreference") private var highContrastPreference = false

    var body: some View {
        Form {
            Section("IA et confidentialité") {
                Toggle("Autoriser l'IA distante", isOn: $state.allowRemoteAI)
                Text("Désactivé par défaut. Aucune clé API n'est embarquée. Une version production doit passer par un serveur intermédiaire, demander consentement et indiquer quelles données quittent l'appareil.")
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

            Section("État réel du prototype") {
                Label("Tâches, Focus, calculs et persistance locale : réels", systemImage: "checkmark.circle")
                Label("Reconnaissance vocale Apple : semi-réelle sur appareil", systemImage: "checkmark.circle")
                Label("Réponses IA et génération de fiches : simulées", systemImage: "circle.dashed")
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
