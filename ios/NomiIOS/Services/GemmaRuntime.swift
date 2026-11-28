import Foundation
import LiteRTLM
import NomiCore

enum GemmaRuntimeError: LocalizedError {
    case emptyResponse

    var errorDescription: String? {
        switch self {
        case .emptyResponse: "Gemma 4 n'a produit aucun texte exploitable."
        }
    }
}

/// Runtime local Gemma 4. Toute la génération passe par LiteRT-LM/Metal ;
/// aucun contenu conversationnel n'est envoyé vers un serveur.
actor GemmaRuntime {
    private var engine: Engine?
    private var loadedModelPath: String?
    private var preparation: (modelPath: String, task: Task<Engine, Error>)?

    func prepare(modelURL: URL) async throws {
        let modelPath = modelURL.path
        if loadedModelPath == modelPath, let engine, await engine.isInitialized() { return }

        if let preparation {
            if preparation.modelPath == modelPath {
                let preparedEngine = try await preparation.task.value
                if loadedModelPath != modelPath {
                    engine = preparedEngine
                    loadedModelPath = modelPath
                }
                return
            }
            _ = try? await preparation.task.value
        }
        if loadedModelPath == modelPath, let engine, await engine.isInitialized() { return }

        engine = nil
        loadedModelPath = nil
        let cacheURL = URL.applicationSupportDirectory.appendingPathComponent("Nomi/GemmaCache", isDirectory: true)
        let task = Task<Engine, Error> {
            try FileManager.default.createDirectory(at: cacheURL, withIntermediateDirectories: true)
            var lastError: Error?
            for backend in [Backend.gpu, .cpu()] {
                try Task.checkCancellation()
                do {
                    let config = try EngineConfig(
                        modelPath: modelPath,
                        backend: backend,
                        maxNumTokens: 6144,
                        cacheDir: cacheURL.path,
                        activationDataType: backend == .gpu ? .float16 : nil
                    )
                    let engine = Engine(engineConfig: config)
                    try await engine.initialize()
                    return engine
                } catch {
                    lastError = error
                }
            }
            throw lastError ?? GemmaRuntimeError.emptyResponse
        }
        preparation = (modelPath: modelPath, task: task)
        do {
            let preparedEngine = try await task.value
            if preparation?.modelPath == modelPath { preparation = nil }
            engine = preparedEngine
            loadedModelPath = modelPath
        } catch {
            if preparation?.modelPath == modelPath { preparation = nil }
            throw error
        }
    }

    func unload() {
        preparation?.task.cancel()
        preparation = nil
        engine = nil
        loadedModelPath = nil
    }


    func generateStructuredJSON(prompt: String, schema: [String: Any], modelURL: URL, maxOutputTokens: Int = 1200) async throws -> Data {
        try await prepare(modelURL: modelURL)
        guard let engine else { throw GemmaRuntimeError.emptyResponse }
        let format = try ResponseFormat.json(schema: schema)
        let systemPrompt = "Tu es Nomi, un assistant local francophone. Tu réponds uniquement avec un objet JSON valide conforme au schéma demandé, sans Markdown ni commentaire. Tu ne dois pas inventer de faits absents de la source."
        let sampler = try SamplerConfig(topK: 32, topP: 0.9, temperature: 0.25)
        let config = ConversationConfig(
            systemMessage: Message(systemPrompt, role: .system),
            samplerConfig: sampler,
            automaticToolCalling: false,
            enableResponseFormat: true
        )
        let conversation = try await engine.createConversation(with: config)
        let response = try await conversation.sendMessage(
            Message(prompt),
            maxOutputTokens: maxOutputTokens,
            responseFormat: format
        ).toString
        guard let data = response.data(using: .utf8) else { throw GemmaRuntimeError.emptyResponse }
        return data
    }

    /// Génère un plan de calcul CONTRAINT — deterministic_calculation_system.md section 5.
    /// Le modèle ne calcule jamais : il produit uniquement des expressions dans la
    /// grammaire du noyau, avec les nombres de la demande, et une réponse à marqueurs.
    func generateCalculationPlan(userText: String, contextNumbers: String, modelURL: URL) async throws -> String {
        let schema: [String: Any] = [
            "type": "object",
            "properties": [
                "calculation": ["type": "boolean"],
                "steps": [
                    "type": "array",
                    "items": [
                        "type": "object",
                        "properties": [
                            "id": ["type": "string"],
                            "expr": ["type": "string"],
                            "unit": ["type": "string"],
                        ],
                        "required": ["id", "expr"],
                    ] as [String: Any],
                ] as [String: Any],
                "answer": ["type": "string"],
            ],
            "required": ["calculation", "steps", "answer"],
        ]
        let prompt = """
        Demande de l'utilisateur : « \(userText) »

        Si cette demande exige un calcul, produis un plan JSON. Règles strictes :
        - Tu ne calcules JAMAIS le résultat. Tu écris seulement les expressions.
        - Chaque expression utilise uniquement les opérateurs + - * / ^ % ( ) et des nombres PRÉSENTS dans la demande (ou les constantes usuelles : comptages, 24, 60, 365…).
        - Jamais de multiplication implicite : écris 80*15/100, pas 80(15/100).
        - Les identifiants (id) sont des mots simples en minuscules ; une étape peut réutiliser l'identifiant d'une étape précédente dans son expression.
        - « answer » est ta réponse en français, SANS AUCUN CHIFFRE sauf les marqueurs {id} là où les résultats seront insérés mécaniquement.
        - Si la demande n'exige aucun calcul, réponds {"calculation": false, "steps": [], "answer": ""}.
        Nombres extraits de la demande (référence) : \(contextNumbers.isEmpty ? "aucun" : contextNumbers)
        """
        let data = try await generateStructuredJSON(prompt: prompt, schema: schema, modelURL: modelURL, maxOutputTokens: 700)
        return String(data: data, encoding: .utf8) ?? ""
    }

    func generateResponse(userText: String, history: [ChatMessage], productivityContext: String, modelURL: URL, onPartial: @escaping @MainActor @Sendable (String) -> Void) async throws -> String {
        try await prepare(modelURL: modelURL)
        guard let engine else { throw GemmaRuntimeError.emptyResponse }

        let systemPrompt = """
        Tu es Nomi, un compagnon de productivité et d'apprentissage francophone fonctionnant localement sur iPhone. Réponds de façon concise, concrète et honnête. Ne prétends jamais avoir exécuté une action que tu n'as pas réellement effectuée. Quand une demande nécessite une notification, un document, un réseau ou une intégration absente, dis explicitement la limite et propose l'action manuelle la plus utile. Pour les calculs, ne remplace pas le vérificateur déterministe. Contexte local actuel : \(productivityContext)
        """

        let initialMessages = history.suffix(10).map { message in
            Message(message.text, role: message.role == .user ? .user : .model)
        }
        let sampler = try SamplerConfig(topK: 40, topP: 0.95, temperature: 0.65)
        let conversationConfig = ConversationConfig(
            systemMessage: Message(systemPrompt, role: .system),
            initialMessages: Array(initialMessages),
            samplerConfig: sampler,
            automaticToolCalling: false
        )
        let conversation = try await engine.createConversation(with: conversationConfig)
        var accumulated = ""
        for try await chunk in conversation.sendMessageStream(Message(userText), maxOutputTokens: 512) {
            let piece = chunk.toString
            guard !piece.isEmpty else { continue }
            accumulated += piece
            let partial = accumulated.trimmingCharacters(in: .whitespacesAndNewlines)
            if !partial.isEmpty {
                await onPartial(partial)
            }
        }
        let cleaned = accumulated.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleaned.isEmpty else { throw GemmaRuntimeError.emptyResponse }
        return cleaned
    }
}
