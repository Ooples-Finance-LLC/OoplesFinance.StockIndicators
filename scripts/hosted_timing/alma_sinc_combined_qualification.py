"""Held190-case combined ALMA/Sinc qualification; preserve both family gates."""
import json
import os
import re
import shutil
import subprocess
from pathlib import Path
from . import pipeline as p
from . import alma_grid_roi as prior

SCOPE = 'alma-sinc-combined-qualification-20261119'
OLD = '48980c2bd9d04dd5cd472ccc768efe9b44e5e20e'
NEW = '4b4e8a385ed1c210998d26de6460e23f5842ffe9'
PUBLISHED = '87405159446f3de0132ce49800b52bd0af00e234'
COUNTS = (1000,10000)
SINC,PAIR = 'SharedWindowedSincBenchmarks','QuanTAlib.Afirma'
ALMA = ['QuanTAlib.Alma','Skender.GetAlma']
UNIT = ['AlmaWideDivisorTests','AlmaGridBatchTests','SharedAlmaWindowTests','AnalyticGridBatchTests','SincGridBatchTests','WindowedSincCompactTests','SharedWeightedWindowTests']
PARITY = ['AlmaComparisonTests','AlmaPublisherAlignmentTests','NamedComparisonPublisherTests','AnalyticKernelComparisonTests','SincBuilderComparisonTests','FixedWeightedComparisonTests','ConvolutionComparisonTests']
BASE_UNIT = ['SharedAlmaWindowTests','AnalyticGridBatchTests']
BASE_PARITY = ['AlmaComparisonTests','AlmaPublisherAlignmentTests','NamedComparisonPublisherTests','AnalyticKernelComparisonTests']
PATHS = {'src/Indicators/WindowedSinc.cs','src/Indicators/ValuesBarExecution.AnalyticGrid.cs','src/Indicators/ValuesBarExecution.BoundedWindows.cs','src/Indicators/ValuesBarExecution.SincGrid.cs','tests/ModelsTests/SincGridBatchTests.cs','src/Indicators/ArnaudLegouxWindow.cs','src/Indicators/ValuesBarExecution.AlmaGrid.cs','src/Indicators/ValuesBarExecution.AlmaWideDivisor.cs','tests/ModelsTests/AlmaGridBatchTests.cs','tests/ModelsTests/AlmaWideDivisorTests.cs'}
MODE = 'authenticated-alma-sinc-combined-qualification'
sha,blob = prior.sha,prior.blob
verify_harness_alias = prior.verify_harness_alias
NATIVE_POLICY = dict(old_rounds=[0,3],candidate_rounds=[1,2],native_round=2,minimum_margin=None,
    require_separated_intervals_both_rounds=True,require_both_family_gates=True,
    require_own_source_faster=True,protect_prior_confident_wins_both_sizes=True,
    require_all_context_prior_native_wins=True,require_net_10000_point_progress=True,
    new_closure_bars=10000,minimum_new_closures_per_family=1,lp_afirma_report_row_credit=0)

def consumer_registry():
    methods = ['BuilderFull','BuilderLatestOnly','NativeSingle','NativeParallel']
    return [dict(fixture='LibraryPairBenchmarks',pair_parameter='PairId',size_parameter='Bars',operations=['QuanTAlib.Gma','QuanTAlib.Sinema','QuanTAlib.Convolution']+ALMA,methods=['Ooples','Competitor'],sizes=list(COUNTS)),
            dict(fixture='SharedWeightedWindowBenchmarks',pair_parameter='Operation',size_parameter='Count',operations=['Gma','Sinema','Convolution'],methods=methods,sizes=list(COUNTS)),
            dict(fixture='SharedFibonacciWeightedBenchmarks',pair_parameter='Operation',size_parameter='Count',operations=['QuanTAlib.Fwma'],methods=methods,sizes=list(COUNTS)),
            dict(fixture=SINC,pair_parameter='Operation',size_parameter='Count',operations=[PAIR],methods=methods,sizes=list(COUNTS)),
            dict(fixture='SharedAlmaWindowBenchmarks',pair_parameter='Operation',size_parameter='Count',operations=ALMA,methods=methods,sizes=list(COUNTS))]

