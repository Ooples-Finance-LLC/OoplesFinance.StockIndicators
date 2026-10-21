"""Discover once, then partition compiled contract methods across CI runners."""
import json
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

PREFIX = "OoplesFinance.StockIndicators.CompetitorTests."
# Scheduling hints from hosted run 39589121403 (2026-10-21), with published
# parity receipts for methods in its timed-out shards. Durations are rounded up.
# SpreadCandle and UltimateAverages long cases are separate methods so a theory
# exceeding the entire job budget is never indivisible. Unknown methods keep
# weight one; all assigned methods and every theory case still execute.
HISTORICAL_SECONDS = {
    PREFIX + 'EmaDifferenceSignalComparisonTests.IndependentRationalContractsCoverLifecycleSelectionsAndWidePeriods': 42,
    PREFIX + 'ExhaustionTests.AddedFixtureAcrossExistingCandlePairs': 58,
    PREFIX + 'ExtendedReversalTests.AddedFixtureAcrossExistingCandlePairs': 33,
    PREFIX + 'MissingSizeBenchmarkTests.MissingSizeIsDiscoverableAndPassesAllFixtureOutputChecks': 141,
    PREFIX + 'RegressionStatisticsComparisonTests.IndependentRationalStatisticsCoverLifecycleAndOverflow': 43,
    PREFIX + 'RemainingAdaptivePairBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 1933,
    PREFIX + 'RemainingAdaptiveQFilterBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 34,
    PREFIX + 'RemainingAdaptiveV2BenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 462,
    PREFIX + 'RemainingConfluenceBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 60,
    PREFIX + 'RemainingDiscreteFourierBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 781,
    PREFIX + 'RemainingEhlersMiscBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 44,
    PREFIX + 'RemainingMacZVwapBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 31,
    PREFIX + 'RemainingPredictionFamilyBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 146,
    PREFIX + 'RemainingPressureFlowBenchmarkTests.KlingerFull10000': 454,
    PREFIX + 'RemainingPressureFlowBenchmarkTests.KlingerLatest10000': 937,
    PREFIX + 'RemainingPressureFlowBenchmarkTests.KvoFull10000': 515,
    PREFIX + 'RemainingPressureFlowBenchmarkTests.KvoLatest10000': 813,
    PREFIX + 'RemainingRecursivePairBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 173,
    PREFIX + 'RemainingSignalAliasesBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 1104,
    PREFIX + 'RemainingSpectralFiltersBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 184,
    PREFIX + 'RemainingSpreadCandleBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 11,
    PREFIX + 'RemainingSpreadCandleBenchmarkTests.RelativeSpreadFull10000': 2555,
    PREFIX + 'RemainingSpreadCandleBenchmarkTests.RelativeSpreadLatest10000': 2578,
    PREFIX + 'RemainingTopsBottomsBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 200,
    PREFIX + 'RemainingUltimateAveragesBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 294,
    PREFIX + 'RemainingUltimateAveragesBenchmarkTests.UltimateBandsFull10000': 1271,
    PREFIX + 'RemainingUltimateAveragesBenchmarkTests.UltimateBandsLatest10000': 1217,
    PREFIX + 'RemainingUltimateAveragesBenchmarkTests.UltimateFull10000': 568,
    PREFIX + 'RemainingUltimateAveragesBenchmarkTests.UltimateLatest10000': 547,
    PREFIX + 'RemainingVariableAveragesBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 96,
    PREFIX + 'RemainingVariableLengthBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 192,
    PREFIX + 'RemainingVolumeVortexBenchmarkTests.VortexFull10000': 569,
    PREFIX + 'RemainingVolumeVortexBenchmarkTests.VortexLatest10000': 507,
    PREFIX + 'RemainingWaddahBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 1313,
    PREFIX + 'RemainingWalkRainbowBenchmarkTests.BothMeasuredArmsPreserveEveryOutputBit': 310,
    PREFIX + 'RemainingWilsonBenchmarkTests.WilsonFull10000': 419,
    PREFIX + 'RemainingWilsonBenchmarkTests.WilsonLatest10000': 368,
    PREFIX + 'SharedTradyNativeCandleBenchmarkTests.NativePrefixesMatchIndependentReferencesAndRetainResults': 242,
    PREFIX + 'SkenderDeviationComparisonTests.IndependentRationalOutputsAndFlagsCoverLifecycleAndExtremeInputs': 53,
}
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
    buckets = [[] for _ in range(total)]
    loads = [0] * total
    # Assign the longest known methods first, with stable name/index tie breaks.
    # Unlike positional striding, adding a method cannot group the known long
    # fixtures together just by shifting their positions in the inventory.
    for name in sorted(methods, key=lambda name: (-HISTORICAL_SECONDS.get(name, 1), name)):
        target = min(range(total), key=lambda index: (loads[index], index))
        buckets[target].append(name)
        loads[target] += HISTORICAL_SECONDS.get(name, 1)
    return sorted(buckets[shard])


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
