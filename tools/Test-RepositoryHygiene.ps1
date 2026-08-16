[CmdletBinding()]
param(
    [switch]$RequireClean
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$errors = [Collections.Generic.List[string]]::new()
$gitCommand = Get-Command git -ErrorAction Stop

function Get-RelativePathCompat {
    param(
        [Parameter(Mandatory)] [string]$BasePath,
        [Parameter(Mandatory)] [string]$TargetPath
    )

    $baseFullPath = [IO.Path]::GetFullPath($BasePath).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $targetFullPath = [IO.Path]::GetFullPath($TargetPath)
    $baseUri = [Uri]$baseFullPath
    $targetUri = [Uri]$targetFullPath
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace('/', '\')
}

$tracked = @(& $gitCommand.Source -C $repoRoot ls-files)
if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed.' }
foreach ($path in $tracked) {
    $normalized = $path.Replace('\', '/')
    if (($normalized -match '(^|/)(bin|obj|outputs|work)/') -or
        ($normalized -match '^logs/' -and $normalized -ne 'logs/.gitkeep')) {
        $errors.Add("Generated/runtime path is tracked: $path")
    }
}

$deprecated = Join-Path $repoRoot 'docs\design\WORLD_CYCLE.md'
if (Test-Path -LiteralPath $deprecated) {
    $errors.Add('Deprecated docs/design/WORLD_CYCLE.md still exists; use WORLD_LIFECYCLE.md.')
}

foreach ($marker in Get-ChildItem -LiteralPath $repoRoot -Filter '.gitkeep' -File -Recurse |
             Where-Object { $_.FullName -notmatch '[\\/](\.git|bin|obj|outputs|work)[\\/]' }) {
    if ((Get-RelativePathCompat -BasePath $repoRoot -TargetPath $marker.FullName).Replace('\', '/') -eq 'logs/.gitkeep') {
        continue
    }
    $otherEntries = @(Get-ChildItem -LiteralPath $marker.DirectoryName -Force |
        Where-Object Name -ne '.gitkeep')
    if ($otherEntries.Count -gt 0) {
        $relative = Get-RelativePathCompat -BasePath $repoRoot -TargetPath $marker.FullName
        $errors.Add("Unnecessary .gitkeep in a non-empty directory: $relative")
    }
}

$markdownFiles = @(
    Get-Item -LiteralPath (Join-Path $repoRoot 'README.md'), (Join-Path $repoRoot 'AGENTS.md')
    Get-ChildItem -LiteralPath (Join-Path $repoRoot 'docs') -Filter '*.md' -File -Recurse
    Get-ChildItem -LiteralPath (Join-Path $repoRoot 'simulation') -Filter '*.md' -File -Recurse
    Get-ChildItem -LiteralPath (Join-Path $repoRoot 'research') -Filter '*.md' -File -Recurse
    Get-ChildItem -LiteralPath (Join-Path $repoRoot 'tools') -Filter '*.md' -File -Recurse
)
$linkPattern = [regex]'\[[^\]]*\]\((?<target>[^)]+)\)'
foreach ($markdown in $markdownFiles | Sort-Object FullName -Unique) {
    $content = Get-Content -LiteralPath $markdown.FullName -Raw
    foreach ($match in $linkPattern.Matches($content)) {
        $target = $match.Groups['target'].Value.Trim().Trim('<', '>')
        if ($target -match '^(https?://|mailto:|#)' -or [string]::IsNullOrWhiteSpace($target)) {
            continue
        }

        $pathPart = [Uri]::UnescapeDataString(($target -split '#', 2)[0])
        if ([string]::IsNullOrWhiteSpace($pathPart)) { continue }
        $resolved = [IO.Path]::GetFullPath((Join-Path $markdown.DirectoryName $pathPart))
        if (-not (Test-Path -LiteralPath $resolved)) {
            $relativeMarkdown = Get-RelativePathCompat -BasePath $repoRoot -TargetPath $markdown.FullName
            $errors.Add("Broken local link in ${relativeMarkdown}: $target")
        }
    }
}

if ($RequireClean) {
    $status = @(& $gitCommand.Source -C $repoRoot status --porcelain=v1 --untracked-files=normal)
    if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }
    if ($status.Count -gt 0) {
        $errors.Add("Git worktree is not clean:`n$($status -join [Environment]::NewLine)")
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "REPOSITORY_HYGIENE_OK tracked=$($tracked.Count) markdown=$($markdownFiles.Count) cleanRequired=$RequireClean"
