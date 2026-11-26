import SwiftUI
import NomiCore

struct ConversationView: View {
    @EnvironmentObject private var state: AppState
    @Environment(\.verticalSizeClass) private var verticalSizeClass
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
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
                        .padding(.bottom, verticalSizeClass == .compact ? 76 : 26)
                    }
                    .onChange(of: state.activeThread?.messages.count ?? 0) { _, _ in
                        withAnimation { proxy.scrollTo("conversation-bottom", anchor: .bottom) }
                    }
                    .defaultScrollAnchor(.bottom)
                    .onAppear {
                        DispatchQueue.main.async { proxy.scrollTo("conversation-bottom", anchor: .bottom) }
                    }
                    .onChange(of: state.activeThreadID) { _, _ in
                        DispatchQueue.main.async { proxy.scrollTo("conversation-bottom", anchor: .bottom) }
                    }
                    .onChange(of: state.activeThread?.messages.last?.text ?? "") { _, _ in
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
                if verticalSizeClass != .compact {
                    Text(state.gemmaManager.selectedModelURL == nil ? "Gemma 4 à installer" : "Gemma 4 local prêt")
                        .font(.caption.weight(.bold))
                        .foregroundStyle(state.gemmaManager.selectedModelURL == nil ? NomiTheme.warning : NomiTheme.success)
                }
            }
            Spacer()
            Button(dynamicTypeSize.isAccessibilitySize ? "" : "Nouvelle", systemImage: "square.and.pencil") { state.newThread() }
                .buttonStyle(.nomiSecondary)
                .accessibilityLabel("Nouvelle conversation")
        }
        .padding(.horizontal, 18)
        .padding(.vertical, verticalSizeClass == .compact ? 6 : 12)
    }

    private func bubble(_ message: ChatMessage) -> some View {
        HStack(alignment: .bottom, spacing: 0) {
            if message.role == .user { Spacer(minLength: 58) }
            VStack(alignment: .leading, spacing: 7) {
                Text(message.role == .assistant ? markdownAttributed(message.text) : AttributedString(message.text))
                    .font(.body)
                    .foregroundStyle(message.role == .user ? .white : NomiTheme.ink)
                    .textSelection(.enabled)
                    .fixedSize(horizontal: false, vertical: true)
                if let verification = message.verification {
                    NomiBadge(verification.status == .verified ? "calcul vérifié localement" : "calcul non vérifié", tint: verification.status == .verified ? NomiTheme.success : NomiTheme.warning)
                }
                Text(message.createdAt.formatted(date: .omitted, time: .shortened))
                    .font(.caption2)
                    .foregroundStyle(message.role == .user ? .white.opacity(0.9) : NomiTheme.ink3)
            }
            .padding(.horizontal, 15)
            .padding(.vertical, 12)
            .nomiLiquidGlass(
                shape: .rounded(23),
                tint: message.role == .user ? NomiTheme.focus : nil,
                tintStrength: message.role == .user ? 0.86 : 0
            )
            .shadow(color: .black.opacity(0.045), radius: 12, y: 6)
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
                TextField(state.gemmaManager.selectedModelURL == nil ? "Installer Gemma 4…" : "Demander à Nomi…", text: $input, axis: .vertical)
                    .textFieldStyle(.plain)
                    .padding(.horizontal, 15)
                    .padding(.vertical, 12)
                    .nomiLiquidGlass(shape: .rounded(22), tintStrength: 0)
                Button {
                    state.sendMessage(input)
                    input = ""
                } label: {
                    Image(systemName: "arrow.up")
                        .font(.headline.weight(.bold))
                        .foregroundStyle(.white)
                        .frame(width: 44, height: 44)
                        .nomiLiquidGlass(shape: .circle, tint: NomiTheme.accent, tintStrength: 0.92, interactive: true)
                        .shadow(color: NomiTheme.accent.opacity(0.18), radius: 10, y: 5)
                }
                .buttonStyle(.plain)
                .disabled(input.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || state.isGeneratingReply)
                .opacity(input.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || state.isGeneratingReply ? 0.48 : 1)
                .accessibilityLabel("Envoyer")
            }
            if verticalSizeClass != .compact {
                Text("Génération Gemma 4 locale après installation ; les calculs simples restent vérifiés par le moteur déterministe.")
                    .font(.caption)
                    .foregroundStyle(NomiTheme.ink2)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
        }
        .padding(.horizontal, 18)
        .padding(.top, verticalSizeClass == .compact ? 5 : 9)
        .padding(.bottom, verticalSizeClass == .compact ? 4 : 8)
    }
}
