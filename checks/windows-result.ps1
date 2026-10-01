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
$history = Join-Path $env:LOCALAPPDATA "Nyne\Nomi\history.json"
if (Test-Path $history) { Remove-Item $history }
$personalize = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
Set-ItemProperty -Path $personalize -Name AppsUseLightTheme -Value 1 -Type DWord
$process = Launch $app $logs
try {
    Invoke (WaitFor $process "NavAssistant" 60 { param($e) $e.Current.IsEnabled })
    $launch = WaitFor $process "LaunchButton" 300 { param($e) $e.Current.IsEnabled }
    Write-Host "Installed application loaded the downloaded model."
    $request = Find $process "RequestBox"
    $request.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue(
        "Un article coûte 80 €. Remise de 15 %, puis TVA de 20 %. Quel est le prix final ?")
    Start-Sleep -Seconds 1
    Invoke $launch
    [void](WaitFor $process "WorkingTitle" 30 { param($e) -not $e.Current.IsOffscreen })
    Start-Sleep -Seconds 4
    Capture $process "artifacts/ui-working.png"
    $copy = WaitFor $process "CopyButton" 420 { param($e) $e.Current.IsEnabled -and -not $e.Current.IsOffscreen }
    Start-Sleep -Seconds 2
    Capture $process "artifacts/ui-result.png"
    Invoke $copy
    Start-Sleep -Milliseconds 400
    Capture $process "artifacts/ui-copied.png"
    $label = (Find $process "CopyLabel").Current.Name
    if ($label -notin "Copié", "Copied") { throw "Copy confirmation not shown: '$label'" }
    if (!(Test-Path $history)) { throw "History was not saved locally." }
    $entries = Get-Content $history -Raw | ConvertFrom-Json
    if (@($entries).Count -lt 1 -or !$entries[0].Output) { throw "History entry has no answer." }
    Write-Host "Answer rendered, copy confirmed, history saved ($(@($entries).Count) entry)."
    Stop-Process -Id $process.Id
    Set-ItemProperty -Path $personalize -Name AppsUseLightTheme -Value 0 -Type DWord
    $process = Launch $app $logs
    Invoke (WaitFor $process "NavAssistant" 60 { param($e) $e.Current.IsEnabled })
    $deadline = (Get-Date).AddSeconds(60)
    do {
        Start-Sleep -Seconds 2
        $buttons = $A::FromHandle($process.MainWindowHandle).FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
        $item = $buttons | Where-Object { $_.Current.Name -match '^(Rouvrir|Reopen) ' } | Select-Object -First 1
    } while (!$item -and (Get-Date) -lt $deadline)
    if (!$item) { throw "History entry not listed after restart." }
    Invoke $item
    [void](WaitFor $process "CopyButton" 30 { param($e) -not $e.Current.IsOffscreen })
    Start-Sleep -Seconds 2
    Capture $process "artifacts/ui-result-dark.png"
    Write-Host "History entry reopened after restart in the dark theme."
}
finally {
    if (Test-Path "$logs\startup.log") { Get-Content "$logs\startup.log" }
    if (!$process.HasExited) { Stop-Process -Id $process.Id }
}
