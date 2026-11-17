"""Authenticated inputs for the bounded six-candle predicate-order qualification."""
import copy
import json
import os
from pathlib import Path
import re
import shutil
import subprocess

from . import pipeline as p
from . import candle_single_pass_roi as common
from . import matchinglow_grid_qualification as previous

SCOPE = 'candle-predicate-order-20261116'
OLD = '6051a95220eefa36e6f837a9a54e036164b76ebe'
NEW = 'caefc553fd01ec4ad1def3d18f205e46f32cf1cd'
OLD_COMPILED = previous.NEW
OLD_DLL = '4d571648599e567bb6df0cda1f823d79e95712241078c2ab1721cf7409c10173'
VARIANTS = previous.VARIANTS
HARNESS_DLL = previous.HARNESS_DLL
HARNESS_ASSEMBLY = previous.HARNESS_ASSEMBLY
BASE_PLAN, BASE_PLAN_SHA = previous.BASE_PLAN, previous.BASE_PLAN_SHA
BASE_PROVENANCE = previous.BASE_PROVENANCE
OLD_PLAN = 'benchmarks/plans/hosted-matchinglow-only-grid-20261116.json'
OLD_PLAN_SHA = 'a60bd3a6efce56b1d56e168e34ebb40d88e7b17b2c980a20fcf50166801819c0'
REFERENCE = 'benchmarks/results/remaining-indicators/hosted-targeted-43992359476-attempt1/decision.json'
TRIPLE = ['TaLib.Candles.'+name for name in ('ThreeInside','TwoCrows','UpsideGapTwoCrows','UniqueThreeRiver')]
PENETRATION = ['TaLib.Candles.PiercingLine','TaLib.Candles.DarkCloudCover']
PAIRS = TRIPLE + PENETRATION
UNIT = ['OoplesFinance.StockIndicators.Tests.Unit.ModelsTests.'+name for name in
    ('TripleBodySharedExecutionTests','TripleBodyParallelExecutionTests','PenetrationSharedExecutionTests')]
PARITY = ['OoplesFinance.StockIndicators.CompetitorTests.'+name for name in ('TripleBodyTests','PenetrationCandleTests')]
PATHS = {'src/Indicators/TripleBodyCandles.cs','src/Indicators/PenetrationCandles.cs',
    'tests/ModelsTests/TripleBodySharedExecutionTests.cs','tests/ModelsTests/PenetrationSharedExecutionTests.cs'}
METHODS = ['BuilderFull','BuilderLatestOnly','NativeSingle','NativeParallel']
NATIVE_POLICY = dict(fixture='LibraryPairBenchmarks',method='Ooples',native_method='Competitor',
    native_round=2,old_rounds=[0,3],candidate_rounds=[1,2],minimum_margin=0.10,
    new_closure_bars=10000,minimum_new_closures=1,require_own_source_faster=True,
    require_net_10000_point_progress=True,protect_prior_confident_wins_both_sizes=True,
    point_win_comparison='strict-less-than')
blob, sha = previous.blob, previous.sha


def cpu_classes(plan, label):
    assert plan.get('candle_predicate_order_qualification') is True
    assert plan['targeted_reference']['scope'] == SCOPE
    expected = dict(old=previous.UNIT, successor=UNIT, variants=['OwnedHistoryBatchTests'])
    selected = plan['cpu_test_classes'][label]
    assert selected == expected[label]
    return selected


def shards():
    def task(fixture, pairs, methods, cases, decisions, short=False):
        return dict(fixture=fixture, operations=[dict(raw=x.rsplit('.',1)[1] if short else x,pair=x) for x in pairs],
            methods=methods,implicit_pair=False,cases=cases,decisions=decisions)
    return [dict(index=0,cases=180,decisions=36,tasks=[
        task('LibraryPairBenchmarks',PAIRS,['Ooples','Competitor'],60,12),
        task('SharedTripleBodyBenchmarks',TRIPLE,METHODS,80,16),
        task('SharedTaReversalContinuationBenchmarks',PENETRATION,METHODS,40,8,short=True)])]


