"""Prepare, measure or collect one held same-binary eligibility diagnostic."""
import argparse
from pathlib import Path
from hosted_timing import pipeline as p,native_eligibility_eight_screen as screen
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('mode',choices=['prepare','measure','collect'])
for name in ('candidate','inputs','evidence','out'):parser.add_argument('--'+name,type=lambda value:Path(value).resolve())
args=parser.parse_args();screen.validate_scope(p.read(p.PLAN))
if args.mode=='prepare':screen.prepare(args.candidate,args.out)
elif args.mode=='measure':
    p.verify_inputs(args.inputs);p.measure(args.inputs,args.out,'baseline-refresh',0)
else:
    p.verify_inputs(args.inputs);result=screen.collect(args.evidence)
    assert result['input_provenance_sha256']==p.digest(args.inputs/'provenance.json')
    p.write(args.out,result);print({key:result[key] for key in ('case_count','decision_count','verdicts','profile_candidates','quarantined_pairs','accepted')})
