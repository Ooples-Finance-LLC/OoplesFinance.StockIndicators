"""Rebind authenticated canonical inputs for a bounded ten-pair A/A refresh.

An inconclusive label comparison admits a pair; it does not prove equivalence.
This module never qualifies production or imports/publishes a report.
"""
import copy
import gzip
import os
from pathlib import Path
import re
import shutil
import xml.etree.ElementTree as ET

from . import pipeline as p, named_projection_refresh as origin
from .candle_family_roi import PATHS, selected_plan

PUBLISHED = 'ab61b7eba91debdf481130c91a14e33d1a3e5888'
CANONICAL = origin.CANONICAL
VARIANTS = origin.VARIANTS
HARNESS = VARIANTS
HARNESS_SHA = '48229494a1ff1e4e59bddd5a018cbd590cae24523d52dd2f3effdd20bd802723'
BASE_PLAN = 'benchmarks/plans/hosted-named-projection-refresh-20261116.json'
BASE_PLAN_SHA = '03e891d5220a12f0983a2f8d8d1b0de2cdc4f751cea0a07625a91dc6e21661b8'
BASE_RUN = '43880487485'
BASE_PROVENANCE_SHA = '92977e9786050146dbe97748c8a28d59e8eb47be5f4b5f85ae064ec4fcacef3a'
REPORT_SHA = '2e454a8bd0f51b83e827c7d44ed47d1c1597faa3bdc5e53b5c0afbb848752311'
APPROVAL = 'benchmarks/plans/evidence/current-baseline-ten-refresh-scope-20261116.json'
APPROVAL_SHA = '2569da5586635ed52a30bfb78db5de36e41b119bf572b19c460289e61749e3ad'
PREFLIGHT = {'files': {'receipt.json': '3b04d56edc384140d5760d17cbbe4344fcdee699ecc51f864b3dba3b3c33e39a', 'authentication.json': 'a8c30c9d0f63eb571cd1d953552d2ad17f069d6e90f6fa2426536640743db6dc', 'frozen-canonical-alignment.json': '01a42de224e1d3947cf8a55c5be09da795a3f4fd1fed2b4176eb9392436796ee', 'canonical-comparators.trx.gz': '96e930a0e597a1b64d9223da9e4489a1e8a00a43336ba85ce226aa414b0192f7', 'preflight.py': '4d0945c17b0a171b4ff960d5b74a4582c30b7250f51d825f1e6ddf2d2aea9720', 'frozen-alignment.ps1': '73e092552b64170d0e9479ff0dd61032fcb2cadf062553407b9a3d3327531a66', 'runtime-dependencies.py': 'da83b2e38a477743006b9a9ebde4344375437556c9ad82904881c1d62133eaf6'}, 'receipt': 'receipt.json', 'trx': {'path': 'canonical-comparators.trx.gz', 'gzip': True, 'sha256': '6f501d8188d164d84f8f58e62b458256f7278e47c1dc11bbc4a3cb3d7c260659', 'passed': 303, 'classes': {'WilderAverageComparisonTests': 10, 'MassNormalizedComparisonTests': 16, 'SampleShapeComparisonTests': 11, 'McGinleyComparisonTests': 8, 'ExhaustionTests': 133, 'MatchedLinesTests': 43, 'TripleBodyTests': 53, 'AverageRangeDojiTests': 29}}}
PAIRS = ['QuanTAlib.Smma', 'TaLib.Candles.Stalled', 'TaLib.Candles.ThreeStarsInSouth',
    'TaLib.Candles.SeparatingLines', 'TaLib.Candles.UpsideGapTwoCrows', 'Skender.GetDynamic',
    'QuanTAlib.Qema', 'QuanTAlib.Skew', 'TaLib.Candles.LongLeggedDoji', 'Skender.GetSmma']
CLASSES = ['WilderAverageComparisonTests', 'MassNormalizedComparisonTests',
    'SampleShapeComparisonTests', 'McGinleyComparisonTests', 'ExhaustionTests',
    'MatchedLinesTests', 'TripleBodyTests', 'AverageRangeDojiTests']
POLICY = origin.POLICY
REPORT_CONTRACT = dict(total_baseline_rows=802, builder_comparisons=1544,
    both_size_inconclusive_required=True, whole_pair_quarantine=True,
    no_current_full_inventory_claim=True, no_optimization_credit=True,
    zero_admitted_no_publication=True)


def _base_plan():
    path = p.REPO / BASE_PLAN
    assert p.digest(path) == BASE_PLAN_SHA
    return p.read(path)


