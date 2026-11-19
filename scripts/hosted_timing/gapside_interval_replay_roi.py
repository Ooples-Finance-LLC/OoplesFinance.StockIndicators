"""Held ten-case GapSide original-state replay diagnostic; never qualification or closure credit."""
import gzip,hashlib,importlib.util,json,os,re,shutil,subprocess,tempfile
from pathlib import Path
from . import pipeline as p
from .candle_topology_replay_roi import checked_inputs
from . import candle_single_pass_roi as common
from . import triple_body_macro_qualification as original
SCOPE='gapside-interval-replay-roi-20261119'
BRANCH='perf/gapside-interval-replay-roi-20261119'
REVISION='gapside-interval-replay-roi-v1-20261119'
KIND='gapside-interval-replay-native-pilot'
OLD='4b4e8a385ed1c210998d26de6460e23f5842ffe9'
NEW='9025295cc3901fed088ed45f09c53ab4edd46988'
PUBLISHED='20d21f263a25f14c4c15a0ab9354bfc91e0dab60'
PAIRS=['TaLib.Candles.GapSideBySideWhiteLines']
PRIMARY={(PAIRS[0],10000)}
PATHS={'src/Indicators/ValuesBarExecution.BoundedWindows.cs','src/Indicators/ValuesBarExecution.GapSideReplay.cs','tests/ModelsTests/GapSideReplayTests.cs'}
HARNESS_PATH=p.BENCH+'/bin/Release/net10.0/'+original.HARNESS_ASSEMBLY
HARNESS_SCOPES=[p.BENCH,p.TESTS,'Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.Config','nuget.config']
sha=lambda raw:hashlib.sha256(raw).hexdigest()
def blob(source,path):return subprocess.check_output(['git','show',source+':'+path],cwd=p.REPO)
def evidence_entry(entry):
    assert set(entry)=={'path','sha256'}
    path=Path(entry['path']);assert path.as_posix().startswith('benchmarks/plans/evidence/') and '..' not in path.parts
    raw=(p.REPO/path).read_bytes();assert sha(raw)==entry['sha256'];return raw
def shards():
    task=dict(fixture='LibraryPairBenchmarks',operations=[dict(raw=x,pair=x) for x in PAIRS],methods=['Ooples','Competitor'],implicit_pair=False,cases=10,decisions=2)
    return [dict(index=0,tasks=[task],cases=10,decisions=2)]
def case_inventory():
    from .checkpoints import filters
    task=shards()[0]['tasks'][0]
    return [dict(round=index,arm=arm,fixture='LibraryPairBenchmarks',pair=pair,bars=bars,method=method,
                 filters=filters('LibraryPairBenchmarks',pair,[method]))
            for index,arm in enumerate(common.POLICY['order']) for pair in PAIRS
            for bars in (1000,10000) for method in p.methods_for(task,index)]
