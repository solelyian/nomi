import SwiftUI
import NomiCore

struct ConversationView: View {
    @EnvironmentObject private var state: AppState
    @State private var input = ""

    var body: some View {
        VStack(spacing: 0) {
            threadSelector
            GeometryReader { geometry in
                ScrollViewReader { proxy in
                    ScrollView {
                        LazyVStack(alignment: .leading, spacing: 13) {
                            ForEach(state.activeThread?.messages ?? []) { message in
                                bubble(message)
                            }
                            Color.clear.frame(height: 1).id("conversation-bottom")
                        }
                        .frame(width: max(0, geometry.size.width - 36), alignment: .leading)
                        .padding(.horizontal, 18)
                        .padding(.top, 14)
                        .padding(.bottom, 18)
                    }
                    .onChange(of: state.activeThread?.messages.count ?? 0) { _ in
                        withAnimation { proxy.scrollTo("conversation-bottom", anchor: .bottom) }
                    }
                    .onChange(of: state.activeThread?.messages.last?.text ?? "") { _ in
                        withAnimation(.linear(duration: 0.08)) { proxy.scrollTo("conversation-bottom", anchor: .bottom) }
                    }
                }
            }
            composer
        }
        .nomiScreenBackground()
    }

    private var threadSelector: some View {
        HStack(spacing: 12) {
            VStack(alignment: .leading, spacing: 2) {
                Text("Nomi")
                    .font(.system(.title2, design: .rounded).weight(.bold))
                Menu(state.activeThread?.title ?? "Conversation") {
                    ForEach(state.threads) { thread in
                        Button(thread.title) { state.activeThreadID = thread.id }
                    }
                }
                .font(.subheadline.weight(.semibold))
                .foregroundStyle(NomiTheme.ink2)
                Text(state.gemmaManager.selectedModelURL == nil ? "Gemma 4 à installer" : "Gemma 4 local prêt")
                    .font(.caption.weight(.bold))
                    .foregroundStyle(state.gemmaManager.selectedModelURL == nil ? NomiTheme.warning : NomiTheme.success)
            }
            Spacer()
            Button("Nouvelle", systemImage: "square.and.pencil") { state.newThread() }
                .buttonStyle(.nomiSecondary)
        }
        .padding(.horizontal, 18)
        .padding(.vertical, 12)
        .background(NomiTheme.surface.opacity(0.26))
    }

    private func bubble(_ message: ChatMessage) -> some View {
        HStack(alignment: .bottom, spacing: 0) {
            if message.role == .user { Spacer(minLength: 58) }
            VStack(alignment: .leading, spacing: 7) {
                Text(markdownAttributed(message.text))
                    .font(.body)
                    .foregroundStyle(message.role == .user ? .white : NomiTheme.ink)
                    .textSelection(.enabled)
                    .fixedSize(horizontal: false, vertical: true)
                if let verification = message.verification {
                    NomiBadge(verification.status == .verified ? "calcul vérifié localement" : "calcul non vérifié", tint: verification.status == .verified ? NomiTheme.success : NomiTheme.warning)
                }
                Text(message.createdAt.formatted(date: .omitted, time: .shortened))
                    .font(.caption2)
                    .foregroundStyle(message.role == .user ? .white.opacity(0.74) : NomiTheme.ink3)
            }
            .padding(.horizontal, 15)
            .padding(.vertical, 12)
            .background {
                if message.role == .user {
                    RoundedRectangle(cornerRadius: 23, style: .continuous)
                        .fill(LinearGradient(colors: [NomiTheme.focus, Color.dynamic(light: "654DDE", dark: "8267EA")], startPoint: .topLeading, endPoint: .bottomTrailing))
                } else {
                    RoundedRectangle(cornerRadius: 23, style: .continuous)
                        .fill(.thinMaterial)
                        .overlay {
                            RoundedRectangle(cornerRadius: 23, style: .continuous)
                                .fill(NomiTheme.surface.opacity(0.56))
                        }
                }
            }
            .overlay {
                if message.role == .assistant {
                    RoundedRectangle(cornerRadius: 23, style: .continuous)
                        .strokeBorder(NomiTheme.glassStroke.opacity(0.62), lineWidth: 1)
                }
            }
            .shadow(color: .black.opacity(0.08), radius: 16, y: 8)
            if message.role == .assistant { Spacer(minLength: 58) }
        }
        .frame(maxWidth: .infinity)
        .accessibilityElement(children: .combine)
    }


    private func markdownAttributed(_ text: String) -> AttributedString {
        (try? AttributedString(markdown: text, options: .init(interpretedSyntax: .inlineOnlyPreservingWhitespace))) ?? AttributedString(text)
    }

    private var composer: some View {
        VStack(spacing: 7) {
            HStack(alignment: .bottom, spacing: 10) {
                TextField(state.gemmaManager.selectedModelURL == nil ? "Installer Gemma 4 dans Réglages pour l’IA…" : "Demander, planifier ou vérifier un calcul…", text: $input, axis: .vertical)
                    .textFieldStyle(.plain)
                    .padding(.horizontal, 15)
                    .padding(.vertical, 12)
                    .background(NomiTheme.surface.opacity(0.58), in: RoundedRectangle(cornerRadius: 22, style: .continuous))
                    .overlay(RoundedRectangle(cornerRadius: 22, style: .continuous).strokeBorder(NomiTheme.glassStroke.opacity(0.62), lineWidth: 1))
                Button {
                    state.sendMessage(input)
                    input = ""
                } label: {
                    Image(systemName: "arrow.up")
                        .font(.headline.weight(.bold))
                        .foregroundStyle(.white)
                        .frame(width: 44, height: 44)
                        .background(NomiTheme.accentGradient, in: Circle())
                        .shadow(color: NomiTheme.accent.opacity(0.28), radius: 12, y: 6)
                }
                .buttonStyle(.plain)
                .disabled(input.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || state.isGeneratingReply)
                .opacity(input.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || state.isGeneratingReply ? 0.48 : 1)
                .accessibilityLabel("Envoyer")
            }
            Text("Génération Gemma 4 locale après installation ; les calculs simples restent vérifiés par le moteur déterministe.")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(.horizontal, 18)
        .padding(.top, 9)
        .padding(.bottom, 8)
        .background(.thinMaterial)
    }
}
