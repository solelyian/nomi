import SwiftUI

struct GemmaSettingsView: View {
    @EnvironmentObject private var state: AppState

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                NomiScreenTitle("Gemma 4 local", subtitle: "Moteur IA open model exécuté sur appareil avec LiteRT-LM et Metal quand le matériel le permet.")
                if shouldShowHero {
                    hero
                }
                ForEach(GemmaModelDescriptor.all) { model in
                    modelCard(model)
                }
                privacyCard
            }
            .padding(.horizontal, 20)
            .padding(.top, 18)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
        .nomiChildNavigation()
    }


    private var shouldShowHero: Bool {
        state.gemmaManager.state(for: state.gemmaManager.selectedModel) != .downloaded || state.gemmaManager.runtimeNotice != nil
    }

    private var hero: some View {
        let selected = state.gemmaManager.selectedModel
        let selectedState = state.gemmaManager.state(for: selected)
        return VStack(alignment: .leading, spacing: 12) {
            HStack(alignment: .top) {
                Image(systemName: selectedState == .downloaded ? "cpu.fill" : "arrow.down.circle.fill")
                    .font(.title2)
                    .foregroundStyle(selectedState == .downloaded ? NomiTheme.success : NomiTheme.focus)
                    .frame(width: 46, height: 46)
                    .background((selectedState == .downloaded ? NomiTheme.success : NomiTheme.focus).opacity(0.12), in: RoundedRectangle(cornerRadius: 17, style: .continuous))
                VStack(alignment: .leading, spacing: 4) {
                    Text(selected.title)
                        .font(.system(.title3, design: .rounded).weight(.bold))
                    Text(selectedState.label)
                        .font(.subheadline.weight(.semibold))
                        .foregroundStyle(selectedState == .downloaded ? NomiTheme.success : NomiTheme.ink2)
                }
                Spacer()
                NomiBadge(GemmaModelManager.formattedBytes(selected.expectedBytes), tint: NomiTheme.focus)
            }
            if let notice = state.gemmaManager.runtimeNotice {
                Text(notice)
                    .font(.footnote)
                    .foregroundStyle(NomiTheme.ink2)
            }
            Text("Le modèle est téléchargé une fois depuis Hugging Face. Après installation, les conversations Nomi sont générées localement, sans envoyer leur contenu vers un serveur.")
                .font(.footnote)
                .foregroundStyle(NomiTheme.ink2)
        }
        .nomiCard(tint: selectedState == .downloaded ? NomiTheme.success : NomiTheme.focus)
    }

    private func modelCard(_ model: GemmaModelDescriptor) -> some View {
        let modelState = state.gemmaManager.state(for: model)
        let isSelected = state.gemmaManager.selectedModelID == model.id
        return VStack(alignment: .leading, spacing: 13) {
            HStack(alignment: .top, spacing: 12) {
                Button {
                    state.gemmaManager.select(model)
                } label: {
                    Image(systemName: isSelected ? "largecircle.fill.circle" : "circle")
                        .font(.title3)
                        .foregroundStyle(isSelected ? NomiTheme.accent : NomiTheme.ink3)
                        .frame(width: 44, height: 44)
                }
                .buttonStyle(.plain)
                .accessibilityLabel("Choisir \(model.title)")
                VStack(alignment: .leading, spacing: 4) {
                    Text(model.title)
                        .font(.system(.headline, design: .rounded).weight(.bold))
                    Text(model.subtitle)
                        .font(.subheadline)
                        .foregroundStyle(NomiTheme.ink2)
                        .fixedSize(horizontal: false, vertical: true)
                    Text("Taille : \(GemmaModelManager.formattedBytes(model.expectedBytes)) · licence Apache 2.0")
                        .font(.caption)
                        .foregroundStyle(NomiTheme.ink3)
                }
            }

            if let progress = modelState.progress, modelState != .downloaded {
                ProgressView(value: progress)
                    .tint(NomiTheme.focus)
                    .accessibilityLabel("Progression du téléchargement")
                    .accessibilityValue("\(Int(progress * 100)) pour cent")
            }

            HStack(spacing: 10) {
                switch modelState {
                case .notDownloaded, .failed:
                    Button("Télécharger", systemImage: "arrow.down") {
                        state.gemmaManager.download(model)
                    }
                    .buttonStyle(.nomiPrimary)
                case .downloading:
                    Button("Pause", systemImage: "pause") { state.gemmaManager.pauseDownload() }
                        .buttonStyle(.nomiSecondary)
                    Button("Annuler", role: .destructive) { state.gemmaManager.cancelDownload() }
                        .buttonStyle(.nomiSecondary)
                case .paused:
                    Button("Reprendre", systemImage: "play") { state.gemmaManager.resumeDownload() }
                        .buttonStyle(.nomiPrimary)
                    Button("Annuler", role: .destructive) { state.gemmaManager.cancelDownload() }
                        .buttonStyle(.nomiSecondary)
                case .downloaded:
                    NomiBadge(isSelected ? "modèle actif" : "installé", tint: NomiTheme.success)
                    Spacer()
                    Button("Supprimer", role: .destructive) {
                        state.gemmaManager.delete(model)
                        Task { await state.gemmaRuntime.unload() }
                    }
                        .buttonStyle(.nomiSecondary)
                }
            }
            if case .failed(let message) = modelState {
                Label(message, systemImage: "exclamationmark.triangle.fill")
                    .font(.footnote)
                    .foregroundStyle(NomiTheme.warning)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
        .nomiCard(tint: isSelected ? NomiTheme.accent : NomiTheme.focus)
    }

    private var privacyCard: some View {
        VStack(alignment: .leading, spacing: 10) {
            Label("Confidentialité et limites", systemImage: "lock.shield.fill")
                .font(.headline)
                .foregroundStyle(NomiTheme.course)
            Text("Le téléchargement contacte Hugging Face uniquement pour récupérer le fichier du modèle. La génération conversationnelle s'effectue ensuite dans LiteRT-LM sur l'appareil. Les performances, la mémoire et la chauffe dépendent de l'iPhone ; Gemma 4 E4B peut être trop lourd pour certains modèles.")
                .font(.footnote)
                .foregroundStyle(NomiTheme.ink2)
            Text("Le premier chargement peut prendre une dizaine de secondes. Les calculs sensibles continuent d'utiliser le vérificateur déterministe.")
                .font(.footnote)
                .foregroundStyle(NomiTheme.ink2)
        }
        .nomiCard(tint: NomiTheme.course)
    }
}
