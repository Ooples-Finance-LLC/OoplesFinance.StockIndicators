"""No preparation, qualification, report import or production acceptance mode."""
import argparse
import os
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('mode',choices=['verify-inputs','measure','collect'])
parser.add_argument('--plan',type=Path,default=Path('benchmarks/plans/trady-macd-control-aa-20261105.json'))
parser.add_argument('--inputs',type=Path,default=Path('hosted-inputs'))
parser.add_argument('--out',type=Path,required=True)
parser.add_argument('--shard',type=int,choices=[0,1])
parser.add_argument('--evidence',type=Path)
args=parser.parse_args()
for name in ('plan','inputs','out','evidence'):
    value=getattr(args,name)
    if value is not None:
        setattr(args,name,value.resolve())
os.environ['HOSTED_TIMING_PLAN']='benchmarks/plans/hosted-pvo-adjacent-certificate-20261105.json'
from hosted_timing import pipeline as p
from hosted_timing.aa_control import validate, runtime_controls

if args.mode=='measure':
    assert args.shard is not None and not runtime_controls(), 'Default runtime required'
    p.measure(args.inputs,args.out,'targeted',args.shard,diagnostic='trady-macd-aa',aa_plan=args.plan)
elif args.mode=='collect':
    from hosted_timing.verify import collect_aa
    assert args.evidence is not None
    summary=collect_aa(args.evidence,args.inputs,args.plan)
    p.write(args.out,summary)
    print({k:summary[k] for k in ('diagnostic_only','same_binary','case_count','comparison_count','separation_counts')})
else:
    d=validate(args.plan,args.inputs)
    p.write(args.out,dict(diagnostic_only=True,complete_original_inputs_verified=True,
        sources=d['sources'],actual_source=d['actual_source'],production_sha256=d['production_sha256'],
        original_input=d['original_input'],diagnostic_plan_sha256=d['_plan_sha256']))
    print('Complete original input tuple verified; both labels use old profile',d['production_sha256'])
