"""Track the entire remaining rollout without inferring performance from coverage.

Uses committed sources, not generated build artifacts. Enum entries represent
batch formulas; handwritten types represent additional facade contracts. They
are separate surfaces, not an additive count of unique mathematical formulas.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path


def source_text(path):
    data = path.read_bytes()
    try:
        text = data.decode('utf-8-sig')
    except UnicodeDecodeError:
        text = data.decode('cp1252')
    return text.replace('\r\n', '\n')


def enum_members(source):
    body = source.split('public enum IndicatorName', 1)[1].split('{', 1)[1].rsplit('}', 1)[0]
    body = re.sub(r'\[Category\(IndicatorType\.\w+\)\]', '', body)
    members = [part.strip() for part in body.split(',') if part.strip()]
    if any(not re.fullmatch(r'\w+', name) for name in members):
        raise ValueError('Unrecognized IndicatorName syntax; update inventory parser')
    if len(members) != len(set(members)) or 'None' not in members:
        raise ValueError('Duplicate members or missing None sentinel')
    return sorted(set(members) - {'None'})


def inventory(root):
    sources = {}

    def read(relative):
        text = source_text(root / relative)
        sources[relative] = hashlib.sha256(text.encode('utf-8')).hexdigest()
        return text

    members = enum_members(read('src/Enums/IndicatorName.cs'))
    targets = re.findall(
        r'\[typeof\((\w+)\)\]\s*=\s*new\(IndicatorName\.(\w+)',
        read('src/Builder/Compute/BuilderArmTargets.cs'))
    if not targets or any(name not in members for _, name in targets):
        raise ValueError('Missing or unknown builder targets')
    manifest = json.loads(read('benchmarks/competitor-library-manifest.json'))
    paired = [row for row in manifest if row['Status'] == 'paired']
    handwritten = []
    for path in sorted((root / 'src/Indicators').glob('*.cs')):
        relative = path.relative_to(root).as_posix()
        source = read(relative)
        for match in re.finditer(
            r'^public (?:sealed )?(?:partial )?class (\w+)\s*:\s*([^\r\n{]+)',
            source, re.MULTILINE
        ):
            if not re.search(r'\b(?:MultiOutputIndicatorBase|IndicatorBase)\b', match[2]):
                continue
            handwritten.append(dict(
                type=match[1], source=relative,
                exact_catalog_mapping_ids=sorted(row['Id'] for row in paired
                                                 if row['OoplesIndicator'] == match[1]),
                status='pending-assessment'))
    if not handwritten:
        raise ValueError('No handwritten indicators found')
    return dict(
        schema_version=1,
        scope='All remaining indicators; no enum formula is silently excluded by competitor overlap.',
        qualification='Inventory only. Exact catalog name matches are navigation hints, not parity or speed evidence. '
                      'All entries await assessment against inherited work; no new optimization is claimed.',
        inherited_pr=255,
        counts=dict(batch_formulas=len(members), handwritten_facade_types=len(handwritten)),
        batch_formulas=[dict(indicator=name,
                             options=sorted(option for option, target in targets if target == name),
                             status='pending-assessment') for name in members],
        handwritten_facades=sorted(handwritten, key=lambda row: row['type']),
        normalized_source_sha256=dict(sorted(sources.items())))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    path = root / 'benchmarks/results/remaining-indicators/inventory.json'
    report = inventory(root)
    content = json.dumps(report, indent=2) + '\n'
    if args.check:
        if path.read_text(encoding='utf-8') != content:
            raise SystemExit('Remaining indicator inventory is stale; regenerate it')
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding='utf-8', newline='\n')
    print(json.dumps(report['counts'], sort_keys=True))


if __name__ == '__main__':
    main()
