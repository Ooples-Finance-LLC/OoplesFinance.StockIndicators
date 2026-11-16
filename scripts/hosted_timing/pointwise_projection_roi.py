"""Bounded same-production, aligned pointwise-harness diagnostic."""
import copy
import os
from pathlib import Path
import shutil
import subprocess

from . import pipeline as p
from .candle_family_roi import BASE_PLAN, BASE_PLAN_SHA, selected_plan

PUBLISHED = 'ce02581b1f55400fd674b5496ee659fa0e8654fa'
VARIANTS = 'ce994435f670a1c2c8d5424e167316914cfe9b51'
HARNESS = '01f83663a8e7580a2941cc8bf2b0e92c1dd4a296'
ADAPTER = 'a9de5f4f233e55d1671939f3ecc18b60dede516c'
ASSEMBLY = 'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'
PAIRS = ['TaLib.Functions.Add', 'TaLib.Functions.Floor', 'TaLib.Functions.Sin']
CLASSES = ['PointwiseComparisonPublisherTests', 'ElementaryMathComparisonTests',
           'CircularComparisonTests', 'TranscendentalComparisonTests']
POLICY = dict(order=['old', 'new', 'new', 'old'], quiet_samples=12, max_cpu_percent=15,
              warmups=10, iterations=10, fixed_invocation_warmups=1000)
CHANGES = {p.BENCH+'/LibraryComparisons/'+name+'.cs' for name in
           ('CircularComparison', 'ElementaryMathComparison', 'TranscendentalComparison', 'PointwiseComparisonPublisher')}
CHANGES.add(p.TESTS+'/PointwiseComparisonPublisherTests.cs')
APPROVAL = 'benchmarks/plans/evidence/pointwise-projection-pilot-scope-20261116.json'
PREPARED = dict(run_id='43790197468', workflow_head='12a4c84373e095d104936ca465adfe5dbd2b3abe',
    plan='benchmarks/plans/evidence/pointwise-projection-verified-preparation-plan-20261116.json',
    plan_sha256='ce6408b8b933a476da13cfab3c9860b3945c13d70868dcb912cba89b3a28e509',
    provenance_sha256='72a71a6fcc069f9447e071a10ed6dfecc5593c746cfebda67ef27cf3007110ee')


def validate_scope(plan):
    approval = p.read(p.REPO/APPROVAL)
    assert approval['approved'] and approval['diagnostic_only'] and not approval['qualification']
    assert plan['roi_approval'] == dict(path=APPROVAL, sha256=p.digest(p.REPO/APPROVAL))
    assert plan['diagnostic_kind'] == 'pointwise-projection-native-pilot' and plan['diagnostic_only']
    assert plan['sources'] == dict(published=PUBLISHED, old=PUBLISHED, successor=PUBLISHED, variants=VARIANTS)
    assert plan['measurement_policy'] == POLICY and plan['runtime_policy_overrides'] == {}
    assert plan['proposed_dotnet_sdk'] == '10.0.403'
    assert plan['runtime_dependency_alignment'] == dict(tensor_version='0.150.0')
    assert plan['harness_source'] == HARNESS and plan['adapter_source'] == ADAPTER
    assert plan['require_fixture_inventory']
    assert (plan['cases'], plan['decisions'], plan['matrix']['shards'], plan['matrix']['max_parallel']) == (30, 6, 1, 1)
    task = dict(fixture='LibraryPairBenchmarks', operations=[dict(raw=x, pair=x) for x in PAIRS],
                methods=['Ooples', 'Competitor'], implicit_pair=False, cases=30, decisions=6)
    assert plan['diagnostic_shards'] == plan['targeted_shards'] == [dict(index=0, tasks=[task], cases=30, decisions=6)]
    assert plan['baseline_inputs']['run_id'] == '43673961720'
    assert plan['baseline_inputs']['plan'] == BASE_PLAN and plan['baseline_inputs']['plan_sha256'] == BASE_PLAN_SHA
    assert not any(plan.get(k) for k in ('targeted_reference', 'coverage_reference', 'reuse_inputs', 'qualify_variants', 'trix_preflight'))
    if plan.get('prepared_harness_reuse'):
        assert plan['prepared_harness_reuse'] == PREPARED


