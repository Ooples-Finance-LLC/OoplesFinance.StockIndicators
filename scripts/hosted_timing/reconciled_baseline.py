"""Authenticate the approved master dependency reconciliation, without reusing its timings."""
import gzip
import hashlib
import json
import subprocess
import tempfile
from pathlib import Path

from .pipeline import BENCH, REPO, TESTS, git, verify_trx

PROJECT = 'src/OoplesFinance.StockIndicators.csproj'
GPU = 'benchmarks/results/shared-rollout/local-gpu-contracts/'
OLD = b'Include="AiDotNet.Tensors" Version="0.144.0"'
NEW = b'Include="AiDotNet.Tensors" Version="0.146.0"'


def blob(source, path):
    return subprocess.check_output(['git', 'show', source + ':' + path], cwd=REPO)


def verify_publication(raw, baseline):
    receipt = json.loads(raw)
    assert receipt['head'] == baseline and receipt['counts']['success'] >= 70
    assert len(receipt['checks']) >= 70
    assert receipt['counts']['success'] == sum(c['conclusion'] == 'success' for c in receipt['checks'])
    assert all(c['status'] == 'completed' and c['conclusion'] in ('success', 'skipped')
               for c in receipt['checks'])
    assert all(s['status'] == 'completed' and s['conclusion'] == 'success'
               for s in receipt['suites'] if s['app'] == 'github-actions')
    assert any(s['app'] == 'sonarqubecloud' and s['status'] == 'completed'
               and s['conclusion'] == 'success' for s in receipt['suites'])
    assert any(s['context'] == 'CodeRabbit' and s['state'] == 'success'
               for s in receipt['statuses'])
    assert receipt['pr'] == dict(number=266, draft=True, auto_merge=None, base='master')


def verify_reconciliation(reference, baseline):
    proof = reference['dependency_reconciliation']
    versions = {
        'master-tensors-0.144.0-to-0.146.0': (OLD, NEW, 64, 38),
        'master-tensors-0.146.0-to-0.147.0': (
            NEW, b'Include="AiDotNet.Tensors" Version="0.147.0"', 53, 56),
        'master-tensors-0.147.0-to-0.148.0': (
            b'Include="AiDotNet.Tensors" Version="0.147.0"',
            b'Include="AiDotNet.Tensors" Version="0.148.0"', 53, 355),
    }
    assert proof['kind'] in versions
    old, new, unit_count, parity_count = versions[proof['kind']]
    assert proof['timings_reused'] is False
    qualified = reference['qualified_source']
    changed = git('diff', '--name-only', qualified, baseline,
                  '--', 'src', 'tests', BENCH, TESTS).splitlines()
    assert changed == [PROJECT], changed
    before = blob(qualified, PROJECT).replace(b'\r\n', b'\n')
    after = blob(baseline, PROJECT).replace(b'\r\n', b'\n')
    assert before.count(old) == 1 and after == before.replace(old, new)
    raw = blob(baseline, proof['evidence'])
    assert hashlib.sha256(raw).hexdigest() == proof['sha256']
    evidence = json.loads(raw)
    assert evidence['status'] == 'correctness-complete'
    assert evidence['unit']['passed'] == unit_count and evidence['parity']['passed'] == parity_count
    assert evidence['gpu_passed'] == 100
    directory = proof['evidence'].rsplit('/', 1)[0]
    if proof['kind'] in ('master-tensors-0.146.0-to-0.147.0', 'master-tensors-0.147.0-to-0.148.0'):
        manifest = json.loads(gzip.decompress(blob(baseline, directory + '/ci-manifest.json.gz')))
        assert manifest['status'] == 'cpu-correctness-complete'
        assert manifest['source'] == evidence['source']
        assert manifest['workflow_head'] == evidence['build_origin']['workflow_head']
        assert manifest['run_id'] == evidence['build_origin']['run_id']
        assert manifest['checks'] == {name: evidence[name] for name in ('unit', 'parity')}
        publication = proof['publication_ci']
        path = (REPO / publication['path']).resolve()
        assert path.is_relative_to(REPO.resolve())
        raw_publication = path.read_bytes()
        assert hashlib.sha256(raw_publication).hexdigest() == publication['sha256']
        verify_publication(raw_publication, baseline)
    else:
        final = json.loads(blob(baseline, directory + '/final-workflow-state.json'))
        assert final == dict(status='completed', conclusion='success',
                             headSha=evidence['build_origin']['workflow_head'])
    assert evidence['build_origin']['source'] == evidence['source']
    with tempfile.TemporaryDirectory() as folder:
        for name in ('unit', 'parity'):
            trx = gzip.decompress(blob(baseline, directory + '/' + name + '.trx.gz'))
            path = Path(folder) / (name + '.trx')
            path.write_bytes(trx)
            assert verify_trx(path, list(evidence[name]['classes'])) == evidence[name]
    assert not git('diff', '--name-only', evidence['source'], baseline,
                   '--', 'src', 'tests', BENCH, TESTS)
    assert len(evidence['production_hashes']) == 4
    assert len(set(evidence['production_hashes'].values())) == 1
    receipt = json.loads(blob(baseline, GPU + 'receipt.json'))
    trx = gzip.decompress(blob(baseline, GPU + 'gpu.trx.gz'))
    methods = json.loads(blob(baseline, 'scripts/competitor-gpu-methods.json'))
    assert receipt['source_sha256'] == evidence['source_fingerprint']
    assert receipt['build_origin'] == evidence['build_origin']
    # The normal preparation verifier also recomputes the baseline fingerprint
    # from its checkout and verifies every source-bound runtime file and TRX.
    import importlib.util
    spec = importlib.util.spec_from_file_location('reconciled_gpu', REPO / 'scripts/verify-competitor-local-gpu.py')
    gpu = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(gpu)
    assert gpu.verify_evidence(receipt, trx, methods, evidence['source_fingerprint']) == 100
