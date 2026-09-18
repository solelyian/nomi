$ErrorActionPreference = "Stop"
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
$process = Start-Process "$app\Nomi.WinUI.exe" -WorkingDirectory $env:WINDIR -PassThru
try {
    $deadline = (Get-Date).AddSeconds(45)
    do {
        Start-Sleep -Milliseconds 500
        $process.Refresh()
        $ready = (Test-Path "$logs\startup.log") -and
            (Select-String -Path "$logs\startup.log" -Pattern "Main window activated" -Quiet)
    } while (!$ready -and !$process.HasExited -and (Get-Date) -lt $deadline)
    if ($process.HasExited -or !$ready -or $process.MainWindowHandle -eq 0) {
        throw "Application did not activate a window. Exit: $($process.ExitCode)"
    }
    Start-Sleep -Seconds 5
    $process.Refresh()
    if ($process.HasExited) { throw "Application exited after startup: $($process.ExitCode)" }
    Write-Host "Installed application activated a window from a different working directory."
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
