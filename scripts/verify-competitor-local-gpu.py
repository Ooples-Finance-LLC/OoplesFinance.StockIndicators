"""Run GPU contracts locally; require matching source and complete TRX evidence in CI."""
import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import platform
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / "benchmarks/results/shared-rollout/local-gpu-contracts"
METHODS = ROOT / "scripts/competitor-gpu-methods.json"
PROJECT = "benchmarks/OoplesFinance.StockIndicators.CompetitorTests"
spec = importlib.util.spec_from_file_location("contracts", ROOT / "scripts/run-competitor-contracts.py")
contracts = importlib.util.module_from_spec(spec)
spec.loader.exec_module(contracts)


def source_fingerprint(root):
    paths = ["src", "benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks", PROJECT,
             "scripts/run-competitor-contracts.py", "scripts/verify-competitor-local-gpu.py",
             "scripts/competitor-gpu-methods.json", "Directory.Build.props", "Directory.Build.targets",
             "Directory.Packages.props", "global.json", "NuGet.Config", "nuget.config"]
    listed = subprocess.check_output(["git", "ls-files", "--cached", "--others", "--exclude-standard",
                                      "-z", "--", *paths], cwd=root).decode().split("\0")
    digest = hashlib.sha256()
    for name in sorted(set(filter(None, listed))):
        # Source files are text; Git's platform line-ending conversion is immaterial.
        content = (root / name).read_bytes().replace(b"\r\n", b"\n")
        digest.update(name.encode() + b"\0" + hashlib.sha256(content).digest())
    return digest.hexdigest()


def partition(methods, gpu):
    if not gpu or gpu != sorted(set(gpu)) or methods != sorted(set(methods)):
        raise ValueError("Contract inventories must be nonempty, sorted and unique")
    if not set(gpu) < set(methods):
        raise ValueError("GPU inventory must be a strict subset of discovered contracts")
    return [name for name in methods if name not in set(gpu)]


def verify_evidence(receipt, trx, gpu, fingerprint):
    if receipt["source_sha256"] != fingerprint or receipt["methods"] != gpu:
        raise ValueError("Local GPU evidence is stale for this source or method inventory")
    if hashlib.sha256(trx).hexdigest() != receipt["trx_sha256"]:
        raise ValueError("Local GPU TRX hash mismatch")
    with tempfile.TemporaryDirectory() as folder:
        path = Path(folder) / "gpu.trx"
        path.write_bytes(trx)
        count = contracts.verify_trx(path, gpu)
    if count != receipt["passed_cases"] or count < len(gpu):
        raise ValueError("Local GPU case count does not match verified results")
    return count


def record(device):
    gpu = json.loads(METHODS.read_text())
    before = source_fingerprint(ROOT)
    subprocess.run(["dotnet", "build", PROJECT, "-c", "Release", "--no-restore",
                    "-p:GeneratePackageOnBuild=false"], cwd=ROOT, check=True)
    dll = ROOT / PROJECT / "bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorTests.dll"
    with tempfile.TemporaryDirectory(prefix="si-gpu-contracts-") as folder:
        directory = Path(folder)
        settings = directory / "serial.runsettings"
        settings.write_text('<RunSettings><RunConfiguration><MaxCpuCount>1</MaxCpuCount></RunConfiguration>'
                            '<xUnit><ParallelizeTestCollections>false</ParallelizeTestCollections>'
                            '<MaxParallelThreads>1</MaxParallelThreads></xUnit></RunSettings>')
        subprocess.run(["dotnet", "vstest", str(dll), "/Settings:" + str(settings),
                        "/TestCaseFilter:" + "|".join("FullyQualifiedName=" + name for name in gpu),
                        "/Logger:trx;LogFileName=gpu.trx", "/ResultsDirectory:" + str(directory)],
                       cwd=ROOT, check=True)
        trx = (directory / "gpu.trx").read_bytes()
        count = contracts.verify_trx(directory / "gpu.trx", gpu)
    if source_fingerprint(ROOT) != before:
        raise ValueError("Sources changed during local GPU validation")
    receipt = {"source_sha256": before, "methods": gpu, "passed_cases": count,
               "trx_sha256": hashlib.sha256(trx).hexdigest(), "host": platform.platform(),
               "device": device, "dotnet_info": subprocess.check_output(["dotnet", "--info"]).decode(),
               "test_assembly_sha256": hashlib.sha256(dll.read_bytes()).hexdigest(),
               "note": "Actual RequiredGPU fixture execution; correctness evidence, not performance qualification."}
    verify_evidence(receipt, trx, gpu, before)
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    (EVIDENCE / "gpu.trx.gz").write_bytes(gzip.compress(trx, mtime=0))
    (EVIDENCE / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"Recorded {count} passing local GPU cases across {len(gpu)} methods")


def main(args):
    if len(args) == 2 and args[0] == "record":
        record(args[1])
    elif len(args) == 3 and args[0] == "verify":
        methods = json.loads(Path(args[1]).read_text())
        gpu = json.loads(METHODS.read_text())
        cpu = partition(methods, gpu)
        receipt = json.loads((EVIDENCE / "receipt.json").read_text())
        count = verify_evidence(receipt, gzip.decompress((EVIDENCE / "gpu.trx.gz").read_bytes()),
                                gpu, source_fingerprint(ROOT))
        Path(args[2]).write_text(json.dumps(cpu, indent=2) + "\n", encoding="utf-8")
        print(f"Verified {count} local GPU cases; {len(cpu)} methods remain assigned to hosted CI")
    else:
        raise ValueError("Usage: record DEVICE_DESCRIPTION | verify ALL_METHODS CPU_METHODS")


if __name__ == "__main__":
    main(sys.argv[1:])
