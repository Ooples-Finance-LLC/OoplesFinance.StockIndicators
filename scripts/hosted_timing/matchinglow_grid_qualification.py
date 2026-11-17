"""Bounded MatchingLow qualification with authenticated, unchanged baseline bytes."""
import copy
import gzip
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

from . import pipeline as p
from . import candle_single_pass_roi as common

SCOPE = 'matchinglow-only-grid-20261116'
OLD = '0005efc1d9e09ec1d008414323290050c142fc3b'
NEW = 'a1f0028c601e1c6e7be5eabfe0d5da5cfe1f0758'
CANONICAL = '5be1a81d02ad01393f3fa8ede764e914252d0e96'
VARIANTS = '9a45372530e49718b01b6524f9a0db993514e1e8'
QUALIFIED = '67c1866712e5d1bec8f2cbcaca42a6641b03c19c'
BASE_PLAN = common.BASE_PLAN
BASE_PLAN_SHA = common.BASE_PLAN_SHA
BASE_PROVENANCE = '92977e9786050146dbe97748c8a28d59e8eb47be5f4b5f85ae064ec4fcacef3a'
CANONICAL_DLL = 'c63770e43fceda43a2286cb5272643ac3850adcc23962cc01eee2c7a9032c6fe'
HARNESS_DLL = '48229494a1ff1e4e59bddd5a018cbd590cae24523d52dd2f3effdd20bd802723'
HARNESS_ASSEMBLY = 'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'
FROZEN_PRODUCTION = 'a1825bc99a35b53573fde9d7baa92b3216c8fd44553e78ec5dac684682d74cc5'
COMMON_MANIFEST_SHA = '8a826c564b8e6c5f1e4ada6c95166cc626c8b8407216ba5db299dbb13c4e3c21'
PAIRS = ['TaLib.Candles.MatchingLow', 'TaLib.Candles.SeparatingLines', 'TaLib.Candles.StickSandwich']
UNIT = ['OoplesFinance.StockIndicators.Tests.Unit.ModelsTests.'+name for name in
        ('MatchingLowGridTests', 'SequenceParallelExecutionTests', 'MatchedLinesParallelExecutionTests')]
PARITY = ['OoplesFinance.StockIndicators.CompetitorTests.'+name for name in
          ('SequenceCandleTests', 'MatchedLinesTests')]
PATHS = {'src/Indicators/ValuesBarExecution.BoundedWindows.cs',
         'src/Indicators/ValuesBarExecution.MatchingLowGrid.cs', 'tests/ModelsTests/MatchingLowGridTests.cs'}
METHODS = ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']
SOURCE_IDENTITY_PATHS = ['src', 'tests', p.BENCH, p.TESTS,
    'scripts/run-competitor-contracts.py', 'scripts/verify-competitor-local-gpu.py',
    'scripts/competitor-gpu-methods.json', 'Directory.Build.props', 'Directory.Build.targets',
    'Directory.Packages.props', 'global.json', 'NuGet.Config', 'nuget.config']
DECISION = 'benchmarks/results/remaining-indicators/hosted-targeted-43673961720-attempt1/decision.json'
# Exact blob identities are frozen in the reviewed plan, not inferred as equivalence.
CONTINUATION_KEYS = {'qualified', 'baseline', 'harness_changes', 'selected_fixtures'}


def blob(source, name):
    return subprocess.check_output(['git', 'show', source+':'+name], cwd=p.REPO)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def optional_blob_sha(source, name):
    return sha(blob(source, name)) if p.git('ls-tree', source, '--', name) else None


def tasks():
    def task(fixture, pairs, methods, cases, decisions):
        return dict(fixture=fixture, operations=[dict(raw=x, pair=x) for x in pairs],
                    methods=methods, implicit_pair=False, cases=cases, decisions=decisions)
    return [task('LibraryPairBenchmarks', PAIRS, ['Ooples', 'Competitor'], 30, 6),
            task('SharedSequenceCandleBenchmarks', [PAIRS[0], PAIRS[2]], METHODS, 40, 8),
            task('SharedMatchedLinesBenchmarks', [PAIRS[1]], METHODS, 20, 4)]


