$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -Namespace Nomi -Name Native -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool PrintWindow(System.IntPtr hwnd, System.IntPtr hdc, uint flags);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool GetWindowRect(System.IntPtr hwnd, out RECT rect);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool SetForegroundWindow(System.IntPtr hwnd);
public struct RECT { public int Left, Top, Right, Bottom; }
'@
function Capture($process, $path) {
    try {
        $process.Refresh()
        $rect = New-Object Nomi.Native+RECT
        [void][Nomi.Native]::GetWindowRect($process.MainWindowHandle, [ref]$rect)
        $width = [Math]::Max(1, $rect.Right - $rect.Left)
        $height = [Math]::Max(1, $rect.Bottom - $rect.Top)
        $bitmap = New-Object System.Drawing.Bitmap $width, $height
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $hdc = $graphics.GetHdc()
        [void][Nomi.Native]::PrintWindow($process.MainWindowHandle, $hdc, 2)
        $graphics.ReleaseHdc($hdc)
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $graphics.Dispose(); $bitmap.Dispose()
        Write-Host "Captured $path (${width}x${height})"
    }
    catch { Write-Host "Capture skipped for ${path}: $($_.Exception.Message)" }
}
function Launch($app, $logs) {
    if (Test-Path "$logs\startup.log") { Remove-Item "$logs\startup.log" }
    $process = Start-Process "$app\Nomi.WinUI.exe" -WorkingDirectory $env:WINDIR -PassThru
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
    return $process
}
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
