"""Accepted4b snapshot measurement refresh; no production qualification or algorithm credit."""
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

from . import pipeline as p
from .candle_topology_replay_roi import checked_inputs, evidence_entry

CANONICAL = HARNESS = '4b4e8a385ed1c210998d26de6460e23f5842ffe9'
PUBLISHED = '20d21f263a25f14c4c15a0ab9354bfc91e0dab60'
HARNESS_SHA = '5a7b5b13be2308661be6c66a51aefa0f5a2f1be863c3501f8213d9e4f2963045'
CORE_SHA = '2aeba2543a532a59337b6a2f87edff9d1d76c9f54f91ad1ccd7b2254c9e1331f'
FINGERPRINT = '1b6b4cf28b0820a992fbd4e049bf2c993cdfa6efdee8deabf9d98fb0cde436ae'
REPORT_SHA = '1f9279984a58092daf1bc6dce0f84161d962528847bf6086db7bdfd4ec10d7be'
COMPOSITION_POLICY = dict(snapshot_source=CANONICAL,published_snapshot=PUBLISHED,report_before_sha256=REPORT_SHA,
    precedence='accepted-source-lineage-and-qualified-scope',wall_clock_timestamp_is_not_source_precedence=True,
    later_accepted_overrides_must_be_preserved_or_reapplied=True,automatic_import=False)
SCOPE_PATH = 'benchmarks/plans/evidence/current-frontier-scope-20261119.json'
SCOPE_SHA = '374752d1f51d2b95112b4064d76768d11d6ca7c421b47fa6c63185251b32afa4'
ASSEMBLY = 'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'
POLICY = dict(order=['old','new','new','old'], quiet_samples=12, max_cpu_percent=15,
              warmups=10, iterations=10, fixed_invocation_warmups=1000)
PREPARATION_BRANCH = 'perf/current-frontier-prep-repair-20261119'
PREPARATION_REVISION = 'accepted-runtime-alias-cli-v2-20261119'


def scope():
    return json.loads(evidence_entry(dict(path=SCOPE_PATH, sha256=SCOPE_SHA)))


PAIRS = scope()['pairs']
REPORT_CONTRACT = scope()['report_contract']


def frozen_catalog():
    return json.loads(evidence_entry(scope()['catalog']))


def case_inventory():
    result=[]
    for shard in scope()['refresh_shards']:
        task=shard['tasks'][0]
        for index,arm in enumerate(POLICY['order']):
            for operation in task['operations']:
                for size in (1000,10000):
                    for method in p.methods_for(task,index):
                        result.append(dict(shard=shard['index'],round=index,arm=arm,fixture='LibraryPairBenchmarks',
                            pair=operation['pair'],bars=size,method=method))
    return result


def verify_snapshot(plan):
    assert plan['published_snapshot']==dict(head=PUBLISHED,branch='perf/remaining-indicator-rollout',source_fingerprint=FINGERPRINT)
    assert not p.git('diff','--name-only',CANONICAL,PUBLISHED,'--','src','tests',p.BENCH,p.TESTS)
    # Publication may advance. This measures the explicitly frozen accepted
    # snapshot; no live-head equality or publication hold is imposed.
    return plan['published_snapshot']


