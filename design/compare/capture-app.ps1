param([string]$App = "artifacts/Nomi-win-x64", [string]$Out = "design/compare/app", [string[]]$Sizes = @("1600x900", "1280x800", "1128x752"), [string[]]$Views = @("today", "board", "nomi"))
$ErrorActionPreference = "Stop"
$Sizes = $Sizes -split ","
$Views = $Views -split ","
. "$PSScriptRoot\..\..\checks\windows-ui.ps1"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms
Add-Type -Namespace Nomi -Name Size -MemberDefinition '[DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool r);'
$A = [System.Windows.Automation.AutomationElement]
function Nav($process, $id) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($A::AutomationIdProperty, $id)
    for ($i = 0; $i -lt 20; $i++) {
        if ($process.HasExited) { throw "Nomi exited: $($process.ExitCode)" }
        $element = $A::FromHandle($process.MainWindowHandle).FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($element) { $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); return }
        Start-Sleep -Milliseconds 500
    }
    throw "$id not found"
}
$app = (Resolve-Path $App).Path
$logs = Join-Path $env:LOCALAPPDATA "Nyne\Nomi\logs"
New-Item -ItemType Directory -Force $Out | Out-Null
Write-Host "Screen: $([System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Size)"
Set-NomiOnboarded
$personalize = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
foreach ($theme in "light", "dark") {
    Set-ItemProperty -Path $personalize -Name AppsUseLightTheme -Value ([int]($theme -eq "light")) -Type DWord
    $process = Launch $app $logs
    try {
        foreach ($size in $Sizes) {
            $w, $h = $size -split "x" | ForEach-Object { [int]$_ }
            [void][Nomi.Size]::MoveWindow($process.MainWindowHandle, 0, 0, $w, $h, $true)
            foreach ($view in $Views) {
                Nav $process ("Nav" + $view.Substring(0, 1).ToUpper() + $view.Substring(1))
                Start-Sleep -Seconds 2
                Capture $process (Join-Path $Out "$size-$theme-$view.png")
            }
        }
    }
    finally { Stop-Process -Id $process.Id -Force }
}
