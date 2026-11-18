"""Prepare only authenticated current-version artifacts; never build production."""
import argparse
from hosted_timing import master_reconciliation as q
parser=argparse.ArgumentParser(description=__doc__)
for name in ('old-candidate','candidate','out'):parser.add_argument('--'+name,required=True)
args=parser.parse_args();q.prepare(args.old_candidate,args.candidate,args.out)
