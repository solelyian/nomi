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
.EXAMPLE
    .\release.ps1 -Version 0.11.0 -Publish -GitLabProject "solelyian/nomi"
    Builds then publishes the artifacts to GitHub Releases AND GitLab.
    Requires env vars: GH_TOKEN or GITHUB_TOKEN (GitHub PAT), GITLAB_TOKEN (api scope).
    GitHub repo defaults to the origin remote; override with -GitHubRepo owner/name.
    -GitLabProject is the GitLab path (group/project); -GitLabHost for self-hosted.
#>
param(
    [string]$Version = "0.0.0",
    [switch]$SkipChecks,
    [switch]$SkipInstaller,
    [switch]$Publish,
    [string]$GitHubRepo = "",
    [string]$GitLabProject = "",
    [string]$GitLabHost = "https://gitlab.com"
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

if ($Publish) {
    # Windows PowerShell 5.1 defaults to TLS 1.0 - GitHub/GitLab need 1.2+.
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    # Big binaries cross flaky connections - retry each upload up to 3 times.
    function Invoke-Upload {
        param([string]$Uri, [hashtable]$Headers, [string]$File, [string]$Method = "Post")
        for ($try = 1; $try -le 3; $try++) {
            try {
                return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $Headers -ContentType "application/octet-stream" -InFile $File
            } catch {
                if ($try -eq 3) { throw }
                Write-Warning "Upload of $(Split-Path $File -Leaf) failed (attempt $try/3): $($_.Exception.Message)"
                Start-Sleep -Seconds (5 * $try)
            }
        }
    }
    $files = @($targets) + @(Get-Item artifacts/SHA256SUMS.txt)
    $tag = "v$Version"
    $headSha = (git rev-parse HEAD 2>$null)
    if (!$GitHubRepo) {
        $remote = git config --get remote.origin.url
        if ($remote -match 'github\.com[:/]+(.+?)(\.git)?$') { $GitHubRepo = $Matches[1] }
    }

    if ($GitHubRepo) {
        $ght = if ($env:GH_TOKEN) { $env:GH_TOKEN } else { $env:GITHUB_TOKEN }
        if (!$ght) { throw "-Publish needs GH_TOKEN (or GITHUB_TOKEN) for GitHub Releases" }
        $ghHeaders = @{ Authorization = "Bearer $ght"; Accept = "application/vnd.github+json" }
        $ghApi = "https://api.github.com/repos/$GitHubRepo"
        $ghBody = @{ tag_name = $tag; name = "Nomi $Version"; body = "Nomi $Version" }
        # Pin the new tag to the commit just built, not whatever the default branch points at.
        if ($headSha) { $ghBody.target_commitish = $headSha }
        try {
            $rel = Invoke-RestMethod -Method Post -Uri "$ghApi/releases" -Headers $ghHeaders -ContentType "application/json" -Body ($ghBody | ConvertTo-Json)
        } catch {
            $rel = Invoke-RestMethod -Uri "$ghApi/releases/tags/$tag" -Headers $ghHeaders
        }
        $uploadBase = $rel.upload_url -replace '\{\?.*\}$', ''
        # Reused release (retry after a partial run): remove same-named assets first, uploads reject duplicates.
        $existing = @(Invoke-RestMethod -Uri "$ghApi/releases/$($rel.id)/assets" -Headers $ghHeaders)
        foreach ($f in $files) {
            foreach ($d in @($existing | Where-Object { $_.name -eq $f.Name })) {
                Invoke-RestMethod -Method Delete -Uri "$ghApi/releases/assets/$($d.id)" -Headers $ghHeaders | Out-Null
            }
            Invoke-Upload -Uri "$uploadBase`?name=$($f.Name)" -Headers $ghHeaders -File $f.FullName | Out-Null
            Write-Host "GitHub + $($f.Name)"
        }
    } else {
        Write-Warning "No GitHub repo found (origin remote) - GitHub release skipped."
    }

    if ($GitLabProject) {
        if (!$env:GITLAB_TOKEN) { throw "-Publish with -GitLabProject needs GITLAB_TOKEN (api scope)" }
        $glHeaders = @{ "PRIVATE-TOKEN" = $env:GITLAB_TOKEN }
        $glApi = "$GitLabHost/api/v4/projects/$([uri]::EscapeDataString($GitLabProject))"
        $links = @()
        foreach ($f in $files) {
            $url = "$glApi/packages/generic/nomi/$Version/$($f.Name)"
            Invoke-Upload -Method Put -Uri $url -Headers $glHeaders -File $f.FullName | Out-Null
            $links += @{ name = $f.Name; url = $url; link_type = "package" }
            Write-Host "GitLab + $($f.Name)"
        }
        # ref is required when the tag does not exist yet on GitLab.
        $glCreate = @{ tag_name = $tag; name = "Nomi $Version"; description = "Nomi $Version"; assets = @{ links = $links } }
        if ($headSha) { $glCreate.ref = $headSha }
        try {
            Invoke-RestMethod -Method Post -Uri "$glApi/releases" -Headers $glHeaders -ContentType "application/json" -Body ($glCreate | ConvertTo-Json -Depth 4) | Out-Null
        } catch {
            # Update accepts only mutable fields - no tag_name/ref.
            $glUpdate = @{ name = "Nomi $Version"; description = "Nomi $Version"; assets = @{ links = $links } }
            Invoke-RestMethod -Method Put -Uri "$glApi/releases/$tag" -Headers $glHeaders -ContentType "application/json" -Body ($glUpdate | ConvertTo-Json -Depth 4) | Out-Null
        }
        Write-Host "GitLab release $tag ready"
    }
    Write-Host "Published $tag"
}
