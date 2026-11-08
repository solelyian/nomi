$ErrorActionPreference = "Stop"
. "$PSScriptRoot\windows-ui.ps1"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]
function Find($process, $id) {
    $root = $A::FromHandle($process.MainWindowHandle)
    $condition = New-Object System.Windows.Automation.PropertyCondition($A::AutomationIdProperty, $id)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function WaitFor($process, $id, $seconds, [scriptblock]$ok) {
    $deadline = (Get-Date).AddSeconds($seconds)
    do {
        $element = Find $process $id
        if ($element -and (& $ok $element)) { return $element }
        Start-Sleep -Seconds 2
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
$personalize = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
Set-ItemProperty -Path $personalize -Name AppsUseLightTheme -Value 1 -Type DWord
$process = Launch $app $logs
try {
    Invoke (WaitFor $process "NavNomi" 60 { param($e) $e.Current.IsEnabled })
    $box = WaitFor $process "NomiBox" 300 { param($e) $e.Current.IsEnabled }
    Write-Host "Installed application opened the Nomi page with the downloaded model."
    Capture $process "artifacts/ui-nomi-empty.png"
    $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue(
        "An item costs 80 EUR. 15% discount, then 20% VAT. What is the final price?")
    Start-Sleep -Seconds 1
    Invoke (WaitFor $process "NomiSend" 10 { param($e) $e.Current.IsEnabled })
    [void](WaitFor $process "NomiSteps" 30 { param($e) -not $e.Current.IsOffscreen })
    Start-Sleep -Seconds 4
    Capture $process "artifacts/ui-working.png"
    $copy = WaitFor $process "NomiCopy" 420 { param($e) $e.Current.IsEnabled -and -not $e.Current.IsOffscreen }
    Start-Sleep -Seconds 2
    Capture $process "artifacts/ui-result.png"
    Invoke $copy
    Start-Sleep -Milliseconds 400
    Capture $process "artifacts/ui-copied.png"
    $label = (Find $process "ToastText").Current.Name
    if ($label -notmatch "copi") { throw "Copy confirmation not shown: '$label'" }
    Invoke (WaitFor $process "NomiDetails" 10 { param($e) $e.Current.IsEnabled })
    [void](WaitFor $process "NomiDetailsPanel" 10 { param($e) $true })
    Capture $process "artifacts/ui-details.png"
    if (!(Test-Path $chats)) { throw "The conversation was not saved locally." }
    $threads = Read-NomiFile $chats $app | ConvertFrom-Json
    if (@($threads).Count -ne 1 -or @($threads[0].Items).Count -lt 2 -or !$threads[0].Items[1].Text) { throw "Saved conversation has no answer." }
    Write-Host "Answer rendered, copy confirmed, routing details shown, conversation saved."
    Stop-Process -Id $process.Id
    Set-ItemProperty -Path $personalize -Name AppsUseLightTheme -Value 0 -Type DWord
    $process = Launch $app $logs
    Invoke (WaitFor $process "NavNomi" 60 { param($e) $e.Current.IsEnabled })
    Invoke (WaitFor $process "NomiThread" 60 { param($e) -not $e.Current.IsOffscreen })
    [void](WaitFor $process "NomiCopy" 30 { param($e) -not $e.Current.IsOffscreen })
    Start-Sleep -Seconds 2
    Capture $process "artifacts/ui-result-dark.png"
    Write-Host "Conversation reopened from the history after restart in the dark theme."
}
finally {
    if (Test-Path "$logs\startup.log") { Get-Content "$logs\startup.log" }
    if (!$process.HasExited) { Stop-Process -Id $process.Id }
}