def all_tasks():
    return [t for shard in shards() for t in shard['tasks']]

def verify_source_boundary():
    assert set(p.git('diff','--name-only',OLD,NEW,'--','src','tests',p.BENCH,p.TESTS).splitlines())==PATHS
    assert not p.git('diff','--name-only',OLD,NEW,'--',*prior.HARNESS_SCOPES)

def verify_publication(plan, *, current=False):
    prior.verify_published_baseline(plan)
    if current:return prior.verify_current_publication(plan)
    return plan['published_baseline']

def harness_role_alias(plan,old,new):
    verify_source_boundary()
    actual={role:auth['manifest']['runtime_files'][prior.HARNESS_PATH] for role,auth in [('old',old),('successor',new)]}
    assert actual==dict(old=plan['old_candidate']['actual_harness_sha256'],successor=plan['candidate']['actual_harness_sha256'])
    assert actual['old']==plan['harness_sha256']
    trees={role:p.git('rev-parse',source+':'+p.BENCH) for role,source in [('old',OLD),('successor',NEW)]}
    assert trees['old']==trees['successor']
    return dict(policy=plan['harness_alias_policy'],actual_artifact_harness_sha256=actual,
        actual_artifact_manifest_sha256=dict(old=plan['old_candidate']['manifest_sha256'],successor=plan['candidate']['manifest_sha256']),
        common_harness_sha256=actual['old'],common_artifact_role='old',benchmark_source_trees=trees,
        source_scopes=prior.HARNESS_SCOPES,scoped_source_identical=True,actual_artifact_binary_identical=actual['old']==actual['successor'])