def validate_scope(plan,*,require_ready=True):
    approved=scope()
    assert plan.get('current_frontier_refresh') is True and plan['baseline_measurement_refresh'] is True
    assert plan['preparation_branch']==PREPARATION_BRANCH and plan['preparation_revision']==PREPARATION_REVISION
    assert not any(plan.get(key) for key in ('diagnostic_only','pointwise_family_refresh','named_output_projection_refresh',
        'current_baseline_ten_refresh','targeted_reference','coverage_reference','standalone_variants','qualify_variants','trix_preflight','reuse_inputs'))
    assert plan['scope_approval']==dict(path=SCOPE_PATH,sha256=SCOPE_SHA)
    assert plan['affected_pairs']==PAIRS and len(PAIRS)==len(set(PAIRS))==154
    catalog=frozen_catalog()
    assert catalog['primary_pairs']==401 and len(catalog['rows'])==401
    assert PAIRS==sorted(row['pair'] for row in catalog['rows'] if row['wave']==1)
    assert all(row['planned_warmups']==1000 and row['historical_native_mean_ns']<=1e9 for row in catalog['rows'] if row['wave']==1)
    assert plan['refresh_shards']==plan['targeted_shards']==approved['refresh_shards']
    assert plan['requested_case_inventory']==case_inventory()
    pairs=[operation['pair'] for shard in plan['refresh_shards'] for task in shard['tasks'] for operation in task['operations']]
    assert len(pairs)==len(set(pairs))==154 and set(pairs)==set(PAIRS)
    assert plan['refresh_shards'][0]['tasks'][0]['operations']==[dict(raw=pair,pair=pair) for pair in approved['priority_first_shard']]
    assert (plan['cases'],plan['decisions'])==(1540,308)
    assert plan['matrix']==dict(shards=12,max_parallel=1,timeout_minutes_per_shard=60)
    assert len(plan['refresh_shards'])==12 and max(len(shard['tasks'][0]['operations']) for shard in plan['refresh_shards'])<=13
    assert sum(shard['cases'] for shard in plan['refresh_shards'])==1540
    assert sum(shard['decisions'] for shard in plan['refresh_shards'])==308
    assert plan['measurement_policy']==POLICY and plan['runtime_policy_overrides']=={}
    assert plan['proposed_dotnet_sdk']=='10.0.403' and plan['expected_runtime_version']=='.NET 10.0.15'
    assert plan['runtime_dependency_alignment']==dict(tensor_version='0.151.1') and plan['require_fixture_inventory'] is True
    assert plan['sources']==dict(published=CANONICAL,old=CANONICAL,successor=CANONICAL,variants=CANONICAL)
    assert plan['gpu_sources']==dict(old=CANONICAL,successor=CANONICAL,variants=CANONICAL)
    assert plan['report_refresh_only'] is True and plan['production_qualification'] is False and plan['production_optimization'] is False
    assert plan['algorithm_credit']==plan['closure_credit']==0 and plan['automatic_import'] is False
    assert plan['remaining_unmeasured_pairs']==247 and plan['report_contract']==REPORT_CONTRACT
    assert plan['report_before_sha256']==REPORT_SHA
    assert plan['composition_policy']==approved['composition_policy']==COMPOSITION_POLICY
    assert plan['harness_sha256']==HARNESS_SHA
    candidate=plan['candidate']
    assert candidate['source']==CANONICAL and candidate['ci_run']=='44469775767'
    assert candidate['producer']=='ce6d1a0373166c42583278e4d7efd38f65e0934d'
    assert candidate['source_fingerprint']==FINGERPRINT and candidate['production_sha256']==CORE_SHA
    assert candidate['actual_harness_sha256']==HARNESS_SHA and (candidate['unit_passed'],candidate['parity_passed'])==(134,86)
    for label in ('old','successor','variants'):assert plan['cpu_test_classes'][label]==candidate['unit_classes']
    cpu=json.loads(evidence_entry(candidate['cpu_root_review']));gpu=json.loads(evidence_entry(candidate['gpu_root_review']))
    assert cpu['status']=='root-independent-cpu-authentication-passed' and cpu['source']==CANONICAL and cpu['run_id']==candidate['ci_run']
    for root_key,key in [('source_fingerprint','source_fingerprint'),('manifest_sha256','manifest_sha256'),('core_sha256','production_sha256'),
                         ('actual_harness_sha256','actual_harness_sha256'),('zip_sha256','artifact_zip_sha256'),('artifact_id','artifact_id')]:
        assert str(cpu[root_key])==str(candidate[key])
    assert (cpu['unit'],cpu['parity'])==(134,86)
    assert sorted(cpu['checks']['unit']['classes'])==sorted(candidate['unit_classes'])
    assert sorted(cpu['checks']['parity']['classes'])==sorted(candidate['parity_classes'])
    assert gpu['status']=='root-independent-actual-gpu-authentication-passed' and gpu['source']==CANONICAL and gpu['run_id']==candidate['ci_run']
    assert gpu['passed_cases']==100 and gpu['methods']==12 and gpu['source_fingerprint']==FINGERPRINT
    assert gpu['cpu_root_review_sha256']==candidate['cpu_root_review']['sha256']
    assert gpu['receipt_sha256']==candidate['gpu_receipt']['sha256']==candidate['gpu_receipt_sha256']
    assert plan['native_win_policy_approval']['sha256']=='414b558eae1deda750c3d169020128986a0e0db9b2acd95f23da035be03bfd7c'
    evidence_entry(plan['native_win_policy_approval']);verify_snapshot(plan)
    if require_ready:
        assert plan['inputs_ready'] is True and plan['parent_review_approved'] is True,'HELD before input or measurement execution'


