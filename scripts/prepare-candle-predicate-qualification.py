"""Prepare only; standard targeted measurement, collection and reporting remain unchanged."""
import argparse
from pathlib import Path
from hosted_timing import candle_predicate_qualification as qualification

parser=argparse.ArgumentParser(description=__doc__)
for name in ('baseline','old-candidate','candidate','out'):
    parser.add_argument('--'+name,type=lambda value:Path(value).resolve(),required=True)
args=parser.parse_args()
qualification.prepare(args.baseline,args.old_candidate,args.candidate,args.out)