def verify_source_scoped_reuse(plan):
    """Reuse recorded checks as source evidence, never as executions on aligned binaries."""
    import gzip
    import tempfile
    descriptor=json.loads(prior.evidence_entry(plan['source_scoped_baseline_reuse']))
    assert descriptor['status']=='explicit-source-scoped-baseline-correctness-reuse'
    assert descriptor['aligned_source']==OLD and descriptor['original_source']=='d3d5c905a5d75b666b440aa6c5f6b4c24b42dc91'
    assert descriptor['aligned_actual_checks']==dict(unit=35,parity=53) and descriptor['original_actual_checks']==dict(unit=53,parity=48)
    assert descriptor['timed_out_supplement_credit']==0 and descriptor['claim_original_tests_executed_on_aligned_binaries'] is False
    assert descriptor['claim_artifact_binary_identity'] is False
    assert descriptor['aligned_manifest_sha256']==plan['old_candidate']['manifest_sha256']
    assert descriptor['aligned_core_sha256']==plan['old_candidate']['production_sha256']
    assert descriptor['aligned_unit_assembly_sha256']=='a954dde821b7a5c6a8733ddf8a781026d2cbf5fa382f0d255dda2e86a0fdf3bd'
    assert descriptor['aligned_parity_assembly_sha256']=='cad1b95ddaadbdb8534cb50764ecd28134b13dfd418d20790e22130e49a21b0c'
    assert descriptor['aligned_harness_sha256']==plan['old_candidate']['actual_harness_sha256']
    root=json.loads(prior.evidence_entry(descriptor['original_cpu_root_review']))
    assert descriptor['original_cpu_root_review']['sha256']=='54f7a87f11b219633941227618ef003d5da69c36e01677abbe039c1e7cb17c17'
    assert root['status']=='root-independent-cpu-authentication-passed' and root['source']==descriptor['original_source'] and (root['unit'],root['parity'])==(53,48)
    raw=gzip.decompress(prior.evidence_entry(descriptor['original_manifest']))
    assert sha(raw)==descriptor['original_manifest_sha256']==root['manifest_sha256']=='60a4a285d4a7879e1be3c667c2757ae3f56a772f26d64c005fa91c392b5b9ce3'
    manifest=json.loads(raw)
    assert manifest['source']==descriptor['original_source'] and manifest['source_sha256']==root['source_fingerprint']
    assert manifest['runtime_files'][prior.HARNESS_PATH]==descriptor['original_harness_sha256']
    for prefix in ('src/bin/Release/net10.0','tests/bin/Release/net10.0',p.BENCH+'/bin/Release/net10.0',p.TESTS+'/bin/Release/net10.0'):
        assert manifest['runtime_files'][prefix+'/OoplesFinance.StockIndicators.dll']==descriptor['original_core_sha256']==root['core_sha256']
    assert descriptor['original_unit_assembly_sha256']==manifest['runtime_files']['tests/bin/Release/net10.0/OoplesFinance.StockIndicators.Tests.Unit.dll']
    assert descriptor['original_parity_assembly_sha256']==manifest['runtime_files'][p.TESTS+'/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorTests.dll']
    assert descriptor['original_core_sha256']!=descriptor['aligned_core_sha256']
    for label,passed in [('unit',53),('parity',48)]:
        trx=gzip.decompress(prior.evidence_entry(descriptor['original_'+label+'_trx']))
        assert sha(trx)==manifest['checks'][label]['sha256']
        with tempfile.TemporaryDirectory(prefix='source-scoped-trx-') as temp:
            path=Path(temp)/'checks.trx';path.write_bytes(trx);checks=p.verify_trx(path,manifest['classes'][label])
        assert checks==manifest['checks'][label] and checks['passed']==passed
    assert not p.git('diff','--name-only',descriptor['original_source'],OLD,'--','src','tests')
    excluded=[p.BENCH+'/LibraryComparisons/AlmaComparison.cs',p.TESTS+'/AlmaPublisherAlignmentTests.cs']
    assert descriptor['excluded_alma_alignment_paths']==excluded
    assert set(p.git('diff','--name-only',descriptor['original_source'],OLD,'--',*prior.HARNESS_SCOPES).splitlines())==set(excluded)
    return descriptor

def verify_evidence(plan, *, require_ready=True):
    verify_source_scoped_reuse(plan)
    for role,key,source,unit,parity in [('baseline','old_candidate',OLD,35,53),('candidate','candidate',NEW,134,86)]:
        c=plan[key];fields=[role+'_cpu_root_review',role+'_gpu_root_review']
        if any(not re.fullmatch('[a-f0-9]{64}',plan[f]['sha256']) for f in fields):
            assert not require_ready, 'UNBOUND combined CPU/GPU root evidence'
            continue
        cpu,gpu=[json.loads(prior.evidence_entry(plan[f])) for f in fields]
        assert cpu['status']=='root-independent-cpu-authentication-passed' and cpu['source']==source and (cpu['unit'],cpu['parity'])==(unit,parity)
        for a,b in [('run_id','ci_run'),('source_fingerprint','source_fingerprint'),('manifest_sha256','manifest_sha256'),('core_sha256','production_sha256'),('actual_harness_sha256','actual_harness_sha256')]:assert cpu[a]==c[b]
        assert gpu['status']=='root-independent-actual-gpu-authentication-passed' and gpu['source']==source and gpu['run_id']==c['ci_run']
        assert gpu['source_fingerprint']==c['source_fingerprint'] and gpu['passed_cases']==100 and gpu['methods']==12
        assert gpu['cpu_root_review_sha256']==plan[fields[0]]['sha256'] and gpu['receipt_sha256']==c['gpu_receipt_sha256']
    entry=plan['combined_route_root_review']
    if not re.fullmatch('[a-f0-9]{64}',entry['sha256']):
        assert not require_ready, 'UNBOUND combined route evidence'
        return
    review=json.loads(prior.evidence_entry(entry))
    assert review['status']=='root-independent-alma-sinc-combined-route-authentication-passed'
    assert review['source']==NEW and review['baseline_source']==OLD
    assert review['candidate_core_sha256']==plan['candidate']['production_sha256']
    assert review['cpu_root_review_sha256']==plan['candidate_cpu_root_review']['sha256'] and review['gpu_root_review_sha256']==plan['candidate_gpu_root_review']['sha256']
    assert review['sinc_actual_builders_equal'] is True and review['alma_actual_paths_equal'] is True and review['alma_cached_multiply_route_observed'] is True


