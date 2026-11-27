import SwiftUI

struct SettingsView: View {
    @EnvironmentObject private var state: AppState

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                header
                gemmaRow
                privacyPanel
                accessibilityPanel
                integrationsPanel
                functionalStatePanel
            }
            .padding(.horizontal, 18)
            .padding(.top, 14)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
        .nomiChildNavigation()
        .navigationTitle("")
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Réglages")
                .font(.system(.largeTitle, design: .serif).weight(.semibold))
                .tracking(-0.7)
                .foregroundStyle(NomiTheme.ink)
            Text("Modèle local, confidentialité et intégrations")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
        }
    }

    private var gemmaRow: some View {
        NavigationLink(value: NomiDetailDestination.gemma) {
            HStack(spacing: 9) {
                Image(systemName: "cpu")
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(NomiTheme.accent)
                    .frame(width: 24, height: 24)
                    .background(NomiTheme.accentSoft, in: RoundedRectangle(cornerRadius: 7, style: .continuous))
                VStack(alignment: .leading, spacing: 2) {
                    Text("Gemma 4 local")
                        .font(.subheadline.weight(.medium))
                        .foregroundStyle(NomiTheme.ink)
                    Text(state.gemmaManager.state(for: state.gemmaManager.selectedModel).label)
                        .font(.caption2)
                        .foregroundStyle(NomiTheme.ink3)
                        .lineLimit(1)
                }
                Spacer()
                Image(systemName: "chevron.right")
                    .font(.system(size: 10, weight: .semibold))
                    .foregroundStyle(NomiTheme.ink3)
            }
            .padding(.horizontal, 10)
            .frame(minHeight: 50)
        }
        .buttonStyle(.plain)
        .background(NomiTheme.surface.opacity(0.94), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.8))
    }

    private var privacyPanel: some View {
        panel(title: "IA ET CONFIDENTIALITÉ") {
            Toggle("Autoriser l’IA distante", isOn: $state.allowRemoteAI)
                .font(.subheadline.weight(.medium))
                .tint(NomiTheme.accent)
            Text("Désactivé par défaut et inutile pour Gemma local. Aucune clé API n’est embarquée ; toute IA distante future devra demander un consentement explicite.")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
                .fixedSize(horizontal: false, vertical: true)
            Divider().overlay(NomiTheme.line.opacity(0.6))
            HStack {
                Text("Langue de transcription")
                    .font(.subheadline.weight(.medium))
                    .foregroundStyle(NomiTheme.ink)
                Spacer()
                Picker("Langue de transcription", selection: $state.transcriptionLanguage) {
                    Text("Français").tag("fr-FR")
                    Text("English").tag("en-US")
                }
                .pickerStyle(.menu)
                .labelsHidden()
            }
            .frame(minHeight: 36)
        }
    }

    private var accessibilityPanel: some View {
        panel(title: "LECTURE ET ACCESSIBILITÉ") {
            settingLine("Dynamic Type", detail: "Les titres et listes suivent la taille système.")
            Divider().overlay(NomiTheme.line.opacity(0.6))
            settingLine("Contraste et mouvement", detail: "Les réglages iOS correspondants restent la source de vérité.")
            Divider().overlay(NomiTheme.line.opacity(0.6))
            settingLine("VoiceOver", detail: "Actions nommées, états désactivés et contenu sous palette masqué.")
        }
    }

    private var integrationsPanel: some View {
        panel(title: "INTÉGRATIONS PRÉVUES") {
            integrationRow("01", "Notifications locales", "routines et rappels de focus")
            Divider().overlay(NomiTheme.line.opacity(0.6))
            integrationRow("02", "Document Picker", "documents liés et pièces jointes")
            Divider().overlay(NomiTheme.line.opacity(0.6))
            integrationRow("03", "App Intents", "tâche, focus et transcription depuis Raccourcis")
            Divider().overlay(NomiTheme.line.opacity(0.6))
            integrationRow("04", "Widgets et Spotlight", "révision du jour et recherche locale")
        }
    }

    private var functionalStatePanel: some View {
        panel(title: "ÉTAT FONCTIONNEL") {
            stateLine(true, "Tâches, Focus, calculs et persistance locale réels")
            stateLine(true, "Transcription Apple Speech semi-réelle sur appareil")
            stateLine(true, "Gemma 4 local réel après installation")
            stateLine(false, "Notifications, iCloud, Documents et partage non implémentés")
        }
    }

    private func panel<Content: View>(title: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(title)
                .font(.caption2.weight(.bold))
                .tracking(1.1)
                .foregroundStyle(NomiTheme.ink3)
                .padding(.horizontal, 1)
            VStack(alignment: .leading, spacing: 9) { content() }
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(12)
                .background(NomiTheme.surface.opacity(0.93), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.7))
        }
    }

    private func settingLine(_ title: String, detail: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title)
                .font(.subheadline.weight(.medium))
                .foregroundStyle(NomiTheme.ink)
            Text(detail)
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    private func integrationRow(_ rank: String, _ title: String, _ detail: String) -> some View {
        HStack(alignment: .top, spacing: 9) {
            Text(rank)
                .font(.caption2.monospaced().weight(.bold))
                .foregroundStyle(NomiTheme.ink3)
                .frame(width: 24, alignment: .leading)
            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                    .font(.subheadline.weight(.medium))
                    .foregroundStyle(NomiTheme.ink)
                Text(detail)
                    .font(.caption)
                    .foregroundStyle(NomiTheme.ink2)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    private func stateLine(_ available: Bool, _ text: String) -> some View {
        Label(text, systemImage: available ? "checkmark.circle" : "circle.dashed")
            .font(.caption)
            .foregroundStyle(available ? NomiTheme.ink2 : NomiTheme.ink3)
            .fixedSize(horizontal: false, vertical: true)
    }
}
