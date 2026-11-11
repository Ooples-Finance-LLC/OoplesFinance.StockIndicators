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
RUNTIME_0150 = 'master-tensors-0.149.0-to-0.150.0-and-net461-json11'


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
        'master-tensors-0.148.0-to-0.149.0': (
            b'Include="AiDotNet.Tensors" Version="0.148.0"',
            b'Include="AiDotNet.Tensors" Version="0.149.0"', 78, 45),
        RUNTIME_0150: (
            b'Include="AiDotNet.Tensors" Version="0.149.0"',
            b'Include="AiDotNet.Tensors" Version="0.150.0"', 161, 292),
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
    assert before.count(old) == 1
    expected = before.replace(old, new)
    if proof['kind'] == RUNTIME_0150:
        old_json = b'Include="System.Text.Json" Version="10.0.14"'
        assert before.count(old_json) == 1
        expected = expected.replace(old_json, b'Include="System.Text.Json" Version="11.0.0"')
    assert after == expected
    raw = blob(baseline, proof['evidence'])
    assert hashlib.sha256(raw).hexdigest() == proof['sha256']
    evidence = json.loads(raw)
    assert evidence['status'] == 'correctness-complete'
    assert evidence['unit']['passed'] == unit_count and evidence['parity']['passed'] == parity_count
    assert evidence['gpu_passed'] == 100
    directory = proof['evidence'].rsplit('/', 1)[0]
    if proof['kind'] in ('master-tensors-0.146.0-to-0.147.0', 'master-tensors-0.147.0-to-0.148.0',
                         'master-tensors-0.148.0-to-0.149.0', RUNTIME_0150):
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
    if proof['kind'] == RUNTIME_0150:
        legacy = json.loads(blob(baseline, directory + '/legacy/verification.json'))
        origin = json.loads(blob(baseline, directory + '/legacy/legacy-source.json'))
        assert legacy['source'] == origin['source'] == evidence['source']
        assert legacy['framework'] == origin['framework'] == 'net461'
        assert legacy['result'] == 'passed'
        assert legacy['run_id'] == origin['run_id']
        assert legacy['workflow_head'] == origin['tooling_sha']
        assert origin['tensor_version'] == '0.150.0' and origin['legacy_json_version'] == '11.0.0'
        project = blob(evidence['source'], PROJECT).replace(b'\r\n', b'\n')
        assert hashlib.sha256(project).hexdigest() == legacy['project_bytes_sha256']
        text_hash = hashlib.sha256(project.decode('utf-8-sig').encode('utf-8')).hexdigest()
        assert text_hash == legacy['project_sha256'] == origin['project_sha256']
        log = gzip.decompress(blob(baseline, directory + '/legacy/legacy-build.log.gz'))
        assert hashlib.sha256(log).hexdigest() == legacy['log_sha256']
        text = log.decode('utf-8-sig')
        assert 'Build succeeded.' in text and '0 Error(s)' in text and 'net461' in text
        dispatch = json.loads(blob(baseline, directory + '/legacy/dispatch.json'))
        assert dispatch['inputs'] == dict(suite='legacy-net461-build', candidate_source=evidence['source'])
        workflow = gzip.decompress(blob(baseline, directory + '/legacy/workflow.yml.gz'))
        assert workflow == blob(legacy['workflow_head'], '.github/workflows/cpu-kernel-performance.yml')
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
