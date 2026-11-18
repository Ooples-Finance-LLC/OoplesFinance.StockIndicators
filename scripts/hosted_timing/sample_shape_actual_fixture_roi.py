"""Pinned SampleShape actual-fixture frozen-baseline feasibility diagnostic; never source qualification or closure credit."""
import gzip,hashlib,importlib.util,json,os,re,shutil,subprocess,tempfile
from pathlib import Path
from . import pipeline as p
from . import candle_single_pass_roi as common
from . import triple_body_macro_qualification as original
SCOPE='sample-shape-actual-fixture-native-roi-20261118'
KIND='sample-shape-actual-fixture-native-pilot'
OLD='c5bfd4950982aa80dddbfd2db6358ad43ddb9e4c'
NEW='7bac3b91d52c4d1ccebfcda86c2b23f1bc37b979'
PAIRS=['QuanTAlib.Skew','QuanTAlib.Kurtosis']
HARNESS_PATH='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/'+original.HARNESS_ASSEMBLY
HARNESS_SCOPES=[p.BENCH,p.TESTS,'Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.Config','nuget.config']
BASE_UNIT=['WiderUltimateExecutionTests','BoundedUltimateExecutionTests','CertifiedUltimateRatioTests']
BASE_PARITY=['TrueRangeRatioComparisonTests']
UNIT=['FixedGridSampleShapeExecutionTests','SharedSampleShapeTests']
PARITY=['SampleShapeComparisonTests']
PATHS={'src/Indicators/ValuesBarExecution.BoundedWindows.cs','src/Indicators/ValuesBarExecution.SampleShape.cs','tests/ModelsTests/FixedGridSampleShapeExecutionTests.cs'}
sha=lambda raw:hashlib.sha256(raw).hexdigest()
def blob(source,path):return subprocess.check_output(['git','show',source+':'+path],cwd=p.REPO)

def shards():
    task=dict(fixture='LibraryPairBenchmarks',operations=[dict(raw=x,pair=x) for x in PAIRS],methods=['Ooples','Competitor'],implicit_pair=False,cases=20,decisions=4)
    return [dict(index=0,tasks=[task],cases=20,decisions=4)]

def validate_scope(plan,*,require_ready=True):
    assert plan['diagnostic_kind']==KIND and plan['diagnostic_only'] is True
    assert plan['sources']==dict(published=OLD,old=OLD,successor=NEW,variants=OLD)
    assert plan['gpu_sources']==dict(old=OLD,successor=NEW,variants=OLD)
    assert (plan['cases'],plan['decisions'])==(20,4)
    assert plan['matrix']==dict(shards=1,max_parallel=1,timeout_minutes_per_shard=120)
    assert plan['diagnostic_shards']==plan['targeted_shards']==shards()
    assert plan['measurement_policy']==common.POLICY and plan['runtime_policy_overrides']=={}
    assert plan['runtime_dependency_alignment']==dict(tensor_version='0.151.1')
    assert plan['require_fixture_inventory'] is True and plan['proposed_dotnet_sdk']=='10.0.403' and plan['expected_runtime_version']=='.NET 10.0.15'
    assert plan['closure_ledger_new_rows']==0 and plan['automatic_qualification'] is False
    assert plan['require_net_10000_point_progress'] is True
    assert plan['frozen_baseline_feasibility_only'] is True
    assert plan['current_combined_source_qualified'] is False and plan['report_import_permitted'] is False
    assert plan['followup_requires_current_published_baseline'] is True
    assert plan['future_builder_fixture']=='SharedSampleShapeBenchmarks'
    assert plan['harness_alias_policy']==dict(common_artifact_role='old',variants_artifact_role='old',baseline_source=OLD,candidate_source=NEW,require_scoped_source_identity=True,claim_artifact_binary_identity=False)
    assert not any(plan.get(k) for k in ('targeted_reference','coverage_reference','standalone_variants','qualify_variants','trix_preflight','reuse_inputs'))
    for label,key,source in [('old','old_candidate',OLD),('successor','candidate',NEW),('variants','old_candidate',OLD)]:
        c=plan[key];assert c['source']==source and plan['cpu_test_classes'][label]==c['unit_classes']
        for category in ('unit','parity'):
            values=c[category+'_classes'];assert values and len(set(values))==len(values) and all(v.isidentifier() for v in values)
    assert plan['old_candidate']['unit_classes']==BASE_UNIT and plan['old_candidate']['parity_classes']==BASE_PARITY
    assert plan['candidate']['unit_classes']==UNIT and plan['candidate']['parity_classes']==PARITY
    assert plan['candidate']['ci_run']=='44393824051' and plan['candidate']['producer']=='ce6d1a0373166c42583278e4d7efd38f65e0934d'
    focused_baseline(plan)
    if require_ready:
        assert plan['inputs_ready'] is True and plan['parent_review_approved'] is True,'UNBOUND actual receipts or parent review'
        assert re.fullmatch('[a-f0-9]{40}',NEW)
        assert 'UNBOUND_UNIT_CLASSES' not in UNIT and 'UNBOUND_PARITY_CLASSES' not in PARITY
        assert re.fullmatch('[a-f0-9]{64}',plan['harness_sha256'])
        for key in ('old_candidate','candidate'):
            c=plan[key]
            for field in ('ci_run','artifact_id'):assert re.fullmatch('[1-9][0-9]*',c[field])
            for field in ('producer','evidence_source'):assert re.fullmatch('[a-f0-9]{40}',c[field])
            for field in ('manifest_sha256','gpu_receipt_sha256','production_sha256','source_fingerprint','artifact_zip_sha256','actual_harness_sha256'):assert re.fullmatch('[a-f0-9]{64}',c[field])
            assert type(c['unit_passed']) is int and c['unit_passed']>0 and type(c['parity_passed']) is int and c['parity_passed']>0
    verify_reachability(plan)
    verify_source_boundary()

