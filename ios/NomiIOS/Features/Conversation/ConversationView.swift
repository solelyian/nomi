import SwiftUI
import NomiCore

struct ConversationView: View {
    @EnvironmentObject private var state: AppState
    @Environment(\.verticalSizeClass) private var verticalSizeClass
    @Environment(\.horizontalSizeClass) private var horizontalSizeClass
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var input = ""
    @State private var drafts: [UUID: String] = [:]
    @State private var threadLibraryPresented = false
    @State private var threadSearch = ""

    private var canSend: Bool {
        !input.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && !state.isGeneratingReply
    }

    private var contentWidth: CGFloat {
        horizontalSizeClass == .regular ? 680 : .infinity
    }

    var body: some View {
        ZStack(alignment: .leading) {
            conversationContent
                .disabled(threadLibraryPresented)
                .accessibilityHidden(threadLibraryPresented)

            if threadLibraryPresented {
                Color.black.opacity(0.24)
                    .ignoresSafeArea()
                    .contentShape(Rectangle())
                    .onTapGesture { closeThreadLibrary() }
                    .accessibilityHidden(true)
                    .transition(.opacity)
                    .zIndex(1)

                threadLibrary
                    .transition(reduceMotion ? .opacity : .move(edge: .leading).combined(with: .opacity))
                    .zIndex(2)
            }
        }
        .animation(reduceMotion ? nil : .easeOut(duration: 0.22), value: threadLibraryPresented)
        .nomiScreenBackground()
    }

    private var conversationContent: some View {
        VStack(spacing: 0) {
            topControls
            GeometryReader { geometry in
                ScrollViewReader { proxy in
                    ScrollView {
                        HStack(spacing: 0) {
                            Spacer(minLength: 0)
                            LazyVStack(alignment: .leading, spacing: 21) {
                                if !(state.activeThread?.messages.contains { $0.role == .user } ?? false) {
                                    emptyState
                                        .frame(maxWidth: .infinity)
                                        .frame(minHeight: max(340, geometry.size.height - 42))
                                } else {
                                    ForEach(state.activeThread?.messages ?? []) { message in
                                        bubble(message)
                                    }
                                }
                                Color.clear.frame(height: 1).id("conversation-bottom")
                            }
                            .frame(maxWidth: contentWidth, alignment: .leading)
                            Spacer(minLength: 0)
                        }
                        .padding(.horizontal, horizontalSizeClass == .regular ? 24 : 18)
                        .padding(.top, 8)
                        .padding(.bottom, verticalSizeClass == .compact ? 76 : 24)
                    }
                    .onChange(of: state.activeThread?.messages.count ?? 0) { _, _ in
                        withAnimation { proxy.scrollTo("conversation-bottom", anchor: .bottom) }
                    }
                    .onAppear {
                        DispatchQueue.main.async { proxy.scrollTo("conversation-bottom", anchor: .bottom) }
                    }
                    .onChange(of: state.activeThreadID) { oldID, newID in
                        if let oldID, oldID != newID {
                            drafts[oldID] = input
                        }
                        if let newID {
                            input = drafts[newID] ?? ""
                        }
                        DispatchQueue.main.async { proxy.scrollTo("conversation-bottom", anchor: .bottom) }
                    }
                    .onChange(of: state.activeThread?.messages.last?.text ?? "") { _, _ in
                        withAnimation(.linear(duration: 0.08)) { proxy.scrollTo("conversation-bottom", anchor: .bottom) }
                    }
                }
            }
            composer
        }
    }

    private var topControls: some View {
        HStack(spacing: 0) {
            Spacer(minLength: 0)
            HStack {
                topIconButton("line.3.horizontal", label: "Conversations récentes") {
                    withAnimation(reduceMotion ? nil : .easeOut(duration: 0.22)) {
                        threadLibraryPresented = true
                    }
                }
                Spacer()
                topIconButton("square.and.pencil", label: "Nouvelle conversation") {
                    state.newThread()
                }
            }
            .frame(maxWidth: contentWidth)
            Spacer(minLength: 0)
        }
        .padding(.horizontal, horizontalSizeClass == .regular ? 24 : 18)
        .padding(.top, verticalSizeClass == .compact ? 4 : 8)
        .padding(.bottom, verticalSizeClass == .compact ? 4 : 7)
    }

