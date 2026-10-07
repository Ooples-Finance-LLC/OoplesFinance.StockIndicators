"""Fail when BenchmarkDotNet omitted an arm, failed setup, or produced no real timings."""
import json
import math
import re
import sys
from pathlib import Path


def verify(manifest_path, artifacts, shard, total):
    if total < 1 or shard < 0 or shard >= total:
        raise ValueError("Invalid performance shard")
    manifest = json.loads(Path(manifest_path).read_text(encoding="utf-8"))
    pairs = sorted(row["Id"] for row in manifest if row["Status"] == "paired")[shard::total]
    expected = {(pair, bars, arm) for pair in pairs for bars in (1000, 10000) for arm in ("Ooples", "Competitor")}
    if not expected:
        raise ValueError("No assigned performance pairs")
    actual = set()
    for path in Path(artifacts).rglob("*-report-full*.json"):
        report = json.loads(path.read_text(encoding="utf-8-sig"))
        for result in report["Benchmarks"]:
            parameters = result["FullName"]
            pair = re.search(r'PairId: "([^"\r\n]+)"' , parameters)
            bars = re.search(r"Bars: (\d+)", parameters)
            if pair is None or bars is None:
                raise ValueError("Unrecognized benchmark parameters: " + parameters)
            key = (pair[1], int(bars[1]), result["Method"])
            if key in actual or key not in expected:
                raise ValueError("Duplicated or unexpected performance arm: " + str(key))
            stats = result.get("Statistics")
            if not stats or stats["N"] < 3 or not math.isfinite(stats["Mean"]) or stats["Mean"] <= 0:
                raise ValueError("Missing or insufficient measured timings: " + str(key))
            actual.add(key)
    if actual != expected:
        raise ValueError("Missing performance arms: " + str(sorted(expected - actual)))
    print(f"Verified timings for {len(actual)} arms across {len(pairs)} pairs.")


if __name__ == "__main__":
    verify(sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4]))
