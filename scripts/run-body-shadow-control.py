"""Diagnostic-only reuse of the complete authenticated body/shadow input tuple."""
import argparse
import os
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('mode',choices=['verify-inputs','measure','collect'])
parser.add_argument('--plan',type=Path,default=Path('benchmarks/plans/harami-body-shadow-control-20261107.json'))
parser.add_argument('--inputs',type=Path,default=Path('hosted-inputs'))
parser.add_argument('--out',type=Path,required=True)
parser.add_argument('--shard',type=int,choices=[0,1,2])
parser.add_argument('--evidence',type=Path)
parser.add_argument('--collection-receipt',type=Path)
args=parser.parse_args()
for name in ('plan','inputs','out','evidence','collection_receipt'):
    value=getattr(args,name)
    if value is not None: setattr(args,name,value.resolve())
os.environ['HOSTED_TIMING_PLAN']='benchmarks/plans/hosted-body-shadow-comparison-weights-20261106.json'
from hosted_timing import pipeline as p
from hosted_timing.aa_control import validate, runtime_controls

if args.mode!='collect': assert args.collection_receipt is None
if args.mode=='measure':
    assert args.shard is not None and not runtime_controls(), 'Default runtime required'
    p.measure(args.inputs,args.out,'targeted',args.shard,diagnostic='body-shadow-control',aa_plan=args.plan)
elif args.mode=='collect':
    from hosted_timing.verify import collect_aa
    assert args.evidence is not None
    summary=collect_aa(args.evidence,args.inputs,args.plan,args.collection_receipt)
    p.write(args.out,summary)
    print({k:summary[k] for k in ('diagnostic_only','case_count','comparison_count','separation_counts')})
else:
    d=validate(args.plan,args.inputs)
    p.write(args.out,dict(diagnostic_only=True,complete_original_inputs_verified=True,
        sources=d['sources'],production_sha256=d['production_hashes'],
        original_input=d['original_input'],diagnostic_plan_sha256=d['_plan_sha256']))
    print('Complete original input tuple verified; diagnostic only')
