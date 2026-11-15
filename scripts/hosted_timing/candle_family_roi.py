"""Authenticated candle-family feasibility measurements; never production qualification."""
import contextlib
import copy
import gzip
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

from . import pipeline as p

APPROVAL = 'benchmarks/plans/evidence/candle-family-native-pilot-scope-20261115.json'
APPROVAL_SHA = '8ddced7b05eb420bee3d3ee170a2f66647e6e3eb97e421a3b8f92fe6fdcf1d8b'
BASE_PLAN = 'benchmarks/plans/hosted-window-log-positive-regions-20261115.json'
BASE_PLAN_SHA = '65c79d85a2ac395a0478d6e0a0c173cb2714d161fb6dafa6732addd26b261503'
OLD = 'ce02581b1f55400fd674b5496ee659fa0e8654fa'
NEW = 'e71d4bc6577e117e78c3c4e099cd2c5a4a3fe0e8'
VARIANTS = 'ce994435f670a1c2c8d5424e167316914cfe9b51'
PRODUCER = '0857eefc242585a9f44bfa3c134ea0bd3eb1b30a'
PAIRS = ['TaLib.Candles.UpsideGapTwoCrows', 'TaLib.Candles.SeparatingLines', 'TaLib.Candles.UniqueThreeRiver']
UNIT = ['CandleDifferenceUpdateTests', 'GapContinuationSharedExecutionTests', 'MatchedLinesSharedExecutionTests', 'MatchedLinesParallelExecutionTests', 'MultiBarCandleSharedExecutionTests', 'NeckParallelExecutionTests', 'NeckSharedExecutionTests', 'SequenceParallelExecutionTests', 'SequenceSharedExecutionTests', 'TripleBodyParallelExecutionTests', 'TripleBodySharedExecutionTests']
PARITY = ['SequenceCandleTests', 'MatchedLinesTests', 'NeckCandleTests', 'GapContinuationTests', 'TripleBodyTests', 'SharedMatchedLinesBenchmarkTests', 'SharedSequenceCandleBenchmarkTests', 'SharedNeckCandleBenchmarkTests', 'SharedGapContinuationBenchmarkTests', 'SharedTripleBodyBenchmarkTests']
POLICY = dict(order=['old', 'new', 'new', 'old'], quiet_samples=12,
              max_cpu_percent=15, warmups=10, iterations=10, fixed_invocation_warmups=1000)
PATHS = ['src', 'benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks',
         'benchmarks/OoplesFinance.StockIndicators.CompetitorTests',
         'scripts/run-competitor-contracts.py', 'scripts/verify-competitor-local-gpu.py',
         'scripts/competitor-gpu-methods.json', 'Directory.Build.props', 'Directory.Build.targets',
         'Directory.Packages.props', 'global.json', 'NuGet.Config', 'nuget.config']


