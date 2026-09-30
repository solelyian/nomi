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
