"""Interleaved diagnostic A/B runs; not a replacement for BenchmarkDotNet acceptance."""
import argparse
import hashlib
import json
import pathlib
import platform
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("binary", type=pathlib.Path)
parser.add_argument("output", type=pathlib.Path)
parser.add_argument("--seconds", type=int, default=2)
parser.add_argument("--repeats", type=int, default=3)
args = parser.parse_args()
if not 1 <= args.seconds <= 300 or args.repeats < 1:
    parser.error("seconds must be 1..300 and repeats must be positive")
args.output.mkdir(parents=True, exist_ok=True)
binary = args.binary.resolve()
metadata = {
    "platform": platform.platform(),
    "binary": str(binary),
    "binary_sha256": hashlib.sha256(binary.read_bytes()).hexdigest(),
    "seconds": args.seconds,
    "repeats": args.repeats,
    "note": "Separate warmed processes, alternating arm order; exploratory timings without confidence intervals.",
}
(args.output / "metadata.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")
results = []
for repeat in range(args.repeats):
    for pair in ("TaLib.Functions.Asin", "TaLib.Functions.Sma", "TaLib.Candles.RickshawMan"):
        arms = ["Builder", "Compute", "Prepared", "Kernel", "Native"]
        if repeat % 2:
            arms.reverse()
        for arm in arms:
            run = subprocess.run(
                ["dotnet", str(binary), "--profile-builder", pair, arm, str(args.seconds)],
                capture_output=True, text=True, check=False,
            )
            (args.output / f"{pair}-{arm}-{repeat}.txt").write_text(run.stdout + run.stderr, encoding="utf-8")
            run.check_returncode()
            line = next(line for line in run.stdout.splitlines() if line.startswith("PROFILE END "))
            values = {k: float(v) for k, v in (field.split("=") for field in line.split()[2:])}
            results.append(dict(pair=pair, arm=arm, repeat=repeat, **values))
            (args.output / "ab-measurements.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
            print(pair, arm, repeat, line, flush=True)