def verify_runtime_identity(plan, manifests):
    validate_scope(plan)
    assert plan['sources']['old'] == plan['sources']['successor'] == PUBLISHED
    assert manifests['old'] == manifests['new'], 'This diagnostic requires byte-identical production and harness under both labels'


def publisher_checks(path):
    checks = p.verify_trx(path, CLASSES)
    assert checks['classes']['PointwiseComparisonPublisherTests'] == 22
    return checks


def verify_authentication(folder, plan, proof):
    validate_scope(plan)
    original = p.read(folder/'evidence/projection-original-preparation.json')
    assert p.digest(folder/'evidence/projection-original-preparation.json') == plan['baseline_inputs']['provenance_sha256']
    assert original['run_id'] == '43673961720'
    assert proof['harness_authentication']['source'] == HARNESS
    assert proof['production_sha256']['old'] == proof['production_sha256']['successor'] == original['production_sha256']['successor']
    assert proof['production_sha256']['variants'] == original['production_sha256']['variants']
    assert proof['harness_authentication']['production_rebuilt'] is False
    assert proof['harness_authentication']['test_classes'] == CLASSES
    assert proof['harness_authentication']['tested_production_sha256'] == original['production_sha256']['successor']
    assert p.digest(folder/'evidence/projection-test-runtime/OoplesFinance.StockIndicators.CompetitorTests.dll') == proof['harness_authentication']['test_assembly_sha256']
    actual = publisher_checks(folder/'evidence/projection-tests.trx')
    assert actual == proof['harness_authentication']['checks']
    assert p.digest(folder/'common'/ASSEMBLY) == proof['harness_authentication']['assembly_sha256']
    for label, original_label in [('old', 'successor'), ('successor', 'successor'), ('variants', 'variants')]:
        assert proof['checks'][label] == original['checks'][original_label]
        for suffix in ('-original-gpu-receipt.json', '-original-gpu-gpu.trx.gz'):
            assert p.digest(folder/'evidence'/(label+suffix)) == original['evidence_manifest'][original_label+suffix]
    assert proof['checks']['variant-harness']['passed'] == 13
    if plan.get('prepared_harness_reuse'):
        assert proof['harness_input_reuse'] == PREPARED
        path=folder/'evidence/projection-verified-preparation.json'
        assert p.digest(path) == PREPARED['provenance_sha256']
        previous=p.read(path)
        assert (previous['run_id'],previous['workflow_head'],previous['plan_sha256']) == (
            PREPARED['run_id'],PREPARED['workflow_head'],PREPARED['plan_sha256'])
        for key in ('sources','sdk','checks','common_manifest','production_sha256','harness_authentication','tested_runtime_dependencies'):
            assert proof[key] == previous[key], key
        for name,sha in previous['evidence_manifest'].items():
            assert p.digest(folder/'evidence'/name) == sha
        assert p.digest(folder/'evidence/projection-original-fixture-inventory.json') == previous['fixture_inventory_sha256']
    else:
        assert not proof.get('harness_input_reuse')


def rebind(source, out):
    plan=p.read(p.PLAN);validate_scope(plan)
    assert plan['prepared_harness_reuse'] == PREPARED
    source,out=Path(source).resolve(),Path(out).resolve()
    assert not out.exists() and not out.is_relative_to(source)
    assert p.digest(p.REPO/PREPARED['plan']) == PREPARED['plan_sha256']
    assert p.digest(source/'provenance.json') == PREPARED['provenance_sha256']
    with selected_plan(p.REPO/PREPARED['plan']):
        previous=p.verify_inputs(source)
        original_plan=p.read(p.REPO/PREPARED['plan'])
    assert original_plan['sources'] == plan['sources']
    assert original_plan['measurement_policy'] == plan['measurement_policy']
    assert previous['run_id'] == PREPARED['run_id'] and previous['workflow_head'] == PREPARED['workflow_head']
    shutil.copytree(source,out)
    shutil.copy2(source/'provenance.json',out/'evidence/projection-verified-preparation.json')
    shutil.copy2(source/'fixture-method-inventory.json',out/'evidence/projection-original-fixture-inventory.json')
    proof=copy.deepcopy(previous)
    proof.update(plan_sha256=p.digest(p.PLAN),workflow_head=p.git('rev-parse','HEAD'),
        run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],
        preparation_mode='authenticated-projection-harness-reused',harness_input_reuse=PREPARED,
        evidence_manifest=p.manifest(out/'evidence'))
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256']=record_inventory(out,plan,p.digest(p.PLAN))
    p.write(out/'provenance.json',proof)
    p.verify_inputs(out)
    return proof


