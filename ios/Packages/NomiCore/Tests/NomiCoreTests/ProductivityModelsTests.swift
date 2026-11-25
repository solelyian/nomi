import XCTest
@testable import NomiCore

final class ProductivityModelsTests: XCTestCase {
    func testParsesFrenchTaskPhrase() {
        let task = TaskPhraseParser.parse("Envoyer le compte rendu demain 30 min important")
        XCTAssertEqual(task.priority, .high)
        XCTAssertEqual(task.estimateMinutes, 30)
        XCTAssertNotNil(task.dueDate)
        XCTAssertFalse(task.title.lowercased().contains("demain"))
        XCTAssertFalse(task.title.lowercased().contains("important"))
    }

    func testParsesHourEstimate() {
        let task = TaskPhraseParser.parse("Réviser ce soir 2 heures")
        XCTAssertEqual(task.estimateMinutes, 120)
    }


    func testParsesMorningRoutinePhrase() {
        let routine = RoutinePhraseParser.parse("Chaque matin rappelle-moi de préparer mes priorités")
        XCTAssertEqual(routine.trigger, .time)
        XCTAssertEqual(routine.hour, 8)
        XCTAssertTrue(routine.weekdays.contains(2))
        XCTAssertFalse(routine.action.lowercased().contains("chaque matin"))
    }

    func testParsesSpecificRoutineTime() {
        let routine = RoutinePhraseParser.parse("Le lundi à 17h30 réviser la fiche")
        XCTAssertEqual(routine.trigger, .time)
        XCTAssertEqual(routine.hour, 17)
        XCTAssertEqual(routine.minute, 30)
        XCTAssertEqual(routine.weekdays, [2])
    }

    func testRoutineDueOnlyAtConfiguredTimeAndWeekday() {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(secondsFromGMT: 0)!
        let mondayNine = calendar.date(from: DateComponents(year: 2026, month: 11, day: 23, hour: 9))!
        let mondayEight = calendar.date(from: DateComponents(year: 2026, month: 11, day: 23, hour: 8))!
        let routine = NomiRoutine(name: "Revue", action: "Préparer", hour: 9, minute: 0, weekdays: [2])
        XCTAssertTrue(routine.isDue(at: mondayNine, calendar: calendar))
        XCTAssertFalse(routine.isDue(at: mondayEight, calendar: calendar))
    }
}
