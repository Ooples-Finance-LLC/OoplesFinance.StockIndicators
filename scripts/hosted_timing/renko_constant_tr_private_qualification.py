"""Pinned full-return Renko source qualification with exact artifact reuse and native progress gate."""
import gzip,hashlib,importlib.util,json,os,re,shutil,subprocess,tempfile
from pathlib import Path
from . import pipeline as p
from . import candle_single_pass_roi as common
from . import triple_body_macro_qualification as original
SCOPE='renko-constant-tr-private-qualification-20261118'
KIND='renko-constant-tr-private-qualification'
OLD='d9d00ca56ebb857b87ead45044616970b607019c'
NEW='20e0e93445bd83bf467575938c705b5f485b67c5'
PAIRS=['Skender.GetRenko','Skender.GetRenkoAtr']
HARNESS_PATH='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/'+original.HARNESS_ASSEMBLY
HARNESS_SCOPES=[p.BENCH,p.TESTS,'Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.Config','nuget.config']
BASE_UNIT=['RenkoPriceGridTests','RenkoPriceNormalizationTests','RenkoCompactVolumeTests','RenkoNormalizedGridTests']
BASE_PARITY=['RenkoComparisonTests']
UNIT=['RenkoPriceGridTests','RenkoPriceNormalizationTests','RenkoCompactVolumeTests','RenkoNormalizedGridTests','RenkoConstantAtrTests']
PARITY=['RenkoComparisonTests']
PATHS={'src/Indicators/RenkoSnapshot.cs','tests/ModelsTests/RenkoConstantAtrTests.cs'}
sha=lambda raw:hashlib.sha256(raw).hexdigest()
def blob(source,path):return subprocess.check_output(['git','show',source+':'+path],cwd=p.REPO)

def shards():
    def task(fixture,methods,cases,decisions):
        return dict(fixture=fixture,operations=[dict(raw=x,pair=x) for x in PAIRS],methods=methods,implicit_pair=False,cases=cases,decisions=decisions)
    return [dict(index=0,tasks=[task('LibraryPairBenchmarks',['Ooples','Competitor'],20,4),
        task('SharedSkenderRenkoChartNativeBenchmarks',['Snapshot','NativeSingle','NativeParallel'],24,4)],cases=44,decisions=8)]