def shards():
    tasks=[]
    for row in consumer_registry():
        owned=sum(m in p.OWNED for m in row['methods']);native=len(row['methods'])-owned
        operations=[dict(raw=raw,pair='QuanTAlib.'+raw if row['fixture']=='SharedWeightedWindowBenchmarks' else raw) for raw in row['operations']]
        tasks.append(dict(fixture=row['fixture'],operations=operations,methods=row['methods'],implicit_pair=False,cases=len(operations)*(8*owned+2*native),decisions=len(operations)*2*owned))
    return [dict(index=0,tasks=[tasks[0],tasks[4]],cases=90,decisions=18),dict(index=1,tasks=tasks[1:4],cases=100,decisions=20)]


def case_inventory():
    from .checkpoints import filters
    metadata={r['fixture']:r for r in consumer_registry()};records=[]
    for shard in shards():
        for index,arm in enumerate(prior.common.POLICY['order']):
            for task in shard['tasks']:
                row=metadata[task['fixture']]
                for operation in task['operations']:
                    for method in p.methods_for(task,index):
                        for bars in COUNTS:
                            records.append(dict(shard=shard['index'],round=index,arm=arm,fixture=task['fixture'],raw=operation['raw'],pair=operation['pair'],method=method,bars=bars,filters=filters(task['fixture'],operation['raw'],[method]),parameters={row['pair_parameter']:operation['raw'],row['size_parameter']:bars}))
    assert len(records)==190 and [sum(r['round']==i for r in records) for i in range(4)]==[38,38,76,38]
    return records




def validate_scope(plan, *, require_ready=True):
    assert plan['alma_sinc_combined_qualification'] is True and not plan.get('diagnostic_kind') and not plan.get('diagnostic_only')
    assert (plan['cases'],plan['decisions'])==(190,38)
    assert plan['matrix']==dict(shards=2,max_parallel=2,timeout_minutes_per_shard=120)
    assert plan['targeted_shards']==shards() and plan['targeted_reference']==dict(scope=SCOPE)
    assert plan['sources']==dict(published=OLD,old=OLD,successor=NEW,variants=OLD) and plan['gpu_sources']==dict(old=OLD,successor=NEW,variants=OLD)
    assert plan['measurement_policy']==prior.common.POLICY and plan['runtime_policy_overrides']=={}
    assert plan['runtime_dependency_alignment']==dict(tensor_version='0.151.1')
    assert plan['full_consumer_registry']==consumer_registry() and plan['requested_case_inventory']==case_inventory()
    assert plan['native_progress_gate']==NATIVE_POLICY and not plan.get('diagnostic_shards') and not plan.get('reuse_inputs')
    assert plan['require_fixture_inventory'] is True and (plan['proposed_dotnet_sdk'],plan['expected_runtime_version'])==('10.0.403','.NET 10.0.15')
    assert plan['lp_afirma_report_row_credit']==0 and plan['lp_afirma_snapshot_excluded'] is True
    assert plan['production_integrated'] is False and plan['report_import_permitted'] is False and plan['automatic_qualification'] is False and plan['closure_ledger_new_rows']==0
    assert plan['old_candidate']['unit_classes']==BASE_UNIT and plan['old_candidate']['parity_classes']==BASE_PARITY
    assert plan['candidate']['unit_classes']==UNIT and plan['candidate']['parity_classes']==PARITY
    assert plan['cpu_test_classes']==dict(old=BASE_UNIT,successor=UNIT,variants=BASE_UNIT)
    assert plan['harness_alias_policy']==dict(common_artifact_role='old',variants_artifact_role='old',baseline_source=OLD,candidate_source=NEW,require_scoped_source_identity=True,claim_artifact_binary_identity=False)
    assert plan['old_candidate']['source']==OLD and plan['candidate']['source']==NEW
    assert type(plan['inputs_ready']) is bool and type(plan['parent_review_approved']) is bool and type(plan['peer_review_approved']) is bool
    if require_ready:assert plan['inputs_ready'] is True and plan['parent_review_approved'] is True and plan['peer_review_approved'] is True,'HELD: root and peer review before SDK/input I/O'
    prior.verify_native_policy(plan);verify_source_boundary();verify_publication(plan);verify_screens(plan);verify_evidence(plan,require_ready=require_ready)
    for source in (OLD,NEW):
        for row in consumer_registry():
            text=blob(source,p.BENCH+'/LibraryComparisons/'+row['fixture']+'.cs').decode()
            assert re.search(r'\[Params\(1_?000,\s*10_?000\)\]\s*public int '+row['size_parameter'],text)
            assert 'public string '+row['pair_parameter'] in text
            assert all(re.search(r'\b'+method+r'\s*\(',text) for method in row['methods'])
            if row['fixture']!='LibraryPairBenchmarks':assert all('"'+raw+'"' in text for raw in row['operations'])


