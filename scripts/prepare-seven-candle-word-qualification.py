"""Prepare only existing authenticated artifacts after all conditional gates pass."""
import argparse
from hosted_timing import seven_candle_qualification as q
parser=argparse.ArgumentParser(description=__doc__)
for name in ('old-candidate','candidate','out'):parser.add_argument('--'+name,required=True)
args=parser.parse_args();q.prepare(args.old_candidate,args.candidate,args.out)
