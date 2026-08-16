[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests,
    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $repoRoot 'tools\RepositoryProvenance.ps1')
$provenance = Get-RepositoryProvenance -RepositoryRoot $repoRoot
if ($provenance.TreeState -ne 'clean' -and -not $AllowDirty) {
    throw 'Release publish requires a clean Git worktree. Commit or stash changes, or use -AllowDirty for a non-release diagnostic build.'
}
$buildProvenanceProperties = @(
    "-p:RepositoryCommit=$($provenance.Commit)",
    "-p:RepositoryTreeState=$($provenance.TreeState)"
)
$appProject = Join-Path $repoRoot 'src\Simulation.App\Simulation.App.csproj'
$runnerProject = Join-Path $repoRoot 'src\Simulation.Runner\Simulation.Runner.csproj'
$versionProps = [xml](Get-Content -Raw (Join-Path $repoRoot 'Directory.Build.props'))
$assemblyVersion = [Version]$versionProps.Project.PropertyGroup.Version
$releaseVersion = "v$($assemblyVersion.Major).$($assemblyVersion.Minor)"
$outputDirectory = Join-Path $repoRoot "outputs\World Sim $releaseVersion"
$dotnetCommand = Get-Command dotnet -ErrorAction Stop

$buildParameters = @{
    Configuration = $Configuration
    RunTests = -not $SkipTests
}

& (Join-Path $repoRoot 'build.ps1') @buildParameters
if ($LASTEXITCODE -ne 0) { throw 'Build or tests failed.' }

New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
& $dotnetCommand.Source publish $appProject `
    --configuration $Configuration `
    --no-restore `
    --nologo `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    @buildProvenanceProperties `
    --output $outputDirectory
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$runnerOutputDirectory = Join-Path $outputDirectory 'tools\Simulation.Runner'
New-Item -ItemType Directory -Force -Path $runnerOutputDirectory | Out-Null
& $dotnetCommand.Source publish $runnerProject `
    --configuration $Configuration `
    --no-restore `
    --nologo `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    @buildProvenanceProperties `
    --output $runnerOutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Simulation.Runner publish failed.' }

$artifactRecords = @(Get-ChildItem -LiteralPath $outputDirectory -File -Recurse |
    Where-Object {
        $relative = $_.FullName.Substring($outputDirectory.Length).TrimStart('\', '/')
        $relative -notmatch '^(logs)(\\|/)' -and $relative -ne 'release-manifest.json'
    } |
    Sort-Object FullName |
    ForEach-Object {
        [ordered]@{
            path = $_.FullName.Substring($outputDirectory.Length).TrimStart('\', '/').Replace('\', '/')
            bytes = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    })
$manifest = [ordered]@{
    schemaVersion = 1
    releaseVersion = $releaseVersion
    applicationVersion = $assemblyVersion.ToString()
    repositoryCommit = $provenance.Commit
    repositoryTreeState = $provenance.TreeState
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    artifacts = $artifactRecords
}
$manifestPath = Join-Path $outputDirectory 'release-manifest.json'
$temporaryManifestPath = $manifestPath + '.tmp'
[IO.File]::WriteAllText(
    $temporaryManifestPath,
    (($manifest | ConvertTo-Json -Depth 6) + [Environment]::NewLine),
    [Text.UTF8Encoding]::new($false))
Move-Item -LiteralPath $temporaryManifestPath -Destination $manifestPath -Force

Write-Host "Published App: $outputDirectory\Simulation.App.exe"
Write-Host "Published SimRunner: $runnerOutputDirectory\Simulation.Runner.exe"
Write-Host "Release manifest: $manifestPath"
