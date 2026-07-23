param(
    [string] $SourcePath,
    [string] $InstallPath = (Join-Path $env:LOCALAPPDATA "Programs\VirusTotalOneClick"),
    [switch] $SkipShellIntegration,
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
    # Close the redirected stdin right away: a child that reads from it
    # (rather than ignoring it) would otherwise block until the timeout below
    # forces it closed, instead of seeing end-of-input and exiting on its own.
    $process.StandardInput.Close()
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
            if (-not $process.WaitForExit(5000)) {
                throw "'$ExecutablePath' did not exit within 5 seconds of being asked to terminate."
            }
        }
        return $process.ExitCode
    }
    finally {
        $process.Dispose()
    }
}

if (-not $SourcePath) {
    $sideBySideExe = Join-Path $PSScriptRoot "VirusTotalOneClick.exe"
    if (Test-Path -LiteralPath $sideBySideExe) {
        $SourcePath = $PSScriptRoot
    }
    else {
        $SourcePath = Join-Path (Split-Path -Parent $PSScriptRoot) "build\publish\win-x64"
    }
}

$sourceExe = Join-Path $SourcePath "VirusTotalOneClick.exe"
if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "Cannot find VirusTotalOneClick.exe in '$SourcePath'."
}

New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
Copy-Item -Path (Join-Path $SourcePath "*") -Destination $InstallPath -Recurse -Force

if (-not $SkipShellIntegration) {
    $installedExe = Join-Path $InstallPath "VirusTotalOneClick.exe"
    $exitCode = Invoke-AppVerb -ExecutablePath $installedExe -Verb "install-shell" -TimeoutSeconds $ShellIntegrationTimeoutSeconds
    if ($exitCode -ne 0) {
        throw "Shell integration install failed with exit code $exitCode."
    }
}

Write-Host "Installed VirusTotal OneClick to $InstallPath"
