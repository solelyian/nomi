import Foundation

public enum WorkColumn: String, Codable, CaseIterable, Sendable {
    case todo, doing, done

    public var frenchTitle: String {
        switch self {
        case .todo: "À faire"
        case .doing: "En cours"
        case .done: "Terminé"
        }
    }
}

public enum WorkPriority: String, Codable, CaseIterable, Sendable, Comparable {
    case low, normal, high, urgent

    public static func < (lhs: WorkPriority, rhs: WorkPriority) -> Bool {
        lhs.rank < rhs.rank
    }

    private var rank: Int {
        switch self {
        case .low: 0
        case .normal: 1
        case .high: 2
        case .urgent: 3
        }
    }

    public var frenchTitle: String {
        switch self {
        case .low: "Basse"
        case .normal: "Normale"
        case .high: "Haute"
        case .urgent: "Urgente"
        }
    }
}

public struct WorkTask: Identifiable, Codable, Sendable, Equatable {
    public var id: UUID
    public var title: String
    public var notes: String
    public var column: WorkColumn
    public var priority: WorkPriority
    public var dueDate: Date?
    public var estimateMinutes: Int?
    public var workedMinutes: Int
    public var createdAt: Date
    public var completedAt: Date?
    public var origin: String

    public init(id: UUID = UUID(), title: String, notes: String = "", column: WorkColumn = .todo, priority: WorkPriority = .normal, dueDate: Date? = nil, estimateMinutes: Int? = nil, workedMinutes: Int = 0, createdAt: Date = .now, completedAt: Date? = nil, origin: String = "manual") {
        self.id = id
        self.title = title
        self.notes = notes
        self.column = column
        self.priority = priority
        self.dueDate = dueDate
        self.estimateMinutes = estimateMinutes
        self.workedMinutes = workedMinutes
        self.createdAt = createdAt
        self.completedAt = completedAt
        self.origin = origin
    }
}

public struct AgendaEvent: Identifiable, Codable, Sendable, Equatable {
    public var id: UUID
    public var title: String
    public var start: Date
    public var end: Date
    public var location: String?

    public init(id: UUID = UUID(), title: String, start: Date, end: Date, location: String? = nil) {
        self.id = id
        self.title = title
        self.start = start
        self.end = end
        self.location = location
    }
}

public enum RoutineTrigger: String, Codable, CaseIterable, Sendable {
    case manual, time, taskDone, focusEnd

    public var frenchTitle: String {
        switch self {
        case .manual: "Manuel"
        case .time: "Heure planifiée"
        case .taskDone: "Tâche terminée"
        case .focusEnd: "Fin de focus"
        }
    }
}

public struct NomiRoutine: Identifiable, Codable, Sendable, Equatable {
    public var id: UUID
    public var name: String
    public var action: String
    public var trigger: RoutineTrigger
    public var hour: Int
    public var minute: Int
    public var weekdays: Set<Int>
    public var enabled: Bool
    public var documentTitle: String?
    public var lastRun: Date?
    public var snoozedUntil: Date?

    public init(id: UUID = UUID(), name: String, action: String, trigger: RoutineTrigger = .time, hour: Int = 9, minute: Int = 0, weekdays: Set<Int> = [2, 3, 4, 5, 6], enabled: Bool = true, documentTitle: String? = nil, lastRun: Date? = nil, snoozedUntil: Date? = nil) {
        self.id = id
        self.name = name
        self.action = action
        self.trigger = trigger
        self.hour = hour
        self.minute = minute
        self.weekdays = weekdays
        self.enabled = enabled
        self.documentTitle = documentTitle
        self.lastRun = lastRun
        self.snoozedUntil = snoozedUntil
    }

