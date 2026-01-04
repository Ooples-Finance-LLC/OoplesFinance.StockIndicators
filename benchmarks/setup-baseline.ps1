param(
    [string]$Ref = "master"
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (git -C $scriptRoot rev-parse --show-toplevel).Trim()
$baselinePath = Join-Path $scriptRoot ".baseline"

if (Test-Path $baselinePath)
{
    Write-Host "Baseline already exists at $baselinePath"
    exit 0
}

git -C $repoRoot worktree add $baselinePath $Ref