def validate_scope(plan, *, require_ready=True):
    assert plan.get('matchinglow_grid_qualification') is True
    assert plan['sources'] == dict(published=OLD, old=OLD, successor=NEW, variants=VARIANTS)
    assert plan['gpu_sources'] == dict(old=CANONICAL, successor=NEW, variants=VARIANTS)
    assert (plan['cases'], plan['decisions']) == (90, 18)
    assert plan['matrix'] == dict(shards=1, max_parallel=1, timeout_minutes_per_shard=120)
    assert plan['measurement_policy'] == common.POLICY and plan['runtime_policy_overrides'] == {}
    assert plan['proposed_dotnet_sdk'] == '10.0.403'
    assert plan['expected_runtime_version'] == '.NET 10.0.15'
    assert plan['runtime_dependency_alignment'] == dict(tensor_version='0.150.0')
    assert plan['require_fixture_inventory']
    assert sha(json.dumps(plan['baseline_common_manifest'], sort_keys=True,
        separators=(',', ':')).encode()) == COMMON_MANIFEST_SHA
    assert plan['targeted_shards'] == [dict(index=0, tasks=tasks(), cases=90, decisions=18)]
    assert plan['targeted_reference']['scope'] == SCOPE
    assert plan['targeted_reference']['qualified_source'] == QUALIFIED
    assert plan['targeted_reference']['decision'] == DECISION
    assert str(plan['targeted_reference']['run_id']) == '43673961720'
    assert plan['baseline_inputs'] == dict(run_id='43880487485', plan=BASE_PLAN,
        plan_sha256=BASE_PLAN_SHA, provenance_sha256=BASE_PROVENANCE)
    assert not any(plan.get(k) for k in ('diagnostic_only', 'reuse_inputs', 'diagnostic_kind',
        'qualify_variants', 'standalone_variants', 'coverage_reference', 'trix_preflight'))
    c = plan['candidate']
    assert c['source'] == NEW and c['unit_classes'] == UNIT and c['parity_classes'] == PARITY
    assert plan['native_crossover_gate'] == dict(pair=PAIRS[0], bars=10000,
        fixture='LibraryPairBenchmarks', method='Ooples', native_method='Competitor',
        minimum_margin=0.10, candidate_rounds=[1, 2], native_round=2,
        require_baseline_point_loss_both_rounds=True)
    approval = plan['scope_approval']
    assert p.digest(p.REPO/approval['path']) == approval['sha256']
    approved = p.read(p.REPO/approval['path'])
    assert approved['approved'] and approved['scope'] == SCOPE and approved['cases'] == 90
    if require_ready:
        assert plan['inputs_ready'] is True, 'Candidate CI/GPU/preflight identities not frozen'
        assert re.fullmatch(r'\d+', c['ci_run'])
        for key in ('producer', 'evidence_source'):
            assert re.fullmatch('[a-f0-9]{40}', c[key])
        for key in ('manifest_sha256', 'gpu_receipt_sha256', 'production_sha256'):
            assert re.fullmatch('[a-f0-9]{64}', c[key])
        assert all(type(c[k]) is int and c[k] > 0 for k in ('unit_passed', 'parity_passed'))
        assert c['unit_passed'] == 30
        assert plan['preflight']['files'] and plan['preflight']['receipt'] in plan['preflight']['files']
        assert all(re.fullmatch('[a-f0-9]{64}', x) for x in plan['preflight']['files'].values())


