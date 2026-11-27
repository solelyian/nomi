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
        case .today: "sun.max"
        case .board: "checklist"
        case .focus: "timer"
        case .conversation: "bubble.left.and.bubble.right"
        case .routines: "repeat"
        case .transcript: "waveform.and.mic"
        case .library: "rectangle.stack"
        case .settings: "gearshape"
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
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
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
                    .navigationTitle("n.")
                } detail: {
                    NavigationStack(path: $detailPath) {
                        (section ?? .today).view
                            .frame(maxWidth: 920)
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
        .disabled(state.commandPalettePresented)
        .accessibilityHidden(state.commandPalettePresented)
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
        .animation(reduceMotion ? nil : .spring(response: 0.24, dampingFraction: 0.86), value: state.commandPalettePresented)
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
        case .today: "sun.max"
        case .board: "checklist"
        case .focus: "timer"
        case .conversation: "bubble.left.and.bubble.right"
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
                    header
                    principles
                    languagePanel
                }
                .padding(.horizontal, 22)
                .padding(.top, 22)
                .padding(.bottom, 12)
            }
            .safeAreaInset(edge: .bottom, spacing: 0) {
                Button("Commencer", systemImage: "arrow.right") { finish() }
                    .buttonStyle(.nomiPrimary)
                    .controlSize(.large)
                    .frame(maxWidth: .infinity)
                    .padding(.horizontal, 22)
                    .padding(.top, 10)
                    .padding(.bottom, 12)
                    .background(NomiTheme.canvas.opacity(0.92))
            }
            .nomiScreenBackground()
            .nomiTopEdgeProtection()
            .nomiNavigationBarHidden()
        }
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 9) {
            Text("n.")
                .font(.system(size: 20, weight: .bold, design: .serif))
                .foregroundStyle(.white)
                .frame(width: 34, height: 34)
                .background(NomiTheme.accent, in: RoundedRectangle(cornerRadius: 9, style: .continuous))
                .accessibilityHidden(true)
            Text("Bienvenue dans Nomi")
                .font(.system(.largeTitle, design: .serif).weight(.semibold))
                .tracking(-0.8)
                .foregroundStyle(NomiTheme.ink)
            Text("Un espace de commande personnel pour décider, agir et réviser — local d’abord, sans automatisation silencieuse.")
                .font(.subheadline)
                .foregroundStyle(NomiTheme.ink2)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    private var principles: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("PRINCIPES")
                .font(.caption2.weight(.bold))
                .tracking(1.1)
                .foregroundStyle(NomiTheme.ink3)
                .padding(.horizontal, 1)
            VStack(spacing: 0) {
                onboardingRow(index: 0, icon: "command", title: "Commander", detail: "Créer, chercher ou demander depuis une seule entrée.")
                divider
                onboardingRow(index: 1, icon: "checklist", title: "Décider", detail: "Une prochaine action visible, pas un tableau décoratif.")
                divider
                onboardingRow(index: 2, icon: "timer", title: "Se concentrer", detail: "Minuteur explicite, pause contrôlée et notes lisibles.")
                divider
                onboardingRow(index: 3, icon: "lock.shield", title: "Rester local", detail: "Gemma 4 fonctionne sur appareil après téléchargement.")
            }
            .background(NomiTheme.surface.opacity(0.93), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).stroke(NomiTheme.line.opacity(0.82), lineWidth: 0.7))
        }
    }

    private var divider: some View {
        Divider().overlay(NomiTheme.line.opacity(0.6)).padding(.leading, 42)
    }

    private var languagePanel: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("TRANSCRIPTION")
                .font(.caption2.weight(.bold))
                .tracking(1.1)
                .foregroundStyle(NomiTheme.ink3)
            Text("Langue de reconnaissance vocale")
                .font(.subheadline.weight(.medium))
                .foregroundStyle(NomiTheme.ink)
            Picker("Langue de transcription", selection: $state.transcriptionLanguage) {
                Text("Français").tag("fr-FR")
                Text("English").tag("en-US")
            }
            .pickerStyle(.segmented)
            Text("L’interface complète reste francophone dans cette itération ; la langue concerne la reconnaissance vocale.")
                .font(.caption)
                .foregroundStyle(NomiTheme.ink2)
                .fixedSize(horizontal: false, vertical: true)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(12)
        .background(NomiTheme.surface.opacity(0.72), in: RoundedRectangle(cornerRadius: 13, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 13, style: .continuous).stroke(NomiTheme.line.opacity(0.68), lineWidth: 0.7))
    }

    private func onboardingRow(index: Int, icon: String, title: String, detail: String) -> some View {
        HStack(alignment: .top, spacing: 9) {
            Image(systemName: icon)
                .font(.system(size: 12, weight: .medium))
                .foregroundStyle(index == 0 ? NomiTheme.accent : NomiTheme.ink2)
                .frame(width: 24, height: 24)
                .background((index == 0 ? NomiTheme.accentSoft : NomiTheme.surface2.opacity(0.55)), in: RoundedRectangle(cornerRadius: 7, style: .continuous))
            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                    .font(.subheadline.weight(.medium))
                    .foregroundStyle(NomiTheme.ink)
                Text(detail)
                    .font(.caption)
                    .foregroundStyle(NomiTheme.ink2)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Spacer()
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 9)
    }
}