def verify_runtime_identity(plan,manifests):
    validate_scope(plan)
    assert manifests['old']==manifests['new'],'A/A refresh requires identical whole runtime manifests'
    assert manifests['old'][p.DLL]==CORE_SHA and manifests['old'][ASSEMBLY]==HARNESS_SHA


def primary_pair_inventory(raw):
    entries=json.loads(raw)
    ids=sorted(entry['Id'] for entry in entries if entry['Status']=='paired')
    expected=sorted(row['pair'] for row in frozen_catalog()['rows'])
    assert len(ids)==len(set(ids))==401 and ids==expected,'Authenticated runtime primary catalog drift'
    return ids


def source_authentication(plan):
    return dict(accepted_candidate=plan['candidate'],production_source=CANONICAL,
        labels={label:dict(source=CANONICAL,core_sha256=CORE_SHA,harness_sha256=HARNESS_SHA) for label in ('old','successor','variants')},
        additional_cpu_executions=0,additional_gpu_executions=0,additional_builds=0)


def prepare(candidate,out):
    plan=p.read(p.PLAN);validate_scope(plan)
    assert os.environ.get('GITHUB_REF_NAME')==PREPARATION_BRANCH,'Wrong successor preparation branch'
    assert subprocess.check_output(['dotnet','--version'],text=True).strip()=='10.0.403'
    candidate,out=map(lambda path:Path(path).resolve(),(candidate,out))
    assert not out.exists() and not out.is_relative_to(candidate)
    auth=checked_inputs(candidate,plan['candidate'])
    assert auth['production']==CORE_SHA
    out.mkdir();(out/'common').mkdir();(out/'production').mkdir();(out/'evidence').mkdir()
    shutil.copytree(candidate,out/'baseline-authentication')
    prefix=p.BENCH+'/bin/Release/net10.0/'
    for name,digest in auth['manifest']['runtime_files'].items():
        if name.startswith(prefix):
            path=out/'common'/name[len(prefix):];path.parent.mkdir(parents=True,exist_ok=True)
            path.write_bytes(auth['read']('blobs/'+digest))
    assert p.digest(out/'common'/ASSEMBLY)==HARNESS_SHA
    metadata=out/'evidence/primary-pair-inventory.json'
    subprocess.run(['dotnet',str(out/'common'/ASSEMBLY),'--comparison-manifest',str(metadata)],cwd=out/'common',check=True,timeout=90)
    primary_pair_inventory(metadata.read_bytes())
    checks={};production={};dependencies={}
    for label in ('old','successor','variants'):
        (out/'production'/(label+'.dll')).write_bytes(auth['read']('blobs/'+CORE_SHA))
        checks[label]=dict(auth['manifest']['checks']['unit'],original_gpu_source=CANONICAL)
        production[label]=CORE_SHA;dependencies[label]=auth['closure']
        for suffix,raw in [('.trx',auth['read']('evidence/unit.trx')),('-parity.trx',auth['read']('evidence/parity.trx')),
                           ('-original-gpu-receipt.json',auth['gpu_receipt']),('-original-gpu-gpu.trx.gz',auth['gpu_trx'])]:
            (out/'evidence'/(label+suffix)).write_bytes(raw)
    observed=subprocess.check_output(['git','ls-remote','origin','refs/heads/perf/remaining-indicator-rollout'],cwd=p.REPO,text=True).split()
    assert len(observed)==2 and re.fullmatch('[a-f0-9]{40}',observed[0])
    proof=dict(plan_sha256=p.digest(p.PLAN),sources=plan['sources'],source_trees={label:p.git('rev-parse',CANONICAL+':src') for label in ('old','successor','variants')},
        sdk='10.0.403',dotnet_info=subprocess.check_output(['dotnet','--info'],text=True),workflow_head=p.git('rev-parse','HEAD'),
        run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],preparation_mode='authenticated-current-frontier-refresh',
        workflow_branch=os.environ['GITHUB_REF_NAME'],preparation_revision=PREPARATION_REVISION,
        source_authentication=source_authentication(plan),common_manifest=p.manifest(out/'common'),evidence_manifest=p.manifest(out/'evidence'),
        production_sha256=production,checks=checks,tested_runtime_dependencies=dependencies,published_snapshot=verify_snapshot(plan),
        observed_publication_head=observed[0],primary_pair_inventory_sha256=p.digest(metadata),production_qualification=False,report_refresh_only=True)
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256']=record_inventory(out,plan,p.digest(p.PLAN))
    p.write(out/'provenance.json',proof);p.verify_inputs(out)
    return proof


