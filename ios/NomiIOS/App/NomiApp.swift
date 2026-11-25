import SwiftUI

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
                    List(AppSection.allCases, selection: $section) { item in
                        Label(item.title, systemImage: item.icon)
                            .tag(item)
                    }
                    .navigationTitle("Nomi")
                } detail: {
                    NavigationStack {
                        (section ?? .today).view
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

struct RootTabView: View {
    var body: some View {
        TabView {
            NavigationStack { HomeView() }
                .tabItem { Label("Aujourd'hui", systemImage: "sun.max.fill") }
            NavigationStack { BoardView() }
                .tabItem { Label("Tableau", systemImage: "checklist") }
            NavigationStack { FocusView() }
                .tabItem { Label("Focus", systemImage: "timer") }
            NavigationStack { ConversationView() }
                .tabItem { Label("Nomi", systemImage: "bubble.left.and.bubble.right.fill") }
            NavigationStack { MoreView() }
                .tabItem { Label("Plus", systemImage: "ellipsis.circle.fill") }
        }
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
                            .font(.largeTitle.weight(.bold))
                        Text("Votre compagnon de journée pour organiser, vous concentrer, transcrire et réviser.")
                            .foregroundStyle(NomiTheme.ink2)
                    }

                    VStack(alignment: .leading, spacing: 14) {
                        onboardingRow("checklist", "Organisez vos priorités", "Ajoutez des tâches en langage naturel et faites-les progresser.")
                        onboardingRow("timer", "Travaillez en focus", "Minuteur explicite avec pause, notes et historique.")
                        onboardingRow("repeat", "Gardez le contrôle", "Les routines sont visibles, modifiables et jamais exécutées sans action configurée.")
                        onboardingRow("lock.shield", "Local d'abord", "Ce prototype n'envoie aucune donnée vers un service externe. Les réponses IA génératives sont simulées.")
                    }

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
                        .buttonStyle(.borderedProminent)
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
                .font(.title3)
                .foregroundStyle(NomiTheme.accent)
                .frame(width: 32)
            VStack(alignment: .leading, spacing: 3) {
                Text(title).font(.headline)
                Text(detail).font(.subheadline).foregroundStyle(NomiTheme.ink2)
            }
        }
    }
}
