#Requires -Version 7.0
<#
.SYNOPSIS
Lints a commit message file against this repo's conventional-commit rules.
Exits 0 when the message passes, 1 with reasons when it does not.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$MessageFile
)

$ErrorActionPreference = 'Stop'

$lines = @((Get-Content -LiteralPath $MessageFile -Raw) -split "`r?`n" | Where-Object { $_ -notmatch '^\s*#' })
$subject = $lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1

if (-not $subject) {
    Write-Host 'Commit message rejected: message is empty.'
    exit 1
}

# Let git-generated messages through untouched.
if ($subject -match '^(fixup!|squash!|Merge |Revert )') {
    exit 0
}

$failures = [System.Collections.Generic.List[string]]::new()

$types = 'build|chore|ci|docs|feat|fix|perf|refactor|style|test'
$scopes = 'core|app|ui|shell|vt|installer|ci|docs|tooling|deps'

if ($subject -cnotmatch "^($types)(\(($scopes)\))?!?: [a-z0-9]") {
    $failures.Add("Subject must be 'type(scope): summary' — type in [$types], optional scope in [$scopes], summary starting lowercase.")
}

if ($subject.Length -gt 72) {
    $failures.Add("Subject is $($subject.Length) characters; the limit is 72.")
}

if ($subject -match '\.\s*$') {
    $failures.Add('Subject must not end with a period.')
}

$summary = ($subject -creplace '^[a-z]+(\([a-z]+\))?!?:\s*', '')
$firstWord = ($summary -split '\s+' | Select-Object -First 1)
if ($firstWord -match '^(added|adds|adding|fixed|fixes|fixing|updated|updates|updating|changed|changes|changing|removed|removes|removing|improved|improves|improving)$') {
    $failures.Add("Summary must be imperative ('add', not '$firstWord').")
}

$subjectIndex = [Array]::IndexOf($lines, $subject)
if ($lines.Count -gt ($subjectIndex + 1) -and -not [string]::IsNullOrWhiteSpace($lines[$subjectIndex + 1])) {
    $failures.Add('The line after the subject must be blank.')
}

foreach ($line in $lines) {
    if ($line -match '^\s*(co-authored-by|signed-off-by)\s*:' ) {
        $failures.Add('Author trailers are not allowed.')
        break
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'Commit message rejected:'
    foreach ($failure in $failures) {
        Write-Host " - $failure"
    }
    Write-Host "Subject: $subject"
    exit 1
}

exit 0
