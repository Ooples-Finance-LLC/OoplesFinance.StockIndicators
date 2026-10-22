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


def contract_receipts(root, targets, read, batch_receipts=None, known_formulas=None):
    receipts = {}
    known_options = {option for option, _ in targets}
    for path in sorted((root / 'benchmarks/results/remaining-indicators').glob('*-contracts.json')):
        # Shared GPU fixture receipts describe test methods, not facade coverage.
        if path.name.endswith('-gpu-contracts.json'):
            continue
        relative = path.relative_to(root).as_posix()
        record = json.loads(read(relative))
        if record.get('receipt_scope') == 'batch-formulas':
            if batch_receipts is None or known_formulas is None:
                raise ValueError('Batch receipts require an explicit enum mapping')
            for name in record['types']:
                if name not in known_formulas:
                    raise ValueError(f'Unknown batch formula in receipt: {name}')
                batch_receipts.setdefault(name, []).append(relative)
            continue
        for name in record['types']:
            option = name + 'SpecOptions'
            if option not in known_options:
                raise ValueError(f'Unmapped receipt type {name}: add an explicit options mapping')
            receipts.setdefault(option, []).append(relative)
    return receipts


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
            handwritten.append({
                'type': match[1], 'source': relative,
                'exact_catalog_mapping_ids': sorted(row['Id'] for row in paired
                                                   if row['OoplesIndicator'] == match[1]),
                'status': 'pending-assessment'})
    if not handwritten:
        raise ValueError('No handwritten indicators found')
    # Receipts record completed batches, not a current-head performance verdict.
    # Keep unmapped options visible: reviewing one alias does not cover them all.
    batch_receipts = {}
    receipts = contract_receipts(root, targets, read, batch_receipts, set(members))
    formulas = []
    for name in members:
        options = sorted(option for option, target in targets if target == name)
        recorded = {option: receipts[option] for option in options if option in receipts}
        row = {'indicator': name, 'options': options, 'status': 'pending-assessment'}
        if recorded:
            row.update(status='contract-receipts-recorded-performance-pending',
                       contract_receipts=recorded,
                       options_without_receipts=[option for option in options if option not in receipts])
        if name in batch_receipts:
            row.update(status='contract-receipts-recorded-performance-pending',
                       batch_contract_receipts=batch_receipts[name],
                       options_without_receipts=[option for option in options if option not in receipts])
        formulas.append(row)
    return {
        'schema_version': 2,
        'scope': 'All remaining indicators; no enum formula is silently excluded by competitor overlap.',
        'qualification': 'Inventory only. Exact catalog name matches are navigation hints, not parity or speed evidence. '
                      'Batch receipts record historical contract checks, not current-head or performance qualification. '
                      'Options without receipts still await assessment against inherited work.',
        'inherited_pr': 255,
        'counts': {'batch_formulas': len(members), 'handwritten_facade_types': len(handwritten)},
        'batch_formulas': formulas,
        'handwritten_facades': sorted(handwritten, key=lambda row: row['type']),
        'normalized_source_sha256': dict(sorted(sources.items()))}


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
