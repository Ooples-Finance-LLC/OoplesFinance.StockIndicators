"""One immutable identical-binary native-eligibility diagnostic; never qualification."""
import json,os,shutil,subprocess,tempfile
from pathlib import Path
from . import pipeline as p, renko_constant_tr_private_qualification as baseline

SCOPE='current-native-eight-bars-screen-20261119'
EXECUTION_NONCE='native-eight-bars-20261119-v2'
SOURCE='48d238f56c37986b8437e5c31164552cc1362802'
BASE_HEAD='a4e2cc4e1c8b2bb4da00cf306703da283f36344a'
BASE_PLAN='benchmarks/plans/hosted-modified-hikkake-sparse-replay-qualification-20261118.json'
BASE_PLAN_SHA='271048d4d5bc7280898a070e474d4235873c4b2b7a659721f62fef8edda11473'
PUBLISHED_BASELINE=dict(pull_request=266,head='cde51367fc534e6d825f603d58f6899d4752ad05',
    branch='perf/remaining-indicator-rollout',source_fingerprint='eee20a82686be9470cf725dde6dae8107224c99505ac205836822433d24e195e')
PAIRS=['TaLib.Functions.PlusDI','TaLib.Functions.Adx','TaLib.Functions.Aroon','TaLib.Functions.AroonOsc',
       'TaLib.Functions.MinIndex','TaLib.Functions.Var','TaLib.Functions.StdDev','Skender.GetStdDev']
INDICATORS=['PriorSeededDirectionalMeasure']*2+['WindowAroon']*2+['WindowExtremeIndex','WindowDispersion','WindowDispersion','StandardDeviationWithDetails']
ROLES=('old','successor','variants')

def frozen_base_plan():
    raw=baseline.blob(BASE_HEAD,BASE_PLAN);assert baseline.sha(raw)==BASE_PLAN_SHA
    return json.loads(raw)

def baseline_record():return frozen_base_plan()['candidate']

def verify_published_baseline(plan):
    """Authenticate this screen's frozen 48d source and published cde binding offline."""
    binding=plan['published_baseline'];record=baseline_record()
    assert binding==PUBLISHED_BASELINE and record['source']==SOURCE
    assert binding['source_fingerprint']==record['source_fingerprint']
    assert baseline.common.source_fingerprint(binding['head'])==binding['source_fingerprint']
    assert not p.git('diff','--name-only',SOURCE,binding['head'],'--','src','tests',*baseline.HARNESS_SCOPES),'Published source differs from 48d'

def verify_current_publication(plan):
    """Check the live PR only at the launch or preparation boundary."""
    verify_published_baseline(plan);binding=plan['published_baseline']
    actual=json.loads(subprocess.check_output(['gh','api','repos/Ooples-Finance-LLC/OoplesFinance.StockIndicators/pulls/266'],cwd=p.REPO))
    captured=dict(pull_request=binding['pull_request'],head=actual['head']['sha'],branch=actual['head']['ref'],source_fingerprint=binding['source_fingerprint'])
    assert captured==binding,'Published PR head changed'
    return captured

def verify_publication_provenance(plan,proof):
    """Replay uses the captured immutable binding after the live PR advances."""
    verify_published_baseline(plan)
    assert proof['published_baseline']==plan['published_baseline'],'Captured publication binding mismatch'


def shards():
    task=dict(fixture='LibraryPairBenchmarks',operations=[dict(raw=pair,pair=pair) for pair in PAIRS],
              methods=['Ooples','Competitor'],implicit_pair=False,counts=[10000],cases=40,decisions=8)
    return [dict(index=0,tasks=[task],cases=40,decisions=8)]

