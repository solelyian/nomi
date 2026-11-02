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
$chat = Join-Path $env:LOCALAPPDATA "Nyne\Nomi\conversation.json"
if (Test-Path $chat) { Remove-Item $chat }
Set-NomiOnboarded
$process = Launch $app $logs
try {
    Invoke (WaitFor $process "ChatButton" 30)
    $box = WaitFor $process "ChatBox" 15
    $context = WaitFor $process "ChatContext" 10
    if ($context.Current.Name -notmatch "^Nomi ") { throw "Unexpected context line: $($context.Current.Name)" }
    Write-Host "Context line: $($context.Current.Name)"
    $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("Par quoi je commence aujourd'hui ?")
    Invoke (WaitFor $process "ChatSend" 10)
    WaitFor $process "ChatUser" 15 | Out-Null
    $deadline = (Get-Date).AddSeconds(30)
    do {
        $state = @("ChatOpenAssistant", "ChatStop", "ChatNomi", "ChatRetry") | Where-Object { Find $process $_ } | Select-Object -First 1
        if ($state) { break }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    if (!$state) { throw "No answer, loading, error or missing model state after sending." }
    Write-Host "State after sending: $state"
    Capture $process "artifacts/ui-conversation.png"
    Start-Sleep -Seconds 1
    if (!(Test-Path $chat)) { throw "The conversation was not saved." }
    $raw = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($chat))
    if ($raw -match "commence") { throw "The conversation is readable on disk." }
    if ($state -eq "ChatStop") { Invoke (Find $process "ChatStop"); Start-Sleep -Seconds 2 }
    Invoke (WaitFor $process "ChatNew" 15)
    Start-Sleep -Seconds 1
    if (Test-Path $chat) { throw "New conversation left the file on disk." }
    WaitFor $process "Chat_chatExample1" 10 | Out-Null
    Write-Host "Conversation sent, saved encrypted and cleared."
} finally {
    if (!$process.HasExited) { Stop-Process -Id $process.Id -Force }
}
