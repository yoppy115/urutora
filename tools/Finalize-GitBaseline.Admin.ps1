#requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$SkipTests,
    [switch]$Publish,
    [switch]$Elevated
)

$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
$isAdministrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdministrator) {
    if ($Elevated) {
        throw 'Elevation was requested but the process is not running as Administrator.'
    }

    $powershellHost = Join-Path $PSHOME 'powershell.exe'
    if (-not (Test-Path -LiteralPath $powershellHost -PathType Leaf)) {
        $powershellHost = (Get-Command powershell.exe -ErrorAction Stop).Source
    }
    $quotedScript = '"' + $PSCommandPath.Replace('"', '""') + '"'
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File $quotedScript -Elevated"
    if ($SkipTests) { $arguments += ' -SkipTests' }
    if ($Publish) { $arguments += ' -Publish' }
    $process = Start-Process -FilePath $powershellHost -Verb RunAs -ArgumentList $arguments -Wait -PassThru
    exit $process.ExitCode
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot '.git') -PathType Container)) {
    throw "Expected urutora .git directory was not found: $repoRoot"
}

$finalizer = Join-Path $repoRoot 'tools\Finalize-GitBaseline.ps1'
& $finalizer -SkipTests:$SkipTests -Publish:$Publish
if ($LASTEXITCODE -ne 0) {
    throw "Git baseline finalization failed with exit code $LASTEXITCODE."
}

Write-Host 'Administrator-scoped baseline finalization completed.'
Write-Host 'Rollback is Git-only: delete tag v0.2.3 if needed, then revert the created commit.'