def validate_scope(plan,*,require_ready=True):
    assert plan['renko_constant_tr_private_qualification'] is True
    assert not plan.get('diagnostic_kind') and not plan.get('diagnostic_only')
    assert plan['sources']==dict(published=OLD,old=OLD,successor=NEW,variants=OLD)
    assert plan['gpu_sources']==dict(old=OLD,successor=NEW,variants=OLD)
    assert (plan['cases'],plan['decisions'])==(44,8)
    assert plan['matrix']==dict(shards=1,max_parallel=1,timeout_minutes_per_shard=120)
    assert plan['targeted_shards']==shards()
    assert plan['targeted_reference']==dict(scope=SCOPE)
    assert plan['measurement_policy']==common.POLICY and plan['runtime_policy_overrides']=={}
    assert plan['runtime_dependency_alignment']==dict(tensor_version='0.151.1')
    assert plan['require_fixture_inventory'] is True and plan['proposed_dotnet_sdk']=='10.0.403' and plan['expected_runtime_version']=='.NET 10.0.15'
    assert plan['closure_ledger_new_rows']==0 and plan['automatic_qualification'] is False
    assert plan['require_net_10000_point_progress'] is True
    assert plan['native_progress_gate']==NATIVE_POLICY
    verify_native_policy(plan)
    verify_screen(plan)
    assert plan['harness_alias_policy']==dict(common_artifact_role='old',variants_artifact_role='old',baseline_source=OLD,candidate_source=NEW,require_scoped_source_identity=True,claim_artifact_binary_identity=False)
    assert not any(plan.get(k) for k in ('coverage_reference','standalone_variants','qualify_variants','trix_preflight','reuse_inputs','diagnostic_shards'))
    for label,key,source in [('old','old_candidate',OLD),('successor','candidate',NEW),('variants','old_candidate',OLD)]:
        c=plan[key];assert c['source']==source and plan['cpu_test_classes'][label]==c['unit_classes']
        for category in ('unit','parity'):
            values=c[category+'_classes'];assert values and len(set(values))==len(values) and all(v.isidentifier() for v in values)
    assert plan['old_candidate']['unit_classes']==BASE_UNIT and plan['old_candidate']['parity_classes']==BASE_PARITY
    assert plan['candidate']['unit_classes']==UNIT and plan['candidate']['parity_classes']==PARITY
    assert plan['candidate']['ci_run']=='44403794727' and plan['candidate']['producer']=='ce6d1a0373166c42583278e4d7efd38f65e0934d'
    focused_baseline(plan)
    if require_ready:
        assert plan['inputs_ready'] is True and plan['parent_review_approved'] is True,'UNBOUND actual receipts or parent review'
        verify_published_baseline(plan)
        assert plan['screen_gate']['independently_authenticated'] is True
        assert re.fullmatch('[a-f0-9]{40}',NEW)
        assert 'UNBOUND_UNIT_CLASSES' not in UNIT and 'UNBOUND_PARITY_CLASSES' not in PARITY
        assert re.fullmatch('[a-f0-9]{64}',plan['harness_sha256'])
        for key in ('old_candidate','candidate'):
            c=plan[key]
            for field in ('ci_run','artifact_id'):assert re.fullmatch('[1-9][0-9]*',c[field])
            for field in ('producer','evidence_source'):assert re.fullmatch('[a-f0-9]{40}',c[field])
            for field in ('manifest_sha256','gpu_receipt_sha256','production_sha256','source_fingerprint','artifact_zip_sha256','actual_harness_sha256'):assert re.fullmatch('[a-f0-9]{64}',c[field])
            assert type(c['unit_passed']) is int and c['unit_passed']>0 and type(c['parity_passed']) is int and c['parity_passed']>0
    verify_actual_input(plan)
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
    validate_scope(plan);assert proof['preparation_mode']=='authenticated-renko-constant-tr-private-qualification'
    verify_publication_provenance(plan,proof)
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
    proof=dict(plan_sha256=p.digest(p.PLAN),sources=plan['sources'],source_trees={label:p.git('rev-parse',plan['sources'][label]+':src') for label in ('old','successor','variants')},sdk=plan['proposed_dotnet_sdk'],dotnet_info=subprocess.check_output(['dotnet','--info'],text=True),workflow_head=p.git('rev-parse','HEAD'),run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],preparation_mode='authenticated-renko-constant-tr-private-qualification',source_authentication=source_authentication(plan),harness_role_alias=alias,common_manifest=p.manifest(out/'common'),evidence_manifest=p.manifest(out/'evidence'),production_sha256=production,checks=checks,tested_runtime_dependencies=dependencies)
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
        prior_statistical_win=prior_interval_win
        eligible=all(v['mean_ns']>=native['mean_ns'] for v in old)
        statistical_new=all(v['upper_ns']<native['lower_ns'] for v in new)
        newly_won=pair in PAIRS and bars==10000 and eligible and own[pair,bars]=='faster' and statistical_new
        detail=dict(source_verdict=own[pair,bars],baseline_point_native_wins={k:v['mean_ns']<native['mean_ns'] for k,v in row['old_rounds'].items()},baseline_interval_native_winner=prior_interval_win,baseline_statistical_native_winner=prior_statistical_win,baseline_new_win_eligible=eligible,candidate_statistical_native_win=statistical_new,new_10000_target_win=newly_won,**row)
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
        minimum_native_margin=None,native_policy_id=POLICY_APPROVAL['policy_id'],native_policy_receipt_sha256=POLICY_APPROVAL['sha256'],native_control_round=2,native_controls_per_pair_size=1,
        automatic_qualification=False,qualification=False,closure_credit=0)

def collect(folder):
    from .verify import _collect
    original.verify_runtime(folder,p.read(p.PLAN)['expected_runtime_version'])
    result=_collect(Path(folder),'targeted',preflight=False,renko_gate=True)
    assert (result['case_count'],result['decision_count'])==(44,8)
    lp=[decision for decision in result['decisions'] if decision['fixture']=='LibraryPairBenchmarks' and decision['method']=='Ooples']
    native=adjudicate_feasibility(native_rows(folder),lp)
    source=result['accepted'] and all(decision['verdict']!='slower' for decision in result['decisions'])
    result.update(source_gate_accepted=source,native_progress_gate_passed=native['passed'],native_progress_gate=native,
        accepted=source and native['passed'],qualification=True,production_integrated=False,report_imported=False)
    return result

