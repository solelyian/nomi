$ErrorActionPreference = "Stop"
. "$PSScriptRoot\windows-ui.ps1"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -Namespace NomiPresence -Name Native -MemberDefinition '[DllImport("user32.dll")] public static extern bool IsWindowVisible(System.IntPtr window);'
$A = [System.Windows.Automation.AutomationElement]
$app = Join-Path $env:RUNNER_TEMP "Nomi installed"
$logs = Join-Path $env:LOCALAPPDATA "Nyne\Nomi\logs"
Set-NomiOnboarded
$process = Launch $app $logs
try {
    $handle = $process.MainWindowHandle
    $window = $A::FromHandle($handle)
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Start-Sleep -Seconds 3
    $process.Refresh()
    if ($process.HasExited) { throw "Closing the window stopped Nomi instead of keeping it in the notification area." }
    if ([NomiPresence.Native]::IsWindowVisible($handle)) { throw "The window is still visible after closing it." }
    Write-Host "Closing the window keeps Nomi running in the notification area."
    $second = Start-Process "$app\Nomi.WinUI.exe" -WorkingDirectory $env:WINDIR -PassThru
    if (!$second.WaitForExit(30000)) { Stop-Process -Id $second.Id; throw "A second Nomi instance kept running." }
    $deadline = (Get-Date).AddSeconds(15)
    while (!([NomiPresence.Native]::IsWindowVisible($handle)) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
    if (!([NomiPresence.Native]::IsWindowVisible($handle))) { throw "Launching Nomi again did not bring back the running window." }
    $process.Refresh()
    if ($process.HasExited) { throw "The first instance exited." }
    Write-Host "A second launch exits and brings back the running window."
}
finally {
    if (!$process.HasExited) { Stop-Process -Id $process.Id }
}
