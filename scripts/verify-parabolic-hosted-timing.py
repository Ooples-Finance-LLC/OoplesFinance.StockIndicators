import hashlib
import importlib.util
import json
import os
from pathlib import Path

module_path=Path(__file__).with_name('verify-sma-hosted-timing.py')
spec=importlib.util.spec_from_file_location('verified_pair_statistics',module_path)
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
pair=os.environ['COMPARISON_PAIR']
assert pair in {'TaLib.Functions.Sar','TaLib.Functions.SarExt','Skender.GetParabolicSar','Trady.Indicator.ParabolicStopAndReverse'}
reports=list(Path('performance/results').glob('*-report-full-compressed.json'))
assert len(reports)==1,reports
result=module.collect(json.loads(reports[0].read_text()),'LibraryPairBenchmarks',{'pair':pair})
provenance=json.loads(Path('binary-build-provenance.json').read_text())
assert provenance['run']['head_sha']==os.environ['MEASURED_SOURCE_SHA']
assert str(provenance['run']['id'])==os.environ['BINARY_RUN_ID']
assert provenance['build']['conclusion']=='success'
result.update(source_sha=provenance['run']['head_sha'],binary_run=provenance['run']['id'],
    workflow_sha=os.environ['GITHUB_SHA'],run_url=os.environ['GITHUB_SERVER_URL']+'/'+os.environ['GITHUB_REPOSITORY']+'/actions/runs/'+os.environ['GITHUB_RUN_ID'],
    report_sha256=hashlib.sha256(reports[0].read_bytes()).hexdigest(),
    benchmark_binary_sha256=hashlib.sha256(Path('binaries/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll').read_bytes()).hexdigest(),
    production_binary_sha256=hashlib.sha256(Path('binaries/OoplesFinance.StockIndicators.dll').read_bytes()).hexdigest(),
    note='Source-pinned direct Parabolic comparison at both sizes. Retained confidence intervals determine the verdict; cross-run times are not controlled speedup evidence.')
Path('performance/parabolic-comparison.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result,indent=2))