def verify_screens(plan):
    pins={'sinc':('44464894451','bedcb62e1bb44fa8ec6aa9167159246e60cc20f2','3b5f2ca831146b70dec237f176da1882776a71d05c7f4299e38c8ecc4ad4f447','d856beb589a0cfd95239e401923d3c25423bd3670f58acb2ad41067c0a4cba2f'),
          'alma':('44466844849','600a096a570abd8a0d488e45177126978cf20d85','4becc6ac0a1cc81a569bb0f918dac93a7ec0697a41c5abbd5f6d095e5c1eec38','32e1ad518096eeb796ebbb2d5eea06dc573d9814e06edd0f2a9c01c9f0e4109a')}
    for family,(run,head,summary_sha,root_sha) in pins.items():
        gate=plan['screen_gates'][family]
        assert gate['run_id']==run and gate['head']==head and gate['summary']['sha256']==summary_sha and gate['root_review']['sha256']==root_sha
        summary,root=[json.loads(prior.evidence_entry(gate[key])) for key in ('summary','root_review')]
        assert (summary['case_count'],summary['decision_count'])==(30,6) and summary['accepted'] is False and summary['qualification'] is False
        assert summary['feasibility_screen']['passed'] is True and root['run_id']==run and root['head']==head and root['summary_sha256']==summary_sha
        assert root['status']=='root-independent-'+family+'-roi-authentication-passed' and root['screen_passed'] is True and root['native_math_independently_recomputed'] is True
        assert root['retained_evidence_files_changed']==0 and root['qualification'] is False and root['closure_credit']==0