def verify_harness_alias(folder,plan,proof):
    """The single accepted runtime supplies every label, including the unused variant alias.

    This replaces only the unrelated legacy WMA/TRIMA harness-proof route.
    Actual common files, core bytes, CPU/parity TRXs and GPU evidence stay bound
    to the accepted immutable artifact; no result or sampling gate is waived.
    """
    folder=Path(folder);validate_scope(plan)
    assert proof['workflow_branch']==PREPARATION_BRANCH
    assert proof['preparation_revision']==PREPARATION_REVISION
    assert proof['source_authentication']==source_authentication(plan)
    candidate=plan['candidate'];accepted=folder/'baseline-authentication'
    raw=(accepted/'manifest.json').read_bytes()
    assert hashlib.sha256(raw).hexdigest()==candidate['manifest_sha256']
    manifest=json.loads(raw)
    assert manifest['schema']==1 and manifest['status']=='cpu-correctness-complete' and manifest['sdk']==plan['proposed_dotnet_sdk']
    assert (manifest['source'],manifest['run_id'],manifest['workflow_head'])==(CANONICAL,candidate['ci_run'],candidate['producer'])
    assert manifest['classes']==dict(unit=candidate['unit_classes'],parity=candidate['parity_classes'])
    assert (manifest['checks']['unit']['passed'],manifest['checks']['parity']['passed'])==(candidate['unit_passed'],candidate['parity_passed'])
    prefix=p.BENCH+'/bin/Release/net10.0/'
    expected={name[len(prefix):]:digest for name,digest in manifest['runtime_files'].items() if name.startswith(prefix)}
    assert expected==proof['common_manifest']==p.manifest(folder/'common')
    assert expected[p.DLL]==CORE_SHA and expected[ASSEMBLY]==HARNESS_SHA
    cpu={}
    for kind in ('unit','parity'):
        value=(accepted/'evidence'/(kind+'.trx')).read_bytes()
        assert hashlib.sha256(value).hexdigest()==manifest['evidence']['evidence/'+kind+'.trx']
        cpu[kind]=value
    gpu_receipt=evidence_entry(candidate['gpu_receipt']);gpu_trx=evidence_entry(candidate['gpu_trx'])
    for label in ('old','successor','variants'):
        assert proof['production_sha256'][label]==CORE_SHA==p.digest(folder/'production'/(label+'.dll'))
        assert proof['checks'][label]==dict(manifest['checks']['unit'],original_gpu_source=CANONICAL)
        assert (folder/'evidence'/(label+'.trx')).read_bytes()==cpu['unit']
        assert (folder/'evidence'/(label+'-parity.trx')).read_bytes()==cpu['parity']
        assert (folder/'evidence'/(label+'-original-gpu-receipt.json')).read_bytes()==gpu_receipt
        assert (folder/'evidence'/(label+'-original-gpu-gpu.trx.gz')).read_bytes()==gpu_trx
        assert proof['tested_runtime_dependencies'][label]==proof['tested_runtime_dependencies']['old']