def validate_scope(plan,*,require_ready=True):
    if require_ready: assert plan['inputs_ready'] is True and plan['parent_review_approved'] is True, 'HELD before SDK/input I/O'
    assert plan['input_pin_status']=='bound', 'HELD until actual CPU/GPU root bindings are complete'
    assert plan['preparation_branch']==BRANCH and plan['preparation_revision']==REVISION
    assert plan['diagnostic_kind']==KIND and plan['diagnostic_only'] is True
    assert plan['sources']==dict(published=OLD,old=OLD,successor=NEW,variants=OLD)
    assert plan['gpu_sources']==dict(old=OLD,successor=NEW,variants=OLD)
    assert (plan['cases'],plan['decisions'])==(10,2)
    assert plan['matrix']==dict(shards=1,max_parallel=1,timeout_minutes_per_shard=60)
    assert plan['diagnostic_shards']==plan['targeted_shards']==shards()
    assert plan['requested_case_inventory']==case_inventory()
    assert plan['measurement_policy']==common.POLICY and plan['runtime_policy_overrides']=={}
    assert plan['runtime_dependency_alignment']==dict(tensor_version='0.151.1')
    assert plan['require_fixture_inventory'] is True and plan['proposed_dotnet_sdk']=='10.0.403' and plan['expected_runtime_version']=='.NET 10.0.15'
    assert plan['closure_ledger_new_rows']==0 and plan['automatic_qualification'] is False and plan['report_import_permitted'] is False
    assert not any(plan.get(k) for k in ('targeted_reference','coverage_reference','standalone_variants','qualify_variants','trix_preflight','reuse_inputs'))
    assert plan['harness_alias_policy']==dict(common_artifact_role='old',variants_artifact_role='old',baseline_source=OLD,candidate_source=NEW,require_scoped_source_identity=True,claim_artifact_binary_identity=False)
    assert plan['harness_sha256']==plan['old_candidate']['actual_harness_sha256']
    for role,key,source,run,counts in [('old','old_candidate',OLD,'44469775767',(134,86)),('successor','candidate',NEW,'44533761016',(26,76))]:
        c=plan[key];assert c['source']==source and c['ci_run']==run and c['producer']=='ce6d1a0373166c42583278e4d7efd38f65e0934d'
        assert (c['unit_passed'],c['parity_passed'])==counts and plan['cpu_test_classes'][role]==c['unit_classes']
        cpu=json.loads(evidence_entry(c['cpu_root_review']));gpu=json.loads(evidence_entry(c['gpu_root_review']))
        assert cpu['status']=='root-independent-cpu-authentication-passed' and cpu['source']==source and cpu['run_id']==run
        for a,b in [('source_fingerprint','source_fingerprint'),('manifest_sha256','manifest_sha256'),('core_sha256','production_sha256'),('actual_harness_sha256','actual_harness_sha256'),('artifact_id','artifact_id'),('zip_sha256','artifact_zip_sha256')]:assert str(cpu[a])==str(c[b])
        assert (cpu['unit'],cpu['parity'])==counts
        assert gpu['status']=='root-independent-actual-gpu-authentication-passed' and gpu['source']==source and gpu['run_id']==run
        assert gpu['passed_cases']==100 and gpu['methods']==12 and gpu['receipt_sha256']==c['gpu_receipt']['sha256']
        assert gpu['cpu_root_review_sha256']==c['cpu_root_review']['sha256']
    assert plan['cpu_test_classes']['variants']==plan['old_candidate']['unit_classes']
    assert plan['native_win_policy_approval']['sha256']=='414b558eae1deda750c3d169020128986a0e0db9b2acd95f23da035be03bfd7c'
    evidence_entry(plan['native_win_policy_approval']);evidence_entry(plan['source_root_review'])
    assert plan['native_progress_gate']==dict(primary_targets=[dict(pair=pair,bars=bars) for pair,bars in sorted(PRIMARY)],old_rounds=[0,3],candidate_rounds=[1,2],native_round=2,require_source_faster=True,require_separated_intervals_both_rounds=True,minimum_margin=None,protect_prior_confident_wins_both_sizes=True,require_net_10000_point_progress=True,qualification=False,closure_credit=0)
    verify_source_boundary();verify_published_baseline(plan)
    if require_ready:assert plan['inputs_ready'] is True and plan['parent_review_approved'] is True,'HELD until root final plan review'
def verify_source_boundary():
    assert set(p.git('diff','--name-only',OLD,NEW,'--','src','tests',p.BENCH,p.TESTS).splitlines())==PATHS
    assert not p.git('diff','--name-only',OLD,NEW,'--',*HARNESS_SCOPES)
    assert not p.git('diff','--name-only',OLD,PUBLISHED,'--','src','tests',p.BENCH,p.TESTS)
def verify_published_baseline(plan):
    assert plan['published_baseline']==dict(head=PUBLISHED,branch='perf/remaining-indicator-rollout',source_fingerprint=plan['old_candidate']['source_fingerprint'])
    return plan['published_baseline']
def verify_current_publication(plan):
    verify_published_baseline(plan)
    actual=subprocess.check_output(['git','ls-remote','origin','refs/heads/perf/remaining-indicator-rollout'],cwd=p.REPO,text=True).split()
    assert actual==[PUBLISHED,'refs/heads/perf/remaining-indicator-rollout'],'Published baseline changed; timing held'
    return plan['published_baseline']
def verify_publication_provenance(plan,proof):assert proof['published_baseline']==verify_published_baseline(plan)
def focused_baseline(plan,manifest=None):return {}
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
    validate_scope(plan);verify_publication_provenance(plan,proof);assert proof['preparation_mode']=='authenticated-gapside-interval-replay-diagnostic'
    assert proof['workflow_branch']==BRANCH and proof['workflow_event']=='push' and proof['preparation_revision']==REVISION
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
    manifest=p.read(folder/'old-authentication/manifest.json');prefix='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/'
    assert proof['common_manifest']=={name[len(prefix):]:digest for name,digest in manifest['runtime_files'].items() if name.startswith(prefix)}