def validate_scope(plan, *, require_ready=True):
    assert plan.get('candle_predicate_order_qualification') is True
    assert plan['sources'] == dict(published=OLD,old=OLD,successor=NEW,variants=VARIANTS)
    assert plan['gpu_sources'] == dict(old=OLD_COMPILED,successor=NEW,variants=VARIANTS)
    assert (plan['cases'],plan['decisions']) == (180,36)
    assert plan['matrix'] == dict(shards=1,max_parallel=1,timeout_minutes_per_shard=180)
    assert plan['targeted_shards'] == shards()
    assert plan['native_progress_gate'] == NATIVE_POLICY
    assert plan['measurement_policy'] == common.POLICY and plan['runtime_policy_overrides'] == {}
    assert plan['proposed_dotnet_sdk'] == '10.0.403' and plan['expected_runtime_version'] == '.NET 10.0.15'
    assert plan['runtime_dependency_alignment'] == dict(tensor_version='0.150.0') and plan['require_fixture_inventory']
    assert sha(json.dumps(plan['baseline_common_manifest'],sort_keys=True,separators=(',',':')).encode()) == previous.COMMON_MANIFEST_SHA
    reference = plan['targeted_reference']
    assert reference['scope'] == SCOPE and reference['decision'] == REFERENCE
    assert str(reference['run_id']) == '43992359476' and reference['qualified_source'] == OLD_COMPILED
    assert plan['baseline_inputs'] == dict(run_id='43880487485',plan=BASE_PLAN,
        plan_sha256=BASE_PLAN_SHA,provenance_sha256=BASE_PROVENANCE)
    assert p.digest(p.REPO/OLD_PLAN) == OLD_PLAN_SHA
    assert plan['old_candidate'] == p.read(p.REPO/OLD_PLAN)['candidate']
    assert plan['old_candidate']['production_sha256'] == OLD_DLL
    assert not any(plan.get(k) for k in ('matchinglow_grid_qualification','diagnostic_only','reuse_inputs',
        'qualify_variants','standalone_variants','coverage_reference','trix_preflight','current_baseline_ten_refresh',
        'native_crossover_gate','baseline_continuation','preflight'))
    for label in ('old','successor','variants'): cpu_classes(plan,label)
    c = plan['candidate']
    assert c['source'] == NEW and c['unit_classes'] == UNIT and c['parity_classes'] == PARITY
    assert c['unit_passed'] == 24 and c['parity_passed'] == 65
    approval = plan['scope_approval']
    assert p.digest(p.REPO/approval['path']) == approval['sha256']
    approved = p.read(p.REPO/approval['path'])
    assert approved['approved'] and approved['scope'] == SCOPE and approved['cases'] == 180
    if require_ready:
        assert plan['inputs_ready'] is True, 'Freeze actual CI/GPU identities before preparation'
        assert c['ci_run'] == '44005551788' and c['producer'] == '2a1737f30e5e1164189d0209b39ea46bf38c1239'
        assert c['evidence_source'] == 'f63b014560a4978b7b47755fcd73f5d39c531e12'
        assert c['manifest_sha256'] == '9a211abdc551bfa6f0b900ef17e5f305ea417d8546f9f3ba2ec8d9c643391156'
        assert c['gpu_receipt_sha256'] == '4f0abd4a553fe0ab1985e1b8faa610d4c680a84084e199d5ae27600ad1366eb9'
        assert c['production_sha256'] == '8bac548a15cf862067fffcaab85b0abbabadd2964948ceb0d160db29c3f85f52'
        assert re.fullmatch(r'\d+',c['ci_run'])
        for key in ('producer','evidence_source'): assert re.fullmatch('[a-f0-9]{40}',c[key])
        for key in ('manifest_sha256','gpu_receipt_sha256','production_sha256'): assert re.fullmatch('[a-f0-9]{64}',c[key])


