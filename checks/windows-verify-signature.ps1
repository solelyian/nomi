param([Parameter(Mandatory)][string]$File)
$ErrorActionPreference = "Stop"
$signature = Get-AuthenticodeSignature $File
if ($signature.Status -ne 'Valid') { throw "$File signature status: $($signature.Status) $($signature.StatusMessage)" }
if (!$signature.TimeStamperCertificate) { throw "$File signature is not timestamped" }
Write-Host "Signed: $File"
Write-Host "  Subject: $($signature.SignerCertificate.Subject)"
Write-Host "  Issuer: $($signature.SignerCertificate.Issuer)"
Write-Host "  Timestamp: $($signature.TimeStamperCertificate.Subject)"
