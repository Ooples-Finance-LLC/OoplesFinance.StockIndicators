"""Pinned ALMA exact-grid three-pair feasibility diagnostic; never source qualification or closure credit."""
import gzip,hashlib,importlib.util,json,os,re,shutil,subprocess,tempfile
from pathlib import Path
from . import pipeline as p
from . import candle_single_pass_roi as common
from . import triple_body_macro_qualification as original
SCOPE='alma-grid-native-roi-20261119'
KIND='alma-grid-native-pilot'
OLD='d3d5c905a5d75b666b440aa6c5f6b4c24b42dc91'
NEW='1500d6b72d776c7f129dc191d8a44bd4a89fe8be'
TARGETS=['QuanTAlib.Alma','Skender.GetAlma']
PAIRS=TARGETS+['QuanTAlib.Gma']
HARNESS_PATH='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/'+original.HARNESS_ASSEMBLY
HARNESS_SCOPES=[p.BENCH,p.TESTS,'Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.Config','nuget.config']
BASE_UNIT=['AnalyticGridBatchTests','SharedWeightedWindowTests','FibonacciWeightedAverageTests','AffineConvolutionTests','WindowedSincCompactTests']
BASE_PARITY=['AnalyticKernelComparisonTests','FixedWeightedComparisonTests','ConvolutionComparisonTests','SincBuilderComparisonTests']
UNIT=['AlmaGridBatchTests','SharedAlmaWindowTests','AnalyticGridBatchTests']
PARITY=['AlmaComparisonTests','AnalyticKernelComparisonTests']
PATHS={'src/Indicators/ArnaudLegouxWindow.cs','src/Indicators/ValuesBarExecution.AlmaGrid.cs','src/Indicators/ValuesBarExecution.BoundedWindows.cs','tests/ModelsTests/AlmaGridBatchTests.cs'}
sha=lambda raw:hashlib.sha256(raw).hexdigest()
def blob(source,path):return subprocess.check_output(['git','show',source+':'+path],cwd=p.REPO)

def shards():
    task=dict(fixture='LibraryPairBenchmarks',operations=[dict(raw=x,pair=x) for x in PAIRS],methods=['Ooples','Competitor'],implicit_pair=False,cases=30,decisions=6)
    return [dict(index=0,tasks=[task],cases=30,decisions=6)]

