[CmdletBinding()]
param(
    [string]$Branch = 'codex/v023-settlement-details-parallel',
    [string]$CommitMessage = 'feat: implement v0.2.3 settlement boundaries and deterministic parallelism',
    [string]$Tag = 'v0.2.3',
    [switch]$SkipTests,
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$gitCommand = Get-Command git -ErrorAction Stop

& (Join-Path $repoRoot 'tools\Test-RepositoryHygiene.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Repository hygiene validation failed.' }

if (-not $SkipTests) {
    & (Join-Path $repoRoot 'build.ps1') -Configuration Release -RunTests
    if ($LASTEXITCODE -ne 0) { throw 'Build or tests failed.' }
}

$currentBranch = (& $gitCommand.Source -C $repoRoot branch --show-current).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Unable to read current branch.' }
if (-not [string]::Equals($currentBranch, $Branch, [StringComparison]::Ordinal)) {
    & $gitCommand.Source -C $repoRoot show-ref --verify --quiet "refs/heads/$Branch"
    if ($LASTEXITCODE -eq 0) {
        & $gitCommand.Source -C $repoRoot switch $Branch
    } else {
        & $gitCommand.Source -C $repoRoot switch -c $Branch
    }
    if ($LASTEXITCODE -ne 0) { throw "Unable to switch to baseline branch $Branch." }
}

& $gitCommand.Source -C $repoRoot add --all
if ($LASTEXITCODE -ne 0) { throw 'git add failed.' }

$staged = @(& $gitCommand.Source -C $repoRoot diff --cached --name-only)
$forbidden = @($staged | Where-Object {
    $normalized = $_.Replace('\', '/')
    ($normalized -match '(^|/)(bin|obj|outputs|work)/') -or
    ($normalized -match '^logs/' -and $normalized -ne 'logs/.gitkeep')
})
if ($forbidden.Count -gt 0) {
    throw "Refusing to commit generated/runtime files:`n$($forbidden -join [Environment]::NewLine)"
}

& $gitCommand.Source -C $repoRoot diff --cached --check
if ($LASTEXITCODE -ne 0) { throw 'Staged diff validation failed.' }
& $gitCommand.Source -C $repoRoot commit -m $CommitMessage
if ($LASTEXITCODE -ne 0) { throw 'git commit failed.' }

if (-not [string]::IsNullOrWhiteSpace($Tag)) {
    & $gitCommand.Source -C $repoRoot show-ref --verify --quiet "refs/tags/$Tag"
    if ($LASTEXITCODE -eq 0) {
        $tagCommit = (& $gitCommand.Source -C $repoRoot rev-list -n 1 $Tag).Trim()
        $headCommit = (& $gitCommand.Source -C $repoRoot rev-parse HEAD).Trim()
        if ($tagCommit -ne $headCommit) {
            throw "Tag $Tag already points to another commit: $tagCommit"
        }
    } else {
        & $gitCommand.Source -C $repoRoot tag -a $Tag -m "World Sim $Tag release"
        if ($LASTEXITCODE -ne 0) { throw "Unable to create tag $Tag." }
    }
}

if ($Publish) {
    & (Join-Path $repoRoot 'publish.ps1') -Configuration Release -SkipTests
    if ($LASTEXITCODE -ne 0) { throw 'Clean release publish failed.' }
}

& (Join-Path $repoRoot 'tools\Test-RepositoryHygiene.ps1') -RequireClean
if ($LASTEXITCODE -ne 0) { throw 'The committed baseline is not clean.' }

$commit = (& $gitCommand.Source -C $repoRoot rev-parse HEAD).Trim()
Write-Host "GIT_BASELINE_OK branch=$Branch commit=$commit tag=$Tag"