    public func isDue(at date: Date, calendar: Calendar = .current) -> Bool {
        guard enabled, trigger == .time else { return false }
        if let snoozedUntil, snoozedUntil > date { return false }
        if let lastRun, calendar.isDate(lastRun, inSameDayAs: date) { return false }
        let weekday = calendar.component(.weekday, from: date)
        guard weekdays.contains(weekday) else { return false }
        let nowMinutes = calendar.component(.hour, from: date) * 60 + calendar.component(.minute, from: date)
        return nowMinutes >= hour * 60 + minute
    }
}

public enum FocusStatus: String, Codable, Sendable {
    case idle, running, paused, finished

    public var frenchTitle: String {
        switch self {
        case .idle: "Inactif"
        case .running: "En cours"
        case .paused: "En pause"
        case .finished: "Terminé"
        }
    }
}

public struct FocusSession: Identifiable, Codable, Sendable, Equatable {
    public var id: UUID
    public var objective: String
    public var startedAt: Date
    public var endedAt: Date?
    public var pausedSeconds: TimeInterval
    public var pausedAt: Date?
    public var notes: String
    public var status: FocusStatus

    public init(id: UUID = UUID(), objective: String, startedAt: Date = .now, endedAt: Date? = nil, pausedSeconds: TimeInterval = 0, pausedAt: Date? = nil, notes: String = "", status: FocusStatus = .running) {
        self.id = id
        self.objective = objective
        self.startedAt = startedAt
        self.endedAt = endedAt
        self.pausedSeconds = pausedSeconds
        self.pausedAt = pausedAt
        self.notes = notes
        self.status = status
    }
}

public struct ChatMessage: Identifiable, Codable, Sendable, Equatable {
    public enum Role: String, Codable, Sendable {
        case user, assistant
    }

    public var id: UUID
    public var role: Role
    public var text: String
    public var createdAt: Date
    public var attachmentTitles: [String]
    public var verification: CalculationVerification?

    public init(id: UUID = UUID(), role: Role, text: String, createdAt: Date = .now, attachmentTitles: [String] = [], verification: CalculationVerification? = nil) {
        self.id = id
        self.role = role
        self.text = text
        self.createdAt = createdAt
        self.attachmentTitles = attachmentTitles
        self.verification = verification
    }
}

public struct ChatThread: Identifiable, Codable, Sendable, Equatable {
    public var id: UUID
    public var title: String
    public var createdAt: Date
    public var messages: [ChatMessage]

    public init(id: UUID = UUID(), title: String, createdAt: Date = .now, messages: [ChatMessage] = []) {
        self.id = id
        self.title = title
        self.createdAt = createdAt
        self.messages = messages
    }
}

