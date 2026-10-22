"""Combine unchanged passing tests with a complete rerun of one corrected class.

Raw TRX files are never rewritten. Any uncovered failure or incomplete run fails.
"""
import xml.etree.ElementTree as ET

NS = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}  # NOSONAR: XML namespace identifier, not a network connection.


def read_run(raw, outcome):
    root = ET.fromstring(raw)
    summary = root.find('t:ResultSummary', NS)
    assert summary.attrib['outcome'] == outcome
    results = root.findall('.//t:Results/t:UnitTestResult', NS)
    assert results
    named = {r.attrib['testName']: r for r in results}
    assert len(named) == len(results), 'Duplicate test identity'
    counters = summary.find('t:Counters', NS).attrib
    assert int(counters['total']) == len(results)
    assert int(counters['passed']) == sum(r.attrib['outcome'] == 'Passed' for r in results)
    assert int(counters['failed']) == sum(r.attrib['outcome'] == 'Failed' for r in results)
    assert int(counters['passed']) + int(counters['failed']) == len(results)
    assert all(r.attrib['outcome'] in ('Passed', 'Failed') for r in results)
    return named


def resolve(original_raw, rerun_raw, corrected_class):
    assert corrected_class and not corrected_class.endswith('.')
    original = read_run(original_raw, 'Failed')
    rerun = read_run(rerun_raw, 'Completed')
    selected = {name: row for name, row in original.items() if name.startswith(corrected_class + '.')}
    assert set(selected) == set(rerun), 'Rerun must cover exactly the corrected class'
    assert any(row.attrib['outcome'] == 'Failed' for row in selected.values())
    assert all(row.attrib['outcome'] == 'Passed' for row in rerun.values())
    retained = {name: row for name, row in original.items() if name not in selected}
    assert all(row.attrib['outcome'] == 'Passed' for row in retained.values()), 'Uncovered failure'
    return list(retained.values()) + list(rerun.values()), len(retained)
