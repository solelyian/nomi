import SwiftUI
import NomiCore

struct ConversationView: View {
    @EnvironmentObject private var state: AppState
    @State private var input = ""

    var body: some View {
        VStack(spacing: 0) {
            threadSelector
            ScrollViewReader { proxy in
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 12) {
                        ForEach(state.activeThread?.messages ?? []) { message in
                            bubble(message)
                        }
                        Color.clear.frame(height: 1).id("conversation-bottom")
                    }
                    .padding()
                }
                .onChange(of: state.activeThread?.messages.count ?? 0) { _ in
                    withAnimation { proxy.scrollTo("conversation-bottom", anchor: .bottom) }
                }
            }
            composer
        }
        .nomiScreenBackground()
        .navigationTitle("Nomi")
    }

    private var threadSelector: some View {
        HStack {
            Menu(state.activeThread?.title ?? "Conversation") {
                ForEach(state.threads) { thread in
                    Button(thread.title) { state.activeThreadID = thread.id }
                }
            }
            .font(.headline)
            Spacer()
            Button("Nouvelle", systemImage: "square.and.pencil") { state.newThread() }
                .buttonStyle(.bordered)
        }
        .padding()
        .background(NomiTheme.surface)
    }

    private func bubble(_ message: ChatMessage) -> some View {
        HStack {
            if message.role == .assistant { Spacer(minLength: 0) }
            VStack(alignment: .leading, spacing: 6) {
                Text(message.text)
                    .font(.body)
                    .foregroundStyle(message.role == .user ? .white : NomiTheme.ink)
                    .textSelection(.enabled)
                if let verification = message.verification {
                    NomiBadge(verification.status == .verified ? "calcul vérifié" : "calcul non vérifié", tint: verification.status == .verified ? NomiTheme.success : NomiTheme.warning)
                }
                Text(message.createdAt.formatted(date: .omitted, time: .shortened))
                    .font(.caption2)
                    .foregroundStyle(message.role == .user ? .white.opacity(0.75) : NomiTheme.ink3)
            }
            .padding(12)
            .background(message.role == .user ? NomiTheme.accent : NomiTheme.surface, in: RoundedRectangle(cornerRadius: 16, style: .continuous))
            .frame(maxWidth: 290, alignment: message.role == .user ? .trailing : .leading)
            if message.role == .user { Spacer(minLength: 0) }
        }
    }

    private var composer: some View {
        VStack(spacing: 8) {
            HStack(alignment: .bottom) {
                TextField("Demander, planifier ou vérifier un calcul…", text: $input, axis: .vertical)
                    .textFieldStyle(.roundedBorder)
                Button("Envoyer", systemImage: "arrow.up.circle.fill") {
                    state.sendMessage(input)
                    input = ""
                }
                .disabled(input.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            }
            Text("Réponses génératives simulées ; les calculs simples sont vérifiés localement par le moteur déterministe.")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
        }
        .padding()
        .background(NomiTheme.surface)
    }
}
