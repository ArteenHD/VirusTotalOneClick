#Requires -Version 7.0
<#
.SYNOPSIS
Points git at the repo's versioned hooks. Run once after cloning.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
git -C $repoRoot config core.hooksPath scripts/hooks
if ($LASTEXITCODE -ne 0) {
    throw 'Could not configure core.hooksPath.'
}

Write-Host 'Git hooks configured: core.hooksPath -> scripts/hooks'
