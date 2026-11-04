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


def verify_reconciliation(reference, baseline):
    proof = reference['dependency_reconciliation']
    assert proof['kind'] == 'master-tensors-0.144.0-to-0.146.0'
    assert proof['timings_reused'] is False
    qualified = reference['qualified_source']
    changed = git('diff', '--name-only', qualified, baseline,
                  '--', 'src', 'tests', BENCH, TESTS).splitlines()
    assert changed == [PROJECT], changed
    before = blob(qualified, PROJECT).replace(b'\r\n', b'\n')
    after = blob(baseline, PROJECT).replace(b'\r\n', b'\n')
    assert before.count(OLD) == 1 and after == before.replace(OLD, NEW)
    raw = blob(baseline, proof['evidence'])
    assert hashlib.sha256(raw).hexdigest() == proof['sha256']
    evidence = json.loads(raw)
    assert evidence['status'] == 'correctness-complete'
    assert evidence['unit']['passed'] == 64 and evidence['parity']['passed'] == 38
    assert evidence['gpu_passed'] == 100
    directory = proof['evidence'].rsplit('/', 1)[0]
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
