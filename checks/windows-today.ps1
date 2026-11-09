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
        if ($element) { return $element }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)
    throw "Timed out waiting for $id"
}
function Invoke($element) {
    $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
function Write-NomiFile($path, $text, $app) {
    if (-not ("System.Security.Cryptography.ProtectedData" -as [type])) {
        try { Add-Type -AssemblyName System.Security } catch { }
    }
    if (-not ("System.Security.Cryptography.ProtectedData" -as [type])) {
        Add-Type -Path (Join-Path $app "System.Security.Cryptography.ProtectedData.dll")
    }
    $entropy = [System.Text.Encoding]::UTF8.GetBytes("Nyne.Nomi.local.v1")
    $sealed = [System.Security.Cryptography.ProtectedData]::Protect([System.Text.Encoding]::UTF8.GetBytes($text),
        $entropy, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
    [System.IO.File]::WriteAllBytes($path, [byte[]]([System.Text.Encoding]::ASCII.GetBytes("NOMI1") + $sealed))
}
$app = Join-Path $env:RUNNER_TEMP "Nomi installed"
$logs = Join-Path $env:LOCALAPPDATA "Nyne\Nomi\logs"
$tasks = Join-Path $env:LOCALAPPDATA "Nyne\Nomi\tasks.json"
New-Item -ItemType Directory -Force -Path artifacts | Out-Null
Set-NomiOnboarded
$personalize = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
$today = (Get-Date).Date
function At($hours) { return $today.AddHours($hours).ToString("yyyy-MM-ddTHH:mm:sszzz") }
$seed = @(
    @{ Title = "Relancer Marc"; Column = "done"; Estimate = 20; SpentSeconds = 1200; Completed = (At 9.8); Created = (At 8) },
    @{ Title = "Préparer le devis Acme"; Column = "todo"; Estimate = 60; Due = (At 15); Space = "Vente & commerce"; Order = 1; Created = (At 8) },
    @{ Title = "Relire la note de cadrage"; Column = "todo"; Estimate = 45; Order = 2; Created = (At 8) },
    @{ Title = "Trier les mails"; Column = "todo"; Estimate = 30; Order = 3; Created = (At 8) }
) | ConvertTo-Json
foreach ($state in "empty", "filled") {
    if (Test-Path $tasks) { Remove-Item $tasks }
    if ($state -eq "filled") { Write-NomiFile $tasks $seed $app }
    foreach ($theme in 1, 0) {
        $name = if ($theme -eq 1) { "light" } else { "dark" }
        Set-ItemProperty -Path $personalize -Name AppsUseLightTheme -Value $theme -Type DWord
        $process = Launch $app $logs
        try {
            foreach ($id in "MetricPlanned", "MetricDone", "MetricDue", "MetricPace", "ActiveTask", "Suggestions", "DayPlan") { [void](WaitFor $process $id 30) }
            if ($state -eq "filled") {
                $due = (WaitFor $process "MetricDue" 5).Current.Name
                if ($due -notmatch "devis Acme") { throw "Next deadline tile does not show the Acme quote: $due" }
                [void](WaitFor $process "TodayDraftWrite" 10)
            }
            Start-Sleep -Seconds 1
            Capture $process "artifacts/ui-today-$state-$name.png"
            if ($theme -eq 1) {
                Invoke (WaitFor $process "FiguresPeek" 10)
                Start-Sleep -Seconds 1
                Capture $process "artifacts/ui-today-$state-figures.png"
            }
            Write-Host "Today ($state) rendered as tiles in the $name theme."
        }
        finally {
            if (!$process.HasExited) { Stop-Process -Id $process.Id }
        }
    }
}
if (Test-Path $tasks) { Remove-Item $tasks }
