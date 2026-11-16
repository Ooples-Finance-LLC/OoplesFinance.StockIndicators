import argparse
from pathlib import Path
from hosted_timing import pipeline as p,pointwise_family_refresh as refresh

parser=argparse.ArgumentParser()
parser.add_argument('mode',choices=['prepare','measure','collect'])
for name in ('seed','candidate','inputs','evidence','out'):
    parser.add_argument('--'+name,type=lambda value:Path(value).resolve())
parser.add_argument('--shard',type=int)
args=parser.parse_args()
refresh.validate_scope(p.read(p.PLAN))
if args.mode=='prepare':
    refresh.prepare(args.seed,args.candidate,args.out)
elif args.mode=='measure':
    p.verify_inputs(args.inputs)
    p.measure(args.inputs,args.out,'baseline-refresh',args.shard)
else:
    p.verify_inputs(args.inputs)
    result=refresh.collect(args.evidence)
    p.write(args.out,result)
    print({key:result[key] for key in ('case_count','decision_count','verdicts','baseline_refresh_verified','production_qualification')})
