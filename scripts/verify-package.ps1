param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath,

    [Parameter(Mandatory = $true)]
    [string] $ChecksumPath,

    [switch] $AllowSymbols
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $PackagePath)) {
    throw "Cannot find package at '$PackagePath'."
}
if (-not (Test-Path -LiteralPath $ChecksumPath)) {
    throw "Cannot find checksum file at '$ChecksumPath'."
}

$checksumLine = (Get-Content -LiteralPath $ChecksumPath -Raw).Trim()
$expectedHash = ($checksumLine -split '\s+')[0]

$stream = [System.IO.File]::OpenRead($PackagePath)
try {
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $actualHash = [System.BitConverter]::ToString($sha256.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}
finally {
    $stream.Dispose()
}

if (-not [string]::Equals($actualHash, $expectedHash, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Package verification failed: checksum mismatch for '$PackagePath'."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$packageName = [System.IO.Path]::GetFileNameWithoutExtension($PackagePath)

$archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
try {
    $entries = $archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }

    $requiredEntries = @(
        "$packageName/VirusTotalOneClick.exe",
        "$packageName/install-local.ps1",
        "$packageName/uninstall-local.ps1"
    )
    foreach ($required in $requiredEntries) {
        if ($entries -notcontains $required) {
            throw "Package verification failed: missing required entry '$required' in '$PackagePath'."
        }
    }

    if (-not $AllowSymbols) {
        $symbolEntries = $entries | Where-Object { $_.EndsWith('.pdb') }
        if ($symbolEntries) {
            throw "Package verification failed: package contains symbol (.pdb) files; pass -AllowSymbols to allow them."
        }
    }
}
finally {
    $archive.Dispose()
}

"Package verified: $PackagePath"
exit 0
