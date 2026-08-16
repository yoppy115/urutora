Set-StrictMode -Version Latest

function Get-RepositoryProvenance {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryRoot
    )

    $resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    $gitCommand = Get-Command git -ErrorAction Stop
    $commit = (& $gitCommand.Source -C $resolvedRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-fA-F]{40}$') {
        throw "Unable to resolve a full Git commit for $resolvedRoot."
    }

    $status = @(& $gitCommand.Source -C $resolvedRoot status --porcelain=v1 --untracked-files=normal)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to inspect the Git worktree for $resolvedRoot."
    }

    [pscustomobject]@{
        Commit = $commit.ToLowerInvariant()
        TreeState = if ($status.Count -eq 0) { 'clean' } else { 'dirty' }
        Changes = $status
    }
}