def validate_scope(plan):
    assert isinstance(APPROVAL_SHA, str) and re.fullmatch('[a-f0-9]{64}', APPROVAL_SHA), 'Preflight not frozen'
    assert isinstance(PREFLIGHT, dict) and PREFLIGHT, 'Preflight not frozen'
    assert p.digest(p.REPO / APPROVAL) == APPROVAL_SHA
    approval = p.read(p.REPO / APPROVAL)
    assert approval['approved'] and not approval['proposal_only']
    assert approval['pairs'] == PAIRS and (approval['cases'], approval['decisions']) == (100, 20)
    assert not approval['source_qualification'] and not approval['production_code_changes'] and not approval['harness_changes']
    assert plan['refresh_approval'] == dict(path=APPROVAL, sha256=APPROVAL_SHA)
    assert plan['current_baseline_ten_refresh'] and plan['report_refresh_only'] and not plan['production_qualification']
    assert not any(plan.get(k) for k in ('named_output_projection_refresh', 'pointwise_family_refresh',
        'diagnostic_only', 'diagnostic_kind', 'targeted_reference', 'coverage_reference', 'qualify_variants',
        'standalone_variants', 'reuse_inputs', 'arithmetic_warmup_approval', 'star_warmup_approval',
        'qualification_warmup_approval'))
    assert plan['sources'] == dict(published=PUBLISHED, old=CANONICAL, successor=CANONICAL, variants=VARIANTS)
    base = _base_plan()
    for key in ('gpu_sources', 'cpu_test_classes', 'candidate', 'frozen_candidate', 'source_correctness',
            'frozen_harness_sha256', 'runtime_dependency_alignment'):
        assert plan[key] == base[key], key
    assert plan['proposed_dotnet_sdk'] == '10.0.403' and plan['runtime_policy_overrides'] == {}
    assert plan['expected_runtime_version'] == '.NET 10.0.15'
    assert plan['measurement_policy'] == POLICY and plan['require_fixture_inventory']
    assert (plan['cases'], plan['decisions']) == (100, 20)
    assert plan['matrix'] == dict(shards=10, max_parallel=2, timeout_minutes_per_shard=45)
    assert plan['affected_pairs'] == PAIRS and plan['report_before_sha256'] == REPORT_SHA
    assert plan['report_contract'] == REPORT_CONTRACT
    assert plan['consumer_preflight'] == PREFLIGHT
    assert plan['baseline_inputs'] == dict(run_id=BASE_RUN, plan=BASE_PLAN,
        plan_sha256=BASE_PLAN_SHA, provenance_sha256=BASE_PROVENANCE_SHA)
    assert len(plan['frozen_common_manifest']) == 94
    assert all(re.fullmatch('[a-f0-9]{64}', value) for value in plan['frozen_common_manifest'].values())
    expected = []
    for index, pair in enumerate(PAIRS):
        task = dict(fixture='LibraryPairBenchmarks', operations=[dict(raw=pair, pair=pair)],
            methods=['Ooples', 'Competitor'], implicit_pair=False, cases=10, decisions=2)
        expected.append(dict(index=index, tasks=[task], cases=10, decisions=2))
    assert plan['refresh_shards'] == plan['targeted_shards'] == expected


def verify_runtime_identity(plan, manifests):
    validate_scope(plan)
    assert manifests['old'] == manifests['new'], 'A/A requires identical whole runtime manifests'


