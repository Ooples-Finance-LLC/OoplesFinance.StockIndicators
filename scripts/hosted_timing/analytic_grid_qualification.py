"""Bounded216-case exact analytic qualification; immutable source/artifact reuse."""
import copy,gzip,hashlib,json,os,re,shutil,subprocess
from pathlib import Path
from . import pipeline as p
from . import analytic_grid_roi as prior
from . import triple_body_macro_qualification as original
SCOPE='analytic-grid-qualification-20261119'
OLD=prior.OLD;NEW=prior.NEW;TARGETS=prior.TARGETS
LP=TARGETS+['QuanTAlib.Convolution','TaLib.Candles.HikkakeModified','TaLib.Functions.Ema','TaLib.Functions.T3']
HARNESS_PATH=prior.HARNESS_PATH;HARNESS_SCOPES=prior.HARNESS_SCOPES
sha=prior.sha;blob=prior.blob
checked_inputs=prior.checked_inputs;harness_role_alias=prior.harness_role_alias
source_authentication=prior.source_authentication;verify_harness_alias=prior.verify_harness_alias
focused_baseline=prior.focused_baseline;verify_source_boundary=prior.verify_source_boundary
verify_actual_input=prior.verify_actual_input;verify_compiled_fixture_path=prior.verify_compiled_fixture_path
verify_current_publication=prior.verify_current_publication;verify_published_baseline=prior.verify_published_baseline
verify_publication_provenance=prior.verify_publication_provenance
NATIVE_POLICY=prior.NATIVE_POLICY

def task(fixture,ops,methods):
    owned=sum(m in p.OWNED for m in methods);native=len(methods)-owned
    return dict(fixture=fixture,operations=[dict(raw=raw,pair=pair) for raw,pair in ops],methods=methods,implicit_pair=False,cases=len(ops)*(8*owned+2*native),decisions=len(ops)*2*owned)

def shards():
    native=['BuilderFull','BuilderLatestOnly','NativeSingle','NativeParallel']
    tasks0=[task('LibraryPairBenchmarks',[(x,x) for x in LP[:3]],['Ooples','Competitor']),task('SharedWeightedWindowBenchmarks',[(x,'QuanTAlib.'+x) for x in ['Gma','Sinema','Convolution']],native),task('SharedWindowedSincBenchmarks',[('QuanTAlib.Afirma','QuanTAlib.Afirma')],native)]
    tasks1=[task('LibraryPairBenchmarks',[(x,x) for x in LP[3:]],['Ooples','Competitor']),task('SharedFibonacciWeightedBenchmarks',[('QuanTAlib.Fwma','QuanTAlib.Fwma')],native),task('SharedHikkakeBenchmarks',[('TaLib.Candles.HikkakeModified','TaLib.Candles.HikkakeModified')],['BuilderFull','BuilderLatestOnly']),task('SharedRecursiveStateBenchmarks',[('Ema','TaLib.Functions.Ema')],native),task('SharedTillsonBenchmarks',[('TaLib.Functions.T3','TaLib.Functions.T3')],native)]
    return [dict(index=i,tasks=ts,cases=sum(t['cases'] for t in ts),decisions=sum(t['decisions'] for t in ts)) for i,ts in enumerate([tasks0,tasks1])]

def roi_projection(plan):
    # Reuse the already root-reviewed exact source/artifact/compiled predicates;
    # only the separately validated execution scope is projected to its old shape.
    result=copy.deepcopy(plan)
    result.update(diagnostic_kind=prior.KIND,diagnostic_only=True,cases=40,decisions=8,matrix=dict(shards=1,max_parallel=1,timeout_minutes_per_shard=120),diagnostic_shards=prior.shards(),targeted_shards=prior.shards(),targeted_reference=None,frozen_baseline_feasibility_only=True,current_combined_source_qualified=False,report_import_permitted=False,followup_requires_current_published_baseline=True)
    return result

def validate_scope(plan,*,require_ready=True):
    assert plan['analytic_grid_qualification'] is True and not plan.get('diagnostic_kind') and not plan.get('diagnostic_only')
    assert (plan['cases'],plan['decisions'])==(216,44)
    assert plan['matrix']==dict(shards=2,max_parallel=2,timeout_minutes_per_shard=120)
    assert plan['targeted_shards']==shards() and plan['targeted_reference']==dict(scope=SCOPE)
    assert not plan.get('diagnostic_shards') and not plan.get('frozen_baseline_feasibility_only')
    assert plan['report_import_permitted'] is False and plan['automatic_qualification'] is False
    prior.validate_scope(roi_projection(plan),require_ready=require_ready)
    verify_fixture_contracts(plan)
    verify_screen(plan,require_ready=require_ready)

