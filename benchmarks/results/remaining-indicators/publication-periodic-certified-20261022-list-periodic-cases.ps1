$ErrorActionPreference='Stop'
$dll='C:/Users/cheat/source/repos/Ooples-Finance-LLC/si-periodic-certified-20261021/tests/bin/Release/net10.0/OoplesFinance.StockIndicators.Tests.Unit.dll'
$a=[Reflection.Assembly]::LoadFrom($dll)
$t=$a.GetType('OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.PeriodicChannelNumericalTests',$true)
$cases=$t.GetProperty('Cases').GetValue($null)
foreach($c in $cases) { $c[0].ToString() }
