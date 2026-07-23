param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $OutputPath = "build\publish\win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\VirusTotalOneClick\VirusTotalOneClick.csproj"
$output = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }

# Publishing restores with SDK-managed tooling packages (single-file/trimming tasks)
# that the locked graph deliberately does not carry, so this restore runs unlocked
# into a scratch lock file instead of rewriting the committed one; package versions
# stay pinned by exact PackageReference versions and the SDK version in global.json.
# The build/test gate still restores in locked mode against the committed lock files.
$scratchLockFile = Join-Path $repoRoot "build\publish.packages.lock.json"
dotnet publish $project `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:NuGetLockFilePath=$scratchLockFile `
    -p:RestoreDisableParallel=true `
    --output $output
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host "Published VirusTotal OneClick to $output"
