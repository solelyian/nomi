import SwiftUI

extension Notification.Name {
    static let nomiCompactTabDidChange = Notification.Name("nomi.compactTabDidChange")
    static let nomiOpenConversation = Notification.Name("nomi.openConversation")
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

enum NomiDetailDestination: Hashable {
    case routines, transcript, library, settings, gemma
    case revisionSheet(UUID)
}

struct RootView: View {
    @EnvironmentObject private var state: AppState
    @Environment(\.horizontalSizeClass) private var horizontalSizeClass
    @Environment(\.scenePhase) private var scenePhase
    @State private var section: AppSection? = .today
    @State private var columnVisibility: NavigationSplitViewVisibility = .doubleColumn
    @State private var detailPath = NavigationPath()
    @AppStorage("nomi.didCompleteOnboarding") private var didCompleteOnboarding = false

    var body: some View {
        Group {
            if horizontalSizeClass == .regular {
                NavigationSplitView(columnVisibility: $columnVisibility) {
                    List(AppSection.allCases, selection: $section) { item in
                        Label(item.title, systemImage: item.icon)
                            .tag(item)
                    }
                    .listStyle(.sidebar)
                    .navigationTitle("Nomi")
                } detail: {
                    NavigationStack(path: $detailPath) {
                        (section ?? .today).view
                            .navigationTitle("")
                            .navigationBarTitleDisplayMode(.inline)
                            .navigationDestination(for: NomiDetailDestination.self) { destination in
                                switch destination {
                                case .routines: RoutinesView()
                                case .transcript: LiveTranscriptionView()
                                case .library: LibraryView()
                                case .settings: SettingsView()
                                case .gemma: GemmaSettingsView()
                                case .revisionSheet(let id):
                                    if let sheet = state.revisionSheets.first(where: { $0.id == id }) {
                                        StoredSheetDetail(sheet: sheet)
                                    } else {
                                        LibraryView()
                                    }
                                }
                            }
                    }
                    .id(section ?? .today)
                    .onChange(of: section) { _, _ in
                        detailPath = NavigationPath()
                    }
                }
            } else {
                RootTabView()
            }
        }
        .nomiTopEdgeProtection()
        .background {
            Button("Ouvrir la commande") { state.commandPalettePresented = true }
                .keyboardShortcut("k", modifiers: [.command])
                .opacity(0)
                .accessibilityHidden(true)
        }
        .overlay {
            if state.commandPalettePresented {
                NomiCommandPalette(isPresented: Binding(
                    get: { state.commandPalettePresented },
                    set: { state.commandPalettePresented = $0 }
                ))
                .transition(.opacity.combined(with: .move(edge: .top)))
                .zIndex(10)
            }
        }
        .animation(.spring(response: 0.24, dampingFraction: 0.86), value: state.commandPalettePresented)
        .onReceive(NotificationCenter.default.publisher(for: .nomiOpenConversation)) { _ in
            section = .conversation
        }
        .fullScreenCover(isPresented: Binding(
            get: { !didCompleteOnboarding },
            set: { _ in }
        )) {
            OnboardingView {
                didCompleteOnboarding = true
            }
        }
        .onChange(of: scenePhase) { _, phase in
            if phase == .background {
                Task { await state.gemmaRuntime.unload() }
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
    @EnvironmentObject private var state: AppState
    @State private var selection: CompactTab = .today

    var body: some View {
        TabView(selection: $selection) {
            ForEach(CompactTab.allCases) { tab in
                NavigationStack {
                    tab.view
                        .navigationTitle("")
                        .navigationBarTitleDisplayMode(.inline)
                        .navigationDestination(for: NomiDetailDestination.self) { destination in
                            switch destination {
                            case .routines: RoutinesView()
                            case .transcript: LiveTranscriptionView()
                            case .library: LibraryView()
                            case .settings: SettingsView()
                            case .gemma: GemmaSettingsView()
                            case .revisionSheet(let id):
                                if let sheet = state.revisionSheets.first(where: { $0.id == id }) {
                                    StoredSheetDetail(sheet: sheet)
                                } else {
                                    LibraryView()
                                }
                            }
                        }
                        .nomiNavigationBarHidden()
                }
                .tabItem {
                    Label(tab.title, systemImage: tab.icon)
                }
                .tag(tab)
            }
        }
        .background(NomiAmbientBackground())
        .onChange(of: selection) { _, newValue in
            NotificationCenter.default.post(name: .nomiCompactTabDidChange, object: newValue)
        }
        .onReceive(NotificationCenter.default.publisher(for: .nomiOpenConversation)) { _ in
            selection = .conversation
        }
    }
}


struct OnboardingView: View {
    @EnvironmentObject private var state: AppState
    let finish: () -> Void

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    VStack(alignment: .leading, spacing: 8) {
                        Image(systemName: "sun.max.fill")
                            .font(.system(size: 44))
                            .foregroundStyle(NomiTheme.accent)
                        Text("Bienvenue dans Nomi")
                            .font(.system(.largeTitle, design: .rounded).weight(.bold))
                        Text("Votre compagnon de journée pour organiser, vous concentrer, transcrire et réviser.")
                            .foregroundStyle(NomiTheme.ink2)
                    }

                    VStack(alignment: .leading, spacing: 12) {
                        onboardingRow("checklist", "Organisez vos priorités", "Ajoutez des tâches et faites-les progresser.")
                        Divider().overlay(NomiTheme.line)
                        onboardingRow("timer", "Travaillez en focus", "Minuteur avec pause, notes et historique.")
                        Divider().overlay(NomiTheme.line)
                        onboardingRow("repeat", "Gardez le contrôle", "Des routines visibles, modifiables et contrôlées.")
                        Divider().overlay(NomiTheme.line)
                        onboardingRow("lock.shield", "Local d'abord", "Gemma 4 fonctionne sur appareil ; vos conversations restent locales.")
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
                }
                .padding(24)
                .padding(.bottom, 12)
            }
            .safeAreaInset(edge: .bottom, spacing: 0) {
                Button("Commencer", systemImage: "arrow.right") { finish() }
                    .buttonStyle(.nomiPrimary)
                    .controlSize(.large)
                    .frame(maxWidth: .infinity)
                    .padding(.horizontal, 24)
                    .padding(.top, 10)
                    .padding(.bottom, 12)
            }
            .nomiScreenBackground()
            .nomiTopEdgeProtection()
            .nomiNavigationBarHidden()
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
