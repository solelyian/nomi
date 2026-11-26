import SwiftUI

extension Notification.Name {
    static let nomiCompactTabDidChange = Notification.Name("nomi.compactTabDidChange")
}

@main
struct NomiApp: App {
    @StateObject private var state = AppState()

    var body: some Scene {
        WindowGroup {
            RootView()
                .environmentObject(state)
                .tint(NomiTheme.accent)
        }
    }
}

enum AppSection: String, CaseIterable, Identifiable {
    case today, board, focus, conversation, routines, transcript, library, settings

    var id: String { rawValue }

    var title: String {
        switch self {
        case .today: "Aujourd'hui"
        case .board: "Tableau"
        case .focus: "Focus"
        case .conversation: "Nomi"
        case .routines: "Routines"
        case .transcript: "Transcription"
        case .library: "Fiches"
        case .settings: "Réglages"
        }
    }

    var icon: String {
        switch self {
        case .today: "sun.max.fill"
        case .board: "checklist"
        case .focus: "timer"
        case .conversation: "bubble.left.and.bubble.right.fill"
        case .routines: "repeat"
        case .transcript: "waveform.and.mic"
        case .library: "rectangle.stack.fill"
        case .settings: "gearshape.fill"
        }
    }

    @ViewBuilder
    var view: some View {
        switch self {
        case .today: HomeView()
        case .board: BoardView()
        case .focus: FocusView()
        case .conversation: ConversationView()
        case .routines: RoutinesView()
        case .transcript: LiveTranscriptionView()
        case .library: LibraryView()
        case .settings: SettingsView()
        }
    }
}

struct RootView: View {
    @Environment(\.horizontalSizeClass) private var horizontalSizeClass
    @State private var section: AppSection? = .today
    @AppStorage("nomi.didCompleteOnboarding") private var didCompleteOnboarding = false

    var body: some View {
        Group {
            if horizontalSizeClass == .regular {
                NavigationSplitView {
                    ScrollView {
                        VStack(alignment: .leading, spacing: 6) {
                            Text("Nomi")
                                .font(.system(.largeTitle, design: .rounded).weight(.bold))
                                .foregroundStyle(NomiTheme.ink)
                                .padding(.horizontal, 12)
                                .padding(.bottom, 12)
                            ForEach(AppSection.allCases) { item in
                                Button {
                                    section = item
                                } label: {
                                    Label(item.title, systemImage: item.icon)
                                        .font(.body.weight((section ?? .today) == item ? .bold : .medium))
                                        .foregroundStyle((section ?? .today) == item ? NomiTheme.accent : NomiTheme.ink2)
                                        .frame(maxWidth: .infinity, alignment: .leading)
                                        .padding(.horizontal, 13)
                                        .padding(.vertical, 11)
                                        .background((section ?? .today) == item ? NomiTheme.surface.opacity(0.72) : Color.clear, in: RoundedRectangle(cornerRadius: 17, style: .continuous))
                                        .overlay {
                                            if (section ?? .today) == item {
                                                RoundedRectangle(cornerRadius: 17, style: .continuous)
                                                    .strokeBorder(NomiTheme.glassStroke.opacity(0.62), lineWidth: 1)
                                            }
                                        }
                                }
                                .buttonStyle(.plain)
                                .accessibilityAddTraits((section ?? .today) == item ? .isSelected : [])
                            }
                        }
                        .padding(14)
                    }
                    .background(NomiAmbientBackground())
                    .toolbar(.hidden, for: .navigationBar)
                } detail: {
                    NavigationStack {
                        (section ?? .today).view
                            .navigationTitle("")
                            .navigationBarTitleDisplayMode(.inline)
                            .toolbarBackground(.thinMaterial, for: .navigationBar)
                            .toolbarBackground(.visible, for: .navigationBar)
                            .id(section ?? .today)
                    }
                }
            } else {
                RootTabView()
            }
        }
        .fullScreenCover(isPresented: Binding(
            get: { !didCompleteOnboarding },
            set: { _ in }
        )) {
            OnboardingView {
                didCompleteOnboarding = true
            }
        }
    }
}

enum CompactTab: String, CaseIterable, Identifiable {
    case today, board, focus, conversation, more

    var id: String { rawValue }

    var title: String {
        switch self {
        case .today: "Aujourd'hui"
        case .board: "Tableau"
        case .focus: "Focus"
        case .conversation: "Nomi"
        case .more: "Plus"
        }
    }

    var icon: String {
        switch self {
        case .today: "sun.max.fill"
        case .board: "checklist"
        case .focus: "timer"
        case .conversation: "bubble.left.and.bubble.right.fill"
        case .more: "ellipsis"
        }
    }

    @ViewBuilder
    var view: some View {
        switch self {
        case .today: HomeView()
        case .board: BoardView()
        case .focus: FocusView()
        case .conversation: ConversationView()
        case .more: MoreView()
        }
    }
}

struct RootTabView: View {
    @State private var selection: CompactTab = .today

