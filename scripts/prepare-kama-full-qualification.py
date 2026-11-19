"""Authenticate frozen full-consumer inputs. No source build or timing."""
import argparse
from hosted_timing.kama_full_qualification import prepare

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--old-candidate',required=True)
    parser.add_argument('--candidate',required=True)
    parser.add_argument('--out',required=True)
    args=parser.parse_args()
    prepare(args.old_candidate,args.candidate,args.out)
