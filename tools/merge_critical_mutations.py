"""Require complete, source-identical mutation shards with independently checked TRX evidence."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import zipfile

from run_critical_mutations import campaign_complete, select_entries, test_outcome


def verify_snapshot(directory, evidence):
    archive = directory / "source-snapshot.zip"
    if hashlib.sha256(archive.read_bytes()).hexdigest() != evidence["sourceArchiveSha256"]:
        raise ValueError("Source archive hash mismatch.")
    files = evidence["sourceFiles"]
    names = [item["path"] for item in files]
    if not names or names != sorted(set(names)):
        raise ValueError("Source files must be nonempty, unique and sorted.")
    digest = hashlib.sha256()
    with zipfile.ZipFile(archive) as bundle:
        if sorted(bundle.namelist()) != names:
            raise ValueError("Source archive membership mismatch.")
        for item in files:
            actual = hashlib.sha256(bundle.read(item["path"])).digest()
            if actual.hex() != item["sha256"]:
                raise ValueError("Source file hash mismatch.")
            digest.update(item["path"].encode("utf-8") + b"\0" + actual)
    if digest.hexdigest() != evidence["sourceSnapshotSha256"]:
        raise ValueError("Source snapshot hash mismatch.")
    return digest.hexdigest()


def merge_shards(directories, manifest):
    if not manifest or len({entry["id"] for entry in manifest}) != len(manifest):
        raise ValueError("Expected manifest must contain distinct faults.")
    if any(not re.fullmatch(r"[A-Za-z0-9_-]+", entry["id"]) for entry in manifest):
        raise ValueError("Mutation IDs must be safe directory names.")
    results, shards = [], []
    snapshot = None
    for directory in directories:
        evidence = json.loads((directory / "evidence.json").read_text(encoding="utf-8"))
        if evidence["manifest"] != manifest:
            raise ValueError("Shard manifest differs from the expected complete manifest.")
        current_snapshot = verify_snapshot(directory, evidence)
        if snapshot is not None and snapshot != current_snapshot:
            raise ValueError("Cannot combine different source snapshots.")
        snapshot = current_snapshot
        baseline = evidence["baseline"]
        if (baseline.get("outcome") != "passed" or baseline.get("exitCode") != 0
                or test_outcome(directory / "baseline" / "results.trx") != "passed"):
            raise ValueError("Each shard requires a completed, passing baseline.")
        selected = select_entries(manifest, evidence["selectedMutationIds"])
        mutations = evidence["mutations"]
        if not campaign_complete(selected, mutations):
            raise ValueError("Each selected fault must be killed exactly once.")
        for mutation in mutations:
            if (mutation.get("outcome") != "failed" or mutation.get("exitCode") != 1
                    or test_outcome(directory / mutation["id"] / "results.trx") != "failed"):
                raise ValueError("A mutation requires completed failing tests, not a timeout or build error.")
        results.extend(mutations)
        shards.append({"directory": directory.name, "selectedMutationIds": evidence["selectedMutationIds"]})
    if not campaign_complete(manifest, results):
        raise ValueError("The complete manifest must be covered exactly once across shards.")
    return {"scope": "reviewed-critical-faults", "requiredKillRate": 1.0,
            "manifest": manifest, "sourceSnapshotSha256": snapshot,
            "shards": shards, "mutations": results, "complete": True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--manifest", default="tools/critical-mutations.json")
    args = parser.parse_args()
    output = Path(args.output)
    if output.exists():
        raise ValueError("Use a fresh merged evidence path.")
    manifest = json.loads(Path(args.manifest).read_text(encoding="utf-8"))
    directories = sorted(path.parent for path in Path(args.input).glob("*/evidence.json"))
    result = merge_shards(directories, manifest)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(f"Verified {len(result['mutations'])} faults across {len(result['shards'])} source-identical shards.")


if __name__ == "__main__":
    main()
