"""Frozen full-consumer KAMA scope. Execution is held pending input integration."""
import csv
import hashlib
import io
import json
import re
import os
import shutil
import subprocess
from collections import Counter
from xml.etree import ElementTree as ET
from pathlib import Path

REPO=Path(__file__).resolve().parents[2]
PLAN=REPO/'benchmarks/plans/hosted-kama-full-qualification-20261119.json'
INVENTORY_PIN='419502923f19febb7a591e58626a9887fbcd104a44d41c925f02342c7a3ab355'
OLD='4b4e8a385ed1c210998d26de6460e23f5842ffe9'
NEW='9e9fbb08c1d9f6164d861446f27b6d2a393430f3'
OWNED={'Ooples','BuilderFull','BuilderLatestOnly'}
sha=lambda raw:hashlib.sha256(raw).hexdigest()

def verify_workflow_entrypoint(text=None):
    if text is None:text=(REPO/'.github/workflows/kama-full-qualification.yml').read_text()
    assert '  workflow_dispatch:' not in text and '  pull_request:' not in text
    assert '    branches: [perf/kama-full-qualification-20261119]\n' in text
    assert '    timeout-minutes: 360\n' in text
    assert text.index('Fail closed before SDK or input setup')<text.index('actions/setup-dotnet@')<text.index('actions/download-artifact@')
    gate=text.split('Fail closed before SDK or input setup',1)[1].split('actions/setup-dotnet@',1)[0]
    for value in ('$plan.cases -ne 370','$plan.decisions -ne 74','$plan.matrix.max_parallel -ne 1',
        "'peer_review_approved'","'input_authentication_complete'","'collector_integration_complete'",'$plan.supplemental_cpu_coverage.sha256'):
        assert value in gate
    assert '--phase diagnostic' not in text and 'python scripts/run-kama-full-qualification-shards.py' in text

