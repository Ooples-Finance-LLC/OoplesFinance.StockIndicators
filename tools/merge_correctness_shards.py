"""Merge disjoint verifier shards; partial evidence can never become full evidence."""
import argparse
from copy import deepcopy
from pathlib import Path
import xml.etree.ElementTree as ET


def merge(paths, count):
    if count < 1 or len(paths) != count:
        raise ValueError('Every expected shard is required.')
    roots = [ET.parse(p).getroot() for p in paths]
    first = roots[0]
    inventory = [n.text for n in first.findall('./inventory/name')]
    if not inventory or len(set(inventory)) != len(inventory):
        raise ValueError('Missing or duplicate inventory.')
    identity = ('assembly', 'assemblySha256', 'sourceRevision', 'runtime', 'os',
                'pointerBits', 'requiredNumericalFixtures')
    if any(not first.get(k) for k in identity):
        raise ValueError('Missing evidence identity.')
    seen, cases = set(), {}
    for root in roots:
        index = int(root.get('shardIndex', '-1'))
        if (root.get('scope') != 'shard' or int(root.get('shardCount', '0')) != count
                or index not in range(count) or index in seen):
            raise ValueError('Invalid or duplicate shard.')
        seen.add(index)
        if any(root.get(k) != first.get(k) for k in identity):
            raise ValueError('Shard identities differ.')
        if [n.text for n in root.findall('./inventory/name')] != inventory:
            raise ValueError('Shard inventories differ.')
        part = root.findall('case')
        names = [c.get('name') for c in part]
        if names != inventory[index::count]:
            raise ValueError('Shard does not exactly cover its assigned configurations.')
        for case in part:
            if case.get('passed') != 'True' or case.find('error') is not None or case.find('failure') is not None:
                raise ValueError('Failed configuration: ' + str(case.get('name')))
            cases[case.get('name')] = case
    result = ET.Element('correctnessEvidence', dict(first.attrib))
    for key in ('shardIndex', 'shardCount'):
        result.attrib.pop(key)
    result.set('scope', 'all-discovered-configurations')
    result.set('mergedShards', str(count))
    for name in inventory:
        result.append(deepcopy(cases[name]))
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--count', type=int, required=True)
    args = parser.parse_args()
    root = merge(sorted(args.input.rglob('correctness-evidence.xml')), args.count)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    ET.ElementTree(root).write(args.output, encoding='utf-8', xml_declaration=True)
    print(f"Verified {len(root.findall('case'))} configurations across {args.count} shards.")