def validate_scope(plan):
    assert plan['diagnostic_kind'] == 'candle-family-native-pilot'
    assert p.digest(p.REPO / APPROVAL) == APPROVAL_SHA
    approval = p.read(p.REPO / APPROVAL)
    assert approval['approved'] and approval['diagnostic_only'] and not approval['qualification']
    assert plan['roi_approval'] == dict(path=APPROVAL, sha256=APPROVAL_SHA)
    assert plan['diagnostic_only'] and plan['runtime_policy_overrides'] == {}
    assert plan['sources'] == dict(published=OLD, old=OLD, successor=NEW, variants=VARIANTS)
    assert (plan['cases'], plan['decisions'], plan['matrix']['shards'], plan['matrix']['max_parallel']) == (30, 6, 1, 1)
    assert plan['proposed_dotnet_sdk'] == '10.0.403'
    assert plan['measurement_policy'] == POLICY
    assert plan['runtime_dependency_alignment'] == dict(tensor_version='0.150.0')
    assert plan['require_fixture_inventory']
    task = dict(fixture='LibraryPairBenchmarks', operations=[dict(raw=x, pair=x) for x in PAIRS],
                methods=['Ooples', 'Competitor'], implicit_pair=False, cases=30, decisions=6)
    assert plan['diagnostic_shards'] == plan['targeted_shards'] == [dict(index=0, tasks=[task], cases=30, decisions=6)]
    assert not any(plan.get(k) for k in ('targeted_reference', 'coverage_reference', 'standalone_variants',
                                       'qualify_variants', 'trix_preflight', 'reuse_inputs'))
    candidate = plan['candidate']
    assert candidate['ci_run'] == '43759123590' and candidate['producer'] == PRODUCER
    assert candidate['source'] == NEW and candidate['unit_classes'] == UNIT and candidate['parity_classes'] == PARITY
    assert re.fullmatch('[a-f0-9]{40}', candidate['evidence_source'])
    for key in ('manifest_sha256', 'gpu_receipt_sha256'):
        assert re.fullmatch('[a-f0-9]{64}', candidate[key])
    assert plan['baseline_inputs']['run_id'] == '43673961720'
    assert plan['baseline_inputs']['plan'] == BASE_PLAN and plan['baseline_inputs']['plan_sha256'] == BASE_PLAN_SHA
    assert re.fullmatch('[a-f0-9]{64}', plan['baseline_inputs']['provenance_sha256'])


@contextlib.contextmanager
def selected_plan(path):
    previous = p.PLAN
    p.PLAN = Path(path)
    try:
        yield
    finally:
        p.PLAN = previous


def prefetch_source_blobs(source):
    # Sparse checkout uses blob:none. Reading a historical tree one blob at a
    # time otherwise makes thousands of separate authenticated network fetches.
    promisor = subprocess.run(['git','config','--get','remote.origin.promisor'],
                              cwd=p.REPO,capture_output=True,text=True)
    if promisor.returncode or promisor.stdout.strip() != 'true': return
    entries = p.git('ls-tree','-r',source,'--',*PATHS).splitlines()
    blobs = sorted({line.split()[2] for line in entries if line.split()[1] == 'blob'})
    assert blobs and all(re.fullmatch('[a-f0-9]{40}', blob) for blob in blobs)
    print(f'Prefetching {len(blobs)} exact source blobs in one request',flush=True)
    # Binary stdin preserves LF on Windows; text-mode CRLF corrupts refspecs.
    subprocess.run(['git','-c','fetch.negotiationAlgorithm=noop','fetch','--no-tags',
                    '--no-write-fetch-head','origin','--stdin'],cwd=p.REPO,
                   input=('\n'.join(blobs)+'\n').encode(),check=True,timeout=180)


def source_fingerprint(source):
    """Use the GPU verifier's canonical algorithm without checking out source."""
    prefetch_source_blobs(source)
    names = sorted(set(p.git('ls-tree', '-r', '--name-only', source, '--', *PATHS).splitlines()))
    assert names
    raw = subprocess.check_output(['git', 'cat-file', '--batch'], cwd=p.REPO,
                                  input=''.join(source + ':' + name + '\n' for name in names).encode())
    stream, result = io.BytesIO(raw), hashlib.sha256()
    for name in names:
        header = stream.readline().split()
        assert len(header) == 3 and header[1] == b'blob'
        content = stream.read(int(header[2])).replace(b'\r\n', b'\n')
        assert stream.read(1) == b'\n'
        result.update(name.encode() + b'\0' + hashlib.sha256(content).digest())
    assert not stream.read()
    return result.hexdigest()


def candidate_inputs(folder, plan):
    return _candidate_inputs(folder, plan, NEW, UNIT, PARITY, 70, 393, '43759123590')