def verify_continuation(reference, baseline):
    """Only this scope may authenticate the recorded harness-only continuation."""
    plan = p.read(p.PLAN)
    validate_scope(plan)
    assert reference == plan['targeted_reference'] and baseline == OLD
    proof = plan['baseline_continuation']
    assert set(proof) == CONTINUATION_KEYS and proof['qualified'] == QUALIFIED and proof['baseline'] == OLD
    assert not p.git('diff', '--name-only', QUALIFIED, OLD, '--', 'src', 'tests')
    changes = p.git('diff', '--name-only', QUALIFIED, OLD, '--', p.BENCH, p.TESTS).splitlines()
    assert len(changes) == 12 and set(changes) == set(proof['harness_changes'])
    for name, hashes in proof['harness_changes'].items():
        assert hashes == dict(before=optional_blob_sha(QUALIFIED, name), after=optional_blob_sha(OLD, name))
    assert len(proof['selected_fixtures']) >= 6
    for name, hashes in proof['selected_fixtures'].items():
        assert name.startswith(p.BENCH+'/')
        assert hashes == dict(current=sha(blob(OLD, name)), frozen=sha(blob(VARIANTS, name)))
    assert not p.git('diff', '--name-only', CANONICAL, OLD, '--', *SOURCE_IDENTITY_PATHS)
    assert set(p.git('diff', '--name-only', OLD, NEW, '--', 'src', 'tests').splitlines()) == PATHS
    assert not p.git('diff', '--name-only', OLD, NEW, '--', p.BENCH, p.TESTS)


def candidate_inputs(folder, plan):
    """Existing canonical CI layout/GPU validators, with this source's producer pinned."""
    c = plan['candidate']; folder = Path(folder).resolve()
    def read(name):
        path = (folder/name).resolve()
        assert path.is_relative_to(folder) and path.is_file()
        return path.read_bytes()
    spec = importlib.util.spec_from_file_location('matchinglow_ci', p.REPO/'scripts/build-candidate-correctness.py')
    ci = importlib.util.module_from_spec(spec); spec.loader.exec_module(ci)
    fingerprint = common.source_fingerprint(NEW)
    assert sha(read('manifest.json')) == c['manifest_sha256']
    manifest = json.loads(read('manifest.json'))
    assert (manifest['source'], manifest['run_id'], manifest['workflow_head']) == (NEW, c['ci_run'], c['producer'])
    assert manifest['classes'] == dict(unit=UNIT, parity=PARITY)
    production = ci.validate_layout(manifest, read, NEW, fingerprint)
    assert production == c['production_sha256']
    for label, classes in [('unit', UNIT), ('parity', PARITY)]:
        with tempfile.TemporaryDirectory(prefix='matchinglow-trx-') as temp:
            path = Path(temp)/'checks.trx'; path.write_bytes(read('evidence/'+label+'.trx'))
            checks = p.verify_trx(path, classes)
        assert checks == manifest['checks'][label] and checks['passed'] == c[label+'_passed']
    assert not p.git('diff', '--name-only', NEW, c['evidence_source'], '--', *SOURCE_IDENTITY_PATHS)
    base = 'benchmarks/results/shared-rollout/local-gpu-contracts/'
    receipt_raw, trx_raw = (blob(c['evidence_source'], base+n) for n in ('receipt.json', 'gpu.trx.gz'))
    assert sha(receipt_raw) == c['gpu_receipt_sha256']
    receipt = json.loads(receipt_raw)
    gpu = ci.gpu_module(p.REPO)
    assert gpu.verify_evidence(receipt, gzip.decompress(trx_raw), json.loads(blob(NEW,
        'scripts/competitor-gpu-methods.json')), fingerprint) == 100
    assert receipt['build_origin']['source'] == NEW and receipt['build_origin']['run_id'] == c['ci_run']
    assert receipt['build_origin']['workflow_head'] == c['producer']
    assert receipt['test_assembly_sha256'] == manifest['runtime_files'][ci.ROOTS[-1]+'/OoplesFinance.StockIndicators.CompetitorTests.dll']
    from .runtime_dependencies import tensor_closure
    with tempfile.TemporaryDirectory(prefix='matchinglow-closure-') as temp:
        runtime = Path(temp); prefix = ci.ROOTS[1]+'/'
        for name, digest in manifest['runtime_files'].items():
            if name.startswith(prefix):
                path = (runtime/name[len(prefix):]).resolve(); assert path.is_relative_to(runtime.resolve())
                path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(read('blobs/'+digest))
        closure = tensor_closure(runtime, 'OoplesFinance.StockIndicators.Tests.Unit.deps.json')
    return dict(manifest=manifest, read=read, production=production, closure=closure,
                gpu_receipt=receipt_raw, gpu_trx=trx_raw, fingerprint=fingerprint)


