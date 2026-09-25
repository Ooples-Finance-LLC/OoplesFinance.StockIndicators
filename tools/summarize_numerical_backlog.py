"""Summarize a completed diagnostic scan and export reproducible review queues."""
import collections
import json
from pathlib import Path
import sys


def summarize(path):
    with path.open(encoding='utf-8-sig') as source:
        header = json.loads(next(source))
        results = [json.loads(line) for line in source]
    if header.get('kind') != 'scan' or header.get('purpose') != 'diagnostic-only':
        raise ValueError('Not a diagnostic scan.')
    selected = set(header['selection'])
    names = [row['name'] for row in results]
    if len(selected) != len(header['selection']) or len(set(names)) != len(names) or set(names) != selected:
        raise ValueError('Missing, duplicate or unselected results; incomplete scans cannot produce complete-family queues.')
    families = collections.defaultdict(list)
    signatures = collections.defaultdict(set)
    for row in results:
        if row.get('kind') != 'result':
            raise ValueError('Unknown evidence record.')
        if row['passed'] and not row['numericalFixturesPassed']:
            raise ValueError('Passing record lacks numerical evidence.')
        families[row['name'].rsplit('/', 1)[0]].append(row)
        for failure in row.get('failures') or []:
            signatures[(failure['Rule'], failure['Fixture'].rsplit('/', 1)[-1])].add(row['name'])
        if row.get('error'):
            signatures[('Exception', row['error'].splitlines()[0])].add(row['name'])
    candidates = sorted(row['name'] for row in results if row['passed'])
    complete = sorted(row['name'] for group in families.values() if all(row['passed'] for row in group) for row in group)
    summary = {
        'purpose': 'diagnostic-review-queue-not-enrollment',
        'assemblySha256': header['assemblySha256'], 'scannerSha256': header['scannerSha256'],
        'mode': header['mode'], 'completed': len(results), 'candidates': len(candidates),
        'needsWork': len(results) - len(candidates),
        'allPassingSelectedFamilies': sum(all(row['passed'] for row in group) for group in families.values()),
        'allPassingSelectedFamilyConfigurations': len(complete),
        'families': {name: {'passed': sum(r['passed'] for r in group), 'total': len(group)} for name, group in sorted(families.items())},
        'failureSignatures': [{'rule': key[0], 'fixtureOrException': key[1], 'configurations': len(value),
                               'examples': sorted(value)[:5]} for key, value in sorted(signatures.items(), key=lambda x: (-len(x[1]), x[0]))]
    }
    return summary, candidates, complete


if __name__ == '__main__':
    if len(sys.argv) != 2:
        raise SystemExit('Usage: summarize_numerical_backlog.py results.jsonl')
    path = Path(sys.argv[1])
    summary, candidates, complete = summarize(path)
    path.with_suffix('.summary.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
    path.with_suffix('.candidates.txt').write_text(''.join(name + '\n' for name in candidates), encoding='utf-8')
    path.with_suffix('.passing-families.txt').write_text(''.join(name + '\n' for name in complete), encoding='utf-8')
    print(f"{summary['completed']} completed; {summary['candidates']} candidates; {summary['needsWork']} need work; "
          f"{summary['allPassingSelectedFamilies']} entirely passing selected families. Review remains required.")
