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
$personalize = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
foreach ($theme in 1, 0) {
    $name = if ($theme -eq 1) { "light" } else { "dark" }
    Set-ItemProperty -Path $personalize -Name AppsUseLightTheme -Value $theme -Type DWord
    $process = Launch $app $logs
    try {
        foreach ($id in "NavToday", "NavBoard", "NavFocus", "NavAssistant") { [void](WaitFor $process $id 30) }
        [void](WaitFor $process "DayPlan" 30)
        [void](WaitFor $process "Suggestions" 30)
        Capture $process "artifacts/ui-v3-today-$name.png"
        Invoke (Find $process "NavBoard")
        $box = WaitFor $process "BoardQuickAdd" 30
        foreach ($column in "todo", "doing", "review", "done") { [void](WaitFor $process "Lane_$column" 30) }
        if ($theme -eq 1) {
            $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("Vérifier les commissions de septembre")
            Invoke (Find $process "BoardAdd")
            Start-Sleep -Seconds 2
            if (!(Test-Path $tasks)) { throw "Task board was not saved locally." }
            $saved = Get-Content $tasks -Raw -Encoding UTF8
            if ($saved -notmatch "commissions de septembre") { throw "Quick-added task missing from tasks.json." }
            Write-Host "Task added from the board and saved locally."
        }
        $root = $A::FromHandle($process.MainWindowHandle)
        $cards = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem))) |
            Where-Object { $_.Current.AutomationId -like "Task_*" }
        if (@($cards).Count -lt 1) { throw "No task card exposed to UI Automation." }
        Write-Host "Board lanes and $(@($cards).Count) task card(s) exposed to UI Automation."
        Capture $process "artifacts/ui-v3-board-$name.png"
        Invoke (Find $process "NavFocus")
        [void](WaitFor $process "FocusPrivacy" 30)
        [void](WaitFor $process "VisionCard" 30)
        [void](WaitFor $process "VisionStatus" 30)
        Capture $process "artifacts/ui-v3-focus-$name.png"
        Write-Host "Today, Board and Focus rendered in the $name theme."
    }
    finally {
        if (!$process.HasExited) { Stop-Process -Id $process.Id }
    }
}
