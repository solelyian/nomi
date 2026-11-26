import Combine
import CryptoKit
import Foundation

struct GemmaModelDescriptor: Identifiable, Hashable, Sendable {
    let id: String
    let title: String
    let subtitle: String
    let fileName: String
    let downloadURL: URL
    let expectedBytes: Int64
    let expectedSHA256: String

    static let e2b = GemmaModelDescriptor(
        id: "gemma-4-e2b-it",
        title: "Gemma 4 E2B Instruct",
        subtitle: "Recommandé pour iPhone — équilibre mémoire/qualité",
        fileName: "gemma-4-E2B-it.litertlm",
        downloadURL: URL(string: "https://huggingface.co/litert-community/gemma-4-E2B-it-litert-lm/resolve/main/gemma-4-E2B-it.litertlm")!,
        expectedBytes: 2_588_147_712,
        expectedSHA256: "181938105e0eefd105961417e8da75903eacda102c4fce9ce90f50b97139a63c"
    )

    static let e4b = GemmaModelDescriptor(
        id: "gemma-4-e4b-it",
        title: "Gemma 4 E4B Instruct",
        subtitle: "Meilleure qualité, réservé aux appareils récents avec mémoire suffisante",
        fileName: "gemma-4-E4B-it.litertlm",
        downloadURL: URL(string: "https://huggingface.co/litert-community/gemma-4-E4B-it-litert-lm/resolve/main/gemma-4-E4B-it.litertlm")!,
        expectedBytes: 3_659_530_240,
        expectedSHA256: "0b2a8980ce155fd97673d8e820b4d29d9c7d99b8fa6806f425d969b145bd52e0"
    )

    static let all: [GemmaModelDescriptor] = [.e2b, .e4b]
}

enum GemmaDownloadState: Equatable, Sendable {
    case notDownloaded
    case downloading(Double)
    case paused(Double)
    case downloaded
    case failed(String)

    var progress: Double? {
        switch self {
        case .downloading(let progress), .paused(let progress): progress
        case .downloaded: 1
        default: nil
        }
    }

    var isActive: Bool {
        if case .downloading = self { return true }
        return false
    }

    var label: String {
        switch self {
        case .notDownloaded: "Non téléchargé"
        case .downloading(let progress): "Téléchargement \(Int(progress * 100)) %"
        case .paused(let progress): "En pause \(Int(progress * 100)) %"
        case .downloaded: "Installé localement"
        case .failed: "Erreur de téléchargement"
        }
    }
}

private enum GemmaDownloadValidationError: LocalizedError {
    case httpStatus(Int)
    case unexpectedSize(expected: Int64, actual: Int64)
    case digestMismatch

    var errorDescription: String? {
        switch self {
        case .httpStatus(let status): "Le serveur du modèle a répondu avec l'erreur HTTP \(status)."
        case .unexpectedSize(let expected, let actual): "Le fichier téléchargé ne correspond pas à la taille signée (\(actual) octets au lieu de \(expected))."
        case .digestMismatch: "L'empreinte SHA-256 du modèle ne correspond pas à la version épinglée. Le fichier a été supprimé par sécurité."
        }
    }
}

private final class GemmaDownloadDelegate: NSObject, URLSessionDownloadDelegate, @unchecked Sendable {
    let destination: URL
    let expectedBytes: Int64
    let expectedSHA256: String
    let onProgress: @Sendable (Int64, Int64) -> Void
    let onFinish: @Sendable (Result<URL, Error>) -> Void