def verify_source_boundary():
    assert set(p.git('diff','--name-only',OLD,NEW,'--','src','tests',p.BENCH,p.TESTS).splitlines())==PATHS
    assert not p.git('diff','--name-only',OLD,NEW,'--',*HARNESS_SCOPES)

def checked_inputs(folder,c):
    folder=Path(folder).resolve()
    def read(name):
        path=(folder/name).resolve();assert path.is_relative_to(folder) and path.is_file();return path.read_bytes()
    spec=importlib.util.spec_from_file_location('master_candidate_ci',p.REPO/'scripts/build-candidate-correctness.py');ci=importlib.util.module_from_spec(spec);spec.loader.exec_module(ci)
    source=c['source'];fingerprint=common.source_fingerprint(source)
    assert fingerprint==c['source_fingerprint'] and sha(read('manifest.json'))==c['manifest_sha256']
    manifest=json.loads(read('manifest.json'));assert (manifest['source'],manifest['run_id'],manifest['workflow_head'])==(source,c['ci_run'],c['producer'])
    assert manifest['classes']==dict(unit=c['unit_classes'],parity=c['parity_classes'])
    production=ci.validate_layout(manifest,read,source,fingerprint);assert production==c['production_sha256']
    assert manifest['runtime_files'][HARNESS_PATH]==c['actual_harness_sha256']
    for label,classes in [('unit',c['unit_classes']),('parity',c['parity_classes'])]:
        with tempfile.TemporaryDirectory(prefix='master-trx-') as temp:
            path=Path(temp)/'checks.trx';path.write_bytes(read('evidence/'+label+'.trx'));checks=p.verify_trx(path,classes)
        assert checks==manifest['checks'][label] and checks['passed']==c[label+'_passed']
    assert not p.git('diff','--name-only',source,c['evidence_source'],'--',*original.previous.SOURCE_IDENTITY_PATHS)
    gpu_path='benchmarks/results/shared-rollout/local-gpu-contracts/'
    receipt_raw,trx_raw=(blob(c['evidence_source'],gpu_path+n) for n in ('receipt.json','gpu.trx.gz'));assert sha(receipt_raw)==c['gpu_receipt_sha256']
    receipt=json.loads(receipt_raw);assert ci.gpu_module(p.REPO).verify_evidence(receipt,gzip.decompress(trx_raw),json.loads(blob(source,'scripts/competitor-gpu-methods.json')),fingerprint)==100
    assert receipt['build_origin']['source']==source and receipt['build_origin']['run_id']==c['ci_run'] and receipt['build_origin']['workflow_head']==c['producer'] and str(receipt['build_origin']['artifact_id'])==c['artifact_id']
    assert receipt['test_assembly_sha256']==manifest['runtime_files'][ci.ROOTS[-1]+'/OoplesFinance.StockIndicators.CompetitorTests.dll']
    from .runtime_dependencies import tensor_closure
    with tempfile.TemporaryDirectory(prefix='master-closure-') as temp:
        runtime=Path(temp);prefix=ci.ROOTS[1]+'/'
        for name,digest in manifest['runtime_files'].items():
            if name.startswith(prefix):
                path=runtime/name[len(prefix):];path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(read('blobs/'+digest))
        closure=tensor_closure(runtime,'OoplesFinance.StockIndicators.Tests.Unit.deps.json')
    assert closure['packages']['AiDotNet.Tensors']=='0.151.1'
    return dict(manifest=manifest,read=read,production=production,closure=closure,gpu_receipt=receipt_raw,gpu_trx=trx_raw)
