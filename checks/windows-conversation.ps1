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
function Invoke($element) {
    $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
$app = Join-Path $env:RUNNER_TEMP "Nomi installed"
$logs = Join-Path $env:LOCALAPPDATA "Nyne\Nomi\logs"
$chats = Join-Path $env:LOCALAPPDATA "Nyne\Nomi\conversations.json"
if (Test-Path $chats) { Remove-Item $chats }
Set-NomiOnboarded
$process = Launch $app $logs
try {
    Invoke (WaitFor $process "ChatButton" 30)
    $deadline = (Get-Date).AddSeconds(20)
    do {
        $entry = @("NomiBox", "NomiDownload") | Where-Object { Find $process $_ } | Select-Object -First 1
        if ($entry) { break }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    if (!$entry) { throw "The Nomi page opened neither the composer nor the model download." }
    WaitFor $process "NomiNew" 10 | Out-Null
    WaitFor $process "NomiSearch" 10 | Out-Null
    if ($entry -eq "NomiDownload") {
        Capture $process "artifacts/ui-nomi-nomodel.png"
        Write-Host "Nomi page shows the local model download."
        return
    }
    WaitFor $process "NomiSuggest_1" 10 | Out-Null
    WaitFor $process "NomiLevel" 10 | Out-Null
    Capture $process "artifacts/ui-nomi-empty.png"
    $box = WaitFor $process "NomiBox" 10
    $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("Where do I start today?")
    Invoke (WaitFor $process "NomiSend" 10)
    WaitFor $process "NomiUser" 15 | Out-Null
    $deadline = (Get-Date).AddSeconds(240)
    do {
        $state = @("NomiCopy", "NomiStop") | Where-Object { Find $process $_ } | Select-Object -First 1
        if ($state -eq "NomiCopy") { break }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    if (!$state) { throw "No answer or loading state after sending." }
    Write-Host "State after sending: $state"
    Capture $process "artifacts/ui-nomi-chat.png"
    if ($state -eq "NomiStop") { Invoke (Find $process "NomiStop"); Start-Sleep -Seconds 2 }
    WaitFor $process "NomiCopy" 15 | Out-Null
    if (!(Test-Path $chats)) { throw "The conversation was not saved." }
    $raw = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($chats))
    if ($raw -match "Where do I start") { throw "The conversation is readable on disk." }
    WaitFor $process "NomiThread" 10 | Out-Null
    Invoke (WaitFor $process "NomiNewTop" 10)
    WaitFor $process "NomiSuggest_1" 10 | Out-Null
    if (!(Test-Path $chats)) { throw "A new conversation must keep the previous one." }
    Write-Host "Nomi page sent, answered, saved encrypted and kept the conversation in the history."
} finally {
    if (!$process.HasExited) { Stop-Process -Id $process.Id -Force }
}