def prepare(baseline, source, out):
    plan = p.read(p.PLAN); validate_scope(plan)
    baseline, source, out = map(lambda x: Path(x).resolve(), (baseline, source, out))
    assert not out.exists() and not out.is_relative_to(baseline)
    assert p.digest(p.REPO/BASE_PLAN) == BASE_PLAN_SHA
    with selected_plan(p.REPO/BASE_PLAN):
        original = p.verify_inputs(baseline)
        original_plan = p.read(p.REPO/BASE_PLAN)
    assert p.digest(baseline/'provenance.json') == plan['baseline_inputs']['provenance_sha256']
    assert p.git('rev-parse', 'HEAD', cwd=source) == HARNESS
    assert not p.git('status', '--porcelain', cwd=source)
    assert set(p.git('diff', '--name-only', VARIANTS, HARNESS).splitlines()) == CHANGES
    assert set(p.git('diff', '--name-only', PUBLISHED, plan['adapter_source']).splitlines()) == CHANGES
    for name in CHANGES:
        assert p.git('rev-parse', HARNESS+':'+name) == p.git('rev-parse', plan['adapter_source']+':'+name)
    # Both timing labels run the already GPU-checked published production DLL.
    assert not p.git('diff', '--name-only', original_plan['sources']['successor'], PUBLISHED, '--', 'src', 'tests')
    assert plan['gpu_sources'] == {label:p.original_gpu_source(original_plan,old) for label, old in
                                  [('old','successor'), ('successor','successor'), ('variants','variants')]}
    assert plan['cpu_test_classes'] == {label:p.cpu_test_classes(original_plan,old) for label, old in
                                       [('old','successor'), ('successor','successor'), ('variants','variants')]}
    shutil.copytree(baseline, out)
    shutil.copy2(baseline/'provenance.json', out/'evidence/projection-original-preparation.json')
    variant = baseline/'production/variants.dll'
    for relative in ('src/bin/Release/net10.0', 'src/obj/Release/net10.0/ref'):
        dest = source/relative; dest.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(variant, dest/p.DLL)
    bench = source/p.BENCH/'bin/Release/net10.0'
    tests = source/p.TESTS/'bin/Release/net10.0'
    shutil.copytree(baseline/'common', bench, dirs_exist_ok=True)
    env = dict(os.environ, UseSharedCompilation='false', MSBUILDDISABLENODEREUSE='1')
    def run(name, args):
        with (out/'evidence'/name).open('wb') as log:
            subprocess.run(args, cwd=source, env=env, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=1200)
    for name, project in [('projection-benchmark-build.log',p.BENCH), ('projection-test-build.log',p.TESTS)]:
        run(name, ['dotnet','build',project,'-c','Release','-p:GeneratePackageOnBuild=false',
                   '-p:BuildProjectReferences=false','-m:2'])
    assert p.digest(source/'src/bin/Release/net10.0'/p.DLL) == p.digest(variant)
    assert p.digest(source/'src/obj/Release/net10.0/ref'/p.DLL) == p.digest(variant)
    assert not p.git('status', '--porcelain', cwd=source)
    # The new adapter suite runs against the exact published production binary.
    shutil.copyfile(baseline/'production/successor.dll', tests/p.DLL)
    run('projection-tests.log', ['dotnet','vstest',str(tests/'OoplesFinance.StockIndicators.CompetitorTests.dll'),
        '/TestCaseFilter:'+'|'.join('FullyQualifiedName~'+c for c in CLASSES),
        '/Logger:trx;LogFileName=projection-tests.trx','/ResultsDirectory:'+str(out/'evidence')])
    checks = publisher_checks(out/'evidence/projection-tests.trx')
    assert p.digest(tests/p.DLL) == original['production_sha256']['successor']
    retained = out/'evidence/projection-test-runtime'; retained.mkdir()
    for suffix in ('.dll','.pdb','.deps.json','.runtimeconfig.json'):
        name = 'OoplesFinance.StockIndicators.CompetitorTests'+suffix
        shutil.copyfile(tests/name,retained/name)
    shutil.copyfile(variant, tests/p.DLL)
    run('projection-variant-tests.log', ['dotnet','vstest',str(tests/'OoplesFinance.StockIndicators.CompetitorTests.dll'),
        '/TestCaseFilter:FullyQualifiedName~SharedClassicalWindowVariantBenchmarkTests',
        '/Logger:trx;LogFileName=variant-harness.trx','/ResultsDirectory:'+str(out/'evidence')])
    variant_checks = p.verify_trx(out/'evidence/variant-harness.trx', ['SharedClassicalWindowVariantBenchmarkTests'])
    assert variant_checks['passed'] == 13
    from .runtime_dependencies import tensor_closure, verify_alignment
    common_closure = tensor_closure(baseline/'common','OoplesFinance.StockIndicators.CompetitorBenchmarks.deps.json')
    for directory, deps in [(bench,'OoplesFinance.StockIndicators.CompetitorBenchmarks.deps.json'),
                            (tests,'OoplesFinance.StockIndicators.CompetitorTests.deps.json')]:
        verify_alignment(common_closure, tensor_closure(directory,deps), '0.150.0')
    # Keep the frozen dependency/configuration files and replace only the rebuilt assembly/PDB.
    for name in (ASSEMBLY, 'OoplesFinance.StockIndicators.CompetitorBenchmarks.pdb'):
        shutil.copyfile(bench/name, out/'common'/name)
    for name in ('OoplesFinance.StockIndicators.CompetitorBenchmarks.deps.json',
                 'OoplesFinance.StockIndicators.CompetitorBenchmarks.runtimeconfig.json'):
        assert (bench/name).read_bytes() == (baseline/'common'/name).read_bytes(), name
    for label in ('old','successor'):
        shutil.copyfile(baseline/'production/successor.dll', out/'production'/(label+'.dll'))
        for suffix in ('.trx','-build.log','-tests.log','-original-gpu-receipt.json','-original-gpu-gpu.trx.gz'):
            shutil.copyfile(baseline/'evidence'/('successor'+suffix), out/'evidence'/(label+suffix))
    proof = copy.deepcopy(original)
    for label in ('old','successor'):
        for key in ('checks','tested_runtime_dependencies','production_sha256'):
            proof[key][label] = copy.deepcopy(original[key]['successor'])
    proof['checks']['variant-harness'] = variant_checks
    proof.update(plan_sha256=p.digest(p.PLAN), sources=plan['sources'],
        source_trees={label:p.git('rev-parse',plan['sources'][label]+':src') for label in ('old','successor','variants')},
        common_manifest=p.manifest(out/'common'), evidence_manifest=p.manifest(out/'evidence'),
        workflow_head=p.git('rev-parse','HEAD'), run_id=os.environ['GITHUB_RUN_ID'], run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],
        diagnostic_only=True, preparation_mode='authenticated-projection-harness',
        harness_authentication=dict(source=HARNESS, assembly_sha256=p.digest(out/'common'/ASSEMBLY),
            production_rebuilt=False, test_classes=CLASSES, checks=checks,
            tested_production_sha256=original['production_sha256']['successor'],
            test_assembly_sha256=p.digest(retained/'OoplesFinance.StockIndicators.CompetitorTests.dll')))
    proof.pop('reused_from', None)
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256'] = record_inventory(out,plan,p.digest(p.PLAN))
    p.write(out/'provenance.json',proof)
    p.verify_inputs(out)
    verify_authentication(out,plan,proof)
    return proof


def collect(folder):
    from .verify import _collect
    result = _collect(Path(folder),'diagnostic',preflight=False,diagnostic=True)
    assert not result['accepted'] and (result['case_count'],result['decision_count']) == (30,6)
    result.update(diagnostic_only=True, qualification=False, production_integrated=False,
                  report_imported=False, policy_changed=False, production_optimization=False,
                  same_production_under_both_labels=True)
    return result
