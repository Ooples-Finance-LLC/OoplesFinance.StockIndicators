"""Eight-pair harness refresh using two fresh authenticated CI builds.

Both measurement labels use the same canonical production and frozen harness.
This path never qualifies production source, regardless of A/A verdicts.
"""
import copy
import os
from pathlib import Path
import re
import subprocess

from . import pipeline as p, candle_family_roi as common

PUBLISHED = 'bde045026cb0a490dfdbd2f73a727547dc48cf29'
CANONICAL = '5be1a81d02ad01393f3fa8ede764e914252d0e96'
HARNESS = VARIANTS = '9a45372530e49718b01b6524f9a0db993514e1e8'
FROZEN_BASE = '01f83663a8e7580a2941cc8bf2b0e92c1dd4a296'
CI_RUN = '43869376457'
FROZEN_CI_RUN = '43871304806'
PRODUCER = '0857eefc242585a9f44bfa3c134ea0bd3eb1b30a'
ASSEMBLY = 'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'
UNIT = ['OwnedHistoryBatchTests']
PARITY = ['NamedComparisonPublisherTests', 'PointwiseComparisonPublisherTests',
          'AwesomeComparisonTests', 'ElderRayComparisonTests', 'LogVolatilityComparisonTests',
          'McGinleyComparisonTests', 'RegressionStatisticsComparisonTests']
VARIANT_CLASS = 'SharedClassicalWindowVariantBenchmarkTests'
FROZEN_PARITY = [VARIANT_CLASS, *PARITY]
PAIRS = ['Skender.GetAwesome', 'QuanTAlib.Historical', 'QuanTAlib.Realized',
         'QuanTAlib.Mgdi', 'Skender.GetDynamic', 'Skender.GetElderRay',
         'QuanTAlib.Slope', 'Skender.GetSlope']
APPROVAL = 'benchmarks/plans/evidence/named-output-projection-refresh-scope-20261116.json'
POLICY = dict(order=['old', 'new', 'new', 'old'], quiet_samples=12, max_cpu_percent=15,
              warmups=10, iterations=10, fixed_invocation_warmups=1000)
CHANGES = {p.BENCH+'/LibraryComparisons/'+name+'.cs' for name in
           ('AwesomeComparison', 'ElderRayComparison', 'LogVolatilityComparison',
            'McGinleyComparison', 'RegressionStatisticsComparison', 'NamedComparisonPublisher')}
CHANGES.add(p.TESTS+'/NamedComparisonPublisherTests.cs')


def validate_scope(plan):
    approval = p.read(p.REPO/APPROVAL)
    assert approval['approved'] and approval['pairs'] == PAIRS
    assert plan['refresh_approval'] == dict(path=APPROVAL, sha256=p.digest(p.REPO/APPROVAL))
    assert plan['named_output_projection_refresh'] and not plan.get('pointwise_family_refresh')
    assert plan['report_refresh_only'] and not plan['production_qualification']
    assert not any(plan.get(k) for k in ('diagnostic_only', 'targeted_reference', 'coverage_reference',
        'qualify_variants', 'standalone_variants', 'reuse_inputs', 'diagnostic_kind'))
    assert plan['sources'] == dict(published=PUBLISHED, old=CANONICAL, successor=CANONICAL, variants=VARIANTS)
    assert plan['gpu_sources'] == dict(old=CANONICAL, successor=CANONICAL, variants=VARIANTS)
    assert plan['cpu_test_classes'] == {label:UNIT for label in ('old', 'successor', 'variants')}
    assert plan['proposed_dotnet_sdk'] == '10.0.403' and plan['runtime_policy_overrides'] == {}
    assert plan['runtime_dependency_alignment'] == dict(tensor_version='0.150.0')
    assert plan['measurement_policy'] == POLICY and plan['require_fixture_inventory']
    assert not any(plan.get(key) for key in ('arithmetic_warmup_approval', 'star_warmup_approval', 'qualification_warmup_approval'))
    assert (plan['cases'], plan['decisions']) == (80, 16)
    assert plan['matrix'] == dict(shards=8, max_parallel=2, timeout_minutes_per_shard=45)
    assert plan['affected_pairs'] == PAIRS
    expected = []
    for index, pair in enumerate(PAIRS):
        task = dict(fixture='LibraryPairBenchmarks', operations=[dict(raw=pair, pair=pair)],
                    methods=['Ooples', 'Competitor'], implicit_pair=False, cases=10, decisions=2)
        expected.append(dict(index=index, tasks=[task], cases=10, decisions=2))
    assert plan['refresh_shards'] == plan['targeted_shards'] == expected
    for key, source, run, parity in [('candidate', CANONICAL, CI_RUN, PARITY),
            ('frozen_candidate', VARIANTS, FROZEN_CI_RUN, FROZEN_PARITY)]:
        candidate = plan[key]
        assert (candidate['source'], candidate['ci_run'], candidate['producer']) == (source, run, PRODUCER)
        assert candidate['unit_classes'] == UNIT and candidate['parity_classes'] == parity
        assert re.fullmatch('[a-f0-9]{40}', candidate['evidence_source'])
        assert all(re.fullmatch('[a-f0-9]{64}', candidate[k]) for k in ('manifest_sha256', 'gpu_receipt_sha256'))
    assert re.fullmatch('[a-f0-9]{64}', plan['frozen_harness_sha256'])