    init(destination: URL, expectedBytes: Int64, expectedSHA256: String, onProgress: @escaping @Sendable (Int64, Int64) -> Void, onFinish: @escaping @Sendable (Result<URL, Error>) -> Void) {
        self.destination = destination
        self.expectedBytes = expectedBytes
        self.expectedSHA256 = expectedSHA256
        self.onProgress = onProgress
        self.onFinish = onFinish
    }

    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask, didWriteData bytesWritten: Int64, totalBytesWritten: Int64, totalBytesExpectedToWrite: Int64) {
        onProgress(totalBytesWritten, totalBytesExpectedToWrite)
    }

    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask, didFinishDownloadingTo location: URL) {
        do {
            if let response = downloadTask.response as? HTTPURLResponse, !(200...299).contains(response.statusCode) {
                throw GemmaDownloadValidationError.httpStatus(response.statusCode)
            }
            try FileManager.default.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
            if FileManager.default.fileExists(atPath: destination.path) {
                try FileManager.default.removeItem(at: destination)
            }
            try FileManager.default.moveItem(at: location, to: destination)
            let attributes = try FileManager.default.attributesOfItem(atPath: destination.path)
            let actualBytes = (attributes[.size] as? NSNumber)?.int64Value ?? -1
            guard actualBytes == expectedBytes else {
                try? FileManager.default.removeItem(at: destination)
                throw GemmaDownloadValidationError.unexpectedSize(expected: expectedBytes, actual: actualBytes)
            }
            guard try Self.sha256Hex(of: destination) == expectedSHA256 else {
                try? FileManager.default.removeItem(at: destination)
                throw GemmaDownloadValidationError.digestMismatch
            }
            onFinish(.success(destination))
        } catch {
            onFinish(.failure(error))
        }
    }

    private static func sha256Hex(of url: URL) throws -> String {
        let handle = try FileHandle(forReadingFrom: url)
        defer { try? handle.close() }
        var hasher = SHA256()
        while true {
            let data = try handle.read(upToCount: 1024 * 1024) ?? Data()
            if data.isEmpty { break }
            hasher.update(data: data)
        }
        return hasher.finalize().map { String(format: "%02x", $0) }.joined()
    }

    func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: Error?) {
        if let error {
            onFinish(.failure(error))
        }
    }
}

@MainActor
final class GemmaModelManager: NSObject, ObservableObject {
    @Published private var states: [String: GemmaDownloadState] = [:]
    @Published var selectedModelID: String
    @Published var runtimeNotice: String?

    private var session: URLSession?
    private var downloadTask: URLSessionDownloadTask?
    private var activeDelegate: GemmaDownloadDelegate?
    private var resumeData: Data?
    private var downloadingModelID: String?
    private var lastProgress: Double = 0

    override init() {
        let stored = UserDefaults.standard.string(forKey: "nomi.selectedGemmaModel")
        self.selectedModelID = stored ?? GemmaModelDescriptor.e2b.id
        super.init()
        refreshStates()
    }

    var selectedModel: GemmaModelDescriptor {
        GemmaModelDescriptor.all.first { $0.id == selectedModelID } ?? .e2b
    }

    var selectedModelURL: URL? {
        let url = fileURL(for: selectedModel)
        return FileManager.default.fileExists(atPath: url.path) ? url : nil
    }

    var modelsDirectory: URL {
        URL.applicationSupportDirectory.appendingPathComponent("Nomi/GemmaModels", isDirectory: true)
    }

    func state(for model: GemmaModelDescriptor) -> GemmaDownloadState {
        states[model.id] ?? .notDownloaded
    }

    func select(_ model: GemmaModelDescriptor) {
        selectedModelID = model.id
        UserDefaults.standard.set(model.id, forKey: "nomi.selectedGemmaModel")
    }

    func fileURL(for model: GemmaModelDescriptor) -> URL {
        modelsDirectory.appendingPathComponent(model.fileName)
    }

    func temporaryURL(for model: GemmaModelDescriptor) -> URL {
        modelsDirectory.appendingPathComponent(model.fileName + ".download")
    }

    func refreshStates() {
        for model in GemmaModelDescriptor.all {
            let current = state(for: model)
            if case .downloading = current { continue }
            if case .paused = current { continue }
            if FileManager.default.fileExists(atPath: fileURL(for: model).path) {
                states[model.id] = .downloaded
            } else if downloadingModelID != model.id {
                states[model.id] = .notDownloaded
            }
        }
    }

    func download(_ model: GemmaModelDescriptor) {
        guard downloadTask == nil else { return }
        select(model)
        refreshStates()
        guard state(for: model) != .downloaded else { return }

        let required = model.expectedBytes * 2
        if let capacity = availableCapacity(), capacity < required {
            states[model.id] = .failed("Espace disponible insuffisant. Prévoyez environ \(Self.formattedBytes(required)) pendant l'installation.")
            return
        }

        startDownload(model, resumeData: nil)
    }

    func resumeDownload() {
        guard let model = GemmaModelDescriptor.all.first(where: { $0.id == downloadingModelID }) ?? GemmaModelDescriptor.all.first(where: { $0.id == selectedModelID }) else { return }
        guard let resumeData else { return }
        startDownload(model, resumeData: resumeData)
    }