def validate_scope(plan=None, *, require_ready=True):
    if plan is None:plan=json.loads(PLAN.read_bytes())
    assert plan['scope']=='kama-full-qualification-20261119'
    verify_workflow_entrypoint()
    assert (plan['cases'],plan['decisions'])==(370,74)
    assert plan['sources']==dict(published=OLD,old=OLD,successor=NEW,variants=OLD)
    assert plan['gpu_sources']==dict(old=OLD,successor=NEW,variants=OLD)
    assert plan['candidate']['source_fingerprint']=='be4aca86a7c67bbdac47207c99fc4fa4eb51fdd963096caf2285b2751924c5cc'
    assert plan['old_candidate']['source']==OLD and plan['candidate']['source']==NEW
    assert plan['matrix']==dict(shards=3,max_parallel=1,timeout_minutes_per_shard=120)
    assert plan['measurement_policy']==dict(order=['old','new','new','old'],quiet_samples=12,max_cpu_percent=15,warmups=10,iterations=10,fixed_invocation_warmups=1000)
    assert plan['runtime_policy_overrides']=={}
    assert plan['runtime_dependency_alignment']==dict(tensor_version='0.151.1')
    assert (plan['proposed_dotnet_sdk'],plan['expected_runtime_version'])==('10.0.403','.NET 10.0.15')
    evidence=REPO/'benchmarks/plans/evidence/kama-full-qualification'
    assert plan['frozen_inventory']==dict(path='benchmarks/plans/evidence/kama-full-qualification/inventory.json',sha256=INVENTORY_PIN)
    assert sha((evidence/'inventory.json').read_bytes())==INVENTORY_PIN
    inventory=json.loads((evidence/'inventory.json').read_bytes())
    frozen_contracts={}
    for row in inventory['registry']:
        if row['group'] not in inventory['scenarios']['kama_only']['groups']:continue
        if row['fixture'] not in frozen_contracts:
            frozen_contracts[row['fixture']]={k:v for k,v in row.items() if k!='group'}
            frozen_contracts[row['fixture']]['operations']=[]
        frozen_contracts[row['fixture']]['operations'].extend(row['operations'])
    assert plan['full_consumer_registry']==list(frozen_contracts.values())
    for filename,pin in plan['frozen_files'].items():
        assert filename in ('case-tuples.csv','pair-profile-size-tuples.csv','alma-sinc-protection-edges.csv')
        assert sha((evidence/filename).read_bytes())==pin
        assert pin==next(a['sha256'] for a in inventory['artifacts'] if Path(a['path']).name==filename)
    assert set(plan['frozen_files'])=={'case-tuples.csv','pair-profile-size-tuples.csv','alma-sinc-protection-edges.csv'}
    frozen=list(csv.DictReader(io.StringIO((evidence/'case-tuples.csv').read_text())))
    expected={(r['fixture'],r['pair'],r['method'],int(r['bars']),int(r['round'])) for r in frozen if 'kama_only' in r['scenario_memberships'].split('|')}
    assert len(expected)==370 and plan['frozen_case_keys']==[list(k) for k in sorted(expected)]
    keys=set();decisions=set();seen_tasks=set();total_cases=total_decisions=0
    contracts={r['fixture']:r for r in plan['full_consumer_registry']}
    assert len(contracts)==len(plan['full_consumer_registry'])==10
    for i,shard in enumerate(plan['targeted_shards']):
        assert shard['index']==i
        cases=count=0
        for task in shard['tasks']:
            fixture=task['fixture'];assert fixture not in seen_tasks;seen_tasks.add(fixture)
            contract=contracts[fixture]
            assert task['operations']==contract['operations'] and task['methods']==contract['methods']
            assert contract['sizes']==[1000,10000] and task['implicit_pair'] is False
            before=len(keys);prior=len(decisions)
            for operation in task['operations']:
                for bars in contract['sizes']:
                    for method in task['methods']:
                        if method in OWNED:decisions.add((fixture,operation['pair'],bars,method))
                        for index in range(4):
                            if index==2 or method in OWNED:
                                key=(fixture,operation['pair'],method,bars,index)
                                assert key not in keys
                                keys.add(key)
            assert task['cases']==len(keys)-before and task['decisions']==len(decisions)-prior
            cases+=task['cases'];count+=task['decisions']
        assert (shard['cases'],shard['decisions'])==(cases,count)
        total_cases+=cases;total_decisions+=count
    assert len(plan['targeted_shards'])==3 and seen_tasks==set(contracts)
    assert keys==expected and (total_cases,total_decisions)==(370,74)
    edges=list(csv.DictReader(io.StringIO((evidence/'alma-sinc-protection-edges.csv').read_text())))
    assert len(edges)==41
    gate=plan['native_progress_gate']
    from . import kama_fused_prefix_roi as prior
    assert gate==dict(prior.NATIVE_POLICY,protect_all_affected_consumers_both_sizes=True,
        protection_edge_count=41,alma_sinc_controls_only=True,ema_t3_controls_only=True,lp_afirma_report_row_credit=0)
    assert gate['protect_all_affected_consumers_both_sizes'] is True and gate['protection_edge_count']==41
    assert gate['alma_sinc_controls_only'] is True and gate['ema_t3_controls_only'] is True and gate['lp_afirma_report_row_credit']==0
    assert all(plan[k] is False for k in ('production_qualification','automatic_qualification','report_import_permitted'))
    assert plan['closure_credit']==0
    assert all(type(plan[k]) is bool for k in ('inputs_ready','parent_review_approved','peer_review_approved'))
    if require_ready:
        assert all(plan[k] is True for k in ('inputs_ready','parent_review_approved','peer_review_approved',
            'input_authentication_complete','collector_integration_complete')),'HELD: full-scope integration and root timing review required'
        verify_cpu_coverage(plan)
        from . import kama_fused_prefix_roi as prior
        prior.verify_source_boundary();prior.verify_registry();prior.verify_publication(plan)
        prior.verify_root_evidence(plan)
    return dict(cases=total_cases,decisions=total_decisions,shards=3,max_parallel=1,protection_edges=len(edges),timing_launched=False)