def validate_scope(plan,*,require_ready=True):
    assert plan['diagnostic_kind']==KIND and plan['diagnostic_only'] is True
    assert plan['sources']==dict(published=OLD,old=OLD,successor=NEW,variants=OLD)
    assert plan['gpu_sources']==dict(old=OLD,successor=NEW,variants=OLD)
    assert (plan['cases'],plan['decisions'])==(30,6)
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
    assert plan['future_builder_fixture']==['SharedAlmaWindowBenchmarks']
    assert plan['harness_alias_policy']==dict(common_artifact_role='old',variants_artifact_role='old',baseline_source=OLD,candidate_source=NEW,require_scoped_source_identity=True,claim_artifact_binary_identity=False)
    assert not any(plan.get(k) for k in ('targeted_reference','coverage_reference','standalone_variants','qualify_variants','trix_preflight','reuse_inputs'))
    for label,key,source in [('old','old_candidate',OLD),('successor','candidate',NEW),('variants','old_candidate',OLD)]:
        c=plan[key];assert c['source']==source and plan['cpu_test_classes'][label]==c['unit_classes']
        for category in ('unit','parity'):
            values=c[category+'_classes'];assert values and len(set(values))==len(values) and all(v.isidentifier() for v in values)
    assert plan['old_candidate']['unit_classes']==BASE_UNIT and plan['old_candidate']['parity_classes']==BASE_PARITY
    assert plan['candidate']['unit_classes']==UNIT and plan['candidate']['parity_classes']==PARITY
    assert (plan['old_candidate']['unit_passed'],plan['old_candidate']['parity_passed'])==(53,48)
    assert (plan['candidate']['unit_passed'],plan['candidate']['parity_passed'])==(49,31)
    assert plan['candidate']['ci_run']=='44447015919' and plan['candidate']['producer']=='ce6d1a0373166c42583278e4d7efd38f65e0934d'
    assert plan['native_progress_gate']==NATIVE_POLICY
    verify_native_policy(plan)
    if require_ready:
        assert plan['inputs_ready'] is True and plan['parent_review_approved'] is True,'UNBOUND actual receipts or parent review'
        focused_baseline(plan)
        verify_compiled_fixture_path(plan)
        verify_actual_input(plan)
        verify_published_baseline(plan)
        assert re.fullmatch('[a-f0-9]{40}',NEW)
        assert 'UNBOUND_UNIT_CLASSES' not in UNIT and 'UNBOUND_PARITY_CLASSES' not in PARITY
        assert re.fullmatch('[a-f0-9]{64}',plan['harness_sha256'])
        for key in ('old_candidate','candidate'):
            c=plan[key]
            for field in ('ci_run','artifact_id'):assert re.fullmatch('[1-9][0-9]*',c[field])
            for field in ('producer','evidence_source'):assert re.fullmatch('[a-f0-9]{40}',c[field])
            for field in ('manifest_sha256','gpu_receipt_sha256','production_sha256','source_fingerprint','artifact_zip_sha256','actual_harness_sha256'):assert re.fullmatch('[a-f0-9]{64}',c[field])
            assert type(c['unit_passed']) is int and c['unit_passed']>0 and type(c['parity_passed']) is int and c['parity_passed']>0
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
    validate_scope(plan);verify_publication_provenance(plan,proof);assert proof['preparation_mode']=='authenticated-alma-grid-diagnostic'
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
    assert proof['actual_input_provenance']==plan['actual_input_provenance']
    focused=focused_baseline(plan,authenticated['old']['manifest'])
    assert proof['supplemental_baseline_checks']=={name:sha(raw) for name,raw in focused.items()}
    for name,raw in focused.items():assert (folder/'evidence'/name).read_bytes()==raw
    manifest=p.read(folder/'old-authentication/manifest.json');prefix='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/'
    assert proof['common_manifest']=={name[len(prefix):]:digest for name,digest in manifest['runtime_files'].items() if name.startswith(prefix)}

