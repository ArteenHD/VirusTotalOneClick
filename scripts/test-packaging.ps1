$ErrorActionPreference = "Stop"

$pester = Get-Module -ListAvailable -Name Pester | Where-Object { $_.Version -ge [version]'5.0' } | Select-Object -First 1
if (-not $pester) {
    Install-Module Pester -Force -Scope CurrentUser -SkipPublisherCheck -MinimumVersion 5.0
}
Import-Module Pester -MinimumVersion 5.0

$testsPath = Join-Path (Split-Path -Parent $PSScriptRoot) "tests\packaging"
$result = Invoke-Pester -Path $testsPath -Output Detailed -PassThru
if ($result.FailedCount -gt 0) {
    exit 1
}
exit 0
