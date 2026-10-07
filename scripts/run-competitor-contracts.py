"""Discover once, then partition compiled contract methods across CI runners."""
import json
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

PREFIX = "OoplesFinance.StockIndicators.CompetitorTests."
# This is VSTest's literal XML namespace identifier, never a network request.
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}  # NOSONAR


def discover(output):
    methods = set()
    for line in output.splitlines():
        name = line.strip().split("(", 1)[0]
        if name.startswith(PREFIX):
            if not re.fullmatch(r"[A-Za-z0-9_.]+", name):
                raise ValueError("Unsupported discovered test name: " + name)
            if not name.endswith(".FullTrajectoryMatchesIndependentFormula"):
                methods.add(name)
    if not methods:
        raise ValueError("No contract tests discovered")
    return sorted(methods)


def assignment(methods, shard, total):
    if methods != sorted(set(methods)) or not 0 <= shard < total <= len(methods):
        raise ValueError("Invalid contract inventory or shard")
    return methods[shard::total]


def verify_trx(path, expected):
    root = ET.parse(path).getroot()
    definitions = {}
    for test in root.findall(".//t:TestDefinitions/t:UnitTest", NS):
        method = test.find("t:TestMethod", NS)
        definitions[test.attrib["id"]] = method.attrib["className"] + "." + method.attrib["name"]
    seen = set()
    results = root.findall(".//t:Results/t:UnitTestResult", NS)
    for result in results:
        if result.attrib["outcome"] != "Passed":
            raise ValueError("Contract failed or was skipped: " + result.attrib["testName"])
        seen.add(definitions[result.attrib["testId"]])
    if seen != set(expected):
        raise ValueError("Executed contract methods differ from assigned inventory")
    return len(results)


def main(args):
    mode, dll, inventory, *rest = args
    if mode == "discover":
        result = subprocess.run(["dotnet", "vstest", dll, "/ListTests"], check=True,
                                capture_output=True, text=True)
        methods = discover(result.stdout)
        Path(inventory).write_text(json.dumps(methods, indent=2) + "\n", encoding="utf-8")
        print(f"Discovered {len(methods)} contract methods")
    elif mode == "run":
        shard, total = map(int, rest)
        selected = assignment(json.loads(Path(inventory).read_text(encoding="utf-8")), shard, total)
        directory = Path(f"contract-results-{shard}").resolve()
        directory.mkdir(exist_ok=False)
        subprocess.run(["dotnet", "vstest", dll,
                        "/TestCaseFilter:" + "|".join("FullyQualifiedName=" + name for name in selected),
                        "/Logger:trx;LogFileName=contracts.trx", "/ResultsDirectory:" + str(directory)], check=True)
        count = verify_trx(directory / "contracts.trx", selected)
        print(f"Verified {count} contract cases across {len(selected)} methods in shard {shard}/{total}")
    else:
        raise ValueError("Unknown mode")


if __name__ == "__main__":
    main(sys.argv[1:])