def prepare(old_candidate,candidate,out):
    plan=p.read(p.PLAN);validate_scope(plan)
    published_baseline=verify_current_publication(plan)
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
    proof=dict(plan_sha256=p.digest(p.PLAN),sources=plan['sources'],source_trees={label:p.git('rev-parse',plan['sources'][label]+':src') for label in ('old','successor','variants')},sdk=plan['proposed_dotnet_sdk'],dotnet_info=subprocess.check_output(['dotnet','--info'],text=True),workflow_head=p.git('rev-parse','HEAD'),run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],preparation_mode='authenticated-alma-grid-diagnostic',source_authentication=source_authentication(plan),harness_role_alias=alias,common_manifest=p.manifest(out/'common'),evidence_manifest=p.manifest(out/'evidence'),production_sha256=production,checks=checks,tested_runtime_dependencies=dependencies)
    for name,raw in focused.items():(out/'evidence'/name).write_bytes(raw)
    proof['published_baseline']=published_baseline
    proof['actual_input_provenance']=plan['actual_input_provenance']
    proof['supplemental_baseline_checks']={name:sha(raw) for name,raw in focused.items()}
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
    """Native gate under unchanged sampling and prior eligibility; no automatic credit."""
    expected={(pair,bars) for pair in PAIRS for bars in (1000,10000)}
    indexed={(r['pair'],r['bars']):r for r in rows}
    own={(d['pair'],d['bars']):d['verdict'] for d in decisions if d['fixture']=='LibraryPairBenchmarks' and d['method']=='Ooples'}
    assert len(rows)==len(indexed)==len(own)==6 and len(decisions)==6 and set(indexed)==set(own)==expected
    assert all(d['verdict'] in ('faster','slower','inconclusive') for d in decisions)
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
        prior_statistical_win=prior_interval_win
        eligible=all(v['mean_ns']>=native['mean_ns'] for v in old)
        statistical_new=all(v['upper_ns']<native['lower_ns'] for v in new)
        newly_won=pair in TARGETS and bars==10000 and eligible and own[pair,bars]=='faster' and statistical_new
        detail=dict(source_verdict=own[pair,bars],baseline_point_native_wins={k:v['mean_ns']<native['mean_ns'] for k,v in row['old_rounds'].items()},baseline_interval_native_winner=prior_interval_win,baseline_statistical_native_winner=prior_statistical_win,baseline_new_win_eligible=eligible,candidate_statistical_native_win=statistical_new,new_10000_target_win=newly_won,**row)
        details.append(detail)
        if newly_won:new_wins.append(dict(pair=pair,bars=bars,source_verdict=own[pair,bars]))
        if prior_interval_win:
            check=dict(pair=pair,bars=bars,preserved=all(v['mean_ns']<native['mean_ns'] for v in new))
            protected.append(check)
            if not check['preserved']:reopened.append(check)
    no_slower=all(d['verdict']!='slower' for d in decisions)
    net_progress=min(new_counts.values())>max(old_counts.values())
    return dict(passed=no_slower and bool(new_wins) and not reopened and net_progress,
        old_10000_point_win_counts=old_counts,candidate_10000_point_win_counts=new_counts,net_10000_point_progress=net_progress,
        all_six_source_comparisons_no_regression=no_slower,at_least_one_new_10000_target_win=bool(new_wins),new_10000_target_wins=new_wins,
        protected_prior_native_wins=protected,prior_native_win_reopenings=reopened,
        per_target_10000=[d for d in details if d['bars']==10000 and d['pair'] in TARGETS],all_pair_size_rows=details,
        eligibility_rule='Both old owned means must be >= the same round2 native mean; a prior point winner in either old round is ineligible for a new-win claim.',
        prior_win_protection_rule='Both old upper intervals below native lower => both candidate means remain strictly below native mean; checked at both sizes.',
        minimum_native_margin=None,native_policy_id=POLICY_APPROVAL['policy_id'],native_policy_receipt_sha256=POLICY_APPROVAL['sha256'],native_control_round=2,native_controls_per_pair_size=1,
        automatic_qualification=False,qualification=False,closure_credit=0)

def collect(folder):
    from .verify import _collect
    original.verify_runtime(folder,p.read(p.PLAN)['expected_runtime_version'])
    result=_collect(Path(folder),'diagnostic',preflight=False,diagnostic=True)
    assert not result['accepted'] and (result['case_count'],result['decision_count'])==(30,6)
    result.update(frozen_baseline_feasibility_only=True,current_combined_source_qualified=False,followup_requires_current_published_baseline=True,diagnostic_only=True,qualification=False,production_integrated=False,report_imported=False,policy_changed=False,closure_ledger_new_rows=0,automatic_qualification=False,feasibility_screen=adjudicate_feasibility(native_rows(folder),result['decisions']))
    return result

def evidence_entry(entry):
    assert isinstance(entry,dict) and set(entry)=={'path','sha256'},'UNBOUND source evidence'
    assert entry['path'].startswith('benchmarks/plans/evidence/') and '..' not in Path(entry['path']).parts
    raw=(p.REPO/entry['path']).read_bytes();assert sha(raw)==entry['sha256'];return raw

def focused_baseline(plan,manifest=None):
    # Reuse authenticated original baseline checks honestly; no supplemental test claim.
    root=json.loads(evidence_entry(plan['baseline_cpu_root_review']))
    assert root['status']=='root-independent-cpu-authentication-passed' and root['source']==OLD
    c=plan['old_candidate']
    for a,b in [('run_id','ci_run'),('source_fingerprint','source_fingerprint'),('manifest_sha256','manifest_sha256'),('core_sha256','production_sha256'),('actual_harness_sha256','actual_harness_sha256')]:assert root[a]==c[b]
    assert root['unit']==53 and root['parity']==48
    return {}

