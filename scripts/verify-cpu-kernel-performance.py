"""Reject empty/failed pilot runs; BenchmarkDotNet can exit zero without timings."""
import argparse
import json
import math
import re
from pathlib import Path


def supported_methods(pair):
    pairs = {"QuanTAlib.Jma", "QuanTAlib.Atr", "Skender.GetRollingPivots", "Skender.GetFractal",
             "TaLib.Candles.RickshawMan", "TaLib.Functions.Asin", "Trady.Candlestick.BullishShortDay",
             "Trady.Indicator.SimpleMovingAverage"}
    if pair not in pairs:
        raise ValueError("Unknown pilot pair: " + pair)
    methods = ["OoplesOwnedBatch", "CompetitorOwnedBatch"]
    if pair.startswith("QuanTAlib."):
        methods += ["OoplesStreaming", "CompetitorStreaming"]
    if pair.startswith("TaLib."):
        methods += ["OoplesReusableBatch", "CompetitorReusableBatch"]
    return methods


def verify(directory, pair, methods=None):
    methods = supported_methods(pair) if methods is None else methods
    expected = {(method, bars) for method in methods for bars in (1000, 10000)}
    seen = set()
    rows = []
    for report in Path(directory).rglob("*full-compressed.json"):
        for row in json.loads(report.read_text(encoding="utf-8-sig"))["Benchmarks"]:
            if row.get("Type") != "CpuKernelBenchmarks":
                continue
            # Parameters is a display field and truncates longer pair IDs.
            parameters = re.search(r'\(Bars: (\d+), PairId: "([^"]+)"\)', row.get("FullName", ""))
            if parameters is None or parameters[2] != pair:
                raise ValueError("Unexpected pair in pilot report")
            key = (row["Method"], int(parameters[1]))
            if key not in expected or key in seen:
                raise ValueError("Unexpected or duplicate pilot measurement: " + str(key))
            mean = (row.get("Statistics") or {}).get("Mean")
            samples = (row.get("Statistics") or {}).get("N", 0)
            allocated = (row.get("Memory") or {}).get("BytesAllocatedPerOperation")
            if samples < 3 or mean is None or not math.isfinite(mean) or mean <= 0:
                raise ValueError("Missing or invalid timing: " + str(key))
            if allocated is None or not math.isfinite(allocated) or allocated < 0:
                raise ValueError("Missing allocation measurement: " + str(key))
            if "Streaming" in key[0]:
                measurements = [m for m in row.get("Measurements", [])
                                if m.get("IterationMode") == "Workload" and m.get("IterationStage") == "Actual"]
                if not measurements or any(m.get("Operations") != 64 for m in measurements):
                    raise ValueError("Streaming must measure one invocation of 64 fresh sequences: " + str(key))
            seen.add(key)
            rows.append({"Pair": pair, "Method": key[0], "Bars": key[1],
                         "MeanNanoseconds": mean, "AllocatedBytes": allocated})
    if seen != expected:
        raise ValueError("Missing pilot measurements: " + str(sorted(expected - seen)))
    return rows


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory")
    parser.add_argument("pair")
    parser.add_argument("--methods", nargs="+", help="Explicit subset for diagnostic runs only")
    args = parser.parse_args()
    print(json.dumps(verify(args.directory, args.pair, args.methods), indent=2))
