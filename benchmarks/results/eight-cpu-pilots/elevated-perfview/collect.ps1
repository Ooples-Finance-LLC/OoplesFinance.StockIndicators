$ErrorActionPreference='Stop'
$traceRoot='C:/Users/cheat/temp/si-elevated-current'
$binary='C:/Users/cheat/source/repos/Ooples-Finance-LLC/si-indicators/benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'
try {
 $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
 $principal=New-Object Security.Principal.WindowsPrincipal($identity)
 if(-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Collector is not elevated.' }
 Add-Content "$traceRoot/status.txt" 'Elevated collector started.'
 foreach($scenario in @('Asin','SmaGrid','SmaDecimal')) {
  foreach($arm in @('Builder','InPlace')) {
   $name="$scenario-$arm"
   $arguments='/AcceptEula /NoGui /NoNGenRundown /CircularMB:256 /BufferSizeMB:128 /SessionName:SiCurrentPilots /LogFile:'+$traceRoot+'/'+$name+'.log /DataFile:'+$traceRoot+'/'+$name+'.etl run "C:/Program Files/dotnet/dotnet.exe" "'+$binary+'" --profile-cost-boundary '+$scenario+' '+$arm+' 10'
   $process=Start-Process C:/Users/cheat/temp/si-perfview/PerfView.exe -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
   if($process.ExitCode -ne 0) { throw "Capture failed: $name ($($process.ExitCode))" }
   Add-Content "$traceRoot/status.txt" "Collected $name"
  }
 }
 Add-Content "$traceRoot/status.txt" 'Complete'
} catch { Add-Content "$traceRoot/status.txt" "FAILED: $_"; exit 1 }