def validate_scope(plan,*,require_ready=True):
    assert plan['native_eligibility_eight_screen'] is True and plan['diagnostic_only'] is True
    assert plan['scope']==SCOPE and plan['execution_nonce']==EXECUTION_NONCE and (plan['cases'],plan['decisions'])==(40,8)
    assert plan['supersedes_failed_run']=='44430729203' and plan['selector_preflight']==dict(parameter='Bars',selected_keys=16,negative_controls=2)
    assert plan['matrix']==dict(shards=1,max_parallel=1,timeout_minutes_per_shard=60)
    assert plan['refresh_shards']==plan['targeted_shards']==shards()
    assert plan['affected_pairs']==PAIRS and plan['bars']==[10000]
    assert plan['published_baseline']==PUBLISHED_BASELINE
    assert plan['sources']==dict(published=plan['published_baseline']['head'],old=SOURCE,successor=SOURCE,variants=SOURCE)
    assert plan['gpu_sources']==dict.fromkeys(ROLES,SOURCE)
    assert plan['measurement_policy']==baseline.common.POLICY and plan['runtime_policy_overrides']=={}
    assert plan['runtime_dependency_alignment']==dict(tensor_version='0.151.1')
    assert plan['proposed_dotnet_sdk']=='10.0.403' and plan['expected_runtime_version']=='.NET 10.0.15'
    assert plan['require_fixture_inventory'] is True and plan['old_candidate']==baseline_record()
    record=baseline_record()
    assert plan['cpu_test_classes']=={role:record['unit_classes'] for role in ROLES}
    assert plan['reused_correctness_scope']==dict(unit_classes=record['unit_classes'],unit_passed=record['unit_passed'],
        parity_classes=record['parity_classes'],parity_passed=record['parity_passed'],gpu_cases=100,new_selected_pair_cohort=False)
    assert plan['baseline_origin']==dict(head=BASE_HEAD,plan=BASE_PLAN,plan_sha256=BASE_PLAN_SHA,artifact_role='candidate')
    assert all(plan[key] is False for key in ('production_qualification','report_refresh_only','automatic_qualification','report_imported','profile_in_this_plan'))
    assert plan['closure_ledger_new_rows']==0 and plan['automatic_profile_start'] is False
    assert not any(plan.get(key) for key in ('pointwise_family_refresh','named_output_projection_refresh','current_baseline_ten_refresh',
        'diagnostic_kind','targeted_reference','coverage_reference','qualify_variants','standalone_variants','reuse_inputs'))
    baseline.verify_native_policy(plan);verify_published_baseline(plan)
    if require_ready:
        assert plan['inputs_ready'] is True and plan['parent_review_approved'] is True,'UNBOUND baseline receipts or parent review'

def source_authentication(plan):
    return dict(reused_baseline=plan['old_candidate'],same_artifact_roles=list(ROLES),
                reused_correctness_scope=plan['reused_correctness_scope'],additional_cpu_executions=0,additional_gpu_executions=0)

def verify_runtime_identity(plan,manifests):
    validate_scope(plan)
    assert manifests['old']==manifests['new'],'A/A requires identical complete runtime manifests'

def verify_registry_data(rows):
    selected=[]
    for pair,indicator in zip(PAIRS,INDICATORS):
        found=[row for row in rows if row['Id']==pair]
        assert len(found)==1 and found[0]['Status']=='paired' and found[0]['OoplesIndicator']==indicator, pair
        selected.append(found[0])
    return selected

def registry_inventory(folder,proof):
    raw=(folder/'registry-manifest.json').read_bytes()
    assert baseline.sha(raw)==proof['registry_manifest_sha256']
    assert verify_registry_data(json.loads(raw))==proof['selected_registry_bindings']

def verify_selection_data(data,plan):
    from .checkpoints import filters
    from .fixture_inventory import parse_listing
    assert data['harness_sha256']==plan['old_candidate']['actual_harness_sha256']
    assert data['fixture']=='LibraryPairBenchmarks' and data['parameter']=='Bars'
    expected=[(pair,10000,method) for pair in PAIRS for method in ('Ooples','Competitor')]
    assert [(row['pair'],row['bars'],row['method']) for row in data['selected']]==expected
    controls=[(PAIRS[0],100000,'Ooples'),('__UNREGISTERED_SELECTOR_CONTROL__',10000,'Ooples')]
    assert [(row['pair'],row['bars'],row['method']) for row in data['negative_controls']]==controls
    for row in [*data['selected'],*data['negative_controls']]:
        patterns=filters('LibraryPairBenchmarks',row['pair'],[row['method']],counts=[10000])
        if row['bars']==100000:patterns=[pattern.replace('Bars: 10000','Bars: 100000') for pattern in patterns]
        assert row['filters']==patterns
        matches=parse_listing(row['listing'],'LibraryPairBenchmarks')
        assert matches==([row['method']] if row in data['selected'] else [])

def record_selection_inventory(runtime,plan,runner=None):
    from .checkpoints import filters
    runtime=Path(runtime).resolve();runner=subprocess.check_output if runner is None else runner
    data=dict(fixture='LibraryPairBenchmarks',parameter='Bars',harness_sha256=p.digest(runtime/baseline.original.HARNESS_ASSEMBLY),
              selected=[],negative_controls=[])
    with tempfile.TemporaryDirectory(prefix='native-eight-selection-') as work:
        for pair,bars,method,target in [(pair,10000,method,'selected') for pair in PAIRS for method in ('Ooples','Competitor')]+[
            (PAIRS[0],100000,'Ooples','negative_controls'),('__UNREGISTERED_SELECTOR_CONTROL__',10000,'Ooples','negative_controls')]:
            patterns=filters('LibraryPairBenchmarks',pair,[method],counts=[10000])
            if bars==100000:patterns=[pattern.replace('Bars: 10000','Bars: 100000') for pattern in patterns]
            output=runner(['dotnet',str(runtime/baseline.original.HARNESS_ASSEMBLY),'--list','flat','--filter',*patterns],
                          cwd=work,stderr=subprocess.STDOUT,timeout=90,text=True)
            data[target].append(dict(pair=pair,bars=bars,method=method,filters=patterns,listing=output))
    verify_selection_data(data,plan);return data

