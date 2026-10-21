"""Conservative API navigation from the exact benchmark source snapshot.

Explicit qualified parameters, native calls, or hash-pinned reviewed helper paths
establish an alias. Unknown wrappers and batched operations remain unresolved;
this creates no timings.
"""
import hashlib
import re
import zipfile


# Reviewed Single<TaSumOperation> -> Calculate<T> -> Functions.Sum<double>.
# Match both complete files; any edit requires renewed source review.
REVIEWED_HELPER_MAPPINGS = [dict(
    fixture='SharedRollingSumBenchmarks', operation='SharedRollingSumBenchmarks',
    pair='TaLib.Functions.Sum', source='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/LibraryComparisons/SharedRollingSumBenchmarks.cs',
    source_sha256='cbe65ca986433b4852bbc1574480321378963e0e5cfc90810d556feb2d7a7d92',
    helper='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/LibraryComparisons/PackedWindowBenchmarkExecution.cs', helper_sha256='2abc7bfd10cb5cad44cb75322a8675e8a2eea7aa24b3beff25d8cd6acea5fa21')]


def direct_skender_mapping(source, pairs):
    """Recognize an untransformed native result from a typed Quote array only."""
    source = re.sub(r'/\*.*?\*/|//[^\n]*', '', source, flags=re.S)
    if not re.search(r'^\s*using Skender\.Stock\.Indicators\s*;', source, re.M):
        return set()
    pattern = (r'public List<\w+> NativeSingle\(\)\s*=>\s*'
               r'(?:\(List<\w+>\)\s*)?(\w+)\.(Get\w+)\(([^();"\']*)\)\s*;')
    result = set()
    for match in re.finditer(pattern, source):
        receiver, method, _ = match.groups()
        if re.search(r'\bprivate\s+Quote\[\]\s+' + re.escape(receiver) + r'\s*[=;]', source):
            pair = 'Skender.' + method
            if pair in pairs:
                result.add(pair)
    return result


def ternary_native_mappings(source, operations, pairs):
    """Resolve only two-way dispatch whose complete branches are direct native calls."""
    if len(operations) != 2 or len(set(operations)) != 2:
        return {}
    result = {}
    call = re.compile(r'\s*(?:new\s+)?(QuanTAlib\.\w+|Functions\.\w+<[^<>]+>|Candles\.\w+<[^<>]+>)\s*\(')

    def read_call(start):
        match = call.match(source, start)
        if not match:
            return None
        depth, index = 1, match.end()
        # Strings/comments are outside this deliberately small expression grammar.
        while index < len(source) and depth:
            char = source[index]
            if char in '\"\';' or source[index:index + 2] in ('//', '/*'):
                return None
            depth += (char == '(') - (char == ')')
            index += 1
        if depth:
            return None
        native = match[1].split('<')[0]
        pair = native if native.startswith('QuanTAlib.') else 'TaLib.' + native
        return pair, index

    for condition in re.finditer(r'\bOperation\s*==\s*"([^"\\]+)"\s*\?', source):
        selected = condition[1]
        if selected not in operations:
            continue
        first = read_call(condition.end())
        if first is None:
            continue
        colon = re.match(r'\s*:', source[first[1]:])
        if colon is None:
            continue
        second = read_call(first[1] + colon.end())
        if second is None or not re.match(r'\s*;', source[second[1]:]):
            continue
        other = next(op for op in operations if op != selected)
        for operation, native in ((selected, first[0]), (other, second[0])):
            if native in pairs:
                result.setdefault(operation, set()).add(native)
    return result


