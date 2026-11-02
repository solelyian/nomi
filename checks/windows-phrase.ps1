$ErrorActionPreference = "Stop"
. "$PSScriptRoot\windows-ui.ps1"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]
$Tree = [System.Windows.Automation.TreeScope]::Descendants
function Find($process, $id) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($A::AutomationIdProperty, $id)
    return $A::FromHandle($process.MainWindowHandle).FindFirst($Tree, $condition)
}
function WaitFor($process, $id, $seconds) {
    $deadline = (Get-Date).AddSeconds($seconds)
    do {
        $element = Find $process $id
        if ($element -and -not $element.Current.IsOffscreen) { return $element }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    throw "Timed out waiting for $id"
}
function Chips($process) {
    $buttons = New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
    return @($A::FromHandle($process.MainWindowHandle).FindAll($Tree, $buttons) | Where-Object { $_.Current.Name -like "Retirer*" })
}
function Invoke($element) {
    $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
$app = Join-Path $env:RUNNER_TEMP "Nomi installed"
$logs = Join-Path $env:LOCALAPPDATA "Nyne\Nomi\logs"
$tasks = Join-Path $env:LOCALAPPDATA "Nyne\Nomi\tasks.json"
if (Test-Path $tasks) { Remove-Item $tasks }
Set-NomiOnboarded
$process = Launch $app $logs
try {
    Invoke (WaitFor $process "NavBoard" 30)
    $box = WaitFor $process "BoardQuickAdd" 30
    $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("appeler Marc demain après le déjeuner, 20 min")
    $understood = WaitFor $process "PhraseUnderstood" 10
    if ($understood.Current.Name -notmatch "^Compris") { throw "Unexpected understood line: $($understood.Current.Name)" }
    Write-Host "Understood line: $($understood.Current.Name)"
    $chips = Chips $process
    if ($chips.Count -ne 2) { throw "Expected a deadline chip and a duration chip, found $($chips.Count)." }
    Invoke $chips[1]
    Start-Sleep -Milliseconds 800
    if ((Chips $process).Count -ne 1) { throw "The duration chip was not removed." }
    Capture $process "artifacts/ui-phrase-preview.png"
    Invoke (WaitFor $process "BoardAdd" 10)
    Start-Sleep -Seconds 2
    $saved = Read-NomiFile $tasks $app
    $tomorrow = (Get-Date).AddDays(1).ToString("yyyy-MM-dd")
    if ($saved -notmatch '"Title":\s*"Appeler Marc, 20 min"') { throw "The removed duration did not go back to the title: $saved" }
    if ($saved -notmatch ('"Due":\s*"' + $tomorrow + 'T13:45')) { throw "The deadline after lunch tomorrow was not saved: $saved" }
    if ($saved -match '"Estimate":\s*20\b') { throw "The removed duration was still applied." }
    Write-Host "Sentence saved with its deadline, without the removed duration."
} finally {
    if (!$process.HasExited) { Stop-Process -Id $process.Id -Force }
}