def verify_authentication(folder,plan,proof):
    validate_scope(plan)
    assert proof['preparation_mode']==MODE
    assert proof['published_baseline']==verify_publication(plan)
    assert proof['source_scoped_baseline_reuse']==plan['source_scoped_baseline_reuse']
    raw = (folder/'case-selection-inventory.json').read_bytes()
    assert sha(raw)==proof['case_selection_inventory_sha256'] and json.loads(raw)==case_inventory()
    verify_harness_alias(folder,plan,proof)
    authenticated = {}
    for label,key in [('old','old_candidate'),('successor','candidate')]:
        auth = prior.checked_inputs(folder/(label+'-authentication'),plan[key])
        authenticated[label] = auth
        assert proof['production_sha256'][label]==auth['production']
        assert proof['tested_runtime_dependencies'][label]==auth['closure']
        assert proof['checks'][label]==dict(auth['manifest']['checks']['unit'],original_gpu_source=plan[key]['source'])
        for suffix,raw in [('.trx',auth['read']('evidence/unit.trx')),('-parity.trx',auth['read']('evidence/parity.trx')),
                           ('-original-gpu-receipt.json',auth['gpu_receipt']),('-original-gpu-gpu.trx.gz',auth['gpu_trx'])]:
            assert (folder/'evidence'/(label+suffix)).read_bytes()==raw
    assert proof['harness_role_alias']==harness_role_alias(plan,authenticated['old'],authenticated['successor'])
    prefix = p.BENCH+'/bin/Release/net10.0/'
    assert proof['common_manifest']=={name[len(prefix):]:value for name,value in authenticated['old']['manifest']['runtime_files'].items()
                                      if name.startswith(prefix)}

def prepare(old_candidate,candidate,out):
    plan = p.read(p.PLAN)
    validate_scope(plan)
    publication = verify_publication(plan,current=True)
    assert subprocess.check_output(['dotnet','--version'],text=True).strip()==plan['proposed_dotnet_sdk']
    old_candidate,candidate,out = map(lambda x:Path(x).resolve(),(old_candidate,candidate,out))
    assert not out.exists() and not out.is_relative_to(old_candidate) and not out.is_relative_to(candidate)
    old,new = prior.checked_inputs(old_candidate,plan['old_candidate']),prior.checked_inputs(candidate,plan['candidate'])
    assert old['closure']==new['closure']
    alias = harness_role_alias(plan,old,new)
    out.mkdir()
    for name in ('common','production','evidence'):(out/name).mkdir()
    for label,folder in [('old',old_candidate),('successor',candidate)]:
        shutil.copytree(folder,out/(label+'-authentication'))
    prefix = p.BENCH+'/bin/Release/net10.0/'
    for name,value in old['manifest']['runtime_files'].items():
        if name.startswith(prefix):
            path = out/'common'/name[len(prefix):]
            path.parent.mkdir(parents=True,exist_ok=True)
            path.write_bytes(old['read']('blobs/'+value))
    checks,production,dependencies = {},{},{}
    for label,auth,source in [('old',old,OLD),('successor',new,NEW),('variants',old,OLD)]:
        (out/'production'/(label+'.dll')).write_bytes(auth['read']('blobs/'+auth['production']))
        checks[label] = dict(auth['manifest']['checks']['unit'],original_gpu_source=source)
        production[label],dependencies[label] = auth['production'],auth['closure']
        for suffix,raw in [('.trx',auth['read']('evidence/unit.trx')),('-parity.trx',auth['read']('evidence/parity.trx')),
                           ('-original-gpu-receipt.json',auth['gpu_receipt']),('-original-gpu-gpu.trx.gz',auth['gpu_trx'])]:
            (out/'evidence'/(label+suffix)).write_bytes(raw)
    proof = dict(plan_sha256=p.digest(p.PLAN),sources=plan['sources'],
        source_trees={label:p.git('rev-parse',plan['sources'][label]+':src') for label in ('old','successor','variants')},
        sdk=plan['proposed_dotnet_sdk'],dotnet_info=subprocess.check_output(['dotnet','--info'],text=True),
        workflow_head=p.git('rev-parse','HEAD'),run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],
        preparation_mode=MODE,source_authentication=prior.source_authentication(plan),harness_role_alias=alias,source_scoped_baseline_reuse=plan['source_scoped_baseline_reuse'],
        published_baseline=publication,common_manifest=p.manifest(out/'common'),evidence_manifest=p.manifest(out/'evidence'),
        production_sha256=production,checks=checks,tested_runtime_dependencies=dependencies)
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256'] = record_inventory(out,plan,p.digest(p.PLAN))
    raw = (json.dumps(case_inventory(),indent=2)+'\n').encode()
    (out/'case-selection-inventory.json').write_bytes(raw)
    proof['case_selection_inventory_sha256'] = sha(raw)
    p.write(out/'provenance.json',proof)
    p.verify_inputs(out)
    return proof

