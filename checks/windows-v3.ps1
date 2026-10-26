$ErrorActionPreference = "Stop"
. "$PSScriptRoot\windows-ui.ps1"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]
function Find($process, $id) {
    $root = $A::FromHandle($process.MainWindowHandle)
    $condition = New-Object System.Windows.Automation.PropertyCondition($A::AutomationIdProperty, $id)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function WaitFor($process, $id, $seconds) {
    $deadline = (Get-Date).AddSeconds($seconds)
    do {
        $element = Find $process $id
        if ($element -and -not $element.Current.IsOffscreen) { return $element }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)
    $root = $A::FromHandle($process.MainWindowHandle)
    $seen = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) |
        Where-Object { $_.Current.AutomationId } |
        ForEach-Object { "$($_.Current.AutomationId)[$($_.Current.ControlType.ProgrammaticName)$(if ($_.Current.IsOffscreen) { ',offscreen' })]" }
    Write-Host "Automation ids: $($seen -join ', ')"
    throw "Timed out waiting for $id"
}
function Invoke($element) {
    $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
$app = Join-Path $env:RUNNER_TEMP "Nomi installed"
$logs = Join-Path $env:LOCALAPPDATA "Nyne\Nomi\logs"
$tasks = Join-Path $env:LOCALAPPDATA "Nyne\Nomi\tasks.json"
if (Test-Path $tasks) { Remove-Item $tasks }
$staleRoutines = Join-Path $env:LOCALAPPDATA "NyneNomi
outines.json"
if (Test-Path $staleRoutines) { Remove-Item $staleRoutines }
Set-NomiOnboarded
$personalize = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
foreach ($theme in 1, 0) {
    $name = if ($theme -eq 1) { "light" } else { "dark" }
    Set-ItemProperty -Path $personalize -Name AppsUseLightTheme -Value $theme -Type DWord
    $process = Launch $app $logs
    try {
        foreach ($id in "NavToday", "NavBoard", "NavFocus", "NavAssistant", "NavRoutines") { [void](WaitFor $process $id 30) }
        [void](WaitFor $process "DayPlan" 30)
        [void](WaitFor $process "Suggestions" 30)
        Capture $process "artifacts/ui-v3-today-$name.png"
        Invoke (WaitFor $process "NavBoard" 30)
        $box = WaitFor $process "BoardQuickAdd" 30
        foreach ($column in "todo", "doing", "review", "done") { [void](WaitFor $process "Lane_$column" 30) }
        if ($theme -eq 1) {
            $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("Vérifier les commissions de septembre")
            Invoke (WaitFor $process "BoardAdd" 30)
            Start-Sleep -Seconds 2
            if (!(Test-Path $tasks)) { throw "Task board was not saved locally." }
            if ((Get-Content $tasks -Raw -Encoding UTF8) -match "commissions de septembre") { throw "tasks.json is stored in clear text." }
            $saved = Read-NomiFile $tasks $app
            if ($saved -notmatch "commissions de septembre") { throw "Quick-added task missing from tasks.json." }
            Write-Host "Task added from the board and saved locally, encrypted."
        }
        $root = $A::FromHandle($process.MainWindowHandle)
        $cards = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem))) |
            Where-Object { $_.Current.AutomationId -like "Task_*" }
        if (@($cards).Count -lt 1) { throw "No task card exposed to UI Automation." }
        Write-Host "Board lanes and $(@($cards).Count) task card(s) exposed to UI Automation."
        Capture $process "artifacts/ui-v3-board-$name.png"
        Invoke (WaitFor $process "NavFocus" 30)
        [void](WaitFor $process "FocusPrivacy" 30)
        [void](WaitFor $process "VisionCard" 30)
        [void](WaitFor $process "VisionStatus" 30)
        Capture $process "artifacts/ui-v3-focus-$name.png"
        Invoke (WaitFor $process "NavRoutines" 30)
        [void](WaitFor $process "AgendaCard" 30)
        if ($theme -eq 1) {
            $phrase = WaitFor $process "RoutinePhrase" 30
            $phrase.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("Chaque lundi après le déjeuner, préparer le point équipe 20 min puis envoyer le compte rendu")
            Invoke (WaitFor $process "RoutineUnderstand" 30)
            [void](WaitFor $process "RoutinePreview" 30)
            Capture $process "artifacts/ui-v3-routine-preview.png"
            $routinesFile = Join-Path $env:LOCALAPPDATA "Nyne\Nomi\routines.json"
            if (Test-Path $routinesFile) { throw "A routine was saved before confirmation." }
            Invoke (WaitFor $process "RoutineSave" 30)
            [void](WaitFor $process "RoutineCard" 30)
            Start-Sleep -Seconds 1
            if (!(Test-Path $routinesFile)) { throw "Routine was not saved locally." }
            if ((Get-Content $routinesFile -Raw -Encoding UTF8) -match "point") { throw "routines.json is stored in clear text." }
            $routinesSaved = Read-NomiFile $routinesFile $app
            if ($routinesSaved -notmatch "after-lunch") { throw "Routine trigger missing from routines.json." }
            Write-Host "Routine understood, confirmed, then saved locally, encrypted."
        }
        Capture $process "artifacts/ui-v3-routines-$name.png"
        Invoke (WaitFor $process "NavToday" 30)
        Invoke (WaitFor $process "DayRescue" 30)
        [void](WaitFor $process "RescuePlan" 30)
        [void](WaitFor $process "RescueApply" 30)
        Capture $process "artifacts/ui-v3-rescue-$name.png"
        Invoke (WaitFor $process "RescueCancel" 30)
        Write-Host "Today, Board, Focus, Routines and day rescue rendered in the $name theme."
    }
    finally {
        if (!$process.HasExited) { Stop-Process -Id $process.Id }
    }
}
