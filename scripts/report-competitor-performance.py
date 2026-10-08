"""Publish a complete paired timing report from verified BenchmarkDotNet shard artifacts."""
import argparse
import csv
import gzip
import hashlib
import importlib.util
import json
import math
from pathlib import Path

spec = importlib.util.spec_from_file_location("performance", Path(__file__).with_name("verify-competitor-performance.py"))
performance = importlib.util.module_from_spec(spec)
spec.loader.exec_module(performance)


def arm_metrics(result):
    stats = result["Statistics"]
    samples = stats.get("OriginalValues", [])
    memory = result.get("Memory", {}).get("BytesAllocatedPerOperation")
    deviation = stats.get("StandardDeviation")
    if (len(samples) != stats["N"] or any(not math.isfinite(x) or x <= 0 for x in samples)
            or memory is None or not math.isfinite(memory) or memory < 0
            or deviation is None or not math.isfinite(deviation) or deviation < 0):
        raise ValueError("Missing finite sample, allocation, or dispersion evidence")
    return {"mean_ns": stats["Mean"], "stddev_ns": deviation, "samples": stats["N"], "allocated_bytes": memory}


def cell(value):
    return str(value or "").replace("|", "\\|").replace("\r", " ").replace("\n", " ")


def report(manifest_path, artifacts, count, output, source_sha, run_url):
    manifest_path, artifacts, output = Path(manifest_path), Path(artifacts), Path(output)
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    ids = [row["Id"] for row in manifest]
    if not ids or len(ids) != len(set(ids)) or any(row["Status"] not in ("paired", "unavailable", "utility") for row in manifest):
        raise ValueError("Invalid or incomplete comparison inventory")
    if count < 1 or count > sum(row["Status"] == "paired" for row in manifest):
        raise ValueError("Invalid shard count")
    expected_dirs = {f"competitor-performance-{i}" for i in range(count)}
    if {p.name for p in artifacts.glob("competitor-performance-*") if p.is_dir()} != expected_dirs:
        raise ValueError("Missing or unexpected performance shards")
    results, raw = {}, []
    for shard in range(count):
        directory = artifacts / f"competitor-performance-{shard}"
        selected = performance.verify(manifest_path, directory, shard, count, quiet=True)
        results.update({key: arm_metrics(value) for key, value in selected.items()})
        for path in sorted(directory.rglob("*-report-full*.json")):
            data = path.read_bytes()
            parsed = json.loads(data.decode("utf-8-sig"))
            raw.append((path.relative_to(artifacts).as_posix(), data, parsed.get("HostEnvironmentInfo", {})))
    rows = []
    for entry in sorted(manifest, key=lambda row: row["Id"]):
        if entry["Status"] != "paired":
            continue
        for bars in (1000, 10000):
            ours, theirs = (results[(entry["Id"], bars, arm)] for arm in ("Ooples", "Competitor"))
            rows.append({"pair": entry["Id"], "ooples_indicator": entry["OoplesIndicator"], "bars": bars,
                         **{f"ooples_{key}": value for key, value in ours.items()},
                         **{f"competitor_{key}": value for key, value in theirs.items()},
                         "competitor_over_ooples": theirs["mean_ns"] / ours["mean_ns"]})
    # Validate the entire union before writing any purportedly complete result.
    if output.exists() and any(output.iterdir()):
        raise ValueError("Use an empty report directory")
    output.mkdir(parents=True, exist_ok=True)
    (output / "inventory.json").write_bytes(manifest_path.read_bytes())
    with (output / "comparisons.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    provenance = {"sourceSha": source_sha, "runUrl": run_url, "pairedIndicators": len(rows) // 2,
                  "measuredArms": len(results), "shards": count,
                  "manifestSha256": hashlib.sha256(manifest_path.read_bytes()).hexdigest(), "reports": []}
    for index, (name, data, host) in enumerate(raw):
        target = f"raw/{index:02d}-report.json.gz"
        (output / "raw").mkdir(exist_ok=True)
        (output / target).write_bytes(gzip.compress(data, mtime=0))
        provenance["reports"].append({"artifactPath": name, "file": target,
                                       "sha256": hashlib.sha256(data).hexdigest(), "host": host})
    (output / "evidence.json").write_text(json.dumps(provenance, indent=2) + "\n", encoding="utf-8")
    lines = ["# Full-library performance comparisons", "",
             f"Source: `{source_sha}` · [GitHub run]({run_url})", "",
             f"{len(rows) // 2} paired APIs, {len(rows)} size comparisons, {len(results)} measured arms. "
             "Every pair runs at 1,000 and 10,000 bars with period 20 where applicable.", "",
             "Times cover each registered batch delegate, including its adapters and retained outputs. "
             "Each pair's two arms run on the same shard. These are not per-tick streaming timings. "
             "BenchmarkDotNet ShortRun uses three warmups and three measured iterations, one invocation per iteration. "
             "Shared GitHub runners and small samples make this diagnostic evidence, not a stable speed guarantee. "
             "Compare within each pair; do not rank unrelated shards by absolute time.", "",
             "Ratio = competitor mean / Ooples mean: **above 1 means Ooples was faster**. "
             "No slow result is omitted. Means and standard deviations below are microseconds; allocations are bytes per operation. "
             "[CSV](comparisons.csv) retains nanoseconds and sample counts; [evidence](evidence.json) identifies "
             "the source, hosts, and compressed raw reports. [API inventory](inventory.json) records "
             "entry points, output mappings, and formula limitations.", ""]
    for library in sorted({row["pair"].split(".")[0] for row in rows}):
        subset = [row for row in rows if row["pair"].split(".")[0] == library]
        lines += [f"## {library} ({len(subset) // 2} paired APIs)", "",
                  "| Competitor API | Ooples indicator | Bars | Ooples µs ± SD | Competitor µs ± SD | Ratio | Ooples B | Competitor B |",
                  "|---|---|---:|---:|---:|---:|---:|---:|"]
        for row in subset:
            lines.append(f"| {cell(row['pair'])} | {cell(row['ooples_indicator'])} | {row['bars']} | "
                         f"{row['ooples_mean_ns']/1000:.3f} ± {row['ooples_stddev_ns']/1000:.3f} | "
                         f"{row['competitor_mean_ns']/1000:.3f} ± {row['competitor_stddev_ns']/1000:.3f} | "
                         f"{row['competitor_over_ooples']:.4f} | {row['ooples_allocated_bytes']:g} | {row['competitor_allocated_bytes']:g} |")
        lines.append("")
    lines += ["## APIs without comparable timings", "",
              "Unavailable competitor implementations cannot provide a timing baseline. Utilities are inventoried separately. "
              "Alternate comparisons do not substitute timings for an unavailable API.", "",
              "| API | Status | Ooples counterpart | Reason |", "|---|---|---|---|"]
    for entry in manifest:
        if entry["Status"] != "paired":
            reason = entry.get("ExclusionReason") or entry.get("CompetitorLimitation")
            lines.append("| " + " | ".join(cell(value) for value in (entry['Id'], entry['Status'], entry.get('OoplesIndicator'), reason)) + " |")
    lines.append("")
    (output / "README.md").write_text("\n".join(lines), encoding="utf-8")
    print(f"Published {len(rows)} comparisons from {len(results)} verified measured arms.")
    return provenance


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest")
    parser.add_argument("artifacts")
    parser.add_argument("shards", type=int)
    parser.add_argument("output")
    parser.add_argument("--source-sha", required=True)
    parser.add_argument("--run-url", required=True)
    args = parser.parse_args()
    report(args.manifest, args.artifacts, args.shards, args.output, args.source_sha, args.run_url)