def candidate_inputs(folder, plan):
    """The reviewed CI layout/GPU checks for this scope's exact24/65 source."""
    c = plan['candidate']; folder = Path(folder).resolve()
    def read(name):
        path = (folder/name).resolve()
        assert path.is_relative_to(folder) and path.is_file()
        return path.read_bytes()
    import importlib.util
    import tempfile
    import gzip
    spec = importlib.util.spec_from_file_location('predicate_ci',p.REPO/'scripts/build-candidate-correctness.py')
    ci = importlib.util.module_from_spec(spec); spec.loader.exec_module(ci)
    fingerprint = common.source_fingerprint(NEW)
    assert sha(read('manifest.json')) == c['manifest_sha256']
    manifest = json.loads(read('manifest.json'))
    assert (manifest['source'],manifest['run_id'],manifest['workflow_head']) == (NEW,c['ci_run'],c['producer'])
    assert manifest['classes'] == dict(unit=UNIT,parity=PARITY)
    production = ci.validate_layout(manifest,read,NEW,fingerprint)
    assert production == c['production_sha256']
    for label,classes in [('unit',UNIT),('parity',PARITY)]:
        with tempfile.TemporaryDirectory(prefix='predicate-trx-') as temp:
            path = Path(temp)/'checks.trx'; path.write_bytes(read('evidence/'+label+'.trx'))
            checks = p.verify_trx(path,classes)
        assert checks == manifest['checks'][label] and checks['passed'] == c[label+'_passed']
    assert not p.git('diff','--name-only',NEW,c['evidence_source'],'--',*previous.SOURCE_IDENTITY_PATHS)
    base = 'benchmarks/results/shared-rollout/local-gpu-contracts/'
    receipt_raw,trx_raw = (blob(c['evidence_source'],base+n) for n in ('receipt.json','gpu.trx.gz'))
    assert sha(receipt_raw) == c['gpu_receipt_sha256']
    receipt = json.loads(receipt_raw); gpu = ci.gpu_module(p.REPO)
    assert gpu.verify_evidence(receipt,gzip.decompress(trx_raw),json.loads(blob(NEW,
        'scripts/competitor-gpu-methods.json')),fingerprint) == 100
    assert receipt['build_origin']['source'] == NEW and receipt['build_origin']['run_id'] == c['ci_run']
    assert receipt['build_origin']['workflow_head'] == c['producer']
    assert receipt['test_assembly_sha256'] == manifest['runtime_files'][ci.ROOTS[-1]+'/OoplesFinance.StockIndicators.CompetitorTests.dll']
    from .runtime_dependencies import tensor_closure
    with tempfile.TemporaryDirectory(prefix='predicate-closure-') as temp:
        runtime = Path(temp); prefix = ci.ROOTS[1]+'/'
        for name,digest in manifest['runtime_files'].items():
            if name.startswith(prefix):
                path = (runtime/name[len(prefix):]).resolve(); assert path.is_relative_to(runtime.resolve())
                path.parent.mkdir(parents=True,exist_ok=True); path.write_bytes(read('blobs/'+digest))
        closure = tensor_closure(runtime,'OoplesFinance.StockIndicators.Tests.Unit.deps.json')
    return dict(manifest=manifest,read=read,production=production,closure=closure,
        gpu_receipt=receipt_raw,gpu_trx=trx_raw,fingerprint=fingerprint)


def old_inputs(folder, plan):
    # Unchanged frozen validator is bound to a1f and its actual30/89/GPU100 evidence.
    return previous.candidate_inputs(folder,dict(candidate=plan['old_candidate']))


def source_authentication(plan):
    return dict(baseline=plan['baseline_inputs'],old_candidate=plan['old_candidate'],candidate=plan['candidate'])


def verify_authentication(folder, plan, proof):
    validate_scope(plan)
    assert proof['preparation_mode'] == 'authenticated-candle-predicate-qualification'
    assert proof['source_authentication'] == source_authentication(plan)
    original_path = folder/'baseline-authentication/provenance.json'
    assert p.digest(original_path) == BASE_PROVENANCE and p.digest(p.REPO/BASE_PLAN) == BASE_PLAN_SHA
    original = p.read(original_path)
    from .named_projection_refresh import verify_authentication as verify_original
    verify_original(folder/'baseline-authentication',p.read(p.REPO/BASE_PLAN),original)
    assert proof['common_manifest'] == original['common_manifest'] == plan['baseline_common_manifest']
    assert p.digest(folder/'common'/HARNESS_ASSEMBLY) == HARNESS_DLL
    assert proof['production_sha256']['variants'] == original['production_sha256']['variants']
    assert proof['checks']['variants'] == original['checks']['variants']
    assert proof['tested_runtime_dependencies']['variants'] == original['tested_runtime_dependencies']['variants']
    for suffix in ('.trx','-original-gpu-receipt.json','-original-gpu-gpu.trx.gz'):
        assert (folder/'evidence'/('variants'+suffix)).read_bytes() == (folder/'baseline-authentication/evidence'/('variants'+suffix)).read_bytes()
    for label,compiled,authenticated in [('old',OLD_COMPILED,old_inputs(folder/'old-candidate-authentication',plan)),
        ('successor',NEW,candidate_inputs(folder/'candidate-authentication',plan))]:
        assert proof['production_sha256'][label] == authenticated['production']
        assert proof['checks'][label] == dict(authenticated['manifest']['checks']['unit'],original_gpu_source=compiled)
        assert proof['tested_runtime_dependencies'][label] == authenticated['closure']
        for suffix,raw in [('.trx',authenticated['read']('evidence/unit.trx')),
            ('-parity.trx',authenticated['read']('evidence/parity.trx')),
            ('-original-gpu-receipt.json',authenticated['gpu_receipt']),
            ('-original-gpu-gpu.trx.gz',authenticated['gpu_trx'])]:
            assert (folder/'evidence'/(label+suffix)).read_bytes() == raw
    assert proof['production_sha256']['old'] == OLD_DLL