def _preflight(folder):
    # Identity is populated from the actual reviewed receipt/TRX, never predicted counts.
    assert isinstance(PREFLIGHT, dict) and PREFLIGHT
    base = (Path(folder) / 'consumer-preflight').resolve()
    for relative, digest in PREFLIGHT['files'].items():
        path = (base / relative).resolve()
        assert path.is_relative_to(base) and path.is_file()
        assert p.digest(path) == digest
    receipt = p.read(base / PREFLIGHT['receipt'])
    base_plan = _base_plan()
    assert receipt['status'] == 'passed' and receipt['source'] == CANONICAL
    assert receipt['canonical_ci'] == origin.CI_RUN and receipt['frozen_source'] == HARNESS
    assert receipt['canonical_manifest_sha256'] == base_plan['candidate']['manifest_sha256']
    assert receipt['production_sha256'] == base_plan['source_correctness']['production_sha256']
    assert receipt['canonical_test_sha256'] == '0560474677efa9c9ffb1190a8dc52e595b06438de7b6ab651727a0d24ceb78d3'
    assert receipt['frozen_benchmark_sha256'] == HARNESS_SHA
    assert receipt['classes'] == CLASSES and receipt['pairs'] == PAIRS and receipt['alignment_cases'] == 20
    assert not any(receipt[k] for k in ('fresh_gpu_execution', 'new_compilation', 'measurement', 'report_changed'))
    auth = p.read(base/'authentication.json')
    assert p.digest(base/'authentication.json') == receipt['authentication_sha256']
    assert auth['status'] == 'authenticated' and auth['source'] == CANONICAL
    assert auth['canonical_ci'] == origin.CI_RUN and auth['frozen_source'] == HARNESS
    assert auth['source_fingerprint'] == base_plan['source_correctness']['source_fingerprint']
    for key in ('canonical_manifest_sha256', 'production_sha256', 'canonical_test_sha256', 'frozen_benchmark_sha256'):
        assert auth[key] == receipt[key], key
    alignment = p.read(base/'frozen-canonical-alignment.json')
    assert p.digest(base/'frozen-canonical-alignment.json') == receipt['frozen_alignment_sha256']
    assert alignment['status'] == 'passed' and alignment['production_sha256'] == receipt['production_sha256']
    assert len(alignment['rows']) == 20 and all(row['passed'] and row['verify_isolation'] for row in alignment['rows'])
    assert {(row['pair'], row['bars']) for row in alignment['rows']} == {(pair, size) for pair in PAIRS for size in (1000, 10000)}
    raw = (base / PREFLIGHT['trx']['path']).read_bytes()
    if PREFLIGHT['trx']['gzip']:
        raw = gzip.decompress(raw)
    import hashlib
    assert hashlib.sha256(raw).hexdigest() == PREFLIGHT['trx']['sha256'] == receipt['comparator_trx_sha256']
    document = ET.fromstring(raw)
    results = document.findall('.//{*}UnitTestResult')
    assert results and all(row.attrib['outcome'] == 'Passed' for row in results)
    definitions = {test.attrib['id']: test.find('{*}TestMethod').attrib for test in document.findall('.//{*}UnitTest')}
    namespace = 'OoplesFinance.StockIndicators.CompetitorTests.'
    counts = {name: 0 for name in CLASSES}
    for result in results:
        class_name = definitions[result.attrib['testId']]['className'].split(',')[0]
        assert class_name.startswith(namespace)
        name = class_name[len(namespace):]
        assert name in counts
        counts[name] += 1
    assert all(counts.values()) and sum(counts.values()) == len(results)
    assert counts == PREFLIGHT['trx']['classes'] == receipt['class_counts']
    assert len(results) == PREFLIGHT['trx']['passed'] == receipt['passed_cases']
    return counts


def verify_authentication(folder, plan, proof):
    validate_scope(plan)
    folder = Path(folder)
    assert proof['preparation_mode'] == 'authenticated-current-baseline-ten-refresh'
    assert proof['production_qualification'] is False and proof['report_refresh_only'] is True
    original_path = folder / 'origin-provenance.json'
    assert p.digest(original_path) == BASE_PROVENANCE_SHA
    original = p.read(original_path)
    assert original['run_id'] == BASE_RUN and original['plan_sha256'] == BASE_PLAN_SHA
    assert proof['baseline_origin'] == plan['baseline_inputs']
    assert not p.git('diff', '--name-only', CANONICAL, PUBLISHED, '--', *PATHS, 'tests')
    assert proof['common_manifest'] == original['common_manifest'] == plan['frozen_common_manifest']
    assert proof['production_sha256'] == original['production_sha256']
    assert proof['production_sha256']['old'] == proof['production_sha256']['successor']
    assert proof['evidence_manifest'] == original['evidence_manifest']
    assert proof['checks'] == original['checks']
    for key in ('source_trees', 'canonical_source_authentication', 'frozen_source_authentication'):
        assert proof[key] == original[key], key
    assert proof['tested_runtime_dependencies'] == original['tested_runtime_dependencies']
    # This is the original canonical/frozen proof, not a candidate diagnostic's receipt.
    origin.verify_authentication(folder, _base_plan(), original)
    observed = _preflight(folder)
    auth = p.read(folder/'consumer-preflight/authentication.json')
    expected_frozen = dict(plan['frozen_common_manifest'])
    expected_frozen[p.DLL] = proof['production_sha256']['old']
    assert auth['frozen_runtime'] == expected_frozen
    assert auth['tensor_closure'] == proof['tested_runtime_dependencies']['old']
    covered = set(observed) | set(p.read(folder/'evidence/candidate-manifest.json')['checks']['parity']['classes'])
    assert set(CLASSES) <= covered
    assert proof['consumer_preflight'] == PREFLIGHT


