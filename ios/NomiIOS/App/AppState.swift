import Foundation
import NomiCore

@MainActor
final class AppState: ObservableObject {
    @Published var revisionSheets: [RevisionSheet]
    @Published var preferredDetailLevel: RevisionDetailLevel = .standard
    @Published var preferredFormat: RevisionFormat = .sheet
    @Published var allowRemoteAI = false
    @Published var transcriptionLanguage = "fr-FR"

    let codeReviewService: any CodeReviewService
    let revisionService: any RevisionService
    let demoTranscriptionService: any LiveTranscriptionService

    init(
        codeReviewService: any CodeReviewService = MockCodeReviewService(),
        revisionService: any RevisionService = MockRevisionService(),
        demoTranscriptionService: any LiveTranscriptionService = MockLiveTranscriptionService()
    ) {
        self.codeReviewService = codeReviewService
        self.revisionService = revisionService
        self.demoTranscriptionService = demoTranscriptionService
        self.revisionSheets = []
    }

    func addSheet(_ sheet: RevisionSheet) {
        revisionSheets.insert(sheet, at: 0)
    }
}
