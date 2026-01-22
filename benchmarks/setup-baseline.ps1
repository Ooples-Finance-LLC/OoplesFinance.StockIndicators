param(
    [string]$Ref = "master",
    [string]$Framework = "net10.0",
    [switch]$NoBuild
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (git -C $scriptRoot rev-parse --show-toplevel).Trim()
$baselinePath = Join-Path $scriptRoot ".baseline"

# Create worktree if it doesn't exist
$worktreeCreated = $false
if (-not (Test-Path $baselinePath))
{
    Write-Host "Creating baseline worktree from '$Ref'..."
    git -C $repoRoot worktree add $baselinePath $Ref
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create worktree"
        exit 1
    }
    $worktreeCreated = $true
}
else
{
    Write-Host "Baseline worktree already exists at $baselinePath"
}

# Build baseline with correct assembly name
if (-not $NoBuild)
{
    $baselineCsproj = Join-Path $baselinePath "src\OoplesFinance.StockIndicators.csproj"
    if (Test-Path $baselineCsproj)
    {
        Write-Host "Building baseline with AssemblyName=OoplesFinance.StockIndicators.Original..."
        dotnet build -c Release `
            -p:AssemblyName=OoplesFinance.StockIndicators.Original `
            -p:TargetFramework=$Framework `
            $baselineCsproj

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to build baseline"
            exit 1
        }

        $expectedDll = Join-Path $baselinePath "src\bin\Release\$Framework\OoplesFinance.StockIndicators.Original.dll"
        if (Test-Path $expectedDll) {
            Write-Host "[OK] Baseline built successfully: $expectedDll"
        }
        else {
            Write-Error "Expected baseline DLL not found: $expectedDll"
            exit 1
        }
    }
    else
    {
        Write-Warning "Baseline csproj not found: $baselineCsproj"
    }
}
else
{
    Write-Host "Skipping build (-NoBuild specified)"
}

Write-Host "Baseline setup complete."
