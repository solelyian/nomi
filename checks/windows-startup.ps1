$ErrorActionPreference = "Stop"
. "$PSScriptRoot\windows-ui.ps1"
$setup = Get-ChildItem artifacts/Nomi-Setup-*.exe | Select-Object -First 1
$app = Join-Path $env:RUNNER_TEMP "Nomi installed"
$logs = Join-Path $env:LOCALAPPDATA "Nyne\Nomi\logs"
if (Test-Path "$logs\startup.log") { Remove-Item "$logs\startup.log" }
$install = Start-Process $setup.FullName -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER /DIR=`"$app`"" -PassThru -Wait
if ($install.ExitCode -ne 0) { throw "Installer exit: $($install.ExitCode)" }
foreach ($name in 'Nomi.WinUI.pri', 'MainWindow.xbf', 'msvcp140.dll', 'vcruntime140.dll', 'vcruntime140_1.dll') {
    if (!(Test-Path "$app\$name")) { throw "Installed dependency missing: $name" }
}
$since = Get-Date
$personalize = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
New-Item -Path $personalize -Force | Out-Null
Set-ItemProperty -Path $personalize -Name AppsUseLightTheme -Value 1 -Type DWord
$process = Launch $app $logs
try {
    Write-Host "Installed application activated a window from a different working directory."
    Capture $process "artifacts/ui-light.png"
    try {
        $shell = New-Object -ComObject WScript.Shell
        [void][Nomi.Native]::SetForegroundWindow($process.MainWindowHandle)
        if ($shell.AppActivate($process.Id)) {
            Start-Sleep -Seconds 1
            $shell.SendKeys("^k")
            Start-Sleep -Seconds 3
            Capture $process "artifacts/ui-palette.png"
            $shell.SendKeys("{ESC}")
        }
    }
    catch { Write-Host "Palette capture skipped: $($_.Exception.Message)" }
    Stop-Process -Id $process.Id
    Set-ItemProperty -Path $personalize -Name AppsUseLightTheme -Value 0 -Type DWord
    $process = Launch $app $logs
    Write-Host "Installed application follows the dark Windows theme."
    Capture $process "artifacts/ui-dark.png"
}
finally {
    if (Test-Path $logs) {
        Copy-Item "$logs\*" artifacts/
        Get-Content "$logs\startup.log"
    }
    Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=$since} -ErrorAction SilentlyContinue |
        Where-Object ProviderName -In 'Application Error', '.NET Runtime' |
        Format-List | Out-File artifacts/startup-events.txt
    if (!$process.HasExited) { Stop-Process -Id $process.Id }
}
