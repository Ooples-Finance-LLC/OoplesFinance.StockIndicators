import argparse
from hosted_timing.current_frontier_refresh import prepare

if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--candidate',required=True)
    parser.add_argument('--out',required=True)
    args=parser.parse_args()
    prepare(args.candidate,args.out)
