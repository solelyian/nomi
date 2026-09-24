param([Parameter(Mandatory)][string[]]$Files)
$ErrorActionPreference = "Stop"
if (!$env:NOMI_SIGNING_PFX_BASE64 -or !$env:NOMI_SIGNING_PASSWORD) {
    Write-Host "Signing secrets absent: leaving $($Files -join ', ') unsigned."
    exit 0
}
$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\signtool.exe" |
    Sort-Object { [version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1
if (!$signtool) { throw "signtool.exe not found" }
$pfx = Join-Path $env:RUNNER_TEMP "nomi-signing.pfx"
$encoded = ($env:NOMI_SIGNING_PFX_BASE64 -split "`r?`n" | Where-Object { $_ -notmatch '^-----' }) -join '' -replace '\s', ''
try { $bytes = [Convert]::FromBase64String($encoded) }
catch {
    throw "NOMI_SIGNING_PFX_BASE64 is not valid base64 ($($encoded.Length) characters after trimming). " +
        'Encode the PFX with [Convert]::ToBase64String([IO.File]::ReadAllBytes("nomi.pfx")) and paste the single-line result.'
}
if ($bytes.Length -lt 4 -or $bytes[0] -ne 0x30) { throw "Decoded NOMI_SIGNING_PFX_BASE64 ($($bytes.Length) bytes) is not a PKCS#12 file." }
[IO.File]::WriteAllBytes($pfx, $bytes)
try {
    foreach ($file in $Files) {
        & $signtool.FullName sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com `
            /f $pfx /p $env:NOMI_SIGNING_PASSWORD /d "Nomi" /du "https://github.com/solelyian/nomi" $file
        if ($LASTEXITCODE -ne 0) { throw "Signing failed for $file" }
        & $signtool.FullName verify /pa /v $file
        if ($LASTEXITCODE -ne 0) { throw "Signature verification failed for $file" }
    }
}
finally { Remove-Item $pfx -Force -ErrorAction SilentlyContinue }
