import SwiftUI
import NomiCore

struct HomeView: View {
    @EnvironmentObject private var state: AppState

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                VStack(alignment: .leading, spacing: 8) {
                    NomiBadge("Prototype iOS · continuité Nomi", tint: NomiTheme.accent)
                    Text("Bonjour.")
                        .font(.largeTitle.weight(.bold))
                        .foregroundStyle(NomiTheme.ink)
                    Text("Travaillez, relisez, transcrivez et révisez avec des outils pensés pour l'iPhone.")
                        .font(.body)
                        .foregroundStyle(NomiTheme.ink2)
                }
                .padding(.top, 8)

                VStack(spacing: 12) {
                    NavigationLink {
                        CodeReviewView()
                    } label: {
                        FeatureRow(
                            title: "Relire du code",
                            subtitle: "Diagnostics, calculs vérifiés et questions sur une sélection.",
                            systemImage: "chevron.left.forwardslash.chevron.right",
                            tint: NomiTheme.code
                        )
                    }

                    NavigationLink {
                        LiveTranscriptionView()
                    } label: {
                        FeatureRow(
                            title: "Transcrire un cours",
                            subtitle: "Microphone réel ou cours de démonstration sur simulateur.",
                            systemImage: "waveform.and.mic",
                            tint: NomiTheme.course
                        )
                    }

                    NavigationLink {
                        RevisionSheetView(sourceText: DemoContent.transcript, sourceTitle: "Probabilités conditionnelles")
                    } label: {
                        FeatureRow(
                            title: "Créer une fiche",
                            subtitle: "Fidélité au source, flashcards, quiz et export.",
                            systemImage: "rectangle.stack.badge.plus",
                            tint: NomiTheme.success
                        )
                    }
                }

                VStack(alignment: .leading, spacing: 12) {
                    NomiSectionTitle("Aujourd’hui", subtitle: "Reprise locale des principes Nomi : plan lisible, actions explicites, rien d’automatique sans accord.")
                    HStack(spacing: 12) {
                        MetricTile(value: "2", label: "actions rapides", tint: NomiTheme.accent)
                        MetricTile(value: "0", label: "données envoyées", tint: NomiTheme.success)
                        MetricTile(value: "\(state.revisionSheets.count)", label: "fiches", tint: NomiTheme.course)
                    }
                }
                .nomiCard()

                VStack(alignment: .leading, spacing: 8) {
                    Label("IA simulée par défaut", systemImage: "checkmark.shield")
                        .font(.headline)
                    Text("Les interfaces appellent des protocoles Swift. Les mocks ne quittent pas l’appareil ; les implémentations réelles sont explicitement marquées avant activation.")
                        .font(.subheadline)
                        .foregroundStyle(NomiTheme.ink2)
                }
                .nomiCard(tint: NomiTheme.success)
            }
            .padding()
        }
        .nomiScreenBackground()
        .navigationTitle("Nomi")
        .navigationBarTitleDisplayMode(.inline)
    }
}

private struct FeatureRow: View {
    var title: String
    var subtitle: String
    var systemImage: String
    var tint: Color

    var body: some View {
        HStack(spacing: 14) {
            Image(systemName: systemImage)
                .font(.title2.weight(.semibold))
                .foregroundStyle(tint)
                .frame(width: 48, height: 48)
                .background(tint.opacity(0.12), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
            VStack(alignment: .leading, spacing: 3) {
                Text(title)
                    .font(.headline)
                    .foregroundStyle(NomiTheme.ink)
                Text(subtitle)
                    .font(.subheadline)
                    .foregroundStyle(NomiTheme.ink2)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Spacer()
            Image(systemName: "chevron.right")
                .font(.caption.weight(.bold))
                .foregroundStyle(NomiTheme.ink3)
        }
        .padding(16)
        .background(NomiTheme.surface, in: RoundedRectangle(cornerRadius: 18, style: .continuous))
        .overlay {
            RoundedRectangle(cornerRadius: 18, style: .continuous)
                .strokeBorder(NomiTheme.line)
        }
        .contentShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
    }
}

private struct MetricTile: View {
    var value: String
    var label: String
    var tint: Color

    var body: some View {
        VStack(alignment: .leading, spacing: 5) {
            Text(value)
                .font(.title.weight(.bold))
                .foregroundStyle(tint)
            Text(label)
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(10)
        .background(tint.opacity(0.09), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
    }
}

#Preview {
    NavigationStack { HomeView() }
        .environmentObject(AppState())
}
