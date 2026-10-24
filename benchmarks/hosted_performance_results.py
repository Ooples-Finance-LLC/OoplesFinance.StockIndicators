"""Verify hosted summary artifacts against their raw, source-pinned measurements."""
import contextlib
import csv
import gzip
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import re
import tempfile


def load_results(folder, source_sha, run_id, manifest):
    """Rebuild the complete summary; never trust supplied CSV values or counts."""
    if not re.fullmatch(r'[0-9a-f]{40}', source_sha):
        raise ValueError('An exact measurement source commit is required')
    if type(run_id) is not int or run_id <= 0:
        raise ValueError('A numeric measurement run ID is required')
    folder = Path(folder)
    evidence = json.loads((folder / 'evidence.json').read_text(encoding='utf-8'))
    url = f'https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/actions/runs/{run_id}'
    if evidence['sourceSha'] != source_sha or evidence['runUrl'] != url:
        raise ValueError('Hosted measurement source/run mismatch')
    if (folder / 'inventory.json').read_bytes() != manifest:
        raise ValueError('Hosted inventory differs from the pinned source')
    if hashlib.sha256(manifest).hexdigest() != evidence['manifestSha256']:
        raise ValueError('Hosted inventory hash mismatch')
    paired = sum(row['Status'] == 'paired' for row in json.loads(manifest))
    if type(evidence['shards']) is not int or evidence['shards'] != min(20, paired):
        raise ValueError('Unexpected hosted shard count')
    script = Path(__file__).resolve().parents[1] / 'scripts/report-competitor-performance.py'
    spec = importlib.util.spec_from_file_location('hosted_summary_verifier', script)
    verifier = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(verifier)
    with tempfile.TemporaryDirectory(prefix='si-hosted-results-') as temporary:
        root = Path(temporary)
        artifacts = root / 'artifacts'
        seen_files, seen_targets = set(), set()
        for receipt in evidence['reports']:
            filename, target = receipt['file'], receipt['artifactPath']
            if not re.fullmatch(r'raw/\d+-report\.json\.gz', filename):
                raise ValueError('Invalid hosted raw report path')
            if not re.fullmatch(r'competitor-performance-\d+/results/[\w.\-]+-report-full[\w.\-]*\.json', target):
                raise ValueError('Invalid hosted artifact path')
            if filename in seen_files or target in seen_targets:
                raise ValueError('Duplicate hosted raw report')
            seen_files.add(filename)
            seen_targets.add(target)
            raw = gzip.decompress((folder / filename).read_bytes())
            if hashlib.sha256(raw).hexdigest() != receipt['sha256']:
                raise ValueError('Hosted raw report hash mismatch')
            path = artifacts / target
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
        manifest_path = root / 'inventory.json'
        manifest_path.write_bytes(manifest)
        output = root / 'regenerated'
        with contextlib.redirect_stdout(io.StringIO()):
            regenerated = verifier.report(manifest_path, artifacts, evidence['shards'], output, source_sha, url)
        if regenerated != evidence:
            raise ValueError('Hosted provenance differs from raw measurements')
        if (output / 'comparisons.csv').read_bytes() != (folder / 'comparisons.csv').read_bytes():
            raise ValueError('Hosted comparison values differ from raw measurements')
        with (output / 'comparisons.csv').open(encoding='utf-8', newline='') as stream:
            rows = list(csv.DictReader(stream))
        for row in rows:
            row['bars'] = int(row['bars'])
            for key in row.keys() - {'pair', 'ooples_indicator', 'bars'}:
                row[key] = float(row[key])
        return {'source_sha': source_sha, 'run_id': run_id, 'url': url,
                'paired_apis': paired, 'rows': rows, 'evidence': evidence}