def verify_fixture_contracts(plan):
    assert plan['qualification_fixture_contracts']==FIXTURES
    for source in (OLD,NEW):
        for fixture,contract in FIXTURES.items():
            text=blob(source,p.BENCH+'/LibraryComparisons/'+fixture+'.cs').decode()
            for needle in contract['source_needles']:assert needle in text,(fixture,needle)
    # Existing premeasurement record_inventory independently checks compiled
    # benchmark methods; source pins bind Params and exact raw operation mapping.

def verify_screen(plan,*,require_ready=True):
    gate=plan['screen_gate'];assert gate['run_id']=='44439792107' and gate['head']=='28f26a95abc585d38d968159988ef30f917162d7'
    assert gate['plan_sha256']=='22a7f87d5acf80c169244f955424f684b765be128d3b5eb8bc09345af80c3388'
    screen=json.loads(prior.evidence_entry(gate['summary']))
    assert (screen['case_count'],screen['decision_count'])==(40,8) and screen['accepted'] is False and screen['qualification'] is False
    assert screen['feasibility_screen']['passed'] is True and set(x['pair'] for x in screen['feasibility_screen']['new_10000_target_wins'])==set(TARGETS)
    if require_ready:
        assert gate['independently_authenticated'] is True,'UNBOUND independent ROI review'
        review=json.loads(prior.evidence_entry(gate['root_review']))
        assert review['run_id']==gate['run_id'] and review['summary_sha256']==gate['summary']['sha256']
        assert review['status']=='root-independent-analytic-grid-roi-authentication-passed' and review['source']==NEW and review['baseline']==OLD
        assert review['workflow_head']==gate['head'] and review['plan_sha256']==gate['plan_sha256']
        assert review['independent_hosted_recollection_matches'] is True and review['independently_recomputed_gates'] is True and review['feasibility_screen_passed'] is True

def verify_authentication(folder,plan,proof):
    validate_scope(plan);assert proof['preparation_mode']=='authenticated-analytic-grid-qualification'
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
    proof=dict(plan_sha256=p.digest(p.PLAN),sources=plan['sources'],source_trees={label:p.git('rev-parse',plan['sources'][label]+':src') for label in ('old','successor','variants')},sdk=plan['proposed_dotnet_sdk'],dotnet_info=subprocess.check_output(['dotnet','--info'],text=True),workflow_head=p.git('rev-parse','HEAD'),run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],preparation_mode='authenticated-analytic-grid-qualification',source_authentication=source_authentication(plan),harness_role_alias=alias,common_manifest=p.manifest(out/'common'),evidence_manifest=p.manifest(out/'evidence'),production_sha256=production,checks=checks,tested_runtime_dependencies=dependencies)
    for name,raw in focused.items():(out/'evidence'/name).write_bytes(raw)
    proof['published_baseline']=published_baseline
    proof['actual_input_provenance']=plan['actual_input_provenance']
    proof['supplemental_baseline_checks']={name:sha(raw) for name,raw in focused.items()}
    proof['evidence_manifest']=p.manifest(out/'evidence')
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256']=record_inventory(out,plan,p.digest(p.PLAN));p.write(out/'provenance.json',proof);p.verify_inputs(out);return proof
def sample_rows(folder):
    from .verify import archived,sample
    from .checkpoints import case_key
    roots=[x.parent for x in Path(folder).glob('*/provenance.json')];assert len(roots)==2
    result=[]
    for root in roots:
        proof=p.read(root/'provenance.json');state=p.read(root/'status.json');index=int(proof['shard']) if 'shard' in proof else int(root.name.removeprefix('shard'))
        tasks={t['fixture']:t for t in shards()[index]['tasks']};group={}
        for record in state['rounds']:
            fixture=record['fixture'];round=str(record['index']);assert fixture in tasks and round in ('0','1','2','3')
            rows={}
            for case in p.read(archived(root,proof['raw_root'],record['report']))['Benchmarks']:
                raw,bars,method=case_key(case);mapping={op['raw']:op['pair'] for op in tasks[fixture]['operations']};assert raw in mapping;key=mapping[raw],bars,method;assert key not in rows;rows[key]=sample(case)
            methods=p.methods_for(tasks[fixture],int(round));expected={(op['pair'],n,m) for op in tasks[fixture]['operations'] for n in (1000,10000) for m in methods}
            assert set(rows)==expected and (fixture,round) not in group;group[fixture,round]=rows
        assert set(group)=={(fixture,r) for fixture in tasks for r in ('0','1','2','3')}
        for fixture,t in tasks.items():
            for op in t['operations']:
                for bars in (1000,10000):
                    for method in [m for m in t['methods'] if m in p.OWNED]:
                        result.append(dict(fixture=fixture,pair=op['pair'],bars=bars,method=method,old_rounds={r:group[fixture,r][op['pair'],bars,method] for r in ('0','3')},candidate_rounds={r:group[fixture,r][op['pair'],bars,method] for r in ('1','2')},native_controls={m:group[fixture,'2'][op['pair'],bars,m] for m in t['methods'] if m not in p.OWNED}))
    assert len(result)==44;return result