def compiled_proof(plan):
    raw=evidence_entry(plan['compiled_fixture_path_proof']);bundle=json.loads(gzip.decompress(raw))
    review=json.loads(evidence_entry(plan['compiled_proof_root_review']))
    assert review['status']=='root-independent-compiled-feasibility-proof-authenticated' and review['independent_authentication_passed'] is True
    assert review['source']==NEW and review['baseline_source']==OLD
    cpu=json.loads(evidence_entry(plan['candidate_cpu_root_review']));gpu=json.loads(evidence_entry(plan['candidate_gpu_root_review']))
    assert review['cpu_root_review_sha256']==plan['candidate_cpu_root_review']['sha256'] and review['gpu_root_review_sha256']==plan['candidate_gpu_root_review']['sha256']
    assert cpu['status']=='root-independent-cpu-authentication-passed' and cpu['source']==NEW and cpu['unit']==49 and cpu['parity']==31 and cpu['gpu_executed'] is False
    c=plan['candidate']
    for a,b in [('run_id','ci_run'),('source_fingerprint','source_fingerprint'),('manifest_sha256','manifest_sha256'),('core_sha256','production_sha256'),('actual_harness_sha256','actual_harness_sha256')]:assert cpu[a]==c[b]
    assert gpu['status']=='root-independent-actual-gpu-authentication-passed' and gpu['source']==NEW and gpu['run_id']==c['ci_run'] and gpu['passed_cases']==100 and gpu['methods']==12
    assert gpu['receipt_sha256']==c['gpu_receipt_sha256']
    receipt=bundle['inspection-receipt.json'];assert sha(receipt.encode())==review['inspection_receipt_sha256']
    receipt=json.loads(receipt)
    assert receipt['builds']==receipt['timing_samples']==receipt['new_gpu_runs']==0 and len(receipt['processes'])==4
    for name,pin in receipt['files'].items():assert sha(bundle[name].encode())==pin
    return dict(bundle=bundle,review=review)

def verify_compiled_fixture_path(plan):
    proof=compiled_proof(plan);review=proof['review'];bundle=proof['bundle']
    assert len(review['pairs'])==2 and {row['pair'] for row in review['pairs']}==set(TARGETS)
    for pair in review['pairs']:
        assert pair['tensor_fma_ymm_observed'] is True and pair['simd_calls']==2
        assert pair['actual_private_batch_mentions']>0 and pair['full_outputs_and_inputs_equal_both_sizes'] is True
        protected=pair['protected_methods'];assert len(protected)==3
        assert sum(x['raw_locals_equal'] is False for x in protected)==1
        assert all(x['normalized_il_equal'] and x['maxstack_equal'] for x in protected)
        for role in ('baseline','candidate'):
            observed=json.loads(bundle[role+'-'+pair['pair']+'-proof.json'])
            assert observed['actual_clr']=='10.0.15' and observed['cpu_engine']=='AiDotNet.Tensors.Engines.CpuEngine'
            assert observed['default_worker_setting_unchanged'] is True
    assert plan['fixture_contracts']==dict(LibraryPairBenchmarks=dict(pair_parameter='PairId',size_parameter='Bars',sizes=[1000,10000],methods=['Ooples','Competitor']))
    verify_source_fixture_routes()

def verify_actual_input(plan):
    assert plan['actual_input_provenance']==plan['compiled_fixture_path_proof']
    proof=compiled_proof(plan)
    for pair in TARGETS:
        a,b=[json.loads(proof['bundle'][role+'-'+pair+'-proof.json'])['reachability'] for role in ('baseline','candidate')]
        assert a==b and len(a)==2 and {r['bars'] for r in a}=={1000,10000}
        for row in a:
            assert row['pair']==pair and row['lp_period']==20 and row['present_is_null'] is False and row['first_valid']==0
            assert row['actual_indicator']=='ArnaudLegouxWindow' and row['first_present']==(19 if pair=='Skender.GetAlma' else 0)
            assert set(row['actual_data_bits'])=={'Opens','Highs','Lows','Closes','Volumes'}
            for key in ('full_returned_output_sha256','presence_sha256','full_input_bar_sha256'):assert re.fullmatch('[a-f0-9]{64}',row[key])

