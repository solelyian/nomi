#Requires -Version 5
<#
.SYNOPSIS
    Local Nomi release - produces the same artifacts as the GitHub 'publish' job, without GitHub.
.DESCRIPTION
    Runs the JavaScript checks, publishes the self-contained x64 build, bundles the Visual C++
    runtime, builds the Inno Setup installer, zips the portable build and writes SHA-256
    checksums. Output lands in artifacts\ - share Nomi-Setup-*.exe (installer) or
    Nomi-win-x64.zip (portable) however you like.
.EXAMPLE
    .\release.ps1 -Version 0.11.0
#>
param(
    [string]$Version = "0.0.0",
    [switch]$SkipChecks,
    [switch]$SkipInstaller
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

# Native commands never raise on failure in Windows PowerShell 5 - check every exit code.
function Invoke-Checked {
    param([scriptblock]$Step, [string]$Name)
    & $Step
    if ($LASTEXITCODE -ne 0) { throw "$Name failed (exit $LASTEXITCODE)" }
}

if (-not $SkipChecks) {
    Invoke-Checked { npm ci } "npm ci"
    Invoke-Checked { npm run check } "npm run check"
}

Invoke-Checked {
    dotnet publish Nomi.WinUI/Nomi.WinUI.csproj -c Release -r win-x64 -p:Platform=x64 -p:PublishReadyToRun=true -o artifacts/Nomi-win-x64
} "dotnet publish"
if (!(Get-ChildItem artifacts/Nomi-win-x64 -Recurse -Filter llama.dll)) {
    throw "Embedded native llama library missing from publish output"
}

# App-local Visual C++ runtime, like the CI job.
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$copied = 0
if (Test-Path $vswhere) {
    $vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    $redist = Get-ChildItem "$vs\VC\Redist\MSVC" -Directory -ErrorAction SilentlyContinue |
        Where-Object Name -Match '^\d+\.\d+\.\d+$' |
        Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if ($redist) {
        Get-ChildItem "$($redist.FullName)\x64\Microsoft.VC*.CRT\*.dll" | Copy-Item -Destination artifacts/Nomi-win-x64
        $copied++
    }
}
if (!$copied) {
    # No Visual Studio on this machine: the installed VC++ redistributable in System32 does the job.
    # From 32-bit PowerShell, System32 is WOW64-redirected to 32-bit DLLs - use Sysnative for the real x64 ones.
    $system32 = Join-Path $env:windir 'System32'
    if (-not [Environment]::Is64BitProcess) { $system32 = Join-Path $env:windir 'Sysnative\System32' }
    foreach ($name in 'msvcp140.dll', 'vcruntime140.dll', 'vcruntime140_1.dll') {
        $dll = Join-Path $system32 $name
        if (Test-Path $dll) { Copy-Item $dll -Destination artifacts/Nomi-win-x64 }
    }
}
foreach ($name in 'msvcp140.dll', 'vcruntime140.dll', 'vcruntime140_1.dll') {
    if (!(Test-Path "artifacts/Nomi-win-x64/$name")) { throw "Missing $name" }
}

$iscc = Get-ChildItem "${env:ProgramFiles(x86)}\Inno Setup *\ISCC.exe", "${env:ProgramFiles}\Inno Setup *\ISCC.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if ($iscc -and !$SkipInstaller) {
    $env:NOMI_VERSION = $Version
    Invoke-Checked { & $iscc.FullName installer\nomi.iss } "ISCC"
} elseif (!$SkipInstaller) {
    Write-Warning "Inno Setup not found - installer skipped, portable zip still produced. Install https://jrsoftware.org/inno-setup/downloads or pass -SkipInstaller."
}

Remove-Item artifacts/Nomi-win-x64.zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path artifacts/Nomi-win-x64/* -DestinationPath artifacts/Nomi-win-x64.zip

# Only the artifacts this run produced - earlier installers of other versions stay out of the manifest.
$targets = @(Get-Item artifacts/Nomi-win-x64.zip)
$setup = "artifacts/Nomi-Setup-$Version.exe"
if (Test-Path $setup) { $targets += Get-Item $setup }
$targets | ForEach-Object {
    "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower())  $($_.Name)"
} | Set-Content -Encoding ascii artifacts/SHA256SUMS.txt

Get-Content artifacts/SHA256SUMS.txt
Write-Host "Release ready in artifacts\ : $Version"