def prepare(baseline, old_candidate, candidate, out):
    plan = p.read(p.PLAN); validate_scope(plan)
    assert subprocess.check_output(['dotnet','--version'],text=True).strip() == plan['proposed_dotnet_sdk']
    dotnet_info = subprocess.check_output(['dotnet','--info'],text=True)
    baseline,old_candidate,candidate,out = map(lambda x:Path(x).resolve(),(baseline,old_candidate,candidate,out))
    assert not out.exists() and all(not out.is_relative_to(x) for x in (baseline,old_candidate,candidate))
    with common.selected_plan(p.REPO/BASE_PLAN): original = p.verify_inputs(baseline)
    assert p.digest(baseline/'provenance.json') == BASE_PROVENANCE
    before = dict(common=p.manifest(baseline/'common'),production=p.manifest(baseline/'production'))
    old,new = old_inputs(old_candidate,plan),candidate_inputs(candidate,plan)
    assert old['production'] == OLD_DLL
    # Fresh copies only. Original common contains frozen9a production intentionally.
    shutil.copytree(baseline,out)
    auth = out/'baseline-authentication';auth.mkdir()
    shutil.copytree(baseline/'evidence',auth/'evidence')
    (auth/'common').mkdir();shutil.copyfile(baseline/'common'/HARNESS_ASSEMBLY,auth/'common'/HARNESS_ASSEMBLY)
    shutil.copyfile(baseline/'provenance.json',auth/'provenance.json')
    shutil.copytree(old_candidate,out/'old-candidate-authentication')
    shutil.copytree(candidate,out/'candidate-authentication')
    proof = copy.deepcopy(original)
    for label,compiled,authenticated in [('old',OLD_COMPILED,old),('successor',NEW,new)]:
        (out/'production'/(label+'.dll')).write_bytes(authenticated['read']('blobs/'+authenticated['production']))
        for suffix,raw in [('.trx',authenticated['read']('evidence/unit.trx')),
            ('-parity.trx',authenticated['read']('evidence/parity.trx')),
            ('-build.log',authenticated['read']('evidence/unit-build.log')),
            ('-tests.log',authenticated['read']('evidence/unit-tests.log')),
            ('-original-gpu-receipt.json',authenticated['gpu_receipt']),
            ('-original-gpu-gpu.trx.gz',authenticated['gpu_trx'])]:
            (out/'evidence'/(label+suffix)).write_bytes(raw)
        proof['checks'][label] = dict(authenticated['manifest']['checks']['unit'],original_gpu_source=compiled)
        proof['tested_runtime_dependencies'][label] = authenticated['closure']
        proof['production_sha256'][label] = authenticated['production']
    proof.update(sources=plan['sources'],dotnet_info=dotnet_info,
        source_trees={label:p.git('rev-parse',plan['sources'][label]+':src') for label in ('old','successor','variants')},
        plan_sha256=p.digest(p.PLAN),workflow_head=p.git('rev-parse','HEAD'),run_id=os.environ['GITHUB_RUN_ID'],
        run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],preparation_mode='authenticated-candle-predicate-qualification',
        evidence_manifest=p.manifest(out/'evidence'),source_authentication=source_authentication(plan))
    proof.pop('reused_from',None)
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256'] = record_inventory(out,plan,p.digest(p.PLAN))
    p.write(out/'provenance.json',proof)
    assert before == dict(common=p.manifest(baseline/'common'),production=p.manifest(baseline/'production'))
    p.verify_inputs(out)
    return proof


def verify_runtime(folder, expected):
    assert expected == '.NET 10.0.15'
    previous._verify_runtime(folder,expected)