def verify_authentication(folder,plan,proof):
    folder=Path(folder);validate_scope(plan)
    assert proof['workflow_branch']==PREPARATION_BRANCH and proof['preparation_revision']==PREPARATION_REVISION
    assert proof['preparation_mode']=='authenticated-current-frontier-refresh'
    assert proof['published_snapshot']==verify_snapshot(plan) and re.fullmatch('[a-f0-9]{40}',proof['observed_publication_head'])
    assert proof['production_qualification'] is False and proof['report_refresh_only'] is True
    assert proof['source_authentication']==source_authentication(plan)
    auth=checked_inputs(folder/'baseline-authentication',plan['candidate'])
    prefix=p.BENCH+'/bin/Release/net10.0/'
    assert proof['common_manifest']=={name[len(prefix):]:digest for name,digest in auth['manifest']['runtime_files'].items() if name.startswith(prefix)}
    assert proof['common_manifest'][ASSEMBLY]==HARNESS_SHA and proof['common_manifest'][p.DLL]==CORE_SHA
    for label in ('old','successor','variants'):
        assert proof['production_sha256'][label]==CORE_SHA and proof['tested_runtime_dependencies'][label]==auth['closure']
        assert proof['checks'][label]==dict(auth['manifest']['checks']['unit'],original_gpu_source=CANONICAL)
        for suffix,raw in [('.trx',auth['read']('evidence/unit.trx')),('-parity.trx',auth['read']('evidence/parity.trx')),
                           ('-original-gpu-receipt.json',auth['gpu_receipt']),('-original-gpu-gpu.trx.gz',auth['gpu_trx'])]:
            assert (folder/'evidence'/(label+suffix)).read_bytes()==raw
    metadata=folder/'evidence/primary-pair-inventory.json'
    assert p.digest(metadata)==proof['primary_pair_inventory_sha256'];primary_pair_inventory(metadata.read_bytes())


def admission(decisions):
    expected={(pair,size,'Ooples') for pair in PAIRS for size in (1000,10000)}
    assert len(decisions)==308 and {(row['pair'],row['bars'],row['method']) for row in decisions}==expected
    assert all(row['fixture']=='LibraryPairBenchmarks' and row['verdict'] in ('faster','slower','inconclusive') for row in decisions)
    admitted=[pair for pair in PAIRS if all(row['verdict']=='inconclusive' for row in decisions if row['pair']==pair)]
    quarantined=[dict(pair=pair,sizes={str(row['bars']):row['verdict'] for row in decisions if row['pair']==pair}) for pair in PAIRS if pair not in admitted]
    return dict(admitted_pairs=admitted,quarantined_pairs=quarantined,admission_is_not_equivalence=True,
        admitted_baseline_rows=2*len(admitted),unchanged_baseline_rows=802-2*len(admitted),unchanged_builder_comparisons=1544,zero_admitted_no_publication=not admitted)


def archive_authentication(prepared,plan):
    """Retain source-bound authentication metadata; never put runtime/CAS bytes in Git."""
    validate_scope(plan)
    prepared=Path(prepared)
    result={}
    approved=scope()
    entries=[plan['scope_approval'],plan['native_win_policy_approval'],approved['catalog'],approved['earlier_plan'],approved['peer_review']]
    entries.extend(plan['candidate'][key] for key in ('cpu_root_review','gpu_root_review','gpu_receipt','gpu_trx'))
    for entry in entries:
        result['scope-evidence/'+Path(entry['path']).name]=evidence_entry(entry)
    authentication=prepared/'baseline-authentication'
    assert p.digest(authentication/'manifest.json')==plan['candidate']['manifest_sha256']
    result['prepared/baseline-authentication/manifest.json']=(authentication/'manifest.json').read_bytes()
    for path in (authentication/'evidence').rglob('*'):
        if path.is_file():
            assert path.suffix.lower() not in ('.dll','.exe','.pdb'),'No runtime in source evidence archive'
            result['prepared/'+path.relative_to(prepared).as_posix()]=path.read_bytes()
    return result


