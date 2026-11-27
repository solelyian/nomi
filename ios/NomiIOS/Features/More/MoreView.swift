import SwiftUI

struct MoreView: View {
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                VStack(alignment: .leading, spacing: 4) {
                    Text("Plus")
                        .font(.system(.largeTitle, design: .serif).weight(.semibold))
                        .tracking(-0.7)
                        .foregroundStyle(NomiTheme.ink)
                    Text("Apprentissage, automatisations et réglages")
                        .font(.caption)
                        .foregroundStyle(NomiTheme.ink2)
                }

                VStack(alignment: .leading, spacing: 6) {
                    sectionLabel("Espaces", detail: "4")
                    VStack(spacing: 0) {
                        destination(index: 0, title: "Routines", subtitle: "Automatisations visibles et contrôlées", icon: "repeat", value: .routines)
                        divider
                        destination(index: 1, title: "Transcription", subtitle: "Capturer un cours ou une réunion", icon: "waveform.and.mic", value: .transcript)
                        divider
                        destination(index: 2, title: "Fiches et révisions", subtitle: "Flashcards, quiz et exports", icon: "rectangle.stack", value: .library)
                        divider
                        destination(index: 3, title: "Réglages", subtitle: "IA locale, confidentialité et accessibilité", icon: "gearshape", value: .settings)
                    }
                    .background(NomiTheme.surface.opacity(0.93), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                    .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.7))
                }

                VStack(alignment: .leading, spacing: 6) {
                    sectionLabel("État local", detail: "HONNÊTE")
                    VStack(alignment: .leading, spacing: 8) {
                        Label("Gemma 4 est téléchargé à la demande et fonctionne sur appareil.", systemImage: "cpu")
                        Label("Les fonctions non disponibles restent marquées comme prévues ou simulées.", systemImage: "checkmark.shield")
                    }
                    .font(.caption)
                    .foregroundStyle(NomiTheme.ink2)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(12)
                    .background(NomiTheme.surface.opacity(0.82), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                    .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.68), lineWidth: 0.7))
                }
            }
            .padding(.horizontal, 18)
            .padding(.top, 14)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
    }

    private var divider: some View {
        Divider().overlay(NomiTheme.line.opacity(0.6)).padding(.leading, 42)
    }

    private func sectionLabel(_ title: String, detail: String) -> some View {
        HStack(alignment: .firstTextBaseline) {
            Text(title.uppercased())
                .font(.caption2.weight(.bold))
                .tracking(1.1)
                .foregroundStyle(NomiTheme.ink3)
            Spacer()
            Text(detail)
                .font(.caption2.monospaced())
                .foregroundStyle(NomiTheme.ink3)
        }
        .padding(.horizontal, 1)
    }

    private func destination(index: Int, title: String, subtitle: String, icon: String, value: NomiDetailDestination) -> some View {
        NavigationLink(value: value) {
            HStack(spacing: 9) {
                Image(systemName: icon)
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(index == 3 ? NomiTheme.accent : NomiTheme.ink2)
                    .frame(width: 23, height: 23)
                    .background((index == 3 ? NomiTheme.accentSoft : NomiTheme.surface2.opacity(0.55)), in: RoundedRectangle(cornerRadius: 7, style: .continuous))
                VStack(alignment: .leading, spacing: 2) {
                    Text(title)
                        .font(.subheadline.weight(.medium))
                        .foregroundStyle(NomiTheme.ink)
                    Text(subtitle)
                        .font(.caption2)
                        .foregroundStyle(NomiTheme.ink3)
                        .lineLimit(1)
                }
                Spacer()
                Image(systemName: "chevron.right")
                    .font(.caption2.weight(.bold))
                    .foregroundStyle(NomiTheme.ink3)
            }
            .padding(.horizontal, 10)
            .frame(minHeight: 48)
        }
        .buttonStyle(.plain)
    }
}