def verify_harness_alias(folder,plan,proof):
    assert proof['source_authentication']==source_authentication(plan)
    for role in ROLES:
        assert proof['production_sha256'][role]==plan['old_candidate']['production_sha256']
        assert proof['checks'][role]==proof['checks']['old']
        assert proof['tested_runtime_dependencies'][role]==proof['tested_runtime_dependencies']['old']
        for suffix in ('.trx','-parity.trx','-original-gpu-receipt.json','-original-gpu-gpu.trx.gz'):
            assert (folder/'evidence'/(role+suffix)).read_bytes()==(folder/'evidence'/('old'+suffix)).read_bytes()

def verify_authentication(folder,plan,proof):
    validate_scope(plan);verify_publication_provenance(plan,proof)
    assert proof['preparation_mode']=='authenticated-current-native-eight-screen'
    verify_harness_alias(folder,plan,proof);registry_inventory(folder,proof)
    raw=(folder/'selected-benchmark-listing.json').read_bytes();assert baseline.sha(raw)==proof['selection_listing_sha256']
    verify_selection_data(json.loads(raw),plan)
    auth=baseline.checked_inputs(folder/'baseline-authentication',plan['old_candidate'])
    assert p.digest(folder/'common'/baseline.original.HARNESS_ASSEMBLY)==plan['old_candidate']['actual_harness_sha256']
    prefix='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/'
    assert proof['common_manifest']=={name[len(prefix):]:digest for name,digest in auth['manifest']['runtime_files'].items() if name.startswith(prefix)}
    for role in ROLES:
        assert proof['checks'][role]==dict(auth['manifest']['checks']['unit'],original_gpu_source=SOURCE)
        assert proof['tested_runtime_dependencies'][role]==auth['closure']
        for suffix,raw in [('.trx',auth['read']('evidence/unit.trx')),('-parity.trx',auth['read']('evidence/parity.trx')),
                           ('-original-gpu-receipt.json',auth['gpu_receipt']),('-original-gpu-gpu.trx.gz',auth['gpu_trx'])]:
            assert (folder/'evidence'/(role+suffix)).read_bytes()==raw

def prepare(candidate,out):
    plan=p.read(p.PLAN);validate_scope(plan)
    published=verify_current_publication(plan)
    assert subprocess.check_output(['dotnet','--version'],text=True).strip()==plan['proposed_dotnet_sdk']
    candidate,out=map(lambda path:Path(path).resolve(),(candidate,out))
    assert not out.exists() and not out.is_relative_to(candidate)
    auth=baseline.checked_inputs(candidate,plan['old_candidate'])
    out.mkdir();(out/'common').mkdir();(out/'production').mkdir();(out/'evidence').mkdir()
    shutil.copytree(candidate,out/'baseline-authentication')
    prefix='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/'
    for name,digest in auth['manifest']['runtime_files'].items():
        if name.startswith(prefix):
            target=out/'common'/name[len(prefix):];target.parent.mkdir(parents=True,exist_ok=True)
            target.write_bytes(auth['read']('blobs/'+digest))
    checks={};production={};dependencies={}
    for role in ROLES:
        (out/'production'/(role+'.dll')).write_bytes(auth['read']('blobs/'+auth['production']))
        checks[role]=dict(auth['manifest']['checks']['unit'],original_gpu_source=SOURCE)
        production[role]=auth['production'];dependencies[role]=auth['closure']
        for suffix,raw in [('.trx',auth['read']('evidence/unit.trx')),('-parity.trx',auth['read']('evidence/parity.trx')),
                           ('-original-gpu-receipt.json',auth['gpu_receipt']),('-original-gpu-gpu.trx.gz',auth['gpu_trx'])]:
            (out/'evidence'/(role+suffix)).write_bytes(raw)
    with tempfile.TemporaryDirectory(prefix='native-eight-registry-') as work:
        path=Path(work)/'registry.json'
        subprocess.run(['dotnet',str(out/'common'/baseline.original.HARNESS_ASSEMBLY),'--comparison-manifest',str(path)],cwd=work,check=True)
        registry=path.read_bytes()
    selected=verify_registry_data(json.loads(registry));(out/'registry-manifest.json').write_bytes(registry)
    p.write(out/'selected-benchmark-listing.json',record_selection_inventory(out/'common',plan))
    proof=dict(plan_sha256=p.digest(p.PLAN),sources=plan['sources'],
        source_trees={role:p.git('rev-parse',SOURCE+':src') for role in ROLES},sdk=plan['proposed_dotnet_sdk'],
        dotnet_info=subprocess.check_output(['dotnet','--info'],text=True),workflow_head=p.git('rev-parse','HEAD'),
        run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],
        preparation_mode='authenticated-current-native-eight-screen',source_authentication=source_authentication(plan),
        published_baseline=published,common_manifest=p.manifest(out/'common'),evidence_manifest=p.manifest(out/'evidence'),
        production_sha256=production,checks=checks,tested_runtime_dependencies=dependencies,
        registry_manifest_sha256=baseline.sha(registry),selected_registry_bindings=selected,
        selection_listing_sha256=p.digest(out/'selected-benchmark-listing.json'))
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256']=record_inventory(out,plan,p.digest(p.PLAN))
    p.write(out/'provenance.json',proof);p.verify_inputs(out);return proof

