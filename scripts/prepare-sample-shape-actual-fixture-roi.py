"""Prepare authenticated existing artifacts only; never builds source."""
import argparse
from hosted_timing import sample_shape_actual_fixture_roi as q
parser=argparse.ArgumentParser(description=__doc__)
for name in ('old-candidate','candidate','out'):parser.add_argument('--'+name,required=True)
args=parser.parse_args();q.prepare(args.old_candidate,args.candidate,args.out)