def harness_role_alias(plan,old,new):
    """Select one authenticated baseline harness, preserving both actual identities."""
    verify_source_boundary()
    actual={role:auth['manifest']['runtime_files'][HARNESS_PATH] for role,auth in [('old',old),('successor',new)]}
    assert actual==dict(old=plan['old_candidate']['actual_harness_sha256'],successor=plan['candidate']['actual_harness_sha256'])
    assert actual['old']==plan['harness_sha256']
    trees={role:p.git('rev-parse',source+':'+p.BENCH) for role,source in [('old',OLD),('successor',NEW)]}
    assert trees['old']==trees['successor']
    return dict(policy=plan['harness_alias_policy'],actual_artifact_harness_sha256=actual,
                actual_artifact_manifest_sha256=dict(old=plan['old_candidate']['manifest_sha256'],successor=plan['candidate']['manifest_sha256']),
                common_harness_sha256=actual['old'],common_artifact_role='old',
                benchmark_source_trees=trees,
                source_scopes=HARNESS_SCOPES,scoped_source_identical=True,
                actual_artifact_binary_identical=actual['old']==actual['successor'])

def source_authentication(plan):return dict(old=plan['old_candidate'],successor=plan['candidate'],variants_alias_of='old',additional_cpu_executions=0,additional_gpu_executions=0)

def verify_harness_alias(folder,plan,proof):
    assert proof['source_authentication']==source_authentication(plan)
    for suffix in ('.trx','-parity.trx','-original-gpu-receipt.json','-original-gpu-gpu.trx.gz'):
        assert (folder/'evidence'/('old'+suffix)).read_bytes()==(folder/'evidence'/('variants'+suffix)).read_bytes()
    assert proof['production_sha256']['variants']==proof['production_sha256']['old']
    assert proof['checks']['variants']==proof['checks']['old']
    assert proof['tested_runtime_dependencies']['variants']==proof['tested_runtime_dependencies']['old']

def verify_authentication(folder,plan,proof):
    validate_scope(plan);assert proof['preparation_mode']=='authenticated-sample-shape-actual-fixture-diagnostic'
    verify_harness_alias(folder,plan,proof)
    assert p.digest(folder/'common'/original.HARNESS_ASSEMBLY)==plan['harness_sha256']
    authenticated={}
    for label,key in [('old','old_candidate'),('successor','candidate')]:
        auth=checked_inputs(folder/(label+'-authentication'),plan[key])
        authenticated[label]=auth
        assert proof['production_sha256'][label]==auth['production'] and proof['tested_runtime_dependencies'][label]==auth['closure']
        assert proof['checks'][label]==dict(auth['manifest']['checks']['unit'],original_gpu_source=plan[key]['source'])
        for suffix,raw in [('.trx',auth['read']('evidence/unit.trx')),('-parity.trx',auth['read']('evidence/parity.trx')),('-original-gpu-receipt.json',auth['gpu_receipt']),('-original-gpu-gpu.trx.gz',auth['gpu_trx'])]:assert (folder/'evidence'/(label+suffix)).read_bytes()==raw
    assert proof['harness_role_alias']==harness_role_alias(plan,authenticated['old'],authenticated['successor'])
    focused=focused_baseline(plan,authenticated['old']['manifest'])
    assert proof['supplemental_baseline_checks']=={name:sha(raw) for name,raw in focused.items()}
    for name,raw in focused.items():assert (folder/'evidence'/name).read_bytes()==raw
    reachability=verify_reachability(plan)
    assert proof['actual_lp_reachability']=={name:sha(raw) for name,raw in reachability.items()}
    for name,raw in reachability.items():assert (folder/'evidence'/('actual-lp-'+name)).read_bytes()==raw
    manifest=p.read(folder/'old-authentication/manifest.json');prefix='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/'
    assert proof['common_manifest']=={name[len(prefix):]:digest for name,digest in manifest['runtime_files'].items() if name.startswith(prefix)}

