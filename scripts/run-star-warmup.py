import argparse
import os
from pathlib import Path
os.environ['HOSTED_TIMING_PLAN']='benchmarks/plans/hosted-star-reversal-certified-difference-20261114.json'
from hosted_timing import star_warmup as a
from hosted_timing import pipeline as p
parser=argparse.ArgumentParser()
parser.add_argument('mode',choices=['verify-inputs','measure','collect'])
parser.add_argument('--inputs',type=Path,default=Path('hosted-inputs'))
parser.add_argument('--out',type=Path,required=True)
parser.add_argument('--shard',type=int,choices=[0,1])
parser.add_argument('--evidence',type=Path)
args=parser.parse_args()
inputs=args.inputs.resolve(); out=args.out.resolve()
if args.mode=='measure': a.measure(inputs,out,args.shard)
elif args.mode=='collect':
    result=a.collect(inputs,args.evidence.resolve()); p.write(out,result)
    print({k:result[k] for k in ('diagnostic_only','case_count','comparison_count')})
else:
    d,proof=a.validate(inputs)
    p.write(out,dict(diagnostic_only=True,complete_original_inputs_verified=True,plan_sha256=a.SHA,production_sha256=proof['production_sha256']))