    func pauseDownload() {
        guard let task = downloadTask, let modelID = downloadingModelID else { return }
        states[modelID] = .paused(lastProgress)
        downloadTask = nil
        task.cancel(byProducingResumeData: { [weak self] data in
            Task { @MainActor in
                self?.resumeData = data
            }
        })
    }

    func cancelDownload() {
        let modelID = downloadingModelID ?? GemmaModelDescriptor.all.first {
            let current = state(for: $0)
            if case .paused = current { return true }
            return current.isActive
        }?.id
        downloadTask?.cancel()
        downloadTask = nil
        downloadingModelID = nil
        resumeData = nil
        if let modelID,
           let model = GemmaModelDescriptor.all.first(where: { $0.id == modelID }) {
            states[modelID] = .notDownloaded
            try? FileManager.default.removeItem(at: temporaryURL(for: model))
        }
        session?.invalidateAndCancel()
        session = nil
    }

    func delete(_ model: GemmaModelDescriptor) {
        if downloadingModelID == model.id { cancelDownload() }
        try? FileManager.default.removeItem(at: fileURL(for: model))
        try? FileManager.default.removeItem(at: temporaryURL(for: model))
        states[model.id] = .notDownloaded
        runtimeNotice = "Le modèle sera rechargé au prochain message."
    }

    private func startDownload(_ model: GemmaModelDescriptor, resumeData data: Data?) {
        let destination = temporaryURL(for: model)
        try? FileManager.default.removeItem(at: destination)
        states[model.id] = .downloading(data == nil ? 0 : lastProgress)
        downloadingModelID = model.id

        let delegate = GemmaDownloadDelegate(destination: destination, expectedBytes: model.expectedBytes, expectedSHA256: model.expectedSHA256) { [weak self] written, expected in
            Task { @MainActor in
                guard let self else { return }
                let total = expected > 0 ? expected : model.expectedBytes
                let progress = min(0.999, Double(written) / Double(total))
                self.lastProgress = progress
                self.states[model.id] = .downloading(progress)
            }
        } onFinish: { [weak self] result in
            Task { @MainActor in
                guard let self else { return }
                switch result {
                case .success:
                    do {
                        let finalURL = self.fileURL(for: model)
                        if FileManager.default.fileExists(atPath: finalURL.path) {
                            try FileManager.default.removeItem(at: finalURL)
                        }
                        try FileManager.default.moveItem(at: self.temporaryURL(for: model), to: finalURL)
                        self.states[model.id] = .downloaded
                        self.runtimeNotice = "Gemma 4 est installé. Son premier chargement peut prendre une dizaine de secondes."
                    } catch {
                        self.states[model.id] = .failed(error.localizedDescription)
                    }
                case .failure(let error):
                    if case .paused = self.states[model.id] {
                        break
                    }
                    if self.downloadingModelID == model.id {
                        self.states[model.id] = .failed(error.localizedDescription)
                    }
                }
                self.downloadTask = nil
                if case .paused = self.states[model.id] {
                    self.session?.finishTasksAndInvalidate()
                    self.session = nil
                } else {
                    self.downloadingModelID = nil
                    self.resumeData = nil
                    self.session?.finishTasksAndInvalidate()
                    self.session = nil
                }
            }
        }

        let configuration = URLSessionConfiguration.default
        configuration.timeoutIntervalForResource = 24 * 3600
        configuration.waitsForConnectivity = true
        let session = URLSession(configuration: configuration, delegate: delegate, delegateQueue: nil)
        self.session = session
        self.activeDelegate = delegate
        let task = data.flatMap { session.downloadTask(withCorrectResumeData: $0) } ?? session.downloadTask(with: model.downloadURL)
        downloadTask = task
        task.resume()
    }

    private func state(forID id: String) -> GemmaDownloadState {
        states[id] ?? .notDownloaded
    }

    private func availableCapacity() -> Int64? {
        let values = try? URL(fileURLWithPath: NSHomeDirectory()).resourceValues(forKeys: [.volumeAvailableCapacityForImportantUsageKey])
        return values?.volumeAvailableCapacityForImportantUsage
    }

    static func formattedBytes(_ bytes: Int64) -> String {
        ByteCountFormatter.string(fromByteCount: bytes, countStyle: .file)
    }
}

private extension URLSession {
    func downloadTask(withCorrectResumeData data: Data) -> URLSessionDownloadTask? {
        downloadTask(withResumeData: data)
    }
}