def verify_source_fixture_routes():
    for source in (OLD,NEW):
        alma=blob(source,p.BENCH+'/LibraryComparisons/AlmaComparison.cs').decode()
        assert 'new ArnaudLegouxWindow(period, offset, sigma, fullWindow)' in alma
        assert 'skender ? "Skender.GetAlma" : "QuanTAlib.Alma"' in alma
        bounded=blob(source,'src/Indicators/ValuesBarExecution.BoundedWindows.cs').decode()
        assert 'FillBoundedWindow<AlmaKernel>' in bounded
    assert blob(OLD,'src/Indicators/ValuesBarExecution.AnalyticGrid.cs')==blob(NEW,'src/Indicators/ValuesBarExecution.AnalyticGrid.cs')

POLICY_APPROVAL=dict(path='benchmarks/plans/evidence/native-win-rule-user-approved-20261118.json',
    sha256='414b558eae1deda750c3d169020128986a0e0db9b2acd95f23da035be03bfd7c',
    policy_id='native-separated-confidence-intervals-both-rounds-20261118')

def verify_native_policy(plan):
    assert plan['native_win_policy_approval']==POLICY_APPROVAL
    raw=(p.REPO/POLICY_APPROVAL['path']).read_bytes();assert sha(raw)==POLICY_APPROVAL['sha256']
    receipt=json.loads(raw)
    assert receipt['status']=='user-approved-native-win-rule' and receipt['policy_id']==POLICY_APPROVAL['policy_id']
    assert receipt['removed_only']=='minimum10percent native mean margin in every round'
    assert receipt['original_frozen_plans_and_results_must_remain_unchanged'] is True

def verify_published_baseline(plan):
    """Validate the pinned historical source using immutable Git objects only."""
    binding=plan['published_baseline']
    assert binding['pull_request']==266 and binding['branch']=='perf/remaining-indicator-rollout'
    assert binding['source_fingerprint']==plan['old_candidate']['source_fingerprint']
    assert re.fullmatch('[a-f0-9]{40}',binding['head'])
    assert common.source_fingerprint(binding['head'])==binding['source_fingerprint']
    assert not p.git('diff','--name-only',OLD,binding['head'],'--','src','tests',*HARNESS_SCOPES),'Published source differs from authenticated d3'

def verify_current_publication(plan):
    """Require the current PR binding only when launching or preparing inputs."""
    verify_published_baseline(plan);binding=plan['published_baseline']
    actual=json.loads(subprocess.check_output(['gh','api','repos/Ooples-Finance-LLC/OoplesFinance.StockIndicators/pulls/266'],cwd=p.REPO))
    captured=dict(pull_request=binding['pull_request'],head=actual['head']['sha'],branch=actual['head']['ref'],source_fingerprint=binding['source_fingerprint'])
    assert captured==binding,'Published PR head changed'
    return captured

def verify_publication_provenance(plan,proof):
    """Frozen preparation evidence remains valid after the live PR advances."""
    verify_published_baseline(plan)
    assert proof['published_baseline']==plan['published_baseline'],'Captured publication binding mismatch'


NATIVE_POLICY=dict(fixture='LibraryPairBenchmarks',method='Ooples',native_method='Competitor',native_round=2,old_rounds=[0,3],candidate_rounds=[1,2],minimum_margin=None,require_separated_intervals_both_rounds=True,new_closure_bars=10000,minimum_new_closures=1,require_own_source_faster=True,require_net_10000_point_progress=True,protect_prior_confident_wins_both_sizes=True,target_pairs=TARGETS)
