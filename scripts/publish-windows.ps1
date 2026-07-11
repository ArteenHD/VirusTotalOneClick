param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $OutputPath = "build\publish\win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\VirusTotalOneClick\VirusTotalOneClick.csproj"
$output = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }

dotnet publish $project `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:RestoreLockedMode=true `
    --output $output
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host "Published VirusTotal OneClick to $output"