def native_rows(folder):
    """Retain separate owned rounds and the sole round2 native control; never pool."""
    from .verify import archived, sample
    from .checkpoints import case_key
    roots = [x.parent for x in Path(folder).glob('*/provenance.json')]
    assert len(roots) == 1
    root = roots[0];proof = p.read(root/'provenance.json');state = p.read(root/'status.json')
    rounds = {}
    for record in state['rounds']:
        if record['fixture'] != 'LibraryPairBenchmarks': continue
        index = str(record['index']);assert index in ('0','1','2','3') and index not in rounds
        report = p.read(archived(root,proof['raw_root'],record['report']))
        rows = {}
        for case in report['Benchmarks']:
            key = case_key(case);assert key not in rows
            rows[key] = sample(case)
        methods = ('Ooples','Competitor') if index == '2' else ('Ooples',)
        assert set(rows) == {(pair,bars,method) for pair in PAIRS for bars in (1000,10000) for method in methods}
        rounds[index] = rows
    assert set(rounds) == {'0','1','2','3'}
    return [dict(pair=pair,bars=bars,
        old_rounds={index:rounds[index][pair,bars,'Ooples'] for index in ('0','3')},
        candidate_rounds={index:rounds[index][pair,bars,'Ooples'] for index in ('1','2')},
        native_control=rounds['2'][pair,bars,'Competitor']) for pair in PAIRS for bars in (1000,10000)]


def adjudicate_native(rows, decisions):
    expected = {(pair,bars) for pair in PAIRS for bars in (1000,10000)}
    indexed = {(row['pair'],row['bars']):row for row in rows}
    assert len(indexed) == len(rows) == 12 and indexed.keys() == expected
    own = {}
    for decision in decisions:
        if decision['fixture'] != 'LibraryPairBenchmarks' or decision['method'] != 'Ooples': continue
        key = decision['pair'],decision['bars'];assert key not in own
        own[key] = decision['verdict']
    assert own.keys() == expected
    old_counts = {'0':0,'3':0};new_counts = {'1':0,'2':0}
    closures = [];protected = [];reopened = []
    for key,row in indexed.items():
        assert set(row['old_rounds']) == {'0','3'} and set(row['candidate_rounds']) == {'1','2'}
        old = list(row['old_rounds'].values());new = list(row['candidate_rounds'].values());native = row['native_control']
        if key[1] == 10000:
            for index,value in row['old_rounds'].items():old_counts[index] += int(value['mean_ns'] < native['mean_ns'])
            for index,value in row['candidate_rounds'].items():new_counts[index] += int(value['mean_ns'] < native['mean_ns'])
            if own[key] == 'faster' and all(value['mean_ns'] >= native['mean_ns'] for value in old) and all(
                value['upper_ns'] < native['lower_ns'] and value['mean_ns'] <= 0.90*native['mean_ns'] for value in new):
                closures.append(dict(pair=key[0],bars=10000,own_source_verdict=own[key]))
        if all(value['upper_ns'] < native['lower_ns'] for value in old):
            check = dict(pair=key[0],bars=key[1],preserved=all(value['mean_ns'] < native['mean_ns'] for value in new))
            protected.append(check)
            if not check['preserved']:reopened.append(check)
    net_progress = min(new_counts.values()) > max(old_counts.values())
    passed = bool(closures) and net_progress and not reopened
    return dict(passed=passed,policy=NATIVE_POLICY,new_10000_closures=closures,
        old_10000_point_win_counts=old_counts,candidate_10000_point_win_counts=new_counts,
        net_10000_point_progress=net_progress,prior_confident_win_checks=protected,
        prior_confident_win_reopenings=reopened,native_control_round=2,native_controls_per_pair_size=1,
        rows=rows,limitation='Separate owned rounds share one LibraryPair round2 Competitor control. No builder controls or pooled timings are used.')


def collect(folder, inputs=None):
    from .verify import _collect
    if inputs is not None:p.verify_inputs(Path(inputs))
    result = _collect(Path(folder),'targeted',preflight=False,candle_predicate_gate=True)
    assert (result['case_count'],result['decision_count']) == (180,36)
    if inputs is not None:assert result['input_provenance_sha256'] == p.digest(Path(inputs)/'provenance.json')
    source_accepted = result['accepted']
    native = adjudicate_native(native_rows(folder),result['decisions'])
    result.update(source_gate_accepted=source_accepted,native_progress_gate_passed=native['passed'],
        native_progress_gate=native,accepted=source_accepted and native['passed'])
    return result