def verify_cpu_coverage(plan):
    from . import kama_fused_prefix_roi as prior
    review=json.loads(prior.evidence(plan['supplemental_cpu_coverage']))
    assert review['status']=='root-independent-kama-full-cpu-coverage-passed'
    assert review['sources']=={'baseline':OLD,'candidate':NEW}
    assert review['combined_coverage_totals']==dict(candidate=dict(unit=196,parity=129),baseline=dict(unit=179,parity=125))
    assert review['original_ci_manifests_and_counts_unchanged'] is True
    assert review['coverage_assembled_from_multiple_actual_sessions'] is True
    assert review['actual_test_names_independently_verified'] is True
    assert review['original_filter_19_expands_to_15_validation_ema_and_4_models_tsi'] is True
    assert review['source_fingerprints']==dict(baseline=plan['old_candidate']['source_fingerprint'],candidate=plan['candidate']['source_fingerprint'])
    bundle=json.loads(prior.evidence(plan['supplemental_cpu_bundle']))
    assert bundle['root_review_sha256']==plan['supplemental_cpu_coverage']['sha256']
    assert bundle['original_ci_manifests_and_counts_unchanged'] is True
    entries={};origins={}
    for entry in bundle['files']:
        assert entry['path'] not in entries and entry['origin_path'] not in origins
        assert entry['path'].startswith('benchmarks/plans/evidence/kama-full-qualification/cpu-coverage/')
        raw=prior.evidence(dict(path=entry['path'],sha256=entry['sha256']))
        entries[entry['path']]=(entry,raw);origins[entry['origin_path']]=entry
    for origin,pin in review['actual_local_trx_pins'].items():assert origins[origin]['sha256']==pin
    assert next(e['sha256'] for e,_ in entries.values() if e['path'].endswith('/original/packet.json'))==review['packet_sha256']
    assert next(e['sha256'] for e,_ in entries.values() if e['path'].endswith('/repair/repair-summary.json'))==review['repair_summary_sha256']
    raw_by_hash={entry['sha256']:raw for entry,raw in entries.values()}
    for role in ('baseline','candidate'):
        expected=plan['old_candidate' if role=='baseline' else 'candidate']
        manifest_raw=next(raw for entry,raw in entries.values() if entry['path'].endswith('/original-ci/'+role+'/manifest.json'))
        assert sha(manifest_raw)==expected['manifest_sha256']
        manifest=json.loads(manifest_raw)
        assert manifest['source']==expected['source']
        for suite in ('unit','parity'):
            assert manifest['checks'][suite]['passed']==expected[suite+'_passed']
            original=next(raw for entry,raw in entries.values() if entry['path'].endswith('/original-ci/'+role+'/evidence/'+suite+'.trx'))
            assert sha(original)==manifest['checks'][suite]['sha256']
            check=review['checks'][role][suite];counts=Counter();names=set()
            for session in check['sessions']:
                tree=ET.fromstring(raw_by_hash[session['trx_sha256']])
                classes={test.attrib['id']:test.find('./{*}TestMethod').attrib['className'] for test in tree.findall('.//{*}UnitTest')}
                results=tree.findall('.//{*}UnitTestResult')
                assert len(results)==session['passed'] and all(r.attrib['outcome']=='Passed' for r in results)
                for result in results:
                    name=result.attrib['testName'];assert name not in names;names.add(name)
                    counts[classes[result.attrib['testId']]]+=1
            assert dict(counts)==check['actual_exact_class_counts'] and sum(counts.values())==check['passed']==review['combined_coverage_totals'][role][suite]
            if suite=='unit':
                assert counts['OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.EmaRecurrenceTests']==15
                assert counts['OoplesFinance.StockIndicators.Tests.Unit.ModelsTests.TsiWordEmaRecurrenceTests']==4
    return review

def case_inventory(plan):
    from .checkpoints import filters
    rows=[];contracts={r['fixture']:r for r in plan['full_consumer_registry']}
    for shard in plan['targeted_shards']:
        for index,arm in enumerate(plan['measurement_policy']['order']):
            for task in shard['tasks']:
                contract=contracts[task['fixture']]
                for operation in task['operations']:
                    for method in task['methods']:
                        if index!=2 and method not in OWNED:continue
                        for bars in (1000,10000):
                            rows.append(dict(shard=shard['index'],round=index,arm=arm,fixture=task['fixture'],
                                raw=operation['raw'],pair=operation['pair'],method=method,bars=bars,
                                filters=filters(task['fixture'],operation['raw'],[method]),
                                parameters={contract['pair_parameter']:operation['raw'],contract['size_parameter']:bars}))
    assert len(rows)==370
    return rows