def prepare(old_candidate,candidate,out):
    plan=p.read(p.PLAN);validate_scope(plan)
    assert subprocess.check_output(['dotnet','--version'],text=True).strip()==plan['proposed_dotnet_sdk']
    old_candidate,candidate,out=map(lambda x:Path(x).resolve(),(old_candidate,candidate,out));assert not out.exists() and not out.is_relative_to(old_candidate) and not out.is_relative_to(candidate)
    old,new=checked_inputs(old_candidate,plan['old_candidate']),checked_inputs(candidate,plan['candidate'])
    assert old['closure']==new['closure']
    focused=focused_baseline(plan,old['manifest'])
    alias=harness_role_alias(plan,old,new)
    out.mkdir();(out/'common').mkdir();(out/'production').mkdir();(out/'evidence').mkdir()
    for label,folder,auth in [('old',old_candidate,old),('successor',candidate,new)]:shutil.copytree(folder,out/(label+'-authentication'))
    prefix='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/'
    for name,digest in old['manifest']['runtime_files'].items():
        if name.startswith(prefix):
            path=out/'common'/name[len(prefix):];path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(old['read']('blobs/'+digest))
    assert p.digest(out/'common'/original.HARNESS_ASSEMBLY)==plan['harness_sha256']
    checks={};production={};dependencies={}
    for label,auth,source in [('old',old,OLD),('successor',new,NEW),('variants',old,OLD)]:
        (out/'production'/(label+'.dll')).write_bytes(auth['read']('blobs/'+auth['production']))
        checks[label]=dict(auth['manifest']['checks']['unit'],original_gpu_source=source);production[label]=auth['production'];dependencies[label]=auth['closure']
        for suffix,raw in [('.trx',auth['read']('evidence/unit.trx')),('-parity.trx',auth['read']('evidence/parity.trx')),('-original-gpu-receipt.json',auth['gpu_receipt']),('-original-gpu-gpu.trx.gz',auth['gpu_trx'])]:(out/'evidence'/(label+suffix)).write_bytes(raw)
    proof=dict(plan_sha256=p.digest(p.PLAN),sources=plan['sources'],source_trees={label:p.git('rev-parse',plan['sources'][label]+':src') for label in ('old','successor','variants')},sdk=plan['proposed_dotnet_sdk'],dotnet_info=subprocess.check_output(['dotnet','--info'],text=True),workflow_head=p.git('rev-parse','HEAD'),run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],preparation_mode='authenticated-sample-shape-actual-fixture-diagnostic',source_authentication=source_authentication(plan),harness_role_alias=alias,common_manifest=p.manifest(out/'common'),evidence_manifest=p.manifest(out/'evidence'),production_sha256=production,checks=checks,tested_runtime_dependencies=dependencies)
    for name,raw in focused.items():(out/'evidence'/name).write_bytes(raw)
    proof['supplemental_baseline_checks']={name:sha(raw) for name,raw in focused.items()}
    reachability=verify_reachability(plan)
    for name,raw in reachability.items():(out/'evidence'/('actual-lp-'+name)).write_bytes(raw)
    proof['actual_lp_reachability']={name:sha(raw) for name,raw in reachability.items()}
    proof['evidence_manifest']=p.manifest(out/'evidence')
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256']=record_inventory(out,plan,p.digest(p.PLAN));p.write(out/'provenance.json',proof);p.verify_inputs(out);return proof
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


