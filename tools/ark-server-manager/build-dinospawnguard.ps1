param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'build\DinoSpawnGuard.dll')
)

$ErrorActionPreference = 'Stop'
$toolRoot = Join-Path $PSScriptRoot 'vendor\msvc\MSVC'
$vcRoot = Join-Path $toolRoot 'VC\Tools\MSVC\14.40.33807'
$kitRoot = Join-Path $toolRoot 'Windows Kits\10'
$apiRoot = Join-Path $PSScriptRoot 'vendor\AseApi\AseApi-main'
$compiler = Join-Path $vcRoot 'bin\Hostx64\x64\cl.exe'
$source = Join-Path $PSScriptRoot 'src\DinoSpawnGuard\DinoSpawnGuard.cpp'

foreach ($required in @($compiler, $source, (Join-Path $apiRoot 'out_lib\ArkApi.lib'))) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required build file was not found: $required" }
}
New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null

$env:INCLUDE = @(
    (Join-Path $vcRoot 'include'),
    (Join-Path $kitRoot 'Include\10.0.26100.0\ucrt'),
    (Join-Path $kitRoot 'Include\10.0.26100.0\shared'),
    (Join-Path $kitRoot 'Include\10.0.26100.0\um'),
    (Join-Path $kitRoot 'Include\10.0.26100.0\winrt'),
    (Join-Path $apiRoot 'version\Core\Public')
) -join ';'
$env:LIB = @(
    (Join-Path $vcRoot 'lib\x64'),
    (Join-Path $kitRoot 'Lib\10.0.26100.0\ucrt\x64'),
    (Join-Path $kitRoot 'Lib\10.0.26100.0\um\x64'),
    (Join-Path $apiRoot 'out_lib')
) -join ';'

$objectPath = Join-Path (Split-Path -Parent $OutputPath) 'DinoSpawnGuard.obj'
& $compiler /nologo /LD /MT /O2 /EHsc /std:c++17 /utf-8 /DARK_GAME /D_WINDOWS /D_USRDLL /DUNICODE /D_UNICODE /DNOMINMAX `
    (('/Fo' + $objectPath)) $source /link ('/OUT:' + $OutputPath) ArkApi.lib
if ($LASTEXITCODE -ne 0) { throw 'DinoSpawnGuard build failed.' }

Write-Output "BUILD_OK $OutputPath"