def classify_native(native,admitted):
    admitted=set(admitted)
    assert len(native['rows'])==308 and {(row['pair'],row['bars']) for row in native['rows']}=={(pair,size) for pair in PAIRS for size in (1000,10000)}
    rows=[]
    for row in native['rows']:
        rounds=row['owned_rounds'];control=row['native_control']
        assert [sample['round'] for sample in rounds]==[0,1,2,3]
        points=['win' if sample['mean_ns']<control['mean_ns'] else 'nonwin' for sample in rounds]
        ci=row['round_classifications']
        expected_ci=['win' if sample['upper_ns']<control['lower_ns'] else 'loss' if sample['lower_ns']>control['upper_ns'] else 'inconclusive' for sample in rounds]
        assert ci==expected_ci
        category=('quarantined_same_binary_label_effect' if row['pair'] not in admitted else
            'mixed_native_point_position' if len(set(points))!=1 else
            'label_consistent_point_nonwinner' if points[0]=='nonwin' else
            'label_consistent_statistical_native_winner' if all(value=='win' for value in ci) else
            'label_consistent_point_winner_without_both_label_native_CI_consensus')
        rows.append(dict(pair=row['pair'],bars=row['bars'],point_positions=points,round_native_CI_classifications=ci,
            A_both_native_CI_wins=ci[0]==ci[3]=='win',B_approved_both_native_CI_wins=ci[1]==ci[2]=='win',
            A_both_point_nonwins=points[0]==points[3]=='nonwin',all_four_point_consistent=len(set(points))==1,
            all_four_separated_native_wins=all(value=='win' for value in ci),category=category,algorithm_credit=0))
    counts={}
    for size in (1000,10000):
        selected=[row for row in rows if row['bars']==size]
        winners=sum(row['category'] in ('label_consistent_statistical_native_winner','label_consistent_point_winner_without_both_label_native_CI_consensus') for row in selected)
        nonwinners=sum(row['category']=='label_consistent_point_nonwinner' for row in selected)
        quarantined=sum(row['category']=='quarantined_same_binary_label_effect' for row in selected)
        mixed=sum(row['category']=='mixed_native_point_position' for row in selected)
        counts[str(size)]=dict(measured_pairs=154,admitted_consistent_point_winners=winners,admitted_consistent_point_nonwinners=nonwinners,
            quarantined_pairs=quarantined,mixed_native_position_pairs=mixed,unmeasured_pairs=247,
            possible_total_point_nonwinners_lower=nonwinners,possible_total_point_nonwinners_upper=401-winners,
            observed_B_native_CI_wins_all_measured_rows=sum(row['B_approved_both_native_CI_wins'] for row in selected),
            observed_all_four_native_CI_wins_all_measured_rows=sum(row['all_four_separated_native_wins'] for row in selected))
    return dict(snapshot_source=CANONICAL,published_snapshot=PUBLISHED,current_full_401_inventory=False,counts_by_size=counts,rows=rows,
        qualification=False,algorithm_credit=0,closure_credit=0,limitation='One round2 native control per pair/size; no native drift measurement, no equivalence claim,247 rows remain unmeasured.')


def collect(folder):
    from .verify import _collect
    from .current_baseline_ten_refresh import _verify_runtime
    from .pointwise_family_refresh import native_review
    result=_collect(Path(folder),'baseline-refresh',preflight=False)
    assert result['accepted'] is False and (result['case_count'],result['decision_count'])==(1540,308)
    _verify_runtime(folder)
    admitted=admission(result['decisions']);native=native_review(Path(folder),pairs=PAIRS)
    native['counts_include_quarantined_pairs']=True
    native['admitted_pair_all_four_native_CI_wins']={str(size):sum(row['bars']==size and row['pair'] in admitted['admitted_pairs'] and row['all_four_separated_native_wins'] for row in native['rows']) for size in (1000,10000)}
    result.update(baseline_refresh_verified=True,report_refresh_only=True,production_qualification=False,production_optimization=False,
        report_imported=False,automatic_import=False,source_changed=False,same_production_under_both_labels=True,
        report_contract=REPORT_CONTRACT,report_before_sha256=p.read(p.PLAN)['report_before_sha256'],native_review=native,
        algorithm_credit=0,closure_credit=0,**admitted)
    result['frontier_inventory']=classify_native(native,admitted['admitted_pairs'])
    return result