def verify_runtime_identity(plan, manifests):
    validate_scope(plan)
    assert manifests['old'] == manifests['new'], 'A/A requires identical whole runtime manifests'


def authenticate(folder, plan, frozen=False):
    selected = copy.deepcopy(plan)
    key = 'frozen_candidate' if frozen else 'candidate'
    selected['candidate'] = plan[key]
    return common._candidate_inputs(folder, selected, VARIANTS if frozen else CANONICAL,
        UNIT, FROZEN_PARITY if frozen else PARITY, 37, 97 if frozen else 84,
        FROZEN_CI_RUN if frozen else CI_RUN)


def verify_authentication(folder, plan, proof):
    validate_scope(plan)
    assert proof['preparation_mode'] == 'authenticated-named-projection-baseline-refresh'
    assert proof['canonical_source_authentication'] == plan['candidate']
    assert proof['frozen_source_authentication'] == plan['frozen_candidate']
    assert p.digest(folder/'common'/ASSEMBLY) == plan['frozen_harness_sha256']
    for label, key, source, run, classes, count in [
            ('old', 'candidate', CANONICAL, CI_RUN, PARITY, 84),
            ('successor', 'candidate', CANONICAL, CI_RUN, PARITY, 84),
            ('variants', 'frozen_candidate', VARIANTS, FROZEN_CI_RUN, FROZEN_PARITY, 97)]:
        candidate = plan[key]
        manifest_path = folder/'evidence'/(key+'-manifest.json')
        assert p.digest(manifest_path) == candidate['manifest_sha256']
        manifest = p.read(manifest_path)
        assert (manifest['source'], manifest['run_id'], manifest['workflow_head']) == (source, run, PRODUCER)
        assert manifest['classes'] == dict(unit=UNIT, parity=classes)
        assert manifest['checks']['unit']['passed'] == 37 and manifest['checks']['parity']['passed'] == count
        if label == 'successor':
            assert plan['source_correctness'] == dict(unit=37, comparator=84, actual_gpu=100,
                production_sha256=manifest['runtime_files']['src/bin/Release/net10.0/'+p.DLL],
                source_fingerprint=manifest['source_sha256'])
        for name, sha in manifest['evidence'].items():
            assert p.digest(folder/'evidence'/(key+'-ci')/Path(name).name) == sha
        assert proof['production_sha256'][label] == manifest['runtime_files']['src/bin/Release/net10.0/'+p.DLL]
        assert p.verify_trx(folder/'evidence'/(label+'.trx'), UNIT) == manifest['checks']['unit']
        receipt_path = folder/'evidence'/(label+'-original-gpu-receipt.json')
        assert p.digest(receipt_path) == candidate['gpu_receipt_sha256']
        receipt = p.read(receipt_path)
        assert receipt['source_sha256'] == manifest['source_sha256']
        assert (receipt['build_origin']['source'], receipt['build_origin']['run_id'],
                receipt['build_origin']['workflow_head']) == (source, run, PRODUCER)
        assert receipt['test_assembly_sha256'] == manifest['runtime_files'][p.TESTS+'/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorTests.dll']
    assert proof['production_sha256']['old'] == proof['production_sha256']['successor']
    frozen = p.read(folder/'evidence/frozen_candidate-manifest.json')
    prefix = p.BENCH+'/bin/Release/net10.0/'
    assert proof['common_manifest'] == {name[len(prefix):]:sha for name, sha in frozen['runtime_files'].items() if name.startswith(prefix)}
    assert proof['common_manifest'][ASSEMBLY] == plan['frozen_harness_sha256']