def _candidate_inputs(folder, plan, NEW, UNIT, PARITY, expected_unit, expected_parity, ci_run):
    folder = Path(folder).resolve()
    def read(name):
        path = (folder / name).resolve()
        assert path.is_relative_to(folder) and path.is_file()
        return path.read_bytes()
    spec = importlib.util.spec_from_file_location('roi_candidate_inputs', p.REPO / 'scripts/build-candidate-correctness.py')
    ci = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(ci)
    fingerprint = source_fingerprint(NEW)
    raw = read('manifest.json')
    assert hashlib.sha256(raw).hexdigest() == plan['candidate']['manifest_sha256']
    manifest = json.loads(raw)
    assert (manifest['run_id'], manifest['workflow_head']) == (ci_run, PRODUCER)
    assert manifest['classes'] == dict(unit=UNIT, parity=PARITY)
    production = ci.validate_layout(manifest, read, NEW, fingerprint)
    for label, classes, expected in [('unit', UNIT, expected_unit), ('parity', PARITY, expected_parity)]:
        with tempfile.TemporaryDirectory(prefix='si-roi-trx-') as work:
            path = Path(work) / 'cases.trx'
            path.write_bytes(read('evidence/' + label + '.trx'))
            checks = p.verify_trx(path, classes)
        assert checks == manifest['checks'][label] and checks['passed'] == expected
    evidence = plan['candidate']['evidence_source']
    assert not p.git('diff', '--name-only', NEW, evidence, '--', *PATHS, 'tests')
    gpu = ci.gpu_module(p.REPO)
    base = 'benchmarks/results/shared-rollout/local-gpu-contracts/'
    receipt_raw = subprocess.check_output(['git', 'show', evidence + ':' + base + 'receipt.json'], cwd=p.REPO)
    trx_raw = subprocess.check_output(['git', 'show', evidence + ':' + base + 'gpu.trx.gz'], cwd=p.REPO)
    assert hashlib.sha256(receipt_raw).hexdigest() == plan['candidate']['gpu_receipt_sha256']
    receipt, trx = json.loads(receipt_raw), gzip.decompress(trx_raw)
    methods = json.loads(p.git('show', NEW + ':scripts/competitor-gpu-methods.json'))
    assert gpu.verify_evidence(receipt, trx, methods, fingerprint) == receipt['passed_cases'] == 100
    assert receipt['build_origin']['source'] == NEW and receipt['build_origin']['run_id'] == ci_run
    assert receipt['build_origin']['workflow_head'] == PRODUCER
    assert receipt['test_assembly_sha256'] == manifest['runtime_files'][ci.ROOTS[-1] + '/OoplesFinance.StockIndicators.CompetitorTests.dll']
    # Verify the whole tested tensor closure before using the frozen harness.
    from .runtime_dependencies import tensor_closure
    with tempfile.TemporaryDirectory(prefix='si-roi-tested-') as work:
        runtime = Path(work).resolve()
        assert runtime.parent == Path(tempfile.gettempdir()).resolve()
        prefix = ci.ROOTS[1] + '/'
        for name, sha in manifest['runtime_files'].items():
            if name.startswith(prefix):
                target = (runtime / name[len(prefix):]).resolve()
                assert target.is_relative_to(runtime)
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(read('blobs/' + sha))
        closure = tensor_closure(runtime, 'OoplesFinance.StockIndicators.Tests.Unit.deps.json')
    return dict(manifest=manifest, read=read, production=production, closure=closure,
                gpu_receipt=receipt_raw, gpu_trx=trx_raw, fingerprint=fingerprint)


def prepare(baseline, candidate, out):
    return _prepare(baseline, candidate, out, validate_scope, candidate_inputs, NEW)


