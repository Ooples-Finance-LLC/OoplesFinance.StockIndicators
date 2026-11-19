import argparse
from hosted_timing.gapside_interval_replay_roi import prepare

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--old-candidate', required=True)
    parser.add_argument('--candidate', required=True)
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    prepare(args.old_candidate, args.candidate, args.out)