def verify_preflight(folder, plan, production):
    folder = Path(folder).resolve(); frozen = plan['preflight']
    assert p.manifest(folder) == frozen['files'], 'Exact preflight file set required'
    receipt = p.read(folder/frozen['receipt'])
    assert receipt['baseline_source'] == CANONICAL and receipt['candidate_source'] == NEW
    assert receipt['baseline_production_sha256'] == CANONICAL_DLL
    assert receipt['candidate_production_sha256'] == production
    assert receipt['frozen_harness_source'] == VARIANTS and receipt['frozen_harness_sha256'] == HARNESS_DLL
    assert receipt['comparison_calls'] == [dict(pair=pair, bars=bars, passed=True)
        for pair in PAIRS for bars in (1000, 10000)]
    assert receipt['jit_gate_passed'] is True and receipt['worker_ownership_unchanged'] is True
    assert receipt['separating_hooks_eliminated'] == ['FillBoundedWindow<MatchedLinesKernel>',
                                                   'ComputeBoundedRegion<MatchedLinesKernel>']
    assert receipt['diagnostic_settings_used_for_timing'] is False
    return receipt


def verify_authentication(folder, plan, proof):
    validate_scope(plan)
    assert proof['preparation_mode'] == 'authenticated-matchinglow-qualification'
    assert proof['source_authentication'] == dict(baseline=plan['baseline_inputs'], candidate=plan['candidate'],
        continuation=plan['baseline_continuation'], preflight=plan['preflight'])
    original_path = folder/'baseline-authentication/provenance.json'
    assert p.digest(original_path) == BASE_PROVENANCE
    original = p.read(original_path); base_plan = p.read(p.REPO/BASE_PLAN)
    assert p.digest(p.REPO/BASE_PLAN) == BASE_PLAN_SHA
    from .named_projection_refresh import verify_authentication as verify_original
    verify_original(folder/'baseline-authentication', base_plan, original)
    assert proof['common_manifest'] == original['common_manifest'] == plan['baseline_common_manifest']
    assert p.digest(folder/'common'/HARNESS_ASSEMBLY) == HARNESS_DLL
    assert proof['production_sha256']['old'] == CANONICAL_DLL
    assert proof['production_sha256']['variants'] == original['production_sha256']['variants']
    for label, source in [('old', 'successor'), ('variants', 'variants')]:
        assert proof['checks'][label] == original['checks'][source]
        assert proof['tested_runtime_dependencies'][label] == original['tested_runtime_dependencies'][source]
        for suffix in ('.trx', '-original-gpu-receipt.json', '-original-gpu-gpu.trx.gz'):
            assert (folder/'evidence'/(label+suffix)).read_bytes() == (folder/'baseline-authentication/evidence'/(source+suffix)).read_bytes()
    candidate = candidate_inputs(folder/'candidate-authentication', plan)
    assert candidate['production'] == proof['production_sha256']['successor']
    assert proof['checks']['successor'] == dict(candidate['manifest']['checks']['unit'], original_gpu_source=NEW)
    assert proof['tested_runtime_dependencies']['successor'] == candidate['closure']
    for name, raw in [('successor.trx', candidate['read']('evidence/unit.trx')),
                      ('successor-parity.trx', candidate['read']('evidence/parity.trx')),
                      ('successor-original-gpu-receipt.json', candidate['gpu_receipt']),
                      ('successor-original-gpu-gpu.trx.gz', candidate['gpu_trx'])]:
        assert (folder/'evidence'/name).read_bytes() == raw
    verify_preflight(folder/'consumer-preflight', plan, candidate['production'])


