"""Execute reviewed critical faults in an isolated source snapshot; never edit the working tree."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import tempfile
import time
import xml.etree.ElementTree as ET
import zipfile

TRX = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}  # NOSONAR(S5332) XML namespace identifier, never a network request.


def test_outcome(path):
    """Missing, empty, skipped, or malformed test evidence never counts as a mutation kill."""
    if not path.exists():
        return "inconclusive"
    try:
        root = ET.parse(path)
        results = root.findall(".//t:UnitTestResult", TRX)
        if not results or any(r.get("outcome") not in ("Passed", "Failed") for r in results):
            return "inconclusive"
        counters = root.find(".//t:ResultSummary/t:Counters", TRX)
        if counters is None or int(counters.get("executed", "-1")) != len(results) or int(counters.get("total", "-1")) != len(results):
            return "inconclusive"
        return "failed" if any(r.get("outcome") == "Failed" for r in results) else "passed"
    except (ET.ParseError, OSError, ValueError):
        return "inconclusive"


def mutate(source, entry):
    start = source.index(entry["scope"]) if entry.get("scope") else 0
    end = len(source)
    if entry.get("scope"):
        line_start = source.rfind("\n", 0, start) + 1
        indent = source[line_start:start]
        if indent.strip():
            raise ValueError("Scope must begin at a declaration, after indentation only.")
        end = source.index("\n" + indent + "}", start) + len(indent) + 2
    region = source[start:end]
    matches = region.count(entry["before"])
    if matches != 1:
        raise ValueError(f"{entry['id']}: expected one mutation site, found {matches}")
    return source[:start] + region.replace(entry["before"], entry["after"], 1) + source[end:]


def stop_process_tree(process, log):
    """A timed-out dotnet parent must not leave a testhost holding the isolated snapshot open."""
    if os.name == "nt":
        subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                       stdout=log, stderr=subprocess.STDOUT, check=False, timeout=30)
    else:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    process.wait(timeout=30)


def run_tests(worktree, directory, test_filter, timeout):
    directory.mkdir(parents=True, exist_ok=True)
    args = ["dotnet", "test", "tests/OoplesFinance.StockIndicators.Tests.Unit.csproj", "-c", "Release",
            "-f", "net10.0", "-p:GeneratePackageOnBuild=false", "--no-restore", "--disable-build-servers", "--filter", test_filter,
            "--logger", "trx;LogFileName=results.trx", "--results-directory", str(directory), "--verbosity", "quiet"]
    env = dict(os.environ, MSBUILDDISABLENODEREUSE="1", DOTNET_CLI_USE_MSBUILD_SERVER="0")
    started = time.monotonic()
    with (directory / "run.log").open("w", encoding="utf-8") as log:
        process = subprocess.Popen(args, cwd=worktree, env=env, stdout=log, stderr=subprocess.STDOUT,
                                   start_new_session=os.name != "nt")
        try:
            exit_code = process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            stop_process_tree(process, log)
            return {"outcome": "inconclusive", "reason": "timeout", "seconds": time.monotonic() - started}
    outcome = test_outcome(directory / "results.trx")
    if exit_code not in (0, 1) or (exit_code == 0) != (outcome == "passed"):
        outcome = "inconclusive"
    return {"outcome": outcome, "exitCode": exit_code, "seconds": time.monotonic() - started}


def select_entries(entries, identifiers):
    if identifiers is None:
        return entries
    if not identifiers or len(set(identifiers)) != len(identifiers):
        raise ValueError("Select distinct nonempty mutation IDs.")
    if set(identifiers) - {entry["id"] for entry in entries}:
        raise ValueError("Unknown mutation ID.")
    return [entry for entry in entries if entry["id"] in identifiers]


def campaign_complete(entries, results):
    return (len(results) == len(entries) and
            {result["id"] for result in results} == {entry["id"] for entry in entries} and
            all(result["disposition"] == "killed" for result in results))


def select_shard(entries, index, count):
    if index is None and count is None:
        return entries
    if index is None or count is None or not 1 <= count <= len(entries) or not 0 <= index < count:
        raise ValueError("Supply a valid zero-based shard index and a count no larger than the manifest.")
    return entries[index::count]


def shard_matrix(entries):
    count = (len(entries) + 9) // 10
    if not 1 <= count <= 256:
        raise ValueError("The manifest must fit GitHub's matrix limit with ten faults per shard.")
    return {"include": [{"shard": index, "count": count} for index in range(count)]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", default="artifacts/correctness/mutations")
    parser.add_argument("--timeout", type=int, default=300)
    parser.add_argument("--baseline-timeout", type=int, default=1800,
                        help="Seconds for the initial build and union of all selected test filters.")
    parser.add_argument("--only", nargs="+", help="Run selected faults; a subset cannot satisfy the full release gate.")
    parser.add_argument("--shard-index", type=int)
    parser.add_argument("--shard-count", type=int)
    parser.add_argument("--list-shards", action="store_true", help="Print the complete CI matrix without executing tests.")
    args = parser.parse_args()
    if args.timeout <= 0 or args.baseline_timeout <= 0:
        raise ValueError("The per-run timeout must be positive.")
    repo = Path(__file__).resolve().parent.parent
    entries = json.loads((repo / "tools/critical-mutations.json").read_text(encoding="utf-8"))
    if not entries or len({m["id"] for m in entries}) != len(entries):
        raise ValueError("Critical faults must be nonempty and uniquely named.")
    all_entries = entries
    if args.list_shards:
        print(json.dumps(shard_matrix(all_entries)))
        return 0
    if args.only is not None and (args.shard_index is not None or args.shard_count is not None):
        raise ValueError("Do not combine a selected subset with CI sharding.")
    entries = select_shard(select_entries(all_entries, args.only), args.shard_index, args.shard_count)
    output = Path(args.output).resolve()
    if output.exists() and any(output.iterdir()):
        raise ValueError("Use a new output directory so stale test results cannot satisfy the gate.")
    output.mkdir(parents=True, exist_ok=True)
    paths = sorted(set(p for p in subprocess.check_output(
        ["git", "ls-files", "-c", "-o", "--exclude-standard", "-z"], cwd=repo).decode("utf-8").split("\0") if p))
    temp_root = Path(tempfile.gettempdir()).resolve()
    worktree = Path(tempfile.mkdtemp(prefix="si-critical-", dir=temp_root)).resolve()
    evidence = {"scope": "reviewed-critical-faults" if len(entries) == len(all_entries) else "selected-critical-faults",
                "requiredKillRate": 1.0, "manifest": all_entries,
                "selectedMutationIds": [entry["id"] for entry in entries], "sourceFiles": [], "mutations": []}
    try:
        snapshot = hashlib.sha256()
        for relative in paths:
            source = (repo / relative).resolve()
            target = (worktree / relative).resolve()
            if not source.is_relative_to(repo) or not target.is_relative_to(worktree):
                raise ValueError("Snapshot path escapes the source or isolated workspace.")
            if not source.is_file():
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
            digest = hashlib.sha256(target.read_bytes()).digest()
            snapshot.update(relative.encode("utf-8") + b"\0" + digest)
            evidence["sourceFiles"].append({"path": relative, "sha256": digest.hex()})
        evidence["sourceSnapshotSha256"] = snapshot.hexdigest()
        archive = output / "source-snapshot.zip"
        with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as bundle:
            for source_file in evidence["sourceFiles"]:
                bundle.write(worktree / source_file["path"], source_file["path"])
        evidence["sourceArchiveSha256"] = hashlib.sha256(archive.read_bytes()).hexdigest()
        # Validate every site before spending time compiling. A stale site is an error, not an exclusion.
        for entry in entries:
            path = (worktree / entry["file"]).resolve()
            if not path.is_relative_to(worktree):
                raise ValueError("Mutation path escapes the isolated workspace.")
            mutate(path.read_text(encoding="utf-8"), entry)
        with (output / "restore.log").open("w", encoding="utf-8") as log:
            subprocess.run(["dotnet", "restore", "tests/OoplesFinance.StockIndicators.Tests.Unit.csproj"],
                           cwd=worktree, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=args.timeout)
        baseline_filter = "|".join(sorted({e["filter"] for e in entries}))
        baseline = run_tests(worktree, output / "baseline", baseline_filter, args.baseline_timeout)
        evidence["baseline"] = baseline
        if baseline["outcome"] != "passed":
            raise RuntimeError("The unchanged snapshot did not pass. No mutation kills can be credited.")
        for entry in entries:
            path = worktree / entry["file"]
            original = path.read_bytes()
            try:
                path.write_text(mutate(original.decode("utf-8").replace("\r\n", "\n"), entry), encoding="utf-8")
                result = run_tests(worktree, output / entry["id"], entry["filter"], args.timeout)
            finally:
                path.write_bytes(original)
            disposition = {"failed": "killed", "passed": "survived"}.get(result["outcome"], "inconclusive")
            evidence["mutations"].append({"id": entry["id"], "module": entry["module"], "disposition": disposition, **result})
            print(f"{entry['id']}: {disposition}", flush=True)
        evidence["selectedComplete"] = campaign_complete(entries, evidence["mutations"])
        evidence["complete"] = campaign_complete(all_entries, evidence["mutations"])
        return 0 if evidence["selectedComplete"] else 1
    finally:
        (output / "evidence.json").write_text(json.dumps(evidence, indent=2), encoding="utf-8")
        # Verify the resolved temporary target before recursive cleanup on Windows or Unix.
        if worktree.parent != temp_root or not worktree.name.startswith("si-critical-"):
            raise RuntimeError("Refusing cleanup outside the verified temporary workspace.")
        shutil.rmtree(worktree)


if __name__ == "__main__":
    raise SystemExit(main())
