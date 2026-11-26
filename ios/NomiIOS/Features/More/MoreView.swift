import SwiftUI

struct MoreView: View {
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                NomiScreenTitle("Plus", subtitle: "Les espaces d'apprentissage, d'automatisation et de réglage.")
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 250), spacing: 14)], spacing: 14) {
                    destination("Routines", subtitle: "Automatisations visibles et contrôlées", icon: "repeat", tint: NomiTheme.success) {
                        RoutinesView()
                    }
                    destination("Transcription", subtitle: "Capturer un cours en direct", icon: "waveform.and.mic", tint: NomiTheme.course) {
                        LiveTranscriptionView()
                    }
                    destination("Fiches et révisions", subtitle: "Flashcards, quiz et exports", icon: "rectangle.stack.fill", tint: NomiTheme.focus) {
                        LibraryView()
                    }
                    destination("Réglages", subtitle: "IA, confidentialité et accessibilité", icon: "gearshape.fill", tint: NomiTheme.accent) {
                        SettingsView()
                    }
                }
            }
            .padding(.horizontal, 20)
            .padding(.top, 18)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
    }

    private func destination<Content: View>(_ title: String, subtitle: String, icon: String, tint: Color, @ViewBuilder content: @escaping () -> Content) -> some View {
        NavigationLink {
            content()
        } label: {
            VStack(alignment: .leading, spacing: 14) {
                Image(systemName: icon)
                    .font(.title3.weight(.semibold))
                    .foregroundStyle(tint)
                    .frame(width: 44, height: 44)
                    .background(tint.opacity(0.12), in: RoundedRectangle(cornerRadius: 16, style: .continuous))
                VStack(alignment: .leading, spacing: 4) {
                    Text(title)
                        .font(.system(.headline, design: .rounded).weight(.bold))
                        .foregroundStyle(NomiTheme.ink)
                    Text(subtitle)
                        .font(.subheadline)
                        .foregroundStyle(NomiTheme.ink2)
                }
                HStack {
                    Spacer()
                    Image(systemName: "arrow.right")
                        .font(.caption.weight(.bold))
                        .foregroundStyle(tint)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .nomiCard(tint: tint)
        }
        .buttonStyle(.plain)
    }
}
