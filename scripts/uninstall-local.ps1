param(
    [string] $InstallPath = (Join-Path $env:LOCALAPPDATA "Programs\VirusTotalOneClick"),
    [switch] $SkipShellIntegration,
    [switch] $RemoveAppData,
    [string] $AppDataPath = (Join-Path $env:APPDATA "VirusTotalOneClick"),
    [switch] $KeepAppFiles,
    [int] $ShellIntegrationTimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"

function Invoke-AppVerb {
    param([string] $ExecutablePath, [string] $Verb, [int] $TimeoutSeconds)

    # The child runs without a window and with its input redirected so a
    # misbehaving executable can never sit waiting on a console. A child that
    # does not finish in time is stopped and reported as a failure.
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $ExecutablePath
    $startInfo.Arguments = $Verb
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true

    $process = [System.Diagnostics.Process]::Start($startInfo)
    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            # The portable scripts must run under Windows PowerShell as well as
            # pwsh, and the kill-process-tree overload of Kill() is only
            # available on .NET 5+ / PowerShell 6+. Windows PowerShell 5.1
            # falls back to taskkill to stop the whole child tree.
            if ($PSVersionTable.PSVersion.Major -ge 6) {
                $process.Kill($true)
            }
            else {
                & taskkill.exe /T /F /PID $process.Id *> $null
            }
            $process.WaitForExit()
        }
        return $process.ExitCode
    }
    finally {
        $process.Dispose()
    }
}

function Assert-SafeInstallPath {
    param([string] $Path)

    $normalized = $Path.TrimEnd('\')
    $parent = Split-Path -Parent $normalized
    $leaf = Split-Path -Leaf $normalized
    $expectedParent = (Join-Path $env:LOCALAPPDATA "Programs").TrimEnd('\')

    $isSafe = [string]::Equals($parent, $expectedParent, [System.StringComparison]::OrdinalIgnoreCase) `
        -and [string]::Equals($leaf, "VirusTotalOneClick", [System.StringComparison]::Ordinal)
    if (-not $isSafe) {
        throw "Refusing to remove '$Path': it is not the expected per-user install folder."
    }
}

function Assert-SafeAppDataPath {
    param([string] $Path)

    $normalized = $Path.TrimEnd('\')
    $parent = Split-Path -Parent $normalized
    $leaf = Split-Path -Leaf $normalized
    $expectedParent = $env:APPDATA.TrimEnd('\')

    $isSafe = [string]::Equals($parent, $expectedParent, [System.StringComparison]::OrdinalIgnoreCase) `
        -and [string]::Equals($leaf, "VirusTotalOneClick", [System.StringComparison]::Ordinal)
    if (-not $isSafe) {
        throw "Refusing to remove '$Path': it is not the expected app-data folder."
    }
}

if (-not $SkipShellIntegration) {
    $installedExe = Join-Path $InstallPath "VirusTotalOneClick.exe"
    if (Test-Path -LiteralPath $installedExe) {
        $exitCode = Invoke-AppVerb -ExecutablePath $installedExe -Verb "uninstall-shell" -TimeoutSeconds $ShellIntegrationTimeoutSeconds
        if ($exitCode -ne 0) {
            throw "Shell integration uninstall failed with exit code $exitCode."
        }
    }
    else {
        # The app is already gone, so its own uninstall-shell verb cannot run.
        # Fall back to removing the shell surfaces directly.
        Remove-Item -Path "HKCU:\Software\Classes\*\shell\VirusTotalOneClick" -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -Path "HKCU:\Software\Classes\Directory\shell\VirusTotalOneClick" -Recurse -Force -ErrorAction SilentlyContinue

        $sendToShortcut = Join-Path $env:APPDATA "Microsoft\Windows\SendTo\Upload to VirusTotal.lnk"
        Remove-Item -LiteralPath $sendToShortcut -Force -ErrorAction SilentlyContinue

        $startMenuFolder = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\VirusTotal OneClick"
        $startMenuShortcut = Join-Path $startMenuFolder "VirusTotal OneClick.lnk"
        Remove-Item -LiteralPath $startMenuShortcut -Force -ErrorAction SilentlyContinue

        $startupShortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup\VirusTotal OneClick Settings.lnk"
        Remove-Item -LiteralPath $startupShortcut -Force -ErrorAction SilentlyContinue

        if ((Test-Path -LiteralPath $startMenuFolder) -and -not (Get-ChildItem -LiteralPath $startMenuFolder -ErrorAction SilentlyContinue)) {
            Remove-Item -LiteralPath $startMenuFolder -Force -ErrorAction SilentlyContinue
        }
    }
}

if (-not $KeepAppFiles) {
    Assert-SafeInstallPath -Path $InstallPath
    if (Test-Path -LiteralPath $InstallPath) {
        Remove-Item -LiteralPath $InstallPath -Recurse -Force
    }
}

if ($RemoveAppData) {
    Assert-SafeAppDataPath -Path $AppDataPath
    if (Test-Path -LiteralPath $AppDataPath) {
        Remove-Item -LiteralPath $AppDataPath -Recurse -Force
    }
}

Write-Host "Uninstalled VirusTotal OneClick from $InstallPath"
