"""Use the standard targeted measurement machinery after bounded authentication."""
import argparse
from pathlib import Path
from hosted_timing import pipeline as p, matchinglow_grid_qualification as q

parser = argparse.ArgumentParser()
parser.add_argument('mode', choices=('prepare', 'measure', 'verify'))
for name in ('baseline', 'candidate', 'preflight', 'inputs', 'prepared'):
    parser.add_argument('--'+name, type=lambda value: Path(value).resolve())
parser.add_argument('--out', type=lambda value: Path(value).resolve(), required=True)
args = parser.parse_args()
q.validate_scope(p.read(p.PLAN))
if args.mode == 'prepare':
    assert args.baseline and args.candidate and args.preflight
    q.prepare(args.baseline, args.candidate, args.preflight, args.out)
elif args.mode == 'measure':
    assert args.inputs
    p.verify_inputs(args.inputs)
    p.measure(args.inputs, args.out, 'targeted', 0)
else:
    assert args.inputs and args.prepared
    result = q.collect(args.inputs, args.prepared)
    p.write(args.out, result)
    print({k:result[k] for k in ('case_count', 'decision_count', 'verdicts', 'accepted', 'native_crossover_gate_passed')})