    private func topIconButton(_ systemName: String, label: String, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Image(systemName: systemName)
                .font(.nomi(.subheadline, weight: .semibold))
                .foregroundStyle(NomiTheme.ink)
                .frame(width: 39, height: 39)
                .contentShape(Circle())
                .nomiLiquidGlass(shape: .circle, tint: NomiTheme.surface, tintStrength: 0.04, interactive: true)
        }
        .buttonStyle(.plain)
        .accessibilityLabel(label)
    }

    @ViewBuilder
    private func bubble(_ message: ChatMessage) -> some View {
        if message.role == .user {
            HStack(alignment: .bottom, spacing: 0) {
                Spacer(minLength: horizontalSizeClass == .regular ? 150 : 56)
                Text(message.text)
                    .font(.nomi(.body))
                    .foregroundStyle(NomiTheme.ink)
                    .textSelection(.enabled)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.horizontal, 13)
                    .padding(.vertical, 10)
                    .background(NomiTheme.accentSoft, in: RoundedRectangle(cornerRadius: 16, style: .continuous))
                    .overlay(RoundedRectangle(cornerRadius: 16, style: .continuous).stroke(NomiTheme.accent.opacity(0.09), lineWidth: 0.7))
            }
            .frame(maxWidth: .infinity)
            .accessibilityElement(children: .combine)
            .accessibilityLabel("Vous")
            .accessibilityValue(message.text)
        } else {
            VStack(alignment: .leading, spacing: 7) {
                Text(markdownAttributed(message.text))
                    .font(.nomi(.body))
                    .foregroundStyle(NomiTheme.ink)
                    .textSelection(.enabled)
                    .fixedSize(horizontal: false, vertical: true)
                if let verification = message.verification {
                    HStack(spacing: 5) {
                        Circle()
                            .fill(verification.status == .verified ? NomiTheme.success : NomiTheme.warning)
                            .frame(width: 5, height: 5)
                        Text(verification.status == .verified ? "calcul vérifié localement" : "calcul non vérifié")
                            .font(.nomi(.caption, weight: .semibold))
                            .foregroundStyle(verification.status == .verified ? NomiTheme.success : NomiTheme.warning)
                    }
                }
            }
            .padding(.leading, 2)
            .padding(.trailing, horizontalSizeClass == .regular ? 72 : 18)
            .frame(maxWidth: .infinity, alignment: .leading)
            .accessibilityElement(children: .combine)
            .accessibilityLabel("Nomi")
            .accessibilityValue(message.text)
        }
    }

    private var emptyState: some View {
        VStack(spacing: 14) {
            Text("✳︎")
                .font(.system(size: 36, weight: .regular))
                .foregroundStyle(NomiTheme.accent)
                .accessibilityHidden(true)
            Text("Bonjour.")
                .font(.system(.title, design: .serif).weight(.regular))
                .tracking(-0.5)
                .multilineTextAlignment(.center)
                .foregroundStyle(NomiTheme.ink)
            NavigationLink(value: NomiDetailDestination.gemma) {
                HStack(spacing: 6) {
                    Circle()
                        .fill(state.gemmaManager.selectedModelURL == nil ? NomiTheme.warning : NomiTheme.success)
                        .frame(width: 5, height: 5)
                    Text(state.gemmaManager.selectedModelURL == nil ? "Gemma 4 · à installer" : "Gemma 4 · local prêt")
                        .font(.nomi(.caption, weight: .semibold))
                    Image(systemName: "chevron.right")
                        .font(.system(size: 8, weight: .bold))
                }
                .foregroundStyle(NomiTheme.ink2)
                .padding(.horizontal, 12)
                .padding(.vertical, 8)
                .background(NomiTheme.surface2.opacity(0.52), in: Capsule())
                .overlay(Capsule().stroke(NomiTheme.line.opacity(0.55), lineWidth: 0.7))
            }
            .buttonStyle(.plain)
            .accessibilityLabel("État Gemma 4 et réglages")
        }
        .padding(.horizontal, 20)
    }

    private func markdownAttributed(_ text: String) -> AttributedString {
        (try? AttributedString(markdown: text, options: .init(interpretedSyntax: .inlineOnlyPreservingWhitespace))) ?? AttributedString(text)
    }

    private var composer: some View {
        HStack(spacing: 0) {
            Spacer(minLength: 0)
            VStack(alignment: .leading, spacing: 7) {
                TextField("Demander à Nomi…", text: $input, axis: .vertical)
                    .textFieldStyle(.plain)
                    .font(.nomi(.callout))
                    .foregroundStyle(NomiTheme.ink)
                    .lineLimit(1...5)
                    .padding(.horizontal, 12)
                    .padding(.top, 12)
                HStack(spacing: 9) {
                    promptMenu
                    gemmaLink
                    Spacer(minLength: 4)
                    sendButton
                }
                .padding(.horizontal, 8)
                .padding(.bottom, 8)
            }
            .frame(maxWidth: contentWidth)
            .background(NomiTheme.surface.opacity(0.94), in: RoundedRectangle(cornerRadius: 21, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 21, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.8))
            .shadow(color: NomiTheme.ink.opacity(0.055), radius: 14, y: 6)
            Spacer(minLength: 0)
        }
        .padding(.horizontal, horizontalSizeClass == .regular ? 24 : 14)
        .padding(.top, verticalSizeClass == .compact ? 5 : 8)
        .padding(.bottom, verticalSizeClass == .compact ? 4 : 8)
    }

    private var promptMenu: some View {
        Menu {
            Button("Planifier ma soirée") { insertPrompt("Planifier ma soirée") }
            Button("Créer une fiche") { insertPrompt("Créer une fiche") }
            Button("Vérifier un calcul") { insertPrompt("Vérifier un calcul") }
        } label: {
            Image(systemName: "plus")
                .font(.nomi(.subheadline, weight: .bold))
                .foregroundStyle(NomiTheme.ink)
                .frame(width: 34, height: 34)
                .background(NomiTheme.surface2.opacity(0.72), in: Circle())
        }
        .accessibilityLabel("Points de départ")
    }

    private var gemmaLink: some View {
        NavigationLink(value: NomiDetailDestination.gemma) {
            HStack(spacing: 5) {
                Circle()
                    .fill(state.gemmaManager.selectedModelURL == nil ? NomiTheme.warning : NomiTheme.success)
                    .frame(width: 5, height: 5)
                Text(state.gemmaManager.selectedModelURL == nil ? "Gemma 4 · à installer" : "Gemma 4 · local")
                    .font(.nomi(.caption, weight: .semibold))
                    .lineLimit(1)
            }
            .foregroundStyle(NomiTheme.ink2)
            .padding(.horizontal, 10)
            .padding(.vertical, 8)
            .background(NomiTheme.surface2.opacity(0.52), in: Capsule())
        }
        .buttonStyle(.plain)
        .accessibilityLabel("Réglages Gemma 4")
    }

    private var sendButton: some View {
        Button {
            state.sendMessage(input)
            input = ""
        } label: {
            Image(systemName: "arrow.up")
                .font(.nomi(.headline, weight: .bold))
                .foregroundStyle(canSend ? .white : NomiTheme.ink3)
                .frame(width: 36, height: 36)
                .background(canSend ? NomiTheme.accent : NomiTheme.surface2.opacity(0.68), in: Circle())
                .overlay(Circle().stroke(NomiTheme.line.opacity(canSend ? 0 : 0.65), lineWidth: 0.8))
        }
        .buttonStyle(.plain)
        .disabled(!canSend)
        .accessibilityLabel("Envoyer")
    }

    private func insertPrompt(_ prompt: String) {
        let trimmed = input.trimmingCharacters(in: .whitespacesAndNewlines)
        input = trimmed.isEmpty ? prompt : trimmed + " " + prompt
    }

    private var filteredThreads: [ChatThread] {
        let query = threadSearch.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        guard !query.isEmpty else { return state.threads }
        return state.threads.filter { thread in
            thread.title.lowercased().contains(query) || thread.messages.contains { $0.text.lowercased().contains(query) }
        }
    }

    private var threadLibrary: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack {
                Text("Nomi")
                    .font(.system(.title2, design: .serif).weight(.semibold))
                    .foregroundStyle(NomiTheme.ink)
                Spacer()
                Button {
                    closeThreadLibrary()
                } label: {
                    Image(systemName: "xmark")
                        .font(.nomi(.caption, weight: .bold))
                        .foregroundStyle(NomiTheme.ink2)
                        .frame(width: 32, height: 32)
                        .background(NomiTheme.surface2.opacity(0.56), in: Circle())
                }
                .buttonStyle(.plain)
                .accessibilityLabel("Fermer les conversations")
            }
            .padding(.horizontal, 18)
            .padding(.top, 14)
            .padding(.bottom, 13)

            HStack(spacing: 7) {
                Image(systemName: "magnifyingglass")
                    .font(.nomi(.caption, weight: .semibold))
                    .foregroundStyle(NomiTheme.ink3)
                TextField("Rechercher", text: $threadSearch)
                    .textFieldStyle(.plain)
                    .font(.nomi(.subheadline))
                    .foregroundStyle(NomiTheme.ink)
            }
            .padding(.horizontal, 11)
            .padding(.vertical, 9)
            .background(NomiTheme.surface.opacity(0.72), in: RoundedRectangle(cornerRadius: 11, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 11, style: .continuous).stroke(NomiTheme.line.opacity(0.62), lineWidth: 0.7))
            .padding(.horizontal, 16)
            .padding(.bottom, 13)

            Text("RÉCENTS")
                .font(.nomi(.caption2, weight: .bold))
                .tracking(1.0)
                .foregroundStyle(NomiTheme.ink3)
                .padding(.horizontal, 19)
                .padding(.bottom, 6)

            ScrollView {
                LazyVStack(spacing: 2) {
                    ForEach(filteredThreads) { thread in
                        threadRow(thread)
                    }
                    if filteredThreads.isEmpty {
                        Text("Aucune conversation ne correspond à cette recherche.")
                            .font(.nomi(.caption))
                            .foregroundStyle(NomiTheme.ink2)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(.horizontal, 19)
                            .padding(.top, 10)
                    }
                }
                .padding(.horizontal, 8)
            }

            Button {
                state.newThread()
                closeThreadLibrary()
            } label: {
                Label("Nouvelle conversation", systemImage: "plus")
                    .frame(maxWidth: .infinity)
            }
            .buttonStyle(.nomiPrimary)
            .padding(.horizontal, 16)
            .padding(.top, 8)
            .padding(.bottom, 14)
        }
        .frame(width: 318)
        .frame(maxHeight: .infinity)
        .background(NomiTheme.canvas.ignoresSafeArea())
        .overlay(alignment: .trailing) {
            Rectangle()
                .fill(NomiTheme.line.opacity(0.7))
                .frame(width: 0.7)
                .ignoresSafeArea()
        }
    }

    private func threadRow(_ thread: ChatThread) -> some View {
        let selected = thread.id == state.activeThreadID
        return Button {
            state.activeThreadID = thread.id
            closeThreadLibrary()
        } label: {
            HStack(alignment: .center, spacing: 9) {
                Image(systemName: "bubble.left")
                    .font(.nomi(.subheadline))
                    .foregroundStyle(selected ? NomiTheme.accent : NomiTheme.ink3)
                    .frame(width: 20)
                VStack(alignment: .leading, spacing: 2) {
                    Text(thread.title)
                        .font(.nomi(.subheadline, weight: selected ? .bold : .regular))
                        .foregroundStyle(NomiTheme.ink)
                        .lineLimit(1)
                    if let preview = thread.messages.last?.text, !preview.isEmpty {
                        Text(preview)
                            .font(.nomi(.caption))
                            .foregroundStyle(NomiTheme.ink2)
                            .lineLimit(1)
                    }
                }
                Spacer(minLength: 5)
                if selected {
                    Image(systemName: "checkmark")
                        .font(.system(size: 10, weight: .bold))
                        .foregroundStyle(NomiTheme.accent)
                }
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 9)
            .background(selected ? NomiTheme.accentSoft.opacity(0.56) : Color.clear, in: RoundedRectangle(cornerRadius: 10, style: .continuous))
            .contentShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
        }
        .buttonStyle(.plain)
    }

    private func closeThreadLibrary() {
        withAnimation(reduceMotion ? nil : .easeOut(duration: 0.20)) {
            threadLibraryPresented = false
        }
    }
}