def prepare(baseline, candidate, preflight, out):
    plan = p.read(p.PLAN); validate_scope(plan)
    assert subprocess.check_output(['dotnet', '--version'], text=True).strip() == plan['proposed_dotnet_sdk']
    dotnet_info = subprocess.check_output(['dotnet', '--info'], text=True)
    baseline, candidate, preflight, out = map(lambda x: Path(x).resolve(), (baseline, candidate, preflight, out))
    assert not out.exists() and all(not out.is_relative_to(x) for x in (baseline, candidate, preflight))
    with common.selected_plan(p.REPO/BASE_PLAN):
        original = p.verify_inputs(baseline)
    assert p.digest(baseline/'provenance.json') == BASE_PROVENANCE
    before = dict(common=p.manifest(baseline/'common'), production=p.manifest(baseline/'production'))
    authenticated = candidate_inputs(candidate, plan)
    verify_preflight(preflight, plan, authenticated['production'])
    # Fresh copies only: never overwrite the protected cache or a baseline hardlink.
    shutil.copytree(baseline, out)
    auth = out/'baseline-authentication'; auth.mkdir()
    shutil.copytree(baseline/'evidence', auth/'evidence')
    (auth/'common').mkdir(); shutil.copyfile(baseline/'common'/HARNESS_ASSEMBLY, auth/'common'/HARNESS_ASSEMBLY)
    shutil.copyfile(baseline/'provenance.json', auth/'provenance.json')
    shutil.copytree(candidate, out/'candidate-authentication')
    shutil.copytree(preflight, out/'consumer-preflight')
    for suffix in ('.trx', '-build.log', '-tests.log', '-original-gpu-receipt.json', '-original-gpu-gpu.trx.gz'):
        shutil.copyfile(baseline/'evidence'/('successor'+suffix), out/'evidence'/('old'+suffix))
    shutil.copyfile(baseline/'production/successor.dll', out/'production/old.dll')
    for name, raw in [('successor.trx', authenticated['read']('evidence/unit.trx')),
                      ('successor-parity.trx', authenticated['read']('evidence/parity.trx')),
                      ('successor-original-gpu-receipt.json', authenticated['gpu_receipt']),
                      ('successor-original-gpu-gpu.trx.gz', authenticated['gpu_trx'])]:
        (out/'evidence'/name).write_bytes(raw)
    (out/'production/successor.dll').write_bytes(authenticated['read']('blobs/'+authenticated['production']))
    proof = copy.deepcopy(original)
    for key in ('checks', 'tested_runtime_dependencies', 'production_sha256'):
        proof[key]['old'] = copy.deepcopy(original[key]['successor'])
    proof['checks']['successor'] = dict(authenticated['manifest']['checks']['unit'], original_gpu_source=NEW)
    proof['tested_runtime_dependencies']['successor'] = authenticated['closure']
    proof['production_sha256']['successor'] = authenticated['production']
    proof.update(sources=plan['sources'], dotnet_info=dotnet_info,
        source_trees={label:p.git('rev-parse', plan['sources'][label]+':src')
        for label in ('old', 'successor', 'variants')}, plan_sha256=p.digest(p.PLAN),
        workflow_head=p.git('rev-parse', 'HEAD'), run_id=os.environ['GITHUB_RUN_ID'],
        run_attempt=os.environ['GITHUB_RUN_ATTEMPT'], preparation_mode='authenticated-matchinglow-qualification',
        evidence_manifest=p.manifest(out/'evidence'), source_authentication=dict(
            baseline=plan['baseline_inputs'], candidate=plan['candidate'],
            continuation=plan['baseline_continuation'], preflight=plan['preflight']))
    proof.pop('reused_from', None)
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256'] = record_inventory(out, plan, p.digest(p.PLAN))
    p.write(out/'provenance.json', proof)
    assert before == dict(common=p.manifest(baseline/'common'), production=p.manifest(baseline/'production'))
    p.verify_inputs(out)
    return proof