def adjudicate_feasibility(rows,decisions):
    """Launch screen only: unchanged sampling, explicit prior eligibility, no credit."""
    expected={(pair,bars) for pair in PAIRS for bars in (1000,10000)}
    indexed={(r['pair'],r['bars']):r for r in rows}
    own={(d['pair'],d['bars']):d['verdict'] for d in decisions if d['fixture']=='LibraryPairBenchmarks' and d['method']=='Ooples'}
    assert len(rows)==len(indexed)==len(decisions)==len(own)==4 and set(indexed)==set(own)==expected
    assert all(verdict in ('faster','slower','inconclusive') for verdict in own.values())
    details=[];new_wins=[];protected=[];reopened=[]
    old_counts={'0':0,'3':0};new_counts={'1':0,'2':0}
    for pair,bars in sorted(expected):
        row=indexed[pair,bars]
        assert set(row['old_rounds'])=={'0','3'} and set(row['candidate_rounds'])=={'1','2'}
        old=list(row['old_rounds'].values());new=list(row['candidate_rounds'].values());native=row['native_control']
        if bars==10000:
            for index,value in row['old_rounds'].items():old_counts[index]+=int(value['mean_ns']<native['mean_ns'])
            for index,value in row['candidate_rounds'].items():new_counts[index]+=int(value['mean_ns']<native['mean_ns'])
        prior_interval_win=all(v['upper_ns']<native['lower_ns'] for v in old)
        prior_robust_win=prior_interval_win and all(v['mean_ns']<=.9*native['mean_ns'] for v in old)
        eligible=all(v['mean_ns']>=native['mean_ns'] for v in old)
        robust_new=all(v['upper_ns']<native['lower_ns'] and v['mean_ns']<=.9*native['mean_ns'] for v in new)
        newly_won=pair in PAIRS and bars==10000 and eligible and own[pair,bars]=='faster' and robust_new
        detail=dict(source_verdict=own[pair,bars],baseline_point_native_wins={k:v['mean_ns']<native['mean_ns'] for k,v in row['old_rounds'].items()},baseline_interval_native_winner=prior_interval_win,baseline_robust_10percent_native_winner=prior_robust_win,baseline_new_win_eligible=eligible,candidate_robust_10percent_native_win=robust_new,new_10000_target_win=newly_won,**row)
        details.append(detail)
        if newly_won:new_wins.append(dict(pair=pair,bars=bars,source_verdict=own[pair,bars]))
        if prior_interval_win:
            check=dict(pair=pair,bars=bars,preserved=all(v['mean_ns']<native['mean_ns'] for v in new))
            protected.append(check)
            if not check['preserved']:reopened.append(check)
    no_slower=all(verdict!='slower' for verdict in own.values())
    net_progress=min(new_counts.values())>max(old_counts.values())
    return dict(passed=no_slower and bool(new_wins) and not reopened and net_progress,
        old_10000_point_win_counts=old_counts,candidate_10000_point_win_counts=new_counts,net_10000_point_progress=net_progress,
        all_four_source_comparisons_no_regression=no_slower,at_least_one_new_10000_target_win=bool(new_wins),new_10000_target_wins=new_wins,
        protected_prior_native_wins=protected,prior_native_win_reopenings=reopened,
        per_target_10000=[d for d in details if d['bars']==10000 and d['pair'] in PAIRS],all_pair_size_rows=details,
        eligibility_rule='Both old owned means must be >= the same round2 native mean; a prior point winner in either old round is ineligible for a new-win claim.',
        prior_win_protection_rule='Both old upper intervals below native lower => both candidate means remain strictly below native mean; checked at both sizes.',
        minimum_native_margin=.1,native_control_round=2,native_controls_per_pair_size=1,
        automatic_qualification=False,qualification=False,closure_credit=0)

