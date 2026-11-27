import SwiftUI
import NomiCore

struct NomiCommandPalette: View {
    @EnvironmentObject private var state: AppState
    @Binding var isPresented: Bool
    @State private var query = ""
    @FocusState private var focused: Bool

    private var trimmedQuery: String {
        query.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private var nextTask: WorkTask? {
        state.openTasks.sorted(by: { $0.priority > $1.priority }).first
    }

    var body: some View {
        ZStack(alignment: .top) {
            Color.black.opacity(0.28)
                .ignoresSafeArea()
                .onTapGesture { isPresented = false }

            VStack(spacing: 0) {
                HStack(spacing: 10) {
                    Text("n.")
                        .font(.system(size: 13, weight: .bold, design: .serif))
                        .foregroundStyle(.white)
                        .frame(width: 24, height: 24)
                        .background(NomiTheme.accent, in: RoundedRectangle(cornerRadius: 7, style: .continuous))
                    TextField("Créer, chercher ou demander…", text: $query)
                        .textFieldStyle(.plain)
                        .font(.nomi(.subheadline))
                        .foregroundStyle(NomiTheme.ink)
                        .focused($focused)
                        .submitLabel(.go)
                        .onSubmit(startRelevantFocus)
                    Button { isPresented = false } label: {
                        Image(systemName: "xmark")
                            .font(.system(size: 11, weight: .bold))
                            .foregroundStyle(NomiTheme.ink3)
                            .frame(width: 28, height: 28)
                            .background(NomiTheme.surface2.opacity(0.5), in: Circle())
                    }
                    .buttonStyle(.plain)
                    .keyboardShortcut(.escape, modifiers: [])
                    .accessibilityLabel("Fermer la commande")
                }
                .padding(.horizontal, 12)
                .frame(height: 52)

                Divider().overlay(NomiTheme.line.opacity(0.7))

                commandRow(
                    icon: "timer",
                    title: "Démarrer un focus",
                    detail: state.focusSession == nil ? focusDetail : "Un focus est déjà actif",
                    key: "↵",
                    enabled: focusObjective != nil && state.focusSession == nil,
                    selected: true,
                    action: startRelevantFocus
                )
                commandRow(
                    icon: "checklist",
                    title: "Créer une tâche",
                    detail: trimmedQuery.isEmpty ? "Écrire l’intention dans le champ" : "« \(trimmedQuery) » sera interprétée localement",
                    key: "T",
                    enabled: !trimmedQuery.isEmpty,
                    selected: false,
                    action: createTask
                )
                commandRow(
                    icon: "bubble.left.and.bubble.right",
                    title: "Demander à Nomi",
                    detail: state.isGeneratingReply ? "Une réponse est déjà en cours" : (state.gemmaManager.selectedModelURL == nil ? "Gemma non installé · repli local limité" : "Gemma 4 local · aucun envoi réseau"),
                    key: "N",
                    enabled: !trimmedQuery.isEmpty && !state.isGeneratingReply,
                    selected: false,
                    action: askNomi
                )

                if !matchingTasks.isEmpty {
                    Divider().overlay(NomiTheme.line.opacity(0.45))
                    ForEach(matchingTasks) { task in
                        commandRow(
                            icon: "circle",
                            title: task.title,
                            detail: "Tâche existante · la marquer en cours",
                            key: "•",
                            enabled: true,
                            selected: false
                        ) {
                            state.moveTask(task, to: .doing)
                            isPresented = false
                        }
                    }
                }

                HStack {
                    Text(resultCount == 1 ? "1 résultat" : "\(resultCount) résultats")
                    Spacer()
                    Text("Échap fermer · ↵ exécuter")
                }
                .font(.caption2.monospaced())
                .foregroundStyle(NomiTheme.ink3)
                .padding(.horizontal, 12)
                .frame(height: 30)
                .background(NomiTheme.surface2.opacity(0.35))
            }
            .background(NomiTheme.surface.opacity(0.98), in: RoundedRectangle(cornerRadius: 18, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).stroke(NomiTheme.line.opacity(0.85), lineWidth: 0.8))
            .shadow(color: .black.opacity(0.28), radius: 28, y: 16)
            .padding(.horizontal, 14)
            .padding(.top, 82)
        }
        .onAppear { focused = true }
    }

    private var focusObjective: String? {
        if !trimmedQuery.isEmpty { return trimmedQuery }
        return nextTask?.title
    }

    private var focusDetail: String {
        if let objective = focusObjective {
            return objective + (nextTask?.estimateMinutes.map { " · \($0) min" } ?? "")
        }
        return "Écrire un objectif ou créer d’abord une tâche"
    }

    private var matchingTasks: [WorkTask] {
        guard !trimmedQuery.isEmpty else { return [] }
        return Array(state.openTasks.filter { $0.title.localizedCaseInsensitiveContains(trimmedQuery) }.prefix(3))
    }

    private var resultCount: Int { 3 + matchingTasks.count }

    private func startRelevantFocus() {
        guard let objective = focusObjective, state.focusSession == nil else { return }
        state.startFocus(objective: objective)
        isPresented = false
    }

    private func createTask() {
        guard !trimmedQuery.isEmpty else { return }
        state.addTask(from: trimmedQuery)
        isPresented = false
    }

    private func askNomi() {
        guard !trimmedQuery.isEmpty, !state.isGeneratingReply else { return }
        state.sendMessage(trimmedQuery)
        NotificationCenter.default.post(name: .nomiOpenConversation, object: nil)
        isPresented = false
    }

    private func commandRow(icon: String, title: String, detail: String, key: String, enabled: Bool, selected: Bool, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            HStack(spacing: 9) {
                Image(systemName: icon)
                    .font(.system(size: 11, weight: .medium))
                    .foregroundStyle(selected ? NomiTheme.accent : NomiTheme.ink2)
                    .frame(width: 23, height: 23)
                    .background(selected ? NomiTheme.accentSoft : NomiTheme.surface2.opacity(0.5), in: RoundedRectangle(cornerRadius: 7, style: .continuous))
                VStack(alignment: .leading, spacing: 2) {
                    Text(title)
                        .font(.nomi(.subheadline, weight: .medium))
                        .foregroundStyle(NomiTheme.ink)
                        .lineLimit(1)
                    Text(detail)
                        .font(.nomi(.caption2))
                        .foregroundStyle(NomiTheme.ink3)
                        .lineLimit(1)
                }
                Spacer(minLength: 8)
                Text(key)
                    .font(.caption2.monospaced())
                    .foregroundStyle(NomiTheme.ink2)
                    .padding(.horizontal, 5)
                    .padding(.vertical, 3)
                    .background(NomiTheme.surface2.opacity(0.5), in: RoundedRectangle(cornerRadius: 4, style: .continuous))
                    .overlay(RoundedRectangle(cornerRadius: 4, style: .continuous).stroke(NomiTheme.line.opacity(0.65), lineWidth: 0.6))
            }
            .padding(.horizontal, 10)
            .frame(minHeight: 45)
            .background(selected ? NomiTheme.accent.opacity(0.065) : .clear)
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
        .opacity(enabled ? 1 : 0.48)
    }
}