def adjudicate_feasibility(rows,decisions):
    """Native gate under unchanged sampling and prior eligibility; no automatic credit."""
    expected={(pair,bars) for pair in LP for bars in (1000,10000)}
    indexed={(r['pair'],r['bars']):r for r in rows}
    own={(d['pair'],d['bars']):d['verdict'] for d in decisions if d['fixture']=='LibraryPairBenchmarks' and d['method']=='Ooples'}
    assert len(rows)==len(indexed)==len(own)==12 and len(decisions)==44 and set(indexed)==set(own)==expected
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
        all_44_source_comparisons_no_regression=no_slower,at_least_one_new_10000_target_win=bool(new_wins),new_10000_target_wins=new_wins,
        protected_prior_native_wins=protected,prior_native_win_reopenings=reopened,
        per_target_10000=[d for d in details if d['bars']==10000 and d['pair'] in TARGETS],all_pair_size_rows=details,
        eligibility_rule='Both old owned means must be >= the same round2 native mean; a prior point winner in either old round is ineligible for a new-win claim.',
        prior_win_protection_rule='Both old upper intervals below native lower => both candidate means remain strictly below native mean; checked at both sizes.',
        minimum_native_margin=None,native_policy_id=prior.POLICY_APPROVAL['policy_id'],native_policy_receipt_sha256=prior.POLICY_APPROVAL['sha256'],native_control_round=2,native_controls_per_pair_size=1,
        automatic_qualification=False,qualification=False,closure_credit=0)

def collect(folder):
    from .verify import _collect
    original.verify_runtime(folder,p.read(p.PLAN)['expected_runtime_version'])
    result=_collect(Path(folder),'targeted',preflight=False)
    assert (result['case_count'],result['decision_count'])==(216,44)
    rows=sample_rows(folder);lp=[dict(pair=r['pair'],bars=r['bars'],old_rounds=r['old_rounds'],candidate_rounds=r['candidate_rounds'],native_control=r['native_controls']['Competitor']) for r in rows if r['fixture']=='LibraryPairBenchmarks']
    native=adjudicate_feasibility(lp,result['decisions']);protected=[]
    for row in rows:
        for method,control in row['native_controls'].items():
            if all(x['upper_ns']<control['lower_ns'] for x in row['old_rounds'].values()):
                protected.append(dict(fixture=row['fixture'],pair=row['pair'],bars=row['bars'],owned_method=row['method'],native_method=method,preserved=all(x['mean_ns']<control['mean_ns'] for x in row['candidate_rounds'].values())))
    prior_preserved=all(x['preserved'] for x in protected)
    source=result['accepted'] and all(x['verdict']!='slower' for x in result['decisions'])
    result.update(source_gate_accepted=source,native_progress_gate_passed=native['passed'],native_progress_gate=native,all_context_prior_native_wins=protected,all_context_prior_native_wins_preserved=prior_preserved,accepted=source and native['passed'] and prior_preserved,qualification=True,production_integrated=False,report_imported=False)
    return result

FIXTURES={'LibraryPairBenchmarks': {'source_needles': ['public class LibraryPairBenchmarks', 'public string PairId', 'public int Bars', ' Ooples()', ' Competitor()']}, 'SharedWeightedWindowBenchmarks': {'source_needles': ['[Params(1_000, 10_000)]', '"Convolution", "Gma", "Sinema"', 'BuilderFull()', 'BuilderLatestOnly()', 'NativeSingle()', 'NativeParallel()']}, 'SharedWindowedSincBenchmarks': {'source_needles': ['[Params("QuanTAlib.Afirma")]', '[Params(1_000, 10_000)]', 'private const int Period = 20;', 'private const int Taps = 21;', 'BuilderFull()', 'BuilderLatestOnly()', 'NativeSingle()', 'NativeParallel()']}, 'SharedFibonacciWeightedBenchmarks': {'source_needles': ['[Params("QuanTAlib.Fwma")]', '[Params(1_000, 10_000)]', 'BuilderFull()', 'BuilderLatestOnly()', 'NativeSingle()', 'NativeParallel()']}, 'SharedHikkakeBenchmarks': {'source_needles': ['[Params(1000, 10000)]', '"TaLib.Candles.HikkakeModified"', 'BuilderFull()', 'BuilderLatestOnly()']}, 'SharedRecursiveStateBenchmarks': {'source_needles': ['public class SharedRecursiveStateBenchmarks', '"Ema"', 'BuilderFull()', 'BuilderLatestOnly()', 'NativeSingle()', 'NativeParallel()']}, 'SharedTillsonBenchmarks': {'source_needles': ['public class SharedTillsonBenchmarks', '"TaLib.Functions.T3"', 'BuilderFull()', 'BuilderLatestOnly()', 'NativeSingle()', 'NativeParallel()']}}
