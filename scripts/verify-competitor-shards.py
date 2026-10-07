"""Require every implemented pair exactly once; pending APIs are never counted as verified."""
import json
import sys
from pathlib import Path


def verify(manifest_path, results_path, total):
    manifest = json.loads(Path(manifest_path).read_text(encoding="utf-8"))
    expected = sorted(row["Id"] for row in manifest if row["Status"] == "paired")
    if total < 1 or not expected or len(expected) != len(set(expected)):
        raise ValueError("Invalid shard count or pair inventory")
    files = list(Path(results_path).glob("shard-*.json"))
    if len(files) != total:
        raise ValueError("Missing or extra shard evidence")
    seen_shards, actual = set(), []
    for path in files:
        result = json.loads(path.read_text(encoding="utf-8"))
        shard = result["Shard"]
        if result["Total"] != total or shard not in range(total) or shard in seen_shards:
            raise ValueError("Invalid or duplicated shard")
        seen_shards.add(shard)
        ids = [row["Id"] for row in result["Results"]]
        if ids != expected[shard::total] or any(row["CheckedValues"] <= 0 for row in result["Results"]):
            raise ValueError("Shard assignment or correctness evidence differs")
        actual.extend(ids)
    if sorted(actual) != expected:
        raise ValueError("Implemented pairs were omitted or duplicated")
    pending = sum(row["Status"] == "pending" for row in manifest)
    print(f"Verified {len(expected)} implemented pairs across {total} shards; {pending} APIs remain pending.")


if __name__ == "__main__":
    verify(sys.argv[1], sys.argv[2], int(sys.argv[3]))
