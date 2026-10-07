"""Plan, run and reconcile unit-test shards using VSTest's discovered fully qualified methods."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET


def read_plan(path):
    plan = json.loads(path.read_text(encoding='utf-8'))
    names = plan['methods']
    if not names or names != sorted(set(names)) or plan['count'] not in range(1, len(names) + 1):
        raise ValueError('Invalid unit-test inventory.')
    return plan


def check_trx(path, expected):
    root = ET.parse(path).getroot()
    results = root.findall('.//{*}UnitTestResult')
    counters = root.find('.//{*}Counters')
    if counters is None or not results or int(counters.get('total', '0')) != len(results):
        raise ValueError('Missing or incomplete unit results.')
    if any(r.get('outcome') != 'Passed' for r in results):
        raise ValueError('Failed or skipped unit tests.')
    definitions = {t.get('id'): t.find('{*}TestMethod') for t in root.findall('.//{*}UnitTest')}
    actual = set()
    for result in results:
        method = definitions.get(result.get('testId'))
        if method is None:
            raise ValueError('Missing test definition.')
        actual.add(method.get('className') + '.' + method.get('name'))
    if actual != set(expected):
        raise ValueError(f'Test selection mismatch: missing {set(expected)-actual}, extra {actual-set(expected)}')
    return len(results)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['plan', 'run', 'merge'])
    parser.add_argument('--assembly', type=Path)
    parser.add_argument('--plan', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--count', type=int, default=20)
    parser.add_argument('--index', type=int)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    if args.action == 'plan':
        listing = args.output / 'discovered.txt'
        subprocess.run(['dotnet', 'vstest', str(args.assembly), '/ListFullyQualifiedTests',
                        '/ListTestsTargetPath:' + str(listing.resolve())], check=True)
        names = sorted(set(listing.read_text(encoding='utf-8-sig').splitlines()))
        if any(not n or any(c in n for c in '|&=()') for n in names):
            raise ValueError('Unsupported filter characters in discovered names.')
        plan = {'methods': names, 'count': args.count,
                'assemblySha256': hashlib.sha256(args.assembly.read_bytes()).hexdigest(),
                'sourceRevision': os.environ.get('GITHUB_SHA', 'local-uncommitted')}
        args.plan.write_text(json.dumps(plan, indent=2), encoding='utf-8')
        read_plan(args.plan)
        return
    plan = read_plan(args.plan)
    if args.action == 'run':
        if args.index not in range(plan['count']):
            raise ValueError('Invalid shard index.')
        if hashlib.sha256(args.assembly.read_bytes()).hexdigest() != plan['assemblySha256']:
            raise ValueError('Test assembly differs from discovered inventory.')
        expected = plan['methods'][args.index::plan['count']]
        settings = ET.Element('RunSettings')
        config = ET.SubElement(settings, 'RunConfiguration')
        ET.SubElement(config, 'TestCaseFilter').text = '|'.join('FullyQualifiedName=' + n for n in expected)
        settings_path = args.output / 'shard.runsettings'
        ET.ElementTree(settings).write(settings_path, encoding='utf-8', xml_declaration=True)
        subprocess.run(['dotnet', 'vstest', str(args.assembly), '/Settings:' + str(settings_path.resolve()),
                        '/Logger:trx;LogFileName=results.trx', '/ResultsDirectory:' + str(args.output.resolve())], check=True)
        executed = check_trx(args.output / 'results.trx', expected)
        receipt = {'index': args.index, 'planSha256': hashlib.sha256(args.plan.read_bytes()).hexdigest(), 'executed': executed}
        (args.output / 'receipt.json').write_text(json.dumps(receipt), encoding='utf-8')
        return
    receipts = list(args.output.rglob('receipt.json'))
    if len(receipts) != plan['count']:
        raise ValueError('Missing unit shards.')
    seen, total = set(), 0
    for path in receipts:
        receipt = json.loads(path.read_text(encoding='utf-8'))
        index = receipt['index']
        if index not in range(plan['count']) or index in seen:
            raise ValueError('Duplicate or invalid unit shard.')
        seen.add(index)
        if receipt['planSha256'] != hashlib.sha256(args.plan.read_bytes()).hexdigest():
            raise ValueError('Unit shard plan mismatch.')
        total += check_trx(path.with_name('results.trx'), plan['methods'][index::plan['count']])
    print(f"All {len(plan['methods'])} discovered methods covered exactly once; {total} passing test cases.")


if __name__ == '__main__':
    main()
