import argparse
from pathlib import Path
from hosted_timing import pipeline as p, three_stars_blocks_roi as roi

parser = argparse.ArgumentParser()
parser.add_argument('mode', choices=['prepare', 'measure', 'verify'])
parser.add_argument('--baseline', type=lambda value: Path(value).resolve())
parser.add_argument('--candidate', type=lambda value: Path(value).resolve())
parser.add_argument('--inputs', type=lambda value: Path(value).resolve())
parser.add_argument('--out', type=lambda value: Path(value).resolve(), required=True)
args = parser.parse_args()
roi.validate_scope(p.read(p.PLAN))
if args.mode == 'prepare':
    roi.prepare(args.baseline, args.candidate, args.out)
elif args.mode == 'measure':
    p.verify_inputs(args.inputs)
    p.measure(args.inputs, args.out, 'diagnostic', 0)
else:
    result = roi.collect(args.inputs)
    p.write(args.out, result)
    print({k: result[k] for k in ('case_count', 'decision_count', 'verdicts', 'diagnostic_only', 'qualification')})