def collect(folder):
    from .verify import _collect
    original.verify_runtime(folder,p.read(p.PLAN)['expected_runtime_version'])
    result=_collect(Path(folder),'diagnostic',preflight=False,diagnostic=True)
    assert not result['accepted'] and (result['case_count'],result['decision_count'])==(20,4)
    result.update(frozen_baseline_feasibility_only=True,current_combined_source_qualified=False,followup_requires_current_published_baseline=True,diagnostic_only=True,qualification=False,production_integrated=False,report_imported=False,policy_changed=False,closure_ledger_new_rows=0,automatic_qualification=False,feasibility_screen=adjudicate_feasibility(native_rows(folder),result['decisions']))
    return result

FOCUSED_RECEIPT_SHA='5830afd1df57bbfd3d7e3ce34a519fa3f36c0f61321dca58d80bcfd35011892e'
def focused_baseline(plan,manifest=None):
    root=p.REPO/'benchmarks/plans/evidence/sample-shape-actual-fixture-20261118'
    prefix='baseline-focused-'
    files={name:(root/(prefix+name)).read_bytes() for name in ('receipt.json','unit.trx','parity.trx')}
    assert sha(files['receipt.json'])==FOCUSED_RECEIPT_SHA
    receipt=json.loads(files['receipt.json']);candidate=plan['old_candidate']
    assert receipt['status']=='actual-supplemental-baseline-focused-checks-passed'
    for field in ('source','source_fingerprint','ci_run','manifest_sha256','actual_harness_sha256'):
        assert receipt[field]==candidate[field]
    assert receipt['core_sha256']==candidate['production_sha256']
    assert receipt['verified_runtime_entries']==444 and receipt['new_builds']==receipt['new_measurements']==0
    assert receipt['original_manifest_unchanged'] is True
    for category,classes,count,assembly in [
        ('unit',['SharedSampleShapeTests'],9,'tests/bin/Release/net10.0/OoplesFinance.StockIndicators.Tests.Unit.dll'),
        ('parity',['SampleShapeComparisonTests'],11,'benchmarks/OoplesFinance.StockIndicators.CompetitorTests/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorTests.dll')]:
        entry=receipt['checks'][category]
        checks=p.verify_trx(root/(prefix+category+'.trx'),classes)
        assert checks==dict(passed=count,classes=entry['classes'],sha256=entry['trx_sha256'])
        assert entry['passed']==count and sum(checks['classes'].values())==count
        if manifest is not None:assert manifest['runtime_files'][assembly]==entry['assembly_sha256']
    return {'baseline-focused-'+name:raw for name,raw in files.items()}

REACHABILITY={'evidence_root': 'benchmarks/plans/evidence/sample-shape-actual-fixture-20261118', 'files': {'actual-lp-setup-proof.json': '835d3861b93fd8d98175cd6c4a8c9abbad4abf0a76f2aaff53b3fc257affe9fd', 'allocation-baseline.json': '30076816ffa0449401f57c7cfdd361aaedecdb8a0ed6c78f5c369aab8617a82d', 'allocation-candidate.json': '28b30788d2242e2316ba9617d52cd5c28c828e5346c38e30dc93e924e30ed441', 'allocation-caller-metadata.json': '00b045036a12c07b9aae3b6db951769d9a0cb86a834584e04546f3fef4e7163f', 'allocation-root-review.json': '2c52df618dc2a3be622cdb3a7ae5636313ac8708a278f1913eb397fe61001f20', 'candidate-cpu-root-review.json': 'ee1ca311961485ed2d33d0b0e56d0514a41a8d981bcdfa7694b550f755ecf944', 'candidate-gpu-root-review.json': '95fd7c0f550eca41d5945b81639f9f7bb89f6adf25109cec6566555e3c481f2a'}, 'allocation_is_timing_evidence': False, 'native_credit': 0, 'rejected_wrong_fixture_screen_run': '44386077377', 'reuse_old_screen_results': False}