def verify_authentication(folder,plan,proof):
    from . import pipeline as p
    from . import kama_fused_prefix_roi as prior
    validate_scope(plan)
    assert proof['workflow_event']=='push' and proof['workflow_ref']=='refs/heads/perf/kama-full-qualification-20261119'
    assert proof['preparation_mode']=='authenticated-kama-full-qualification'
    assert proof['published_baseline']==prior.verify_publication(plan)
    assert proof['supplemental_cpu_coverage']==verify_cpu_coverage(plan)
    raw=(folder/'case-selection-inventory.json').read_bytes()
    assert sha(raw)==proof['case_selection_inventory_sha256'] and json.loads(raw)==case_inventory(plan)
    prior.verify_harness_alias(folder,plan,proof)
    bridge=proof['common_harness_bridge']
    raw=(folder/'evidence/common-harness-bridge.json').read_bytes()
    assert sha(raw)==bridge['receipt_sha256'] and bridge['runner_source_pins']==plan['runner_source_pins']
    assert bridge['expected_sha256']==plan['worker_bridge_expected']['sha256']
    assert bridge['common_runtime_unchanged'] is True and bridge['temporary_core_only_replacement'] is True
    prior.verify_bootstrap_evidence(folder,plan,bridge);prior.verify_bridge_record(plan,json.loads(raw))
    authenticated={}
    for label,key in [('old','old_candidate'),('successor','candidate')]:
        auth=prior.checked_inputs(folder/(label+'-authentication'),plan[key]);authenticated[label]=auth
        assert proof['production_sha256'][label]==auth['production'] and proof['tested_runtime_dependencies'][label]==auth['closure']
        assert proof['checks'][label]==dict(auth['manifest']['checks']['unit'],original_gpu_source=plan[key]['source'])
        for suffix,raw in [('.trx',auth['read']('evidence/unit.trx')),('-parity.trx',auth['read']('evidence/parity.trx')),
            ('-original-gpu-receipt.json',auth['gpu_receipt']),('-original-gpu-gpu.trx.gz',auth['gpu_trx'])]:
            assert (folder/'evidence'/(label+suffix)).read_bytes()==raw
    assert proof['harness_role_alias']==prior.harness_role_alias(plan,authenticated['old'],authenticated['successor'])
    prefix=p.BENCH+'/bin/Release/net10.0/'
    assert proof['common_manifest']=={name[len(prefix):]:value for name,value in authenticated['old']['manifest']['runtime_files'].items() if name.startswith(prefix)}

def prepare(old_candidate,candidate,out):
    from . import pipeline as p
    from . import kama_fused_prefix_roi as prior
    plan=p.read(p.PLAN);validate_scope(plan)
    assert os.environ['GITHUB_EVENT_NAME']=='push' and os.environ['GITHUB_REF']=='refs/heads/perf/kama-full-qualification-20261119'
    publication=prior.verify_publication(plan,current=True)
    assert subprocess.check_output(['dotnet','--version'],text=True).strip()==plan['proposed_dotnet_sdk']
    old_candidate,candidate,out=map(lambda x:Path(x).resolve(),(old_candidate,candidate,out))
    assert not out.exists() and not out.is_relative_to(old_candidate) and not out.is_relative_to(candidate)
    old,new=prior.checked_inputs(old_candidate,plan['old_candidate']),prior.checked_inputs(candidate,plan['candidate'])
    assert old['closure']==new['closure']
    alias=prior.harness_role_alias(plan,old,new)
    out.mkdir();(out/'common').mkdir();(out/'production').mkdir();(out/'evidence').mkdir()
    for label,folder in [('old',old_candidate),('successor',candidate)]:shutil.copytree(folder,out/(label+'-authentication'))
    prefix=p.BENCH+'/bin/Release/net10.0/'
    for name,value in old['manifest']['runtime_files'].items():
        if name.startswith(prefix):
            path=out/'common'/name[len(prefix):];path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(old['read']('blobs/'+value))
    checks={};production={};dependencies={}
    for label,auth,source in [('old',old,OLD),('successor',new,NEW),('variants',old,OLD)]:
        (out/'production'/(label+'.dll')).write_bytes(auth['read']('blobs/'+auth['production']))
        checks[label]=dict(auth['manifest']['checks']['unit'],original_gpu_source=source)
        production[label]=auth['production'];dependencies[label]=auth['closure']
        for suffix,raw in [('.trx',auth['read']('evidence/unit.trx')),('-parity.trx',auth['read']('evidence/parity.trx')),
            ('-original-gpu-receipt.json',auth['gpu_receipt']),('-original-gpu-gpu.trx.gz',auth['gpu_trx'])]:
            (out/'evidence'/(label+suffix)).write_bytes(raw)
    proof=dict(plan_sha256=p.digest(p.PLAN),sources=plan['sources'],source_trees={label:p.git('rev-parse',plan['sources'][label]+':src') for label in ('old','successor','variants')},
        sdk=plan['proposed_dotnet_sdk'],dotnet_info=subprocess.check_output(['dotnet','--info'],text=True),workflow_head=p.git('rev-parse','HEAD'),
        run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],workflow_event=os.environ['GITHUB_EVENT_NAME'],workflow_ref=os.environ['GITHUB_REF'],
        preparation_mode='authenticated-kama-full-qualification',source_authentication=prior.source_authentication(plan),
        supplemental_cpu_coverage=verify_cpu_coverage(plan),harness_role_alias=alias,published_baseline=publication,
        common_manifest=p.manifest(out/'common'),evidence_manifest=p.manifest(out/'evidence'),production_sha256=production,checks=checks,tested_runtime_dependencies=dependencies)
    proof['common_harness_bridge']=prior.common_harness_bridge(out,plan)
    proof['evidence_manifest']=p.manifest(out/'evidence')
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256']=record_inventory(out,plan,p.digest(p.PLAN))
    raw=(json.dumps(case_inventory(plan),indent=2)+'\n').encode()
    (out/'case-selection-inventory.json').write_bytes(raw);proof['case_selection_inventory_sha256']=sha(raw)
    p.write(out/'provenance.json',proof);p.verify_inputs(out)
    return proof

