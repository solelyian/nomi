import SwiftUI

@main
struct NomiApp: App {
    @StateObject private var state = AppState()

    var body: some Scene {
        WindowGroup {
            RootTabView()
                .environmentObject(state)
                .tint(NomiTheme.accent)
        }
    }
}

struct RootTabView: View {
    var body: some View {
        TabView {
            NavigationStack {
                HomeView()
            }
            .tabItem { Label("Accueil", systemImage: "house.fill") }

            NavigationStack {
                CodeReviewView()
            }
            .tabItem { Label("Code", systemImage: "chevron.left.forwardslash.chevron.right") }

            NavigationStack {
                LiveTranscriptionView()
            }
            .tabItem { Label("Cours", systemImage: "waveform.and.mic") }

            NavigationStack {
                LibraryView()
            }
            .tabItem { Label("Fiches", systemImage: "rectangle.stack.fill") }

            NavigationStack {
                SettingsView()
            }
            .tabItem { Label("Réglages", systemImage: "gearshape.fill") }
        }
    }
}
