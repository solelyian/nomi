import XCTest
@testable import NomiCore

final class ProductivityModelsTests: XCTestCase {

    func testParsesFractionalHourEstimate() {
        let task = TaskPhraseParser.parse("Préparer le dossier 1,5 h")
        XCTAssertEqual(task.estimateMinutes, 90)
        XCTAssertFalse(task.title.contains("1"))
        XCTAssertFalse(task.title.contains("5"))
    }


    func testParsesTodayWithCurlyApostrophe() {
        let task = TaskPhraseParser.parse("Relire aujourd’hui")
        XCTAssertNotNil(task.dueDate)
        XCTAssertFalse(task.title.lowercased().contains("aujourd"))
    }


    func testRejectsOversizedEstimateWithoutCrashing() {
        let task = TaskPhraseParser.parse("Préparer le dossier 999999999999999999999 h")
        XCTAssertNil(task.estimateMinutes)
    }

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


    func testParsesWholeHourRoutineWithoutRecurrence() {
        let routine = RoutinePhraseParser.parse("Préparer le sac à 17h")
        XCTAssertEqual(routine.trigger, .time)
        XCTAssertEqual(routine.hour, 17)
    }


    func testEveningOverridesRecurrenceDefaultHour() {
        let weekend = RoutinePhraseParser.parse("Le week-end le soir ranger l'appartement")
        XCTAssertEqual(weekend.hour, 18)
        XCTAssertEqual(weekend.weekdays, [1, 7])
        let daily = RoutinePhraseParser.parse("Chaque jour le soir réviser")
        XCTAssertEqual(daily.hour, 18)
    }


    func testDoesNotTreatSoirInsideRasoirAsEvening() {
        let routine = RoutinePhraseParser.parse("Chaque jour nettoyer le rasoir")
        XCTAssertEqual(routine.hour, 9)
    }


    func testParsesSpacedMinutesInRoutineTime() {
        let routine = RoutinePhraseParser.parse("À 17h 30 préparer le sac")
        XCTAssertEqual(routine.hour, 17)
        XCTAssertEqual(routine.minute, 30)
        XCTAssertFalse(routine.action.contains("30"))
    }


    func testParsesPluralMorningRoutine() {
        let routine = RoutinePhraseParser.parse("Tous les matins préparer le café")
        XCTAssertEqual(routine.trigger, .time)
        XCTAssertEqual(routine.hour, 8)
    }


    func testLeadingEveningCueWinsOverMorningObject() {
        let routine = RoutinePhraseParser.parse("Chaque soir planifier mes matins")
        XCTAssertEqual(routine.hour, 18)
    }

    func testLeadingMorningCueWinsOverEveningObject() {
        let routine = RoutinePhraseParser.parse("Chaque matin préparer le soir")
        XCTAssertEqual(routine.hour, 8)
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