    var body: some View {
        VStack(spacing: 0) {
            ZStack {
                ForEach(CompactTab.allCases) { tab in
                    NavigationStack {
                        tab.view
                            .navigationTitle("")
                            .navigationBarTitleDisplayMode(.inline)
                            .toolbarBackground(.thinMaterial, for: .navigationBar)
                            .toolbarBackground(.visible, for: .navigationBar)
                    }
                    .opacity(selection == tab ? 1 : 0)
                    .allowsHitTesting(selection == tab)
                    .accessibilityHidden(selection != tab)
                    .zIndex(selection == tab ? 1 : 0)
                }
            }
            NomiTabBar(selection: $selection)
        }
        .background(NomiAmbientBackground())
        .onChange(of: selection) { newValue in
            NotificationCenter.default.post(name: .nomiCompactTabDidChange, object: newValue)
        }
    }
}

struct NomiTabBar: View {
    @Binding var selection: CompactTab

    var body: some View {
        HStack(spacing: 0) {
            ForEach(CompactTab.allCases) { tab in
                Button {
                    withAnimation(NomiTheme.morphAnimation) { selection = tab }
                } label: {
                    VStack(spacing: 3) {
                        Image(systemName: tab.icon)
                            .font(.system(size: 17, weight: .semibold))
                            .frame(width: 34, height: 27)
                            .background(selection == tab ? NomiTheme.accent.opacity(0.15) : Color.clear, in: RoundedRectangle(cornerRadius: 13, style: .continuous))
                        Text(tab.title)
                            .font(.caption2.weight(selection == tab ? .bold : .medium))
                    }
                    .foregroundStyle(selection == tab ? NomiTheme.accent : NomiTheme.ink2)
                    .frame(maxWidth: .infinity)
                    .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .accessibilityLabel(tab.title)
                .accessibilityAddTraits(selection == tab ? .isSelected : [])
            }
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 8)
        .background {
            ZStack {
                RoundedRectangle(cornerRadius: 28, style: .continuous).fill(.thinMaterial)
                RoundedRectangle(cornerRadius: 28, style: .continuous).fill(NomiTheme.surface.opacity(0.45))
            }
        }
        .overlay(RoundedRectangle(cornerRadius: 28, style: .continuous).strokeBorder(NomiTheme.glassStroke.opacity(0.72), lineWidth: 1))
        .shadow(color: .black.opacity(0.12), radius: 24, y: 10)
        .padding(.horizontal, 18)
        .padding(.bottom, 8)
    }
}


struct OnboardingView: View {
    @EnvironmentObject private var state: AppState
    let finish: () -> Void

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 22) {
                    VStack(alignment: .leading, spacing: 8) {
                        Image(systemName: "sun.max.fill")
                            .font(.system(size: 44))
                            .foregroundStyle(NomiTheme.accent)
                        Text("Bienvenue dans Nomi")
                            .font(.system(.largeTitle, design: .rounded).weight(.bold))
                        Text("Votre compagnon de journée pour organiser, vous concentrer, transcrire et réviser.")
                            .foregroundStyle(NomiTheme.ink2)
                    }

                    VStack(alignment: .leading, spacing: 15) {
                        onboardingRow("checklist", "Organisez vos priorités", "Ajoutez des tâches en langage naturel et faites-les progresser.")
                        Divider().overlay(NomiTheme.line)
                        onboardingRow("timer", "Travaillez en focus", "Minuteur explicite avec pause, notes et historique.")
                        Divider().overlay(NomiTheme.line)
                        onboardingRow("repeat", "Gardez le contrôle", "Les routines sont visibles, modifiables et jamais exécutées sans action configurée.")
                        Divider().overlay(NomiTheme.line)
                        onboardingRow("lock.shield", "Local d'abord", "Ce prototype n'envoie aucune donnée vers un service externe. Les réponses IA génératives sont simulées.")
                    }
                    .nomiCard(tint: NomiTheme.accent)

                    VStack(alignment: .leading, spacing: 8) {
                        Text("Langue de transcription")
                            .font(.headline)
                        Picker("Langue de transcription", selection: $state.transcriptionLanguage) {
                            Text("Français").tag("fr-FR")
                            Text("English").tag("en-US")
                        }
                        .pickerStyle(.segmented)
                        Text("Le choix concerne la reconnaissance vocale ; l'interface complète reste francophone dans cette itération.")
                            .font(.caption)
                            .foregroundStyle(NomiTheme.ink2)
                    }
                    .nomiCard(tint: NomiTheme.course)

                    Button("Commencer", systemImage: "arrow.right") { finish() }
                        .buttonStyle(.nomiPrimary)
                        .controlSize(.large)
                        .frame(maxWidth: .infinity)
                }
                .padding(24)
            }
            .nomiScreenBackground()
        }
    }

    private func onboardingRow(_ icon: String, _ title: String, _ detail: String) -> some View {
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: icon)
                .font(.headline.weight(.bold))
                .foregroundStyle(NomiTheme.accent)
                .frame(width: 40, height: 40)
                .background(NomiTheme.accent.opacity(0.11), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
            VStack(alignment: .leading, spacing: 3) {
                Text(title).font(.headline)
                Text(detail).font(.subheadline).foregroundStyle(NomiTheme.ink2)
            }
        }
    }
}
