#Requires -Version 7.2
<#
.SYNOPSIS
Lints every commit message in a range with lint-commit-message.ps1.
When BaseSha is empty, all zeros, or unknown, lints all commits reachable from HeadSha.
#>
[CmdletBinding()]
param(
    [string]$BaseSha,
    [Parameter(Mandatory = $true)]
    [string]$HeadSha
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$lintScript = Join-Path $PSScriptRoot 'lint-commit-message.ps1'

$baseIsUsable = $false
if (-not [string]::IsNullOrWhiteSpace($BaseSha) -and $BaseSha -notmatch '^0+$') {
    git -C $repoRoot cat-file -e "$BaseSha^{commit}" 2>$null
    $baseIsUsable = ($LASTEXITCODE -eq 0)
}

$commits = if ($baseIsUsable) {
    git -C $repoRoot rev-list "$BaseSha..$HeadSha"
}
else {
    git -C $repoRoot rev-list $HeadSha
}
if ($LASTEXITCODE -ne 0) {
    throw "Could not list commits for range '$BaseSha'..'$HeadSha'."
}

$failedCount = 0
foreach ($commit in @($commits | Where-Object { $_ })) {
    # Dependabot's generated messages carry a mandatory sign-off trailer and
    # package names that can push the subject past any length limit; they are
    # squash-merged under conventional subjects, so the branch commits pass.
    $author = git -C $repoRoot log -1 --format=%an $commit
    if ($author -eq 'dependabot[bot]') {
        Write-Host "skip $commit (dependabot)"
        continue
    }

    $messageFile = Join-Path ([System.IO.Path]::GetTempPath()) "commit-message-$commit.txt"
    git -C $repoRoot log -1 --format=%B $commit | Set-Content -LiteralPath $messageFile
    try {
        pwsh -NoProfile -File $lintScript -MessageFile $messageFile
        if ($LASTEXITCODE -ne 0) {
            Write-Host "FAIL $commit"
            $failedCount++
        }
        else {
            Write-Host "ok   $commit"
        }
    }
    finally {
        Remove-Item -LiteralPath $messageFile -Force -ErrorAction SilentlyContinue
    }
}

if ($failedCount -gt 0) {
    Write-Host "$failedCount commit message(s) failed lint."
    exit 1
}

Write-Host 'All commit messages pass.'
exit 0