def source_mappings(source, fixture, pairs):
    match = re.search(r'\[Params\((.*?)\)\]\s*public string Operation', source, re.S)
    if match:
        operations = re.findall(r'"([^"]+)"', match[1])
    elif '[ParamsSource(nameof(Cases))]' in source:
        match = re.search(r'Cases\s*=>\s*new\[\]\s*\{(.*?)\}', source, re.S)
        operations = re.findall(r'"([^"]+)"', match[1]) if match else []
    elif 'public string Group' in source:
        return []  # A multi-API batch is not an individual API measurement.
    else:
        operations = [fixture]
    ternary = ternary_native_mappings(source, operations, pairs)
    rows = []
    for operation in operations:
        candidates = set(ternary.get(operation, ()))
        if operation in pairs:
            candidates.add(operation)
        # Require the actual switch arm, not an unrelated reference in the file.
        arm = re.escape('"' + operation + '"') + r'\s*=>\s*(?:new\s+)?'
        for expression, prefix in [(r'Functions\.(\w+)<', 'TaLib.Functions.'),
                                   (r'Candles\.(\w+)<', 'TaLib.Candles.'),
                                   (r'QuanTAlib\.(\w+)\(', 'QuanTAlib.')]:
            pattern = arm + expression if operation != fixture else expression
            candidates.update(prefix + name for name in re.findall(pattern, source)
                              if prefix + name in pairs)
        # A single-operation fixture can expose an exact comparison ID directly.
        if operation == fixture:
            candidates.update(re.findall(r'ComparisonPairs.Get\("([^"]+)"\)', source))
            candidates.update(direct_skender_mapping(source, pairs))
        if len(candidates) == 1 and next(iter(candidates)) in pairs:
            rows.append({'fixture': fixture, 'operation': operation, 'pair': candidates.pop()})
    return rows


def snapshot_mappings(archive, fixtures, pairs):
    rows = []
    with zipfile.ZipFile(archive) as source:
        for fixture in fixtures:
            data = source.read(fixture['source'])
            for row in source_mappings(data.decode('utf-8-sig'), fixture['fixture'], pairs):
                rows.append(dict(row, source=fixture['source'], source_sha256=hashlib.sha256(data).hexdigest()))
            for reviewed in REVIEWED_HELPER_MAPPINGS:
                if (reviewed['fixture'] != fixture['fixture'] or reviewed['source'] != fixture['source']
                        or reviewed['pair'] not in pairs
                        or hashlib.sha256(data).hexdigest() != reviewed['source_sha256']
                        or reviewed['helper'] not in source.namelist()):
                    continue
                if hashlib.sha256(source.read(reviewed['helper'])).hexdigest() != reviewed['helper_sha256']:
                    continue
                rows.append(dict(fixture=reviewed['fixture'], operation=reviewed['operation'],
                                 pair=reviewed['pair'], source=reviewed['source'],
                                 source_sha256=reviewed['source_sha256'],
                                 dependencies=[dict(source=reviewed['helper'], sha256=reviewed['helper_sha256'])]))
    return {'snapshot': archive.parent.name, 'archive_sha256': hashlib.sha256(archive.read_bytes()).hexdigest(),
            'status': 'Static operation mapping only; measurements and wrapper reconciliation remain separate.',
            'mappings': rows, 'mapped_pairs': len({r['pair'] for r in rows}),
            'unmapped_pairs': sorted(set(pairs) - {r['pair'] for r in rows})}


def reconcile(fresh, mappings, pairs, snapshot=None):
    aliases = {}
    for row in mappings:
        key = (row['fixture'], row['operation'])
        if key in aliases and aliases[key] != row['pair']:
            raise ValueError('Conflicting fixture/API mapping: ' + str(key))
        aliases[key] = row['pair']
    groups = {}
    unmatched = []
    for row in fresh:
        pair = aliases.get((row['fixture'], row['operation'])) if snapshot is None or row.get('commit') == snapshot else None
        if pair is None and row['operation'] in pairs:
            pair = row['operation']
        if pair not in pairs or row['bars'] not in [1000, 10000] or row['mode'] not in ['BuilderFull', 'BuilderLatestOnly']:
            unmatched.append(dict(row, mapping_status='Outside reconciled API/size/mode grid'))
            continue
        groups.setdefault((pair, row['bars'], row['mode']), []).append(row)
    matched = {}
    for key, rows in groups.items():
        if len(rows) == 1:
            matched[key] = rows[0]
        else:
            # Never cherry-pick the fastest fixture or double-count one slot.
            unmatched.extend(dict(row, mapping_status='Multiple fixtures for one API slot; review required') for row in rows)
    assert len(matched) + len(unmatched) == len(fresh)
    return matched, unmatched
