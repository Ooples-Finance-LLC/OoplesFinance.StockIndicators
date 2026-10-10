$ErrorActionPreference='Stop'
$root='C:/Users/cheat/temp/si-simd-certification'
$bin='C:/Users/cheat/source/repos/Ooples-Finance-LLC/si-indicators/benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0'
try {
    foreach($label in @('baseline-before','candidate-matched','candidate-repeat','baseline-after')) {
        $variant=if($label.StartsWith('baseline')) {'baseline'} else {'candidate'}
        Copy-Item -LiteralPath "$root/$variant.dll" -Destination "$bin/OoplesFinance.StockIndicators.dll"
        $filters=@('*PilotCostBoundaryBenchmarks.OoplesLatestOnlyBuilder*')
        if($label -eq 'candidate-matched') {
            $filters+=@('*PilotCostBoundaryBenchmarks.TalibValues*','*PilotCostBoundaryBenchmarks.TalibInPlaceLatestOnlyPayload*')
        }
        & dotnet "$bin/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll" --filter $filters --warmupCount 6 --iterationCount 10 --exporters json --artifacts "$root/$label" > "$root/$label.log" 2>&1
        if($LASTEXITCODE -ne 0) { throw "Benchmark failed: $label" }
        Add-Content "$root/status.txt" "Complete $label"
    }
} finally {
    Copy-Item -LiteralPath "$root/candidate.dll" -Destination "$bin/OoplesFinance.StockIndicators.dll"
}