def native_crossover(rounds):
    """Both candidate rounds must newly beat the same measured native control."""
    assert set(rounds) == {0, 1, 2, 3}
    native = rounds[2]['Competitor']
    old = [rounds[i]['Ooples'] for i in (0, 3)]
    new = [rounds[i]['Ooples'] for i in (1, 2)]
    return (all(row['mean_ns'] >= native['mean_ns'] for row in old)
        and all(row['upper_ns'] < native['lower_ns'] and row['mean_ns'] <= 0.90*native['mean_ns'] for row in new))


def verify_runtime_version(actual, expected):
    assert actual == expected or actual.startswith(expected+' '), (actual, expected)


def _verify_runtime(folder, expected):
    from .verify import archived
    for root in sorted(path.parent for path in Path(folder).glob('*/provenance.json')):
        proof = p.read(root/'provenance.json')
        for record in p.read(root/'status.json')['rounds']:
            # Check every original operation report as well as each merged report;
            # merged HostEnvironmentInfo alone retains only its first operation.
            paths = [record['report']] + [row['report'] for row in record['operation_checkpoints']]
            for path in paths:
                report = p.read(archived(root, proof['raw_root'], path))
                verify_runtime_version(report['HostEnvironmentInfo']['RuntimeVersion'], expected)


def collect(folder, inputs=None):
    """Standard full collector first; the additional crossover can only reject."""
    plan = p.read(p.PLAN); validate_scope(plan)
    if inputs is not None:
        p.verify_inputs(Path(inputs))
    from .verify import _collect, archived, sample
    from .checkpoints import case_key
    folder = Path(folder)
    _verify_runtime(folder, plan['expected_runtime_version'])
    result = _collect(folder, 'targeted', preflight=False, matchinglow_gate=True)
    if inputs is not None:
        assert result['input_provenance_sha256'] == p.digest(Path(inputs)/'provenance.json')
    assert (result['case_count'], result['decision_count']) == (90, 18)
    roots = list(folder.rglob('status.json'))
    roots = [x.parent for x in roots if (x.parent/'provenance.json').is_file()]
    assert len(roots) == 1
    root = roots[0]; proof = p.read(root/'provenance.json'); state = p.read(root/'status.json')
    embedded = proof['input_provenance']
    assert embedded['preparation_mode'] == 'authenticated-matchinglow-qualification'
    assert embedded['source_authentication'] == dict(baseline=plan['baseline_inputs'], candidate=plan['candidate'],
        continuation=plan['baseline_continuation'], preflight=plan['preflight'])
    assert embedded['common_manifest'] == plan['baseline_common_manifest']
    assert embedded['production_sha256'] == dict(old=CANONICAL_DLL,
        successor=plan['candidate']['production_sha256'], variants=FROZEN_PRODUCTION)
    rounds = {}
    for record in state['rounds']:
        if record['fixture'] != 'LibraryPairBenchmarks':
            continue
        report = p.read(archived(root, proof['raw_root'], record['report']))
        rows = {}
        for case in report['Benchmarks']:
            pair, bars, method = case_key(case)
            if pair == PAIRS[0] and bars == 10000:
                assert method not in rows; rows[method] = sample(case)
        assert set(rows) == ({'Ooples', 'Competitor'} if record['index'] == 2 else {'Ooples'})
        rounds[record['index']] = rows
    gate = native_crossover(rounds)
    result.update(native_crossover_gate_passed=gate, native_crossover_samples=rounds,
        accepted=result['accepted'] and gate, production_integrated=False, report_imported=False)
    return result