def native_rows(folder):
    from .verify import archived,sample
    from .checkpoints import case_key
    roots=[path.parent for path in Path(folder).glob('*/provenance.json')];assert len(roots)==1
    root=roots[0];proof=p.read(root/'provenance.json');state=p.read(root/'status.json');rounds={}
    for record in state['rounds']:
        assert record['fixture']=='LibraryPairBenchmarks' and str(record['index']) not in rounds
        index=str(record['index']);report=p.read(archived(root,proof['raw_root'],record['report']))
        values={case_key(case):sample(case) for case in report['Benchmarks']}
        methods=('Ooples','Competitor') if index=='2' else ('Ooples',)
        assert len(values)==len(report['Benchmarks']) and set(values)=={(pair,10000,method) for pair in PAIRS for method in methods}
        rounds[index]=values
    assert set(rounds)=={'0','1','2','3'}
    return [dict(pair=pair,bars=10000,owned_rounds={index:rounds[index][pair,10000,'Ooples'] for index in ('0','1','2','3')},
                 native_control=rounds['2'][pair,10000,'Competitor']) for pair in PAIRS]

def eligibility(rows,decisions):
    indexed={row['pair']:row for row in rows};verdicts={row['pair']:row['verdict'] for row in decisions}
    assert len(rows)==len(indexed)==len(decisions)==len(verdicts)==8 and set(indexed)==set(verdicts)==set(PAIRS)
    assert {(row['fixture'],row['method'],row['bars']) for row in decisions}=={('LibraryPairBenchmarks','Ooples',10000)}
    result=[]
    for pair in PAIRS:
        row=indexed[pair];assert row['bars']==10000 and set(row['owned_rounds'])=={'0','1','2','3'}
        own=row['owned_rounds'];native=row['native_control'];verdict=verdicts[pair]
        assert verdict in ('faster','slower','inconclusive')
        eligible=all(own[index]['mean_ns']>=native['mean_ns'] for index in ('0','3'))
        stable=verdict=='inconclusive'
        result.append(dict(**row,aa_verdict=verdict,aa_admitted_for_triage=stable,
            frozen_new_win_eligible=eligible,profile_candidate=stable and eligible,
            old_point_native_wins={index:own[index]['mean_ns']<native['mean_ns'] for index in ('0','3')},
            old_confident_native_win=all(own[index]['upper_ns']<native['lower_ns'] for index in ('0','3')),
            owned_interval_native_wins={index:value['upper_ns']<native['lower_ns'] for index,value in own.items()}))
    return result

def collect(folder):
    from .verify import _collect
    plan=p.read(p.PLAN);validate_scope(plan)
    baseline.original.verify_runtime(folder,plan['expected_runtime_version'])
    result=_collect(Path(folder),'baseline-refresh',preflight=False)
    assert not result['accepted'] and (result['case_count'],result['decision_count'])==(40,8)
    rows=eligibility(native_rows(folder),result['decisions'])
    result.update(diagnostic_only=True,qualification=False,production_qualification=False,report_refresh_only=False,
        production_optimization=False,source_changed=False,report_imported=False,closure_credit=0,
        automatic_qualification=False,same_production_under_both_labels=True,baseline_native_rows=rows,
        aa_admission_is_not_equivalence=True,profile_candidates=[row['pair'] for row in rows if row['profile_candidate']],
        quarantined_pairs=[row['pair'] for row in rows if not row['aa_admitted_for_triage']],
        automatic_profile_started=False,native_policy_id=baseline.POLICY_APPROVAL['policy_id'],
        native_policy_receipt_sha256=baseline.POLICY_APPROVAL['sha256'])
    return result
