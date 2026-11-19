"""Authenticate the aligned baseline and combined immutable candidate; never build."""
import argparse
from hosted_timing.alma_sinc_combined_qualification import prepare

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--old-candidate', required=True)
    parser.add_argument('--candidate', required=True)
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    prepare(args.old_candidate, args.candidate, args.out)