def prepare(baseline, preflight, out):
    plan = p.read(p.PLAN)
    validate_scope(plan)
    import subprocess
    assert subprocess.check_output(['dotnet', '--version'], text=True).strip() == plan['proposed_dotnet_sdk']
    baseline, preflight, out = map(lambda path: Path(path).resolve(), (baseline, preflight, out))
    assert not out.exists() and not out.is_relative_to(baseline) and not out.is_relative_to(preflight)
    with selected_plan(p.REPO / BASE_PLAN):
        original = p.verify_inputs(baseline)
    assert p.digest(baseline/'provenance.json') == BASE_PROVENANCE_SHA
    assert original['run_id'] == BASE_RUN
    assert not p.git('diff', '--name-only', CANONICAL, PUBLISHED, '--', *PATHS, 'tests')
    shutil.copytree(baseline, out)
    shutil.copyfile(baseline/'provenance.json', out/'origin-provenance.json')
    dest = out/'consumer-preflight'
    assert not dest.exists()
    dest.mkdir()
    for relative, digest in PREFLIGHT['files'].items():
        source = (preflight/relative).resolve()
        assert source.is_relative_to(preflight) and p.digest(source) == digest
        target = (dest/relative).resolve()
        assert target.is_relative_to(dest)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
    _preflight(out)
    proof = copy.deepcopy(original)
    proof.update(plan_sha256=p.digest(p.PLAN), sources=plan['sources'],
        workflow_head=p.git('rev-parse', 'HEAD'), run_id=os.environ['GITHUB_RUN_ID'],
        run_attempt=os.environ['GITHUB_RUN_ATTEMPT'], baseline_origin=plan['baseline_inputs'],
        preparation_mode='authenticated-current-baseline-ten-refresh', consumer_preflight=PREFLIGHT,
        production_qualification=False, report_refresh_only=True,
        dotnet_info=subprocess.check_output(['dotnet', '--info'], text=True))
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256'] = record_inventory(out, plan, p.digest(p.PLAN))
    p.write(out/'provenance.json', proof)
    p.verify_inputs(out)
    return proof


def admission(decisions):
    assert len(decisions) == 20
    keys = {(row['pair'], row['bars'], row['method']) for row in decisions}
    assert keys == {(pair, size, 'Ooples') for pair in PAIRS for size in (1000, 10000)}
    assert all(row['verdict'] in ('faster', 'slower', 'inconclusive') for row in decisions)
    admitted = [pair for pair in PAIRS if all(row['verdict'] == 'inconclusive'
        for row in decisions if row['pair'] == pair)]
    quarantined = [dict(pair=pair, sizes={str(row['bars']):row['verdict']
        for row in decisions if row['pair'] == pair}) for pair in PAIRS if pair not in admitted]
    return dict(admitted_pairs=admitted, quarantined_pairs=quarantined,
        admission_is_not_equivalence=True, admitted_baseline_rows=2*len(admitted),
        unchanged_baseline_rows=802-2*len(admitted), unchanged_builder_comparisons=1544,
        zero_admitted_no_publication=not admitted)


def _verify_runtime(folder):
    from .verify import archived
    expected = p.read(p.PLAN)['expected_runtime_version']
    for root in sorted(path.parent for path in Path(folder).glob('*/provenance.json')):
        proof = p.read(root/'provenance.json')
        for record in p.read(root/'status.json')['rounds']:
            report = p.read(archived(root, proof['raw_root'], record['report']))
            actual = report['HostEnvironmentInfo']['RuntimeVersion']
            assert actual == expected or actual.startswith(expected + ' '), (actual, expected)


def collect(folder):
    from .verify import _collect
    result = _collect(Path(folder), 'baseline-refresh', preflight=False)
    assert not result['accepted'] and (result['case_count'], result['decision_count']) == (100, 20)
    _verify_runtime(folder)
    result.update(baseline_refresh_verified=True, report_refresh_only=True, production_qualification=False,
        production_optimization=False, report_imported=False, source_changed=False,
        same_production_under_both_labels=True, report_contract=REPORT_CONTRACT,
        report_before_sha256=REPORT_SHA, **admission(result['decisions']))
    from .pointwise_family_refresh import native_review
    result['native_review'] = native_review(Path(folder), pairs=PAIRS)
    return result