def expected_contexts():
    return {(t['fixture'],o['pair'],n,m) for t in all_tasks() for o in t['operations'] for n in COUNTS for m in t['methods'] if m in p.OWNED}


def native_rows(folder):
    from .verify import archived,sample
    from .checkpoints import case_key
    roots=[x.parent for x in Path(folder).glob('*/provenance.json')];assert len(roots)==2
    metadata={r['fixture']:r for r in consumer_registry()};result=[];seen=set()
    for root in roots:
        proof,state=p.read(root/'provenance.json'),p.read(root/'status.json');shard=int(root.name.removeprefix('shard'))
        assert shard in (0,1) and shard not in seen;seen.add(shard)
        tasks={t['fixture']:t for t in shards()[shard]['tasks']};rounds={}
        for record in state['rounds']:
            fixture,index=record['fixture'],record['index'];assert fixture in tasks and index in (0,1,2,3) and (fixture,index) not in rounds
            path=archived(root,proof['raw_root'],record['report']);assert p.digest(path)==record['sha256']
            task,contract=tasks[fixture],metadata[fixture];mapping={o['raw']:o['pair'] for o in task['operations']};rows={}
            for case in p.read(path)['Benchmarks']:
                assert case['Type']==fixture;raw,bars,method=case_key(case);assert raw in mapping and method==case['Method']
                assert re.search(r'\b'+contract['pair_parameter']+r': "'+re.escape(raw)+r'"',case['FullName'])
                assert re.search(r'\b'+contract['size_parameter']+r': '+str(bars)+r'(?:,|\))',case['FullName'])
                key=mapping[raw],bars,method;assert key not in rows;rows[key]=sample(case)
            assert set(rows)=={(o['pair'],n,m) for o in task['operations'] for n in COUNTS for m in p.methods_for(task,index)};rounds[fixture,index]=rows
        assert set(rounds)=={(f,i) for f in tasks for i in range(4)}
        for fixture,task in tasks.items():
            for operation in task['operations']:
                pair=operation['pair']
                for bars in COUNTS:
                    native={m:rounds[fixture,2][pair,bars,m] for m in task['methods'] if m not in p.OWNED}
                    for method in (m for m in task['methods'] if m in p.OWNED):result.append(dict(fixture=fixture,pair=pair,bars=bars,method=method,old_rounds={str(i):rounds[fixture,i][pair,bars,method] for i in (0,3)},candidate_rounds={str(i):rounds[fixture,i][pair,bars,method] for i in (1,2)},native_controls=native))
    assert seen=={0,1} and len(result)==38;return result