def expected_contexts(plan):
    return {(t['fixture'],o['pair'],n,m) for shard in plan['targeted_shards'] for t in shard['tasks']
        for o in t['operations'] for n in (1000,10000) for m in t['methods'] if m in OWNED}

def native_rows(folder,plan):
    """Authenticate every full-scope archived report; never pool hosts or rounds."""
    from . import pipeline as p
    from .verify import archived,sample
    from .checkpoints import case_key
    roots=[x.parent for x in Path(folder).glob('*/provenance.json')]
    assert len(roots)==3
    contracts={r['fixture']:r for r in plan['full_consumer_registry']}
    result=[];seen=set()
    for root in roots:
        shard=int(root.name.removeprefix('shard'))
        assert shard in (0,1,2) and shard not in seen;seen.add(shard)
        proof=p.read(root/'provenance.json');state=p.read(root/'status.json')
        tasks={t['fixture']:t for t in plan['targeted_shards'][shard]['tasks']};rounds={}
        for record in state['rounds']:
            fixture,index=record['fixture'],record['index']
            assert fixture in tasks and index in (0,1,2,3) and (fixture,index) not in rounds
            path=archived(root,proof['raw_root'],record['report']);assert p.digest(path)==record['sha256']
            contract=contracts[fixture];task=tasks[fixture];mapping={o['raw']:o['pair'] for o in task['operations']};rows={}
            for case in p.read(path)['Benchmarks']:
                assert case['Type']==fixture
                raw,bars,method=case_key(case)
                assert raw in mapping and method==case['Method']
                assert re.search(r'\b'+contract['pair_parameter']+r': "'+re.escape(raw)+r'"',case['FullName'])
                assert re.search(r'\b'+contract['size_parameter']+r': '+str(bars)+r'(?:,|\))',case['FullName'])
                key=mapping[raw],bars,method;assert key not in rows;rows[key]=sample(case)
            assert set(rows)=={(o['pair'],n,m) for o in task['operations'] for n in (1000,10000) for m in p.methods_for(task,index)}
            rounds[fixture,index]=rows
        assert set(rounds)=={(f,i) for f in tasks for i in range(4)}
        for fixture,task in tasks.items():
            for operation in task['operations']:
                pair=operation['pair']
                for bars in (1000,10000):
                    native={m:rounds[fixture,2][pair,bars,m] for m in task['methods'] if m not in OWNED}
                    for method in (m for m in task['methods'] if m in OWNED):
                        result.append(dict(fixture=fixture,pair=pair,bars=bars,method=method,
                            old_rounds={str(i):rounds[fixture,i][pair,bars,method] for i in (0,3)},
                            candidate_rounds={str(i):rounds[fixture,i][pair,bars,method] for i in (1,2)},native_controls=native))
    assert seen=={0,1,2} and len(result)==74
    return result