def prepare(candidate, frozen, out):
    plan = p.read(p.PLAN)
    validate_scope(plan)
    assert subprocess.check_output(['dotnet', '--version'], text=True).strip() == plan['proposed_dotnet_sdk']
    candidate, frozen, out = map(lambda path:Path(path).resolve(), (candidate, frozen, out))
    assert not out.exists() and not out.is_relative_to(candidate) and not out.is_relative_to(frozen)
    assert not p.git('diff', '--name-only', PUBLISHED, CANONICAL, '--', 'src', 'tests')
    assert set(p.git('diff', '--name-only', PUBLISHED, CANONICAL).splitlines()) == CHANGES
    assert set(p.git('diff', '--name-only', FROZEN_BASE, HARNESS).splitlines()) == CHANGES
    for path in CHANGES:
        assert p.git('rev-parse', CANONICAL+':'+path) == p.git('rev-parse', HARNESS+':'+path)
    canonical = authenticate(candidate, plan)
    variant = authenticate(frozen, plan, frozen=True)
    evidence = out/'evidence'
    evidence.mkdir(parents=True)
    (out/'production').mkdir()
    common_runtime = out/'common'
    common_runtime.mkdir()
    prefix = p.BENCH+'/bin/Release/net10.0/'
    for name, sha in variant['manifest']['runtime_files'].items():
        if name.startswith(prefix):
            target = common_runtime/name[len(prefix):]
            assert target.resolve().is_relative_to(common_runtime.resolve())
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(variant['read']('blobs/'+sha))
    for key, data, origin in [('candidate', canonical, candidate), ('frozen_candidate', variant, frozen)]:
        (evidence/(key+'-manifest.json')).write_bytes((origin/'manifest.json').read_bytes())
        retained = evidence/(key+'-ci')
        retained.mkdir()
        for name in data['manifest']['evidence']:
            (retained/Path(name).name).write_bytes(data['read'](name))
    checks, production, dependencies = {}, {}, {}
    for label, data, source in [('old', canonical, CANONICAL), ('successor', canonical, CANONICAL), ('variants', variant, VARIANTS)]:
        (out/'production'/(label+'.dll')).write_bytes(data['read']('blobs/'+data['production']))
        production[label] = data['production']
        dependencies[label] = data['closure']
        checks[label] = dict(data['manifest']['checks']['unit'], original_gpu_source=source)
        for original, target in [('unit.trx', label+'.trx'), ('unit-build.log', label+'-build.log'), ('unit-tests.log', label+'-tests.log')]:
            (evidence/target).write_bytes(data['read']('evidence/'+original))
        (evidence/(label+'-original-gpu-receipt.json')).write_bytes(data['gpu_receipt'])
        (evidence/(label+'-original-gpu-gpu.trx.gz')).write_bytes(data['gpu_trx'])
    # Execute the dedicated 13-case binding suite on the authenticated frozen runtime.
    runtime = out.parent/(out.name+'-variant-test-runtime')
    assert not runtime.exists()
    runtime.mkdir()
    prefix = p.TESTS+'/bin/Release/net10.0/'
    for name, sha in variant['manifest']['runtime_files'].items():
        if name.startswith(prefix):
            target = runtime/name[len(prefix):]
            assert target.resolve().is_relative_to(runtime.resolve())
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(variant['read']('blobs/'+sha))
    p.command(['dotnet', 'vstest', runtime/'OoplesFinance.StockIndicators.CompetitorTests.dll',
        '/TestCaseFilter:FullyQualifiedName~'+VARIANT_CLASS, '/Logger:trx;LogFileName=variant-harness.trx',
        '/ResultsDirectory:'+str(evidence)], p.REPO, evidence/'variant-harness-tests.log')
    checks['variant-harness'] = p.verify_trx(evidence/'variant-harness.trx', [VARIANT_CLASS])
    assert checks['variant-harness']['passed'] == 13
    proof = dict(plan_sha256=p.digest(p.PLAN), sources=plan['sources'],
        source_trees={label:p.git('rev-parse', plan['sources'][label]+':src') for label in production},
        production_sha256=production, common_manifest=p.manifest(common_runtime),
        evidence_manifest=p.manifest(evidence), checks=checks, tested_runtime_dependencies=dependencies,
        sdk=plan['proposed_dotnet_sdk'], dotnet_info=subprocess.check_output(['dotnet', '--info'], text=True),
        workflow_head=p.git('rev-parse', 'HEAD'), run_id=os.environ['GITHUB_RUN_ID'],
        run_attempt=os.environ['GITHUB_RUN_ATTEMPT'], production_qualification=False, report_refresh_only=True,
        preparation_mode='authenticated-named-projection-baseline-refresh',
        canonical_source_authentication=plan['candidate'], frozen_source_authentication=plan['frozen_candidate'])
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256'] = record_inventory(out, plan, p.digest(p.PLAN))
    p.write(out/'provenance.json', proof)
    p.verify_inputs(out)
    return proof


def collect(folder):
    from .verify import _collect
    result = _collect(Path(folder), 'baseline-refresh', preflight=False)
    assert not result['accepted'] and (result['case_count'], result['decision_count']) == (80, 16)
    result.update(baseline_refresh_verified=True, report_refresh_only=True, production_qualification=False,
        production_optimization=False, report_imported=False, source_changed=False,
        same_production_under_both_labels=True)
    from .pointwise_family_refresh import native_review
    result['native_review'] = native_review(Path(folder), pairs=PAIRS)
    return result