def adjudicate_feasibility(rows,decisions):
    expected=expected_contexts();indexed={(r['fixture'],r['pair'],r['bars'],r['method']):r for r in rows};own={(d['fixture'],d['pair'],d['bars'],d['method']):d['verdict'] for d in decisions}
    assert len(rows)==len(indexed)==len(decisions)==len(own)==38 and set(indexed)==set(own)==expected and set(own.values())<={'faster','slower','inconclusive'}
    details,primary,builder,protected,reopened=[],[],[],[],[];shared={}
    counts={f:dict(old={'0':0,'3':0},candidate={'1':0,'2':0}) for f in ('all','alma','sinc')}
    for key in sorted(expected):
        row=indexed[key];assert set(row['old_rounds'])=={'0','3'} and set(row['candidate_rounds'])=={'1','2'}
        assert set(row['native_controls'])==({'Competitor'} if row['fixture']=='LibraryPairBenchmarks' else {'NativeSingle','NativeParallel'})
        control_key=key[:3]
        if control_key in shared:assert shared[control_key]==row['native_controls']
        else:shared[control_key]=row['native_controls']
        controls=list(row['native_controls'].values());best_mean=min(v['mean_ns'] for v in controls);best_lower=min(v['lower_ns'] for v in controls)
        old,new=list(row['old_rounds'].values()),list(row['candidate_rounds'].values())
        eligible=all(v['mean_ns']>=best_mean for v in old);separated=all(v['upper_ns']<best_lower for v in new)
        won=row['bars']==10000 and eligible and own[key]=='faster' and separated
        identity={k:row[k] for k in ('fixture','pair','bars','method')}
        if won and row['fixture']==SINC:builder.append(identity)
        if won and row['fixture']=='LibraryPairBenchmarks' and row['pair'] in ALMA:primary.append(identity)
        families=['all']
        if row['fixture']=='LibraryPairBenchmarks' and row['pair'] in ALMA+['QuanTAlib.Gma']:families.append('alma')
        if row['fixture']==SINC or row['fixture']=='LibraryPairBenchmarks' and row['pair']=='QuanTAlib.Gma':families.append('sinc')
        if row['bars']==10000:
            for family in families:
                for i,v in row['old_rounds'].items():counts[family]['old'][i]+=int(v['mean_ns']<best_mean)
                for i,v in row['candidate_rounds'].items():counts[family]['candidate'][i]+=int(v['mean_ns']<best_mean)
        for method,control in row['native_controls'].items():
            if all(v['upper_ns']<control['lower_ns'] for v in old):
                check=dict(**identity,native_method=method,preserved=all(v['mean_ns']<control['mean_ns'] for v in new));protected.append(check)
                if not check['preserved']:reopened.append(check)
        details.append(dict(**row,source_verdict=own[key],best_native_mean_ns=best_mean,best_native_lower_ns=best_lower,baseline_new_win_eligible=eligible,candidate_statistical_native_win=separated))
    gates={family:dict(old_10000_point_win_counts=c['old'],candidate_10000_point_win_counts=c['candidate'],net_10000_point_progress=min(c['candidate'].values())>max(c['old'].values())) for family,c in counts.items()}
    no_slower=all(v!='slower' for v in own.values());passed=no_slower and bool(primary) and bool(builder) and not reopened and all(g['net_10000_point_progress'] for g in gates.values())
    return dict(passed=passed,all_38_source_comparisons_no_regression=no_slower,family_gates=gates,
        new_10000_alma_primary_wins=primary,new_10000_sinc_builder_wins=builder,
        protected_prior_native_wins=protected,prior_native_win_reopenings=reopened,all_context_rows=details,
        minimum_native_margin=None,native_control_round=2,native_policy_id=prior.POLICY_APPROVAL['policy_id'],
        lp_afirma_report_row_credit=0,closure_credit=0,automatic_qualification=False)


def collect(folder):
    from .verify import _collect
    prior.original.verify_runtime(folder,p.read(p.PLAN)['expected_runtime_version'])
    result=_collect(Path(folder),'targeted',preflight=False);assert (result['case_count'],result['decision_count'])==(190,38)
    native=adjudicate_feasibility(native_rows(folder),result['decisions']);source=result['accepted'] and all(d['verdict']!='slower' for d in result['decisions'])
    accepted=source and native['passed']
    result.update(source_gate_accepted=source,native_progress_gate_passed=native['passed'],native_progress_gate=native,
        accepted=accepted,qualification=True,production_integrated=False,report_imported=False,
        qualified_primary_native_wins=native['new_10000_alma_primary_wins'] if accepted else [],
        qualified_builder_native_wins=native['new_10000_sinc_builder_wins'] if accepted else [],
        all_context_prior_native_wins=native['protected_prior_native_wins'],all_context_prior_native_wins_preserved=not native['prior_native_win_reopenings'],
        lp_afirma_report_row_credit=0,closure_ledger_new_rows=0,closure_credit=0,automatic_qualification=False)
    return result
