import SwiftUI

struct GemmaSettingsView: View {
    @EnvironmentObject private var state: AppState

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                header
                statusPanel
                modelList
                privacyPanel
            }
            .padding(.horizontal, 18)
            .padding(.top, 14)
            .padding(.bottom, 34)
        }
        .nomiScreenBackground()
        .nomiChildNavigation()
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Gemma 4 local")
                .font(.system(.largeTitle, design: .serif).weight(.semibold))
                .tracking(-0.7)
                .foregroundStyle(NomiTheme.ink)
            Text("Modèle open model exécuté sur appareil")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
        }
    }

    private var statusPanel: some View {
        let selected = state.gemmaManager.selectedModel
        let selectedState = state.gemmaManager.state(for: selected)
        return HStack(spacing: 10) {
            Image(systemName: selectedState == .downloaded ? "checkmark.circle.fill" : "arrow.down.circle")
                .font(.system(size: 15, weight: .medium))
                .foregroundStyle(selectedState == .downloaded ? NomiTheme.success : NomiTheme.accent)
                .frame(width: 27, height: 27)
                .background((selectedState == .downloaded ? NomiTheme.success.opacity(0.12) : NomiTheme.accentSoft), in: RoundedRectangle(cornerRadius: 8, style: .continuous))
            VStack(alignment: .leading, spacing: 2) {
                Text(selectedState == .downloaded ? "Modèle actif installé" : "Installation requise")
                    .font(.subheadline.weight(.medium))
                    .foregroundStyle(NomiTheme.ink)
                Text(state.gemmaManager.runtimeNotice ?? "\(selected.title) · \(GemmaModelManager.formattedBytes(selected.expectedBytes))")
                    .font(.caption2)
                    .foregroundStyle(NomiTheme.ink3)
                    .lineLimit(2)
            }
            Spacer()
        }
        .padding(11)
        .background(NomiTheme.surface.opacity(0.94), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.8))
    }

    private var modelList: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .firstTextBaseline) {
                Text("MODÈLES")
                    .font(.caption2.weight(.bold))
                    .tracking(1.1)
                    .foregroundStyle(NomiTheme.ink3)
                Spacer()
                Text("\(GemmaModelDescriptor.all.count)")
                    .font(.caption2.monospaced())
                    .foregroundStyle(NomiTheme.ink3)
            }
            .padding(.horizontal, 1)
            VStack(spacing: 0) {
                ForEach(Array(GemmaModelDescriptor.all.enumerated()), id: \.element.id) { index, model in
                    if index > 0 { Divider().overlay(NomiTheme.line.opacity(0.6)).padding(.leading, 42) }
                    modelRow(model)
                }
            }
            .background(NomiTheme.surface.opacity(0.93), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.7))
        }
    }

    private func modelRow(_ model: GemmaModelDescriptor) -> some View {
        let modelState = state.gemmaManager.state(for: model)
        let isSelected = state.gemmaManager.selectedModelID == model.id
        return VStack(alignment: .leading, spacing: 8) {
            HStack(alignment: .top, spacing: 9) {
                Button {
                    state.gemmaManager.select(model)
                } label: {
                    Image(systemName: isSelected ? "checkmark.circle.fill" : "circle")
                        .font(.system(size: 15, weight: .medium))
                        .foregroundStyle(isSelected ? NomiTheme.accent : NomiTheme.ink3)
                        .frame(width: 24, height: 24)
                        .background((isSelected ? NomiTheme.accentSoft : NomiTheme.surface2.opacity(0.55)), in: RoundedRectangle(cornerRadius: 7, style: .continuous))
                }
                .buttonStyle(.plain)
                .accessibilityLabel("Choisir \(model.title)")

                VStack(alignment: .leading, spacing: 3) {
                    HStack(alignment: .firstTextBaseline) {
                        Text(model.title)
                            .font(.subheadline.weight(.semibold))
                            .foregroundStyle(NomiTheme.ink)
                        Spacer()
                        Text(GemmaModelManager.formattedBytes(model.expectedBytes))
                            .font(.caption2.monospaced())
                            .foregroundStyle(NomiTheme.ink3)
                    }
                    Text(model.subtitle)
                        .font(.caption)
                        .foregroundStyle(NomiTheme.ink2)
                        .fixedSize(horizontal: false, vertical: true)
                    Text("Apache 2.0 · \(modelState.label)")
                        .font(.caption2)
                        .foregroundStyle(modelState == .downloaded ? NomiTheme.success : NomiTheme.ink3)
                }
            }

            if let progress = modelState.progress, modelState != .downloaded {
                ProgressView(value: progress)
                    .tint(NomiTheme.accent)
                    .padding(.leading, 33)
                    .accessibilityLabel("Progression du téléchargement")
                    .accessibilityValue("\(Int(progress * 100)) pour cent")
            }

            HStack(spacing: 7) {
                Spacer(minLength: 33)
                switch modelState {
                case .notDownloaded, .failed:
                    Button("Télécharger", systemImage: "arrow.down") { state.gemmaManager.download(model) }
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
                    if isSelected {
                        Text("ACTIF")
                            .font(.system(size: 9, weight: .bold, design: .monospaced))
                            .tracking(0.8)
                            .foregroundStyle(NomiTheme.success)
                    }
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
                    .font(.caption)
                    .foregroundStyle(NomiTheme.warning)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.leading, 33)
            }
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 10)
    }

    private var privacyPanel: some View {
        VStack(alignment: .leading, spacing: 7) {
            Text("CONFIDENTIALITÉ")
                .font(.caption2.weight(.bold))
                .tracking(1.1)
                .foregroundStyle(NomiTheme.ink3)
            Text("Le téléchargement contacte Hugging Face uniquement pour récupérer le modèle. La génération s’effectue ensuite localement dans LiteRT-LM. E4B peut être trop lourd pour certains iPhone.")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
                .fixedSize(horizontal: false, vertical: true)
            Text("Le premier chargement peut prendre une dizaine de secondes ; les calculs sensibles continuent d’utiliser le vérificateur déterministe.")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
                .fixedSize(horizontal: false, vertical: true)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(12)
        .background(NomiTheme.surface.opacity(0.72), in: RoundedRectangle(cornerRadius: 13, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 13, style: .continuous).stroke(NomiTheme.line.opacity(0.68), lineWidth: 0.7))
    }
}
