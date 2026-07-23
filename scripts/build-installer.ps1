param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $PublishPath = "build\publish\win-x64",
    [string] $InnoSetupCompiler,
    [switch] $SkipPublish
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishAbsolute = if ([System.IO.Path]::IsPathRooted($PublishPath)) { $PublishPath } else { Join-Path $repoRoot $PublishPath }

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot "publish-windows.ps1") -Configuration $Configuration -Runtime $Runtime -OutputPath $publishAbsolute
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed with exit code $LASTEXITCODE."
    }
}

if (-not $InnoSetupCompiler) {
    $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($command) {
        $InnoSetupCompiler = $command.Source
    }
    else {
        $candidates = @(
            (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
            (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
        )
        $found = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
        if ($found) {
            $InnoSetupCompiler = $found
        }
        else {
            throw "Cannot find ISCC.exe. Install Inno Setup or pass -InnoSetupCompiler."
        }
    }
}

$installerScript = Join-Path $repoRoot "installer\VirusTotalOneClick.iss"

$buildProps = Join-Path $repoRoot "Directory.Build.props"
$version = ([xml](Get-Content -Raw $buildProps)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "No <Version> found in $buildProps."
}

& $InnoSetupCompiler "/DPublishPath=$PublishPath" "/DAppVersion=$version" $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed with exit code $LASTEXITCODE."
}

Write-Host "Built installer for VirusTotal OneClick $version"
