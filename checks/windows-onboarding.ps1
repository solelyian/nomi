$ErrorActionPreference = "Stop"
. "$PSScriptRoot\windows-ui.ps1"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]
function Find($process, $id) {
    try {
        $root = $A::FromHandle($process.MainWindowHandle)
        $condition = New-Object System.Windows.Automation.PropertyCondition($A::AutomationIdProperty, $id)
        return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    }
    catch { Start-Sleep -Milliseconds 300; return $null }
}
function WaitFor($process, $id, $seconds) {
    $deadline = (Get-Date).AddSeconds($seconds)
    do {
        $element = Find $process $id
        if ($element -and -not $element.Current.IsOffscreen) { return $element }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    $process.Refresh()
    Write-Host "Waiting for $id failed. Exited: $($process.HasExited), responding: $(if (!$process.HasExited) { $process.Responding })"
    Capture $process (Join-Path $env:RUNNER_TEMP "onboarding-failure.png")
    $root = $A::FromHandle($process.MainWindowHandle)
    $seen = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) |
        Where-Object { $_.Current.AutomationId } | ForEach-Object { $_.Current.AutomationId }
    Write-Host "Automation ids: $($seen -join ', ')"
    throw "Timed out waiting for $id"
}
function Invoke($element) { $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Choose($process, $group, $name) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($A::NameProperty, $name)
    $item = $null
    for ($try = 0; !$item -and $try -lt 10; $try++) {
        try { $item = (WaitFor $process $group 15).FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition) }
        catch { Start-Sleep -Milliseconds 500 }
    }
    if (!$item) { throw "No '$name' choice in $group" }
    try { $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
    catch [System.Windows.Automation.ElementNotAvailableException] { }
    catch { if ($_.Exception.InnerException -isnot [System.Windows.Automation.ElementNotAvailableException]) { throw } }
    Start-Sleep -Milliseconds 800
}
function Step($process, $step) {
    [void](WaitFor $process "OnboardingStep_$step" 20)
    Capture $process (Join-Path $env:RUNNER_TEMP "onboarding-$step.png")
}
function Next($process) { Invoke (WaitFor $process "OnboardingNext" 10); Start-Sleep -Milliseconds 800 }

$app = Join-Path $env:RUNNER_TEMP "Nomi installed"
$logs = Join-Path $env:LOCALAPPDATA "Nyne\Nomi\logs"
$data = Join-Path $env:LOCALAPPDATA "Nyne\Nomi"
foreach ($file in "tasks.json", "profile.json", "routines.json") { if (Test-Path "$data\$file") { Remove-Item "$data\$file" } }
New-Item -ItemType Directory -Force -Path $data | Out-Null
[System.IO.File]::WriteAllText("$data\profile.json", '{"CheckUpdates":false}')

$process = Launch $app $logs
try {
    Step $process "welcome"
    if (Find $process "OnboardingBack") { throw "The welcome step offers Back." }
    if (Find $process "OnboardingSkip") { throw "The language step can be skipped." }
    Choose $process "OnboardingLanguage" "English"
    Step $process "welcome"
    Next $process
    Step $process "reading"
    Choose $process "OnboardingTheme" "Dark"
    Choose $process "OnboardingSpacing" "Airy"
    Step $process "reading"
    Next $process
    Step $process "numbers"
    Next $process
    Step $process "day"
    $saturday = WaitFor $process "OnboardingWorkday_Saturday" 10
    $saturday.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Start-Sleep -Milliseconds 800
    Next $process
    Step $process "pace"
    Invoke (WaitFor $process "OnboardingBack" 10)
    Step $process "day"
    $saturday = WaitFor $process "OnboardingWorkday_Saturday" 10
    if ($saturday.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne "On") { throw "Saturday was not kept as a working day." }
    Next $process
    Step $process "pace"
    Invoke (WaitFor $process "OnboardingSkip" 10)
    Step $process "model"
    [void](WaitFor $process "OnboardingDownload" 10)
    Invoke (WaitFor $process "OnboardingModelLater" 10)
    Step $process "presence"
    [void](WaitFor $process "OnboardingCapture" 10)
    Invoke (WaitFor $process "OnboardingLater" 10)
    Start-Sleep -Seconds 2
    if (Find $process "Onboarding") { throw "Finish later did not close the welcome." }
    [void](WaitFor $process "NavToday" 15)
    Write-Host "Finish later closes the welcome on the main window."
}
finally { if (!$process.HasExited) { Stop-Process -Id $process.Id; Start-Sleep -Seconds 2 } }

$process = Launch $app $logs
try {
    Step $process "presence"
    Write-Host "The welcome resumes at the saved step after a restart."
    Next $process
    Step $process "agenda"
    Invoke (WaitFor $process "OnboardingSkip" 10)
    Step $process "data"
    Next $process
    Step $process "tasks"
    $box = WaitFor $process "OnboardingTasks" 10
    $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("Call the garage`r`n- Send the quote`r`n`r`n")
    Start-Sleep -Milliseconds 800
    Step $process "tasks"
    Next $process
    Start-Sleep -Seconds 2
    if (Find $process "Onboarding") { throw "The welcome is still open after the last step." }
    [void](WaitFor $process "NavToday" 15)
    Capture $process (Join-Path $env:RUNNER_TEMP "onboarding-today.png")
    $tasks = Read-NomiFile "$data\tasks.json" $app
    if ($tasks -notmatch "Call the garage" -or $tasks -notmatch '"Send the quote"') { throw "The first tasks were not added: $tasks" }
    $profile = Read-NomiFile "$data\profile.json" $app | ConvertFrom-Json
    if (!$profile.Onboarded -or $profile.Language -ne "en" -or $profile.Theme -ne "dark" -or $profile.Spacing -ne "airy") { throw "The welcome choices were not saved." }
    if (@($profile.WorkingDays) -notcontains 6) { throw "Saturday is not saved as a working day." }
    Write-Host "The welcome saves its choices in the encrypted profile, adds the first tasks and opens Today."
}
finally { if (!$process.HasExited) { Stop-Process -Id $process.Id; Start-Sleep -Seconds 2 } }

$process = Launch $app $logs
try {
    Start-Sleep -Seconds 2
    if (Find $process "Onboarding") { throw "The welcome came back after it was completed." }
    Write-Host "A completed welcome does not come back."
}
finally { if (!$process.HasExited) { Stop-Process -Id $process.Id } }