public enum TaskPhraseParser {
    public static func parse(_ input: String, now: Date = .now, calendar: Calendar = .current) -> WorkTask {
        var title = input.trimmingCharacters(in: .whitespacesAndNewlines)
        var dueDate: Date?
        var priority: WorkPriority = .normal
        var estimate: Int?
        let lower = title.lowercased()
        if lower.contains("urgent") { priority = .urgent }
        else if lower.contains("important") { priority = .high }
        title = title.replacingOccurrences(of: "urgente?", with: "", options: [.regularExpression, .caseInsensitive])
        title = title.replacingOccurrences(of: "importante?", with: "", options: [.regularExpression, .caseInsensitive])

        if lower.contains("demain") {
            dueDate = calendar.date(byAdding: .day, value: 1, to: calendar.startOfDay(for: now))
            title = title.replacingOccurrences(of: "demain", with: "", options: .caseInsensitive)
        } else if lower.range(of: "aujourd['’]hui", options: .regularExpression) != nil || lower.contains("ce soir") {
            dueDate = calendar.startOfDay(for: now)
            title = title.replacingOccurrences(of: "aujourd['’]hui", with: "", options: [.regularExpression, .caseInsensitive])
            title = title.replacingOccurrences(of: "ce soir", with: "", options: .caseInsensitive)
        }
        if let range = title.range(of: #"\b(\d+(?:[,.]\d+)?)\s*(min|minutes|h|heure|heures)\b"#, options: [.regularExpression, .caseInsensitive]) {
            let token = String(title[range])
            let normalized = token.replacingOccurrences(of: ",", with: ".")
            if let numberRange = normalized.range(of: #"\d+(?:\.\d+)?"#, options: .regularExpression),
               let value = Double(normalized[numberRange]) {
                let isHour = token.lowercased().range(of: #"h|heure"#, options: .regularExpression) != nil
                let minutes = (isHour ? value * 60 : value).rounded()
                if minutes.isFinite && minutes >= 0 && minutes <= 1_000_000 {
                    estimate = Int(minutes)
                }
            }
            title.removeSubrange(range)
        }
        title = title.replacingOccurrences(of: "  ", with: " ").trimmingCharacters(in: CharacterSet(charactersIn: " .,-"))
        return WorkTask(title: title.isEmpty ? input : title, priority: priority, dueDate: dueDate, estimateMinutes: estimate)
    }
}

public enum RoutinePhraseParser {
    public static func parse(_ input: String) -> NomiRoutine {
        var text = input.trimmingCharacters(in: .whitespacesAndNewlines)
        let lower = text.lowercased()
        var weekdays: Set<Int> = [2, 3, 4, 5, 6]
        var hour = 9
        var minute = 0
        var trigger: RoutineTrigger = .manual

        if lower.contains("week-end") || lower.contains("weekend") {
            weekdays = [1, 7]
            trigger = .time
        } else if lower.contains("chaque jour") || lower.contains("tous les jours") || lower.contains("quotidien") {
            weekdays = [1, 2, 3, 4, 5, 6, 7]
            trigger = .time
        } else if lower.range(of: #"\bmatin\b"#, options: .regularExpression) != nil || lower.range(of: #"\bsoir\b"#, options: .regularExpression) != nil {
            weekdays = [1, 2, 3, 4, 5, 6, 7]
            trigger = .time
        }
        if lower.range(of: #"\bmatin\b"#, options: .regularExpression) != nil {
            hour = 8
            trigger = .time
        } else if lower.range(of: #"\bsoir\b"#, options: .regularExpression) != nil {
            hour = 18
            trigger = .time
        }

        let dayPatterns: [(String, Int)] = [
            ("dimanche", 1), ("lundi", 2), ("mardi", 3), ("mercredi", 4),
            ("jeudi", 5), ("vendredi", 6), ("samedi", 7)
        ]
        for (name, value) in dayPatterns where lower.contains(name) {
            weekdays = [value]
            trigger = .time
        }

        if let match = text.range(of: #"(?:à|a)\s+(\d{1,2})[h:]\s*(\d{2})"#, options: [.regularExpression, .caseInsensitive]) {
            let token = String(text[match])
            let numbers = token.split { !$0.isNumber }.compactMap { Int($0) }
            if numbers.count >= 2 {
                hour = min(23, max(0, numbers[0]))
                minute = min(59, max(0, numbers[1]))
                trigger = .time
            }
            text.removeSubrange(match)
        } else if let match = text.range(of: #"(?:à|a)\s+(\d{1,2})h"#, options: [.regularExpression, .caseInsensitive]) {
            let digits = String(text[match]).filter(\.isNumber)
            if let value = Int(digits) {
                hour = min(23, max(0, value))
                trigger = .time
            }
            text.removeSubrange(match)
        }

        var action = text
        for filler in ["chaque matin", "chaque soir", "chaque jour", "tous les jours", "tous les matins", "le matin", "le soir", "routine", "rappelle-moi de", "me rappeler de"] {
            action = action.replacingOccurrences(of: filler, with: "", options: .caseInsensitive)
        }
        action = action.replacingOccurrences(of: #"\s+"#, with: " ", options: .regularExpression)
            .trimmingCharacters(in: CharacterSet(charactersIn: " .,-"))
        if action.isEmpty { action = input }
        let name = String(action.prefix(48))
        return NomiRoutine(name: name, action: action, trigger: trigger, hour: hour, minute: minute, weekdays: weekdays)
    }
}
