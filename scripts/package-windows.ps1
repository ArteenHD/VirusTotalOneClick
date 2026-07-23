param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $PublishPath = "build\publish\win-x64",
    [string] $PackagePath = "build\package",
    [switch] $SkipPublish,
    [switch] $IncludeSymbols
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = if ([System.IO.Path]::IsPathRooted($PublishPath)) { $PublishPath } else { Join-Path $repoRoot $PublishPath }
$packageDir = if ([System.IO.Path]::IsPathRooted($PackagePath)) { $PackagePath } else { Join-Path $repoRoot $PackagePath }
$packageName = "VirusTotalOneClick-win-x64"

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot "publish-windows.ps1") -Configuration $Configuration -Runtime $Runtime -OutputPath $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $publishDir "VirusTotalOneClick.exe"))) {
    throw "Cannot find VirusTotalOneClick.exe in publish output '$publishDir'."
}

$stagingRoot = Join-Path ([System.IO.Path]::GetTempPath()) "vt-oneclick-package-$([guid]::NewGuid().ToString('N'))"
$stagingContent = Join-Path $stagingRoot $packageName

try {
    New-Item -ItemType Directory -Path $stagingContent -Force | Out-Null
    Copy-Item -Path (Join-Path $publishDir "*") -Destination $stagingContent -Recurse -Force

    Copy-Item -Path (Join-Path $PSScriptRoot "install-local.ps1") -Destination $stagingContent -Force
    Copy-Item -Path (Join-Path $PSScriptRoot "uninstall-local.ps1") -Destination $stagingContent -Force
    Copy-Item -Path (Join-Path $repoRoot "LICENSE") -Destination $stagingContent -Force
    Copy-Item -Path (Join-Path $repoRoot "THIRD_PARTY_NOTICES.md") -Destination $stagingContent -Force

    if (-not $IncludeSymbols) {
        Get-ChildItem -Path $stagingContent -Filter "*.pdb" -Recurse | Remove-Item -Force
    }

    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
    $zipPath = Join-Path $packageDir "$packageName.zip"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $stagingRoot,
        $zipPath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}

$stream = [System.IO.File]::OpenRead($zipPath)
try {
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = [System.BitConverter]::ToString($sha256.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}
finally {
    $stream.Dispose()
}

$checksumPath = "$zipPath.sha256"
$checksumLine = "$hash  $(Split-Path -Leaf $zipPath)"
Set-Content -LiteralPath $checksumPath -Value $checksumLine -Encoding ascii

& (Join-Path $PSScriptRoot "verify-package.ps1") -PackagePath $zipPath -ChecksumPath $checksumPath -AllowSymbols:$IncludeSymbols
if ($LASTEXITCODE -ne 0) {
    throw "Package verification failed for '$zipPath'."
}

Write-Host "Packaged VirusTotal OneClick to $zipPath"
