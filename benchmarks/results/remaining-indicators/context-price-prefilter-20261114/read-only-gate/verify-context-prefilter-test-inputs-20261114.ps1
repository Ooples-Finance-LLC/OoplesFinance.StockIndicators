$ErrorActionPreference='Stop'
$runtime='C:\Users\cheat\source\repos\Ooples-Finance-LLC\si-star-reversal-certified-difference-20261114\benchmarks\OoplesFinance.StockIndicators.CompetitorBenchmarks\bin\Release\net10.0'
if((Get-FileHash (Join-Path $runtime 'OoplesFinance.StockIndicators.dll')).Hash.ToLowerInvariant() -ne '186c441ad23620cfaae3a94e9d10df7f0b46cacc66cdb8fb03860536b00ce7e5'){throw 'hash mismatch'}
[System.Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving({param($context,$name) $path=Join-Path $runtime ($name.Name+'.dll');if(Test-Path $path){$context.LoadFromAssemblyPath($path)}})
$a=[System.Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath((Join-Path $runtime 'OoplesFinance.StockIndicators.dll'))
$barType=$a.GetType('OoplesFinance.StockIndicators.Indicators.Bar',$true)
$flags=[Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
foreach($name in @('InvertedHammer','ShootingStar')){
 $owner=[Activator]::CreateInstance($a.GetType('OoplesFinance.StockIndicators.Indicators.'+$name+'Candle',$true),[object[]]@(1))
 $state=$owner.GetType().GetMethod('CreateState',$flags).Invoke($owner,@())
 $update=$state.GetType().GetMethod('Update',$flags)
 $values=@()
 $tops=if($name -eq 'InvertedHammer'){@(3.125,5.0,6.0,3.125)}else{@(12.125,10.125,9.125,12.125)}
 foreach($top in $tops){
  foreach($fields in @(@(10.0,20.0,0.0,5.0),@(10.0,20.0,0.0,5.0),@(($top-.125),($top+5),($top-.125),$top))){
   $o,$h,$l,$c=$fields

   $bar=[Activator]::CreateInstance($barType,[object[]]@([DateTime]::UnixEpoch,[double]$o,[double]$h,[double]$l,[double]$c,1.0))
   $values+= $update.Invoke($state,[object[]]@($bar))
  }
 }
 $signal=if($name -eq 'InvertedHammer'){100}else{-100}
 if($values[2] -ne $signal -or $values[5] -ne 0 -or $values[8] -ne 0 -or $values[11] -ne $signal){throw ('Bad synthetic expectations '+($values -join ','))}
 Write-Output ($name+': original binary confirms signal / equality rejection / wrong-side rejection / following signal')
}

