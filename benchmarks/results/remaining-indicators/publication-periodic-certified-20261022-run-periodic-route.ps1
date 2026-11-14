param([Parameter(Mandatory=$true)][string]$Configuration,[Parameter(Mandatory=$true)][string]$Route)
$ErrorActionPreference='Stop'
$dll='C:/Users/cheat/source/repos/Ooples-Finance-LLC/si-periodic-certified-20261021/tests/bin/Release/net10.0/OoplesFinance.StockIndicators.Tests.Unit.dll'
$a=[Reflection.Assembly]::LoadFrom($dll)
$t=$a.GetType('OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.PeriodicChannelNumericalTests',$true)
$matches=@($t.GetProperty('Cases').GetValue($null) | Where-Object { $_[0].ToString() -eq $Configuration })
if($matches.Count -ne 1) { throw "Expected exactly one configuration, got $($matches.Count)" }
$case=$matches[0][0]
$instance=[Activator]::CreateInstance($t)
$method=$t.GetMethod('EveryRouteMatchesIndependentCenteredMoments')
$watch=[Diagnostics.Stopwatch]::StartNew()
$null=$method.Invoke($instance,[object[]]@($case,$Route))
@{passed=$true;configuration=$Configuration;route=$Route;method=$method.Name;elapsed_seconds=$watch.Elapsed.TotalSeconds;assembly_sha256=(Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant()} | ConvertTo-Json -Compress
