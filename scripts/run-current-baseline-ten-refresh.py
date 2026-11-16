"""Prepare, measure or collect only the approved ten-pair baseline refresh."""
import argparse
from pathlib import Path
from hosted_timing import pipeline as p, current_baseline_ten_refresh as refresh

parser = argparse.ArgumentParser()
parser.add_argument('mode', choices=['prepare', 'measure', 'collect'])
for name in ('baseline', 'preflight', 'inputs', 'evidence', 'out'):
    parser.add_argument('--'+name, type=lambda value: Path(value).resolve())
parser.add_argument('--shard', type=int)
args = parser.parse_args()
refresh.validate_scope(p.read(p.PLAN))
if args.mode == 'prepare':
    refresh.prepare(args.baseline, args.preflight, args.out)
elif args.mode == 'measure':
    p.verify_inputs(args.inputs)
    p.measure(args.inputs, args.out, 'baseline-refresh', args.shard)
else:
    p.verify_inputs(args.inputs)
    result = refresh.collect(args.evidence)
    assert result['input_provenance_sha256'] == p.digest(args.inputs/'provenance.json')
    p.write(args.out, result)
    print({key: result[key] for key in ('case_count', 'decision_count', 'verdicts',
        'admitted_pairs', 'quarantined_pairs', 'zero_admitted_no_publication')})
