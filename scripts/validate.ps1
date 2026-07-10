#Requires -Version 7.0
<#
.SYNOPSIS
The validation gate: locked restore, format verification, build with
warnings as errors, full test run. Exits 0 only when everything is green.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    Write-Host '== Restore (locked mode) =='
    dotnet restore --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

    Write-Host '== Format verification =='
    dotnet format --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Formatting check failed. Run 'dotnet format' and review the diff." }

    Write-Host '== Build (warnings as errors) =='
    dotnet build --no-restore -warnaserror --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

    Write-Host '== Tests =='
    dotnet test --no-build --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

    Write-Host 'Validation passed.'
}
finally {
    Pop-Location
}