def prepare(old_candidate,candidate,out):
    plan=p.read(p.PLAN);validate_scope(plan)
    assert os.environ.get('GITHUB_REF_NAME')==BRANCH and os.environ.get('GITHUB_EVENT_NAME')=='push'
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
    proof=dict(plan_sha256=p.digest(p.PLAN),sources=plan['sources'],source_trees={label:p.git('rev-parse',plan['sources'][label]+':src') for label in ('old','successor','variants')},sdk=plan['proposed_dotnet_sdk'],dotnet_info=subprocess.check_output(['dotnet','--info'],text=True),workflow_head=p.git('rev-parse','HEAD'),run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],workflow_branch=os.environ['GITHUB_REF_NAME'],workflow_event=os.environ['GITHUB_EVENT_NAME'],preparation_revision=REVISION,preparation_mode='authenticated-gapside-interval-replay-diagnostic',source_authentication=source_authentication(plan),harness_role_alias=alias,common_manifest=p.manifest(out/'common'),evidence_manifest=p.manifest(out/'evidence'),production_sha256=production,checks=checks,tested_runtime_dependencies=dependencies)
    for name,raw in focused.items():(out/'evidence'/name).write_bytes(raw)
    proof['published_baseline']=published_baseline
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
        assert record['fixture'] == 'LibraryPairBenchmarks'
        index = str(record['index']);assert index in ('0','1','2','3') and index not in rounds
        path=archived(root,proof['raw_root'],record['report']);assert p.digest(path)==record['sha256']
        report=p.read(path)
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
    expected={(pair,bars) for pair in PAIRS for bars in (1000,10000)}
    indexed={(r['pair'],r['bars']):r for r in rows}
    own={(d['pair'],d['bars']):d['verdict'] for d in decisions if d['fixture']=='LibraryPairBenchmarks' and d['method']=='Ooples'}
    assert len(rows)==len(indexed)==len(decisions)==len(own)==2 and set(indexed)==set(own)==expected
    assert set(own.values())<={'faster','slower','inconclusive'}
    details=[];wins=[];reopened=[]
    old_counts={'0':0,'3':0};new_counts={'1':0,'2':0}
    for key in sorted(expected):
        row=indexed[key];native=row['native_control'];old=list(row['old_rounds'].values());new=list(row['candidate_rounds'].values())
        assert set(row['old_rounds'])=={'0','3'} and set(row['candidate_rounds'])=={'1','2'}
        if key[1]==10000:
            for index,value in row['old_rounds'].items():old_counts[index]+=int(value['mean_ns']<native['mean_ns'])
            for index,value in row['candidate_rounds'].items():new_counts[index]+=int(value['mean_ns']<native['mean_ns'])
        eligible=all(v['mean_ns']>=native['mean_ns'] for v in old)
        prior=all(v['upper_ns']<native['lower_ns'] for v in old)
        separated=all(v['upper_ns']<native['lower_ns'] for v in new)
        won=key in PRIMARY and eligible and own[key]=='faster' and separated
        preserved=not prior or all(v['mean_ns']<native['mean_ns'] for v in new)
        if won:wins.append(dict(pair=key[0],bars=key[1]))
        if not preserved:reopened.append(dict(pair=key[0],bars=key[1]))
        details.append(dict(**row,primary_target=key in PRIMARY,source_verdict=own[key],baseline_new_win_eligible=eligible,baseline_interval_native_win=prior,candidate_statistical_native_win=separated,primary_native_crossover=won,prior_win_preserved=preserved))
    net=min(new_counts.values())>max(old_counts.values())
    return dict(passed=bool(wins) and not reopened and 'slower' not in own.values() and net,primary_native_crossovers=wins,protected_reopenings=reopened,old_10000_point_win_counts=old_counts,candidate_10000_point_win_counts=new_counts,net_10000_point_progress=net,all_context_rows=details,native_control_round=2,qualification=False,closure_credit=0,automatic_qualification=False)
def collect(folder):
    from .verify import _collect
    original.verify_runtime(folder,p.read(p.PLAN)['expected_runtime_version'])
    result=_collect(Path(folder),'diagnostic',preflight=False,diagnostic=True)
    assert not result['accepted'] and (result['case_count'],result['decision_count'])==(10,2)
    result.update(diagnostic_only=True,qualification=False,report_imported=False,closure_ledger_new_rows=0,automatic_qualification=False,feasibility_screen=adjudicate_feasibility(native_rows(folder),result['decisions']))
    return result