def focused_baseline(plan,manifest=None):
    # The accepted d9 artifact already contains the complete focused Renko
    # cohort. Reuse it directly; C5 supplemental executions do not apply.
    candidate=plan['old_candidate']
    assert candidate['source']==OLD and candidate['unit_classes']==BASE_UNIT and candidate['parity_classes']==BASE_PARITY
    assert (candidate['unit_passed'],candidate['parity_passed'])==(55,8)
    if manifest is not None:
        assert manifest['classes']==dict(unit=BASE_UNIT,parity=BASE_PARITY)
        assert (manifest['checks']['unit']['passed'],manifest['checks']['parity']['passed'])==(55,8)
    return {}

ACTUAL_INPUT_PROVENANCE={'path': 'benchmarks/plans/evidence/renko-normalized-actual-lp-setup-proof-20261118.json', 'sha256': '835d3861b93fd8d98175cd6c4a8c9abbad4abf0a76f2aaff53b3fc257affe9fd', 'actual_setup_export_sha256': '57f4d67aee400fbdce65f886cecd8ebb82a093aa90552ef85019795c636eddf4', 'source': '409c5f5eea78724f83c999469b76bacbbb193911', 'harness_sha256': '148921163c52c848959ed8fff76eacff0b49e93743ee2a7f0671ed84c0280f49'}

def verify_actual_input(plan):
    assert plan['actual_input_provenance']==ACTUAL_INPUT_PROVENANCE
    entry=plan['actual_input_provenance'];raw=(p.REPO/entry['path']).read_bytes()
    assert sha(raw)==entry['sha256'];proof=json.loads(raw)
    for key in ('source','harness_sha256','actual_setup_export_sha256'):assert proof[key]==entry[key]
    assert proof['new_builds']==proof['new_profiles']==proof['new_timings']==0
    rows=[row for row in proof['rows'] if row['pair'] in PAIRS]
    assert len(rows)==4 and {(row['pair'],row['bars']) for row in rows}=={(pair,bars) for pair in PAIRS for bars in (1000,10000)}
    assert all(row['period']==20 and row['actual_setup_arrays_equal_prior_authenticated_model'] is True for row in rows)
    assert not p.git('diff','--name-only',entry['source'],OLD,'--',*HARNESS_SCOPES)

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
    assert not p.git('diff','--name-only',OLD,binding['head'],'--','src','tests',*HARNESS_SCOPES),'Published source differs from d9'

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

NATIVE_POLICY=dict(fixture='LibraryPairBenchmarks',method='Ooples',native_method='Competitor',native_round=2,
    old_rounds=[0,3],candidate_rounds=[1,2],minimum_margin=None,require_separated_intervals_both_rounds=True,new_closure_bars=10000,
    minimum_new_closures=1,require_own_source_faster=True,require_net_10000_point_progress=True,
    protect_prior_confident_wins_both_sizes=True,target_pairs=PAIRS)
SCREEN_GATE=dict(run='44411577289',workflow_head='a32abd63367ed10228e8a12d24c3d22758735e05',
    plan_sha256='6e017d94e832a2ecba65ef844e0611b039ebcaa16ed25cfaaa7f1aba62414711',
    summary_sha256='55c4bc2e31dfc0419fbb8e46b226d578ab67e52f687421945c73190e85703e33',
    path='benchmarks/plans/evidence/renko-constant-tr-private-screen-20261118.json',
    frozen_baseline_source='c5bfd4950982aa80dddbfd2db6358ad43ddb9e4c',
    root_review_path='benchmarks/plans/evidence/renko-constant-tr-private-screen-root-review-20261118.json',
    root_review_sha256='e5836c5cafd6459496aada2546f94f37c5d531e11d8895b9931a1cb9cd062eca')
def verify_screen(plan):
    assert {key:plan['screen_gate'][key] for key in SCREEN_GATE}==SCREEN_GATE
    raw=(p.REPO/SCREEN_GATE['path']).read_bytes();assert sha(raw)==SCREEN_GATE['summary_sha256']
    summary=json.loads(raw)
    assert (summary['case_count'],summary['decision_count'])==(20,4)
    # This original screen remains on its frozen C5 baseline and 10% policy.
    assert summary['sources']==dict(old=SCREEN_GATE['frozen_baseline_source'],new=NEW) and summary['diagnostic_only'] is True
    assert summary['qualification'] is False and summary['accepted'] is False
    assert summary['feasibility_screen']['passed'] is True
    raw=(p.REPO/SCREEN_GATE['root_review_path']).read_bytes();assert sha(raw)==SCREEN_GATE['root_review_sha256']
    review=json.loads(raw)
    assert review['run_id']==SCREEN_GATE['run'] and review['summary_sha256']==SCREEN_GATE['summary_sha256']
    assert review['source']==NEW and review['baseline']==SCREEN_GATE['frozen_baseline_source']
