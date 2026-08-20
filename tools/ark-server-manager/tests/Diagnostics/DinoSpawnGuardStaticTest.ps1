param(
    [string]$DllPath = (Join-Path $PSScriptRoot '..\..\build\DinoSpawnGuard.dll')
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$sourcePath = Join-Path $root 'src\DinoSpawnGuard\DinoSpawnGuard.cpp'
$configPath = Join-Path $root 'src\DinoSpawnGuard\config.json'
$infoPath = Join-Path $root 'src\DinoSpawnGuard\PluginInfo.json'

$source = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
$config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
$info = Get-Content -LiteralPath $infoPath -Raw -Encoding UTF8 | ConvertFrom-Json

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "ASSERT_FAILED: $Message" }
}

Assert-True ($info.Version -eq 1.0) 'PluginInfo version must be 1.0.'
Assert-True ($info.MinApiVersion -le 3.56) 'Plugin must support the installed ArkApi version.'
Assert-True ($config.Enabled -eq $true) 'The deployed default must be enabled.'
Assert-True ($config.DryRun -eq $false) 'The requested relocation mode must be active.'
Assert-True ($config.DinosPerTick -ge 1 -and $config.DinosPerTick -le 200) 'Batch size is outside the guarded range.'
Assert-True ($config.MaxRelocationDistance -le 2500) 'Relocation must stay local to the original realm.'

$patterns = @($config.MajorDinoClasses | ForEach-Object { $_.ToLowerInvariant() })
Assert-True ($patterns.Count -eq ($patterns | Select-Object -Unique).Count) 'MajorDinoClasses contains duplicates.'
foreach ($required in @('rex_character', 'gigant_character', 'carcha_character', 'lionfishlion_character', 'andrewsarchus_character', 'crab_character')) {
    Assert-True ($patterns -contains $required) "Required major class is missing: $required"
}
foreach ($forbidden in @('wyvern', 'griffin', 'desmodus', 'fjordhawk', 'tuso', 'basilosaurus', 'boss')) {
    Assert-True (-not ($patterns -match $forbidden)) "Unsafe default class is present: $forbidden"
}

foreach ($requiredToken in @(
    'LastTimeSavedWorldField',
    'DelayAfterSaveSeconds',
    'EncroachingBlockingGeometry',
    'FindTeleportSpot',
    'TeleportTo',
    'BPIsTamed',
    'TargetingTeamField() >= 50000',
    'AddRconCommand("DinoSpawnGuard.Pause"',
    'AddRconCommand("DinoSpawnGuard.Resume"'
)) {
    Assert-True ($source.Contains($requiredToken)) "Safety token is missing: $requiredToken"
}
Assert-True (-not $source.Contains('DinoSpawnGuard.Run')) 'Manual runs would violate after-save-only behavior.'
Assert-True (-not $source.Contains('DestroyActor')) 'The plugin must never destroy a dino on relocation failure.'

Assert-True (Test-Path -LiteralPath $DllPath -PathType Leaf) "Built DLL is missing: $DllPath"
$bytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $DllPath))
$peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
$machine = [BitConverter]::ToUInt16($bytes, $peOffset + 4)
Assert-True ($machine -eq 0x8664) 'DLL is not an x64 PE image.'

Write-Output "DINOSPAWNGUARD_STATIC_OK classes=$($patterns.Count) machine=0x$($machine.ToString('X4'))"
