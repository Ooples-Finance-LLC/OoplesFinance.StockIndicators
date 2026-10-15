from pathlib import Path
import gzip, hashlib, json, re, xml.etree.ElementTree as E
folder = Path(__file__).resolve().parent
retained = json.loads((folder / "retained-case.json").read_text())
original = E.fromstring(gzip.decompress((folder / "comparator-aborted-redundant-ownership.trx.gz").read_bytes()))
old = [r for r in original.findall(".//{*}UnitTestResult") if r.attrib["testName"] == retained["test_name"]]
assert len(old) == 1 and old[0].attrib["outcome"] == "Passed"
assert hashlib.sha256(gzip.decompress((folder / "original-test-source.cs.gz").read_bytes())).hexdigest() == retained["original_test_source_sha256"]
followup = E.fromstring(gzip.decompress((folder / "comparator-followup.trx.gz").read_bytes()))
rows = followup.findall(".//{*}UnitTestResult")
assert len(rows) == 64 and all(r.attrib["outcome"] == "Passed" for r in rows)
assert all(r.attrib["testName"] != retained["test_name"] for r in rows)
counters = followup.find(".//{*}Counters").attrib
assert int(counters["failed"]) == int(counters["notExecuted"]) == 0
cases = set()
facts = []
for row in old + rows:
    name = row.attrib["testName"]
    match = re.search(r'operation: "([^"]+)", count: (\d+)\)', name)
    if match:
        key = (match.group(1), int(match.group(2)))
        assert key not in cases
        cases.add(key)
    else: facts.append(name)
expected = {(op,n) for op in ("DarkCloudCover","DragonifyDoji","RisingThreeMethods","FallingThreeMethods") for n in (1,2,3,4,19,20,21,23,24,25,183,184,185,513,1000,10000)}
assert cases == expected and len(facts) == 1 and facts[0].endswith("FourEagerArmsCoverFourOperationsAndBothSizes")
print("Verified complete coverage: 64 passing follow-up cases plus one previously completed 10000-bar FallingThreeMethods case. The earlier run was intentionally aborted, not a passing run.")