def adjudicate_feasibility(rows,decisions,plan):
    """Keep original KAMA target gate; extend no-regression/protection to all74."""
    from . import kama_fused_prefix_roi as prior
    indexed={(r['fixture'],r['pair'],r['bars'],r['method']):r for r in rows}
    verdicts={(d['fixture'],d['pair'],d['bars'],d['method']):d['verdict'] for d in decisions}
    assert len(rows)==len(indexed)==len(decisions)==len(verdicts)==74
    assert set(indexed)==set(verdicts)==expected_contexts(plan)
    assert set(verdicts.values())<={'faster','slower','inconclusive'}
    pilot_keys={(prior.FIXTURE,pair,n,'Ooples') for pair in prior.PAIRS for n in (1000,10000)}
    pilot=prior.adjudicate_feasibility([indexed[k] for k in sorted(pilot_keys)],
        [d for d in decisions if (d['fixture'],d['pair'],d['bars'],d['method']) in pilot_keys])
    protected_by_key={};controls={}
    for key,row in indexed.items():
        expected_native={'Competitor'} if row['fixture']=='LibraryPairBenchmarks' else {'NativeSingle','NativeParallel'}
        assert set(row['native_controls'])==expected_native
        assert set(row['old_rounds'])=={'0','3'} and set(row['candidate_rounds'])=={'1','2'}
        context=key[:3]
        if context in controls:assert controls[context]==row['native_controls']
        controls[context]=row['native_controls']
        for method,native in row['native_controls'].items():
            if all(v['upper_ns']<native['lower_ns'] for v in row['old_rounds'].values()):
                edge=dict(fixture=key[0],pair=key[1],bars=key[2],method=key[3],native_method=method,
                    preserved=all(v['mean_ns']<native['mean_ns'] for v in row['candidate_rounds'].values()))
                edge.update(fresh_old_ci_edge=True,historical_edge=False)
                protected_by_key[key+(method,)]=edge
    edges=csv.DictReader(io.StringIO((REPO/'benchmarks/plans/evidence/kama-full-qualification/alma-sinc-protection-edges.csv').read_text()))
    covered=0
    for edge in edges:
        key=edge['fixture'],edge['pair'],int(edge['bars']),edge['profile']
        assert key in indexed and edge['native_method'] in indexed[key]['native_controls'];covered+=1
        row=indexed[key];native=row['native_controls'][edge['native_method']]
        edge_key=key+(edge['native_method'],)
        if edge_key not in protected_by_key:
            protected_by_key[edge_key]=dict(fixture=key[0],pair=key[1],bars=key[2],method=key[3],native_method=edge['native_method'],
                fresh_old_ci_edge=False,preserved=all(v['mean_ns']<native['mean_ns'] for v in row['candidate_rounds'].values()))
        protected_by_key[edge_key]['historical_edge']=True
    assert covered==41
    protected=list(protected_by_key.values());reopened=[e for e in protected if not e['preserved']]
    no_slower='slower' not in verdicts.values()
    return dict(passed=no_slower and pilot['passed'] and not reopened,
        all_74_source_comparisons_no_regression=no_slower,kama_target_gate=pilot,
        historical_protection_edges_covered=covered,protected_prior_native_wins=protected,
        prior_native_win_reopenings=reopened,closure_credit=0,report_imported=False)

def collect(folder):
    from . import pipeline as p
    from .verify import _collect
    from .triple_body_macro_qualification import verify_runtime
    plan=p.read(p.PLAN);validate_scope(plan)
    verify_runtime(folder,plan['expected_runtime_version'])
    result=_collect(Path(folder),'targeted',preflight=False)
    assert (result['case_count'],result['decision_count'])==(370,74)
    native=adjudicate_feasibility(native_rows(folder,plan),result['decisions'],plan)
    accepted=result['accepted'] and native['passed']
    result.update(accepted=accepted,qualification=True,native_progress_gate=native,
        production_integrated=False,report_imported=False,closure_credit=0,
        closure_ledger_new_rows=0,automatic_qualification=False,
        qualified_primary_native_wins=native['kama_target_gate']['new_10000_target_wins'] if accepted else [])
    return result