def verify_reachability(plan):
    """Bind actual LP inputs and observed allocation effect; never infer timing."""
    assert plan['actual_lp_reachability']==REACHABILITY
    root=p.REPO/REACHABILITY['evidence_root']
    files={name:(root/name).read_bytes() for name in REACHABILITY['files']}
    assert {name:sha(raw) for name,raw in files.items()}==REACHABILITY['files']
    data={name:json.loads(raw) for name,raw in files.items()}
    setup=data['actual-lp-setup-proof.json']
    actual=[row for row in setup['rows'] if row['pair'] in PAIRS]
    indexed={(row['pair'],row['bars']):row for row in actual}
    expected={(pair,bars) for pair in PAIRS for bars in (1000,10000)}
    assert len(actual)==len(indexed)==4 and set(indexed)==expected
    for (pair,bars),row in indexed.items():
        assert row['period']==20 and row['grid']==-4 and row['actual_custom_fixture_proven'] is True
        assert row['maximum_integer']==(1318 if bars==1000 else 10318)
        assert row['proposed_whole_region_guard'] is True
    for path in ('LibraryComparisons/LibraryPairBenchmarks.cs','LibraryComparisons/ComparisonVerifier.cs','LibraryComparisons/SampleShapeComparison.cs','CompetitorData.cs'):
        identities={p.git('rev-parse',source+':'+p.BENCH+'/'+path) for source in (OLD,NEW,setup['source'])}
        assert len(identities)==1,'Actual factory source must match both timed inputs'
    caller=data['allocation-caller-metadata.json'];review=data['allocation-root-review.json']
    assert caller['status']=='completed-not-timing-or-qualification' and caller['host_clr']=='.NET 10.0.15'
    assert caller['observations_per_case']==1 and caller['timing_samples']==caller['new_builds']==caller['new_profiles']==0
    assert caller['automatic_retry'] is False
    assert review['status']=='root-independent-allocation-reachability-review-passed'
    assert review['caller_metadata_sha256']==sha(files['allocation-caller-metadata.json']) and review['source']==NEW
    assert review['verified_installed_runtime_files']==200 and review['timing_samples']==review['new_measurements']==0
    assert review['qualified'] is False and review['native_credit']==0
    rows={}
    for role,key in [('baseline','old_candidate'),('candidate','candidate')]:
        raw=files['allocation-'+role+'.json'];entry=data['allocation-'+role+'.json'];identity=plan[key]
        assert entry['source_fingerprint']==identity['source_fingerprint'] and entry['core_sha256']==identity['production_sha256']
        assert entry['harness_sha256']==identity['actual_harness_sha256'] and entry['allocation_observations_per_case']==1
        assert entry['timing_samples']==entry['new_builds']==entry['new_profiles']==0
        meta=caller['roles'][role]
        assert meta['benchmark_closure_before']==meta['benchmark_closure_after']==100 and meta['status']=='completed'
        assert meta['source']==identity['source'] and meta['source_fingerprint']==identity['source_fingerprint']
        assert meta['manifest_sha256']==identity['manifest_sha256'] and meta['output_sha256']==sha(raw)
        rows[role]={(row['pair'],row['bars']):row for row in entry['rows']}
        assert len(entry['rows'])==len(rows[role])==4 and set(rows[role])==expected
    assert data['allocation-baseline.json']['compiled_kernel_IL_sha256'] is None
    assert data['allocation-candidate.json']['compiled_kernel_IL_sha256']=='6683f3c23ac2c1be0565ae4f67246d4bdd43d867b7dfde6518ec3a440e09b03f'
    for pair,bars in expected:
        before,after=rows['baseline'][pair,bars],rows['candidate'][pair,bars]
        assert before['period']==after['period']==20 and before['workers']==after['workers']==1
        assert before['fully_returned_output_sha256']==after['fully_returned_output_sha256']
        assert after['compiled_kernel_present'] is True and before['compiled_kernel_present'] is False
        assert after['region_gate_admitted'] is (bars==10000)
        if bars==1000:assert before['allocated_process_bytes']==after['allocated_process_bytes']
        else:assert after['allocated_process_bytes']<before['allocated_process_bytes']
    cpu=data['candidate-cpu-root-review.json'];gpu=data['candidate-gpu-root-review.json']
    assert cpu['source']==gpu['source']==NEW and cpu['source_fingerprint']==gpu['source_fingerprint']==plan['candidate']['source_fingerprint']
    assert (cpu['unit'],cpu['parity'])==(30,11) and gpu['passed_cases']==100 and gpu['methods']==12
    assert gpu['receipt_sha256']==plan['candidate']['gpu_receipt_sha256']
    assert gpu['cpu_root_review_sha256']==sha(files['candidate-cpu-root-review.json'])
    return files
