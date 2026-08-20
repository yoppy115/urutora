param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'build\ARK Server Manager.exe'),
    [switch]$RunTests
)

$ErrorActionPreference = 'Stop'
$sourceRoot = Join-Path $PSScriptRoot 'src\ArkServerManager'
$testRoot = Join-Path $PSScriptRoot 'tests\ArkServerManager'
$buildRoot = Split-Path -Parent $OutputPath
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'C# compiler was not found.'
}
New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null

$commonArguments = @(
    '/nologo',
    '/platform:x64',
    '/optimize+',
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Xml.Linq.dll',
    (Join-Path $sourceRoot 'ArkServerManager.cs'),
    (Join-Path $sourceRoot 'RemoteControlServer.cs'),
    (Join-Path $sourceRoot 'ScheduleLogic.cs')
)

$mapPath = (Resolve-Path (Join-Path $sourceRoot 'assets\FjordurMap.png')).Path
$managerArguments = @(
    '/target:winexe',
    ('/out:' + $OutputPath),
    ('/win32icon:' + (Join-Path $sourceRoot 'assets\ark-manager.ico')),
    ('/win32manifest:' + (Join-Path $sourceRoot 'ArkServerManager.manifest')),
    ('/resource:' + $mapPath + ',ArkServerManager.FjordurMap.png')
) + $commonArguments

& $compiler $managerArguments
if ($LASTEXITCODE -ne 0) { throw 'ARK Server Manager build failed.' }

if ($RunTests) {
    $testExe = Join-Path $buildRoot 'ScheduleIntegrationTest.exe'
    $testArguments = @(
        '/target:exe',
        '/main:ArkServerManager.ScheduleIntegrationTest',
        ('/out:' + $testExe)
    ) + $commonArguments + (Join-Path $testRoot 'ScheduleIntegrationTest.cs')
    & $compiler $testArguments
    if ($LASTEXITCODE -ne 0) { throw 'Schedule integration test build failed.' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Schedule integration test failed.' }
}

$version = (Get-Item -LiteralPath $OutputPath).VersionInfo.FileVersion
Write-Output "BUILD_OK $version $OutputPath"
