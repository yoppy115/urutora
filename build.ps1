[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$RunTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $repoRoot 'tools\RepositoryProvenance.ps1')
$provenance = Get-RepositoryProvenance -RepositoryRoot $repoRoot
$buildProvenanceProperties = @(
    "-p:RepositoryCommit=$($provenance.Commit)",
    "-p:RepositoryTreeState=$($provenance.TreeState)"
)
$solution = Join-Path $repoRoot 'WorldSim.sln'
$nugetConfig = Join-Path $repoRoot 'NuGet.Config'
$toolStateRoot = Join-Path $repoRoot 'work\dotnet-cli'
$env:DOTNET_CLI_HOME = Join-Path $toolStateRoot 'home'
$env:APPDATA = Join-Path $toolStateRoot 'appdata'
$env:NUGET_PACKAGES = Join-Path $toolStateRoot 'packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $toolStateRoot 'http-cache'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME, $env:APPDATA, $env:NUGET_PACKAGES | Out-Null
$dotnetCommand = Get-Command dotnet -ErrorAction Stop
$sdkList = & $dotnetCommand.Source --list-sdks
if ($LASTEXITCODE -ne 0 -or -not ($sdkList -match '^10\.')) {
    throw '.NET 10 SDK is required.'
}

& $dotnetCommand.Source restore $solution --configfile $nugetConfig --nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

Write-Host "Build provenance: commit=$($provenance.Commit) tree=$($provenance.TreeState)"
& $dotnetCommand.Source build $solution --configuration $Configuration --no-restore --nologo @buildProvenanceProperties
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

if ($RunTests) {
    $testAssemblies = @(
        (Join-Path $repoRoot "tests\Simulation.Core.Tests\bin\$Configuration\net10.0\Simulation.Core.Tests.dll"),
        (Join-Path $repoRoot "tests\Simulation.Runner.Tests\bin\$Configuration\net10.0\Simulation.Runner.Tests.dll"),
        (Join-Path $repoRoot "tests\Simulation.App.Tests\bin\$Configuration\net10.0-windows\Simulation.App.Tests.dll")
    )
    foreach ($testAssembly in $testAssemblies) {
        & $dotnetCommand.Source $testAssembly
        if ($LASTEXITCODE -ne 0) { throw "Tests failed: $testAssembly" }
    }
}
