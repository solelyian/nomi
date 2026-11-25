import SwiftUI

struct MoreView: View {
    var body: some View {
        List {
            Section("Productivité") {
                NavigationLink {
                    RoutinesView()
                } label: {
                    Label("Routines", systemImage: "repeat")
                }
                NavigationLink {
                    LiveTranscriptionView()
                } label: {
                    Label("Transcription", systemImage: "waveform.and.mic")
                }
                NavigationLink {
                    LibraryView()
                } label: {
                    Label("Fiches et révisions", systemImage: "rectangle.stack")
                }
            }
            Section("Application") {
                NavigationLink {
                    SettingsView()
                } label: {
                    Label("Réglages", systemImage: "gearshape")
                }
            }
        }
        .scrollContentBackground(.hidden)
        .nomiScreenBackground()
        .navigationTitle("Plus")
    }
}