def _prepare(baseline, candidate, out, validate_scope, candidate_inputs, NEW):
    plan = p.read(p.PLAN)
    validate_scope(plan)
    baseline, out = Path(baseline).resolve(), Path(out).resolve()
    assert not out.exists() and not out.is_relative_to(baseline)
    base_plan = p.REPO / BASE_PLAN
    assert p.digest(base_plan) == BASE_PLAN_SHA
    original_plan = p.read(base_plan)
    refs = set(original_plan['sources'].values()) | set(original_plan['gpu_sources'].values())
    refs.update([NEW, plan['candidate']['evidence_source'], original_plan['targeted_reference']['qualified_source']])
    for source in sorted(refs):
        assert re.fullmatch('[a-f0-9]{40}', source)
        present = subprocess.run(['git', 'cat-file', '-e', source + '^{commit}'], cwd=p.REPO,
                                 stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        if present.returncode:
            subprocess.run(['git', 'fetch', 'origin', source], cwd=p.REPO, check=True)
    with selected_plan(base_plan):
        original = p.verify_inputs(baseline)
        original_plan = p.read(base_plan)
    assert p.digest(baseline / 'provenance.json') == plan['baseline_inputs']['provenance_sha256']
    assert original['run_id'] == '43673961720' and original['sources']['successor'] == original_plan['sources']['successor']
    assert not p.git('diff', '--name-only', original_plan['sources']['successor'], OLD, '--', *PATHS, 'tests')
    for label in ('old', 'variants'):
        original_label = 'successor' if label == 'old' else label
        assert p.cpu_test_classes(plan, label) == p.cpu_test_classes(original_plan, original_label)
        assert p.original_gpu_source(plan, label) == p.original_gpu_source(original_plan, original_label)
    authenticated = candidate_inputs(candidate, plan)
    shutil.copytree(baseline, out)
    for suffix in ('.trx', '-build.log', '-tests.log', '-original-gpu-receipt.json', '-original-gpu-gpu.trx.gz'):
        shutil.copyfile(baseline / 'evidence' / ('successor' + suffix), out / 'evidence' / ('old' + suffix))
    shutil.copyfile(baseline / 'production/successor.dll', out / 'production/old.dll')
    for source, target in [('evidence/unit.trx', 'successor.trx'), ('evidence/parity.trx', 'successor-parity.trx')]:
        (out / 'evidence' / target).write_bytes(authenticated['read'](source))
    (out / 'evidence/successor-original-gpu-receipt.json').write_bytes(authenticated['gpu_receipt'])
    (out / 'evidence/successor-original-gpu-gpu.trx.gz').write_bytes(authenticated['gpu_trx'])
    (out / 'production/successor.dll').write_bytes(authenticated['read']('blobs/' + authenticated['production']))
    proof = copy.deepcopy(original)
    for key in ('checks', 'tested_runtime_dependencies', 'production_sha256'):
        proof[key]['old'] = copy.deepcopy(original[key]['successor'])
    proof['checks']['successor'] = dict(authenticated['manifest']['checks']['unit'], original_gpu_source=NEW)
    proof['tested_runtime_dependencies']['successor'] = authenticated['closure']
    proof['production_sha256']['successor'] = authenticated['production']
    proof.update(source_trees={label:p.git('rev-parse', plan['sources'][label]+':src') for label in ('old','successor','variants')}, sources=plan['sources'], plan_sha256=p.digest(p.PLAN), workflow_head=p.git('rev-parse', 'HEAD'),
                 run_id=os.environ['GITHUB_RUN_ID'], run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],
                 preparation_mode='authenticated-diagnostic', diagnostic_only=True,
                 evidence_manifest=p.manifest(out / 'evidence'), source_authentication=dict(
                     baseline=plan['baseline_inputs'], candidate=plan['candidate'], source_sha256=authenticated['fingerprint']))
    proof.pop('reused_from', None)
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256'] = record_inventory(out, plan, p.digest(p.PLAN))
    p.write(out / 'provenance.json', proof)
    p.verify_inputs(out)
    return proof


def collect(folder):
    from .verify import _collect
    result = _collect(Path(folder), 'diagnostic', preflight=False, diagnostic=True)
    assert not result['accepted'] and (result['case_count'], result['decision_count']) == (30, 6)
    result.update(diagnostic_only=True, qualification=False, production_integrated=False,
                  report_imported=False, policy_changed=False)
    return result
