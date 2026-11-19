"""HELD actual KAMA fused-prefix native feasibility screen; no automatic qualification or credit."""
import gzip
import hashlib
import importlib.util
import json
import os
import re
import shutil
import subprocess
import tempfile
from pathlib import Path

from . import pipeline as p
from . import candle_single_pass_roi as common
from . import triple_body_macro_qualification as original
from . import analytic_grid_roi as policy

KIND = 'kama-fused-prefix-v3-native-pilot'
OLD = 'd3d5c905a5d75b666b440aa6c5f6b4c24b42dc91'
NEW = '7de04493efa8304cb2a21fea45c21d177ca0903f'
PUBLISHED = '87405159446f3de0132ce49800b52bd0af00e234'
FIXTURE = 'LibraryPairBenchmarks'
TARGETS = ['QuanTAlib.Kama','Skender.GetKama']
KAMA = TARGETS + ['TaLib.Functions.Kama','Trady.Indicator.KaufmanAdaptiveMovingAverage']
PAIRS = KAMA + ['TaLib.Functions.Ema','TaLib.Functions.T3']
WORKFLOW_BRANCH = 'perf/kama-fused-prefix-roi-v3-20261119'
COUNTS = (1000,10000)
HARNESS_PATH = p.BENCH + '/bin/Release/net10.0/' + original.HARNESS_ASSEMBLY
HARNESS_SCOPES = [p.BENCH,'Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.Config','nuget.config']
PARITY_FILE = p.TESTS+'/SeededAdaptiveBatchParityTests.cs'
PATHS = {'src/Indicators/ValuesBarExecution.cs','src/Indicators/ValuesBarExecution.SeededAdaptive.cs',
         'src/Indicators/ValuesBarExecution.SeededAdaptiveBatch.cs','tests/ModelsTests/SeededAdaptiveBatchTests.cs',PARITY_FILE}
UNIT = ['SeededAdaptiveBatchTests','SeededAdaptiveSharedExecutionTests','EmaRecurrenceTests','TillsonSharedExecutionTests']
PARITY = ['SeededAdaptiveBatchParityTests','SeededAdaptiveComparisonTests','TaAdaptiveComparisonTests','TradyAdaptiveComparisonTests','ExponentialAverageComparisonTests','SharedTillsonBenchmarkTests']
BASE_UNIT = ['AnalyticGridBatchTests','SharedWeightedWindowTests','FibonacciWeightedAverageTests','AffineConvolutionTests','WindowedSincCompactTests']
BASE_PARITY = ['AnalyticKernelComparisonTests','FixedWeightedComparisonTests','ConvolutionComparisonTests','SincBuilderComparisonTests']
sha = lambda raw: hashlib.sha256(raw).hexdigest()

def blob(source,path):
    return subprocess.check_output(['git','show',source+':'+path],cwd=p.REPO)

def evidence(entry):
    assert isinstance(entry,dict) and set(entry)=={'path','sha256'}, 'UNBOUND evidence'
    path=Path(entry['path'])
    assert path.as_posix().startswith('benchmarks/plans/evidence/') and '..' not in path.parts
    raw=(p.REPO/path).read_bytes()
    assert sha(raw)==entry['sha256']
    return raw

def shards():
    task=dict(fixture=FIXTURE,operations=[dict(raw=x,pair=x) for x in PAIRS],
              methods=['Ooples','Competitor'],implicit_pair=False,cases=60,decisions=12)
    return [dict(index=0,tasks=[task],cases=60,decisions=12)]

def consumer_registry():
    return [dict(fixture=FIXTURE,pair_parameter='PairId',size_parameter='Bars',
                 operations=PAIRS,methods=['Ooples','Competitor'],sizes=list(COUNTS))]

def case_inventory():
    from .checkpoints import filters
    records=[];task=shards()[0]['tasks'][0]
    for index,arm in enumerate(common.POLICY['order']):
        for pair in PAIRS:
            for method in p.methods_for(task,index):
                for bars in COUNTS:
                    records.append(dict(round=index,arm=arm,fixture=FIXTURE,raw=pair,pair=pair,
                        method=method,bars=bars,filters=filters(FIXTURE,pair,[method]),
                        parameters={'PairId':pair,'Bars':bars}))
    assert len(records)==60 and [sum(r['round']==i for r in records) for i in range(4)]==[12,12,24,12]
    return records

NATIVE_POLICY = dict(fixture=FIXTURE,owned_methods=['Ooples'],native_methods=['Competitor'],
    native_round=2,old_rounds=[0,3],candidate_rounds=[1,2],minimum_margin=None,
    require_separated_intervals_both_rounds=True,new_closure_bars=10000,minimum_new_closures=1,
    target_pairs=TARGETS,require_own_source_faster=True,require_net_10000_point_progress=True,
    protect_prior_confident_wins_both_sizes=True,prior_controls=PAIRS[2:])

def verify_source_boundary():
    assert set(p.git('diff','--name-only',OLD,NEW,'--','src','tests',p.BENCH,p.TESTS).splitlines())==PATHS
    assert not p.git('diff','--name-only',OLD,NEW,'--',*HARNESS_SCOPES)
    assert p.git('diff','--name-only',OLD,NEW,'--',p.TESTS).splitlines()==[PARITY_FILE]
    # The added sibling comparator test is not a benchmark compilation input.
    project=blob(NEW,p.BENCH+'/OoplesFinance.StockIndicators.CompetitorBenchmarks.csproj').decode()
    refs=re.findall(r'<ProjectReference\s+Include="([^"]+)"',project)
    assert refs==[r'..\..\src\OoplesFinance.StockIndicators.csproj'], refs
    assert not re.search(r'<Compile\b|EnableDefaultCompileItems|CompetitorTests',project)

def verify_workflow_entrypoint(text=None):
    """Registration and gate checks are independent of the two readiness flags."""
    if text is None:text=(p.REPO/'.github/workflows/kama-fused-prefix-roi-v3.yml').read_text()
    assert '  workflow_dispatch:' not in text and '  pull_request:' not in text
    assert '  push:\n    branches: ['+WORKFLOW_BRANCH+']\n' in text
    assert '    paths: [.github/workflows/kama-fused-prefix-roi-v3.yml, benchmarks/plans/hosted-kama-fused-prefix-roi-v3-20261119.json]\n' in text
    assert "    if: github.event_name == 'push'\n" in text
    assert text.index('Fail closed before SDK or input setup')<text.index('actions/setup-dotnet@')<text.index('actions/download-artifact@')
    gate=text.split('Fail closed before SDK or input setup',1)[1].split('actions/setup-dotnet@',1)[0]
    if 'v3' in WORKFLOW_BRANCH:
        assert "$plan.current_baseline_reconciliation.status -ne 'complete'" in gate
    assert '$plan.inputs_ready -isnot [bool] -or !$plan.inputs_ready' in gate
    assert '$plan.parent_review_approved -isnot [bool] -or !$plan.parent_review_approved' in gate
    assert NEW in gate and OLD in gate

def verify_entrypoint_environment(environment):
    assert environment.get('GITHUB_EVENT_NAME')=='push','Only the reviewed single push may run this diagnostic'
    assert environment.get('GITHUB_REF')=='refs/heads/'+WORKFLOW_BRANCH,'Wrong diagnostic push branch'

def verify_registry():
    for source in (OLD,NEW):
        fixture=blob(source,p.BENCH+'/LibraryComparisons/LibraryPairBenchmarks.cs').decode()
        assert re.search(r'\[Params\(1_?000,\s*10_?000\)\]\s*public int Bars',fixture)
        assert 'public string PairId' in fixture and 'private const int Period = 20;' in fixture
        assert all(re.search(r'\b'+method+r'\s*\(',fixture) for method in ('Setup','Ooples','Competitor'))
        registry=''.join(blob(source,p.BENCH+'/LibraryComparisons/'+name+'.cs').decode() for name in ('SeededAdaptiveComparison','TaAdaptiveComparison','TradyAdaptiveComparison','ExponentialAverageComparison','TillsonComparison'))
        assert all('"'+pair+'"' in registry for pair in PAIRS)

def verify_publication(plan, *, current=False):
    binding=plan['published_baseline']
    assert binding==dict(pull_request=266,head=PUBLISHED,branch='perf/remaining-indicator-rollout',
                        source_fingerprint=plan['old_candidate']['source_fingerprint'])
    assert common.source_fingerprint(PUBLISHED)==binding['source_fingerprint']
    assert not p.git('diff','--name-only',OLD,PUBLISHED,'--','src','tests',*HARNESS_SCOPES)
    if current:
        actual=json.loads(subprocess.check_output(['gh','api','repos/Ooples-Finance-LLC/OoplesFinance.StockIndicators/pulls/266'],cwd=p.REPO))
        assert actual['head']['sha']==PUBLISHED and actual['head']['ref']==binding['branch'], 'Published baseline advanced'
    return binding

def verify_root_evidence(plan):
    for key,source,unit,parity in [('old_candidate',OLD,53,48),('candidate',NEW,62,43)]:
        c=plan[key];cpu=json.loads(evidence(c['cpu_root_review']));gpu=json.loads(evidence(c['gpu_root_review']))
        assert cpu['status']=='root-independent-cpu-authentication-passed' and cpu['source']==source
        assert (cpu['unit'],cpu['parity'])==(unit,parity)
        for a,b in [('run_id','ci_run'),('producer','producer'),('source_fingerprint','source_fingerprint'),('manifest_sha256','manifest_sha256'),
                    ('core_sha256','production_sha256'),('actual_harness_sha256','actual_harness_sha256'),('zip_sha256','artifact_zip_sha256')]: assert cpu[a]==c[b]
        assert str(cpu['artifact_id'])==c['artifact_id']
        assert gpu['status']=='root-independent-actual-gpu-authentication-passed' and gpu['source']==source
        assert gpu['run_id']==c['ci_run'] and gpu['passed_cases']==100 and gpu['methods']==12
        assert gpu['receipt_sha256']==c['gpu_receipt']['sha256']
        assert sha(evidence(c['gpu_receipt']))==c['gpu_receipt']['sha256']
    root=json.loads(evidence(plan['worker_proof_root_review']))
    index=json.loads(evidence(plan['worker_proof_index']))
    assert root['status']=='root-independent-kama-four-api-worker-proof-passed' and root['source']==NEW
    assert root['index_sha256']==plan['worker_proof_index']['sha256']
    assert root['context_files_verified_local_and_archive']==28
    assert root['raw_speedscope_thread_contexts_independently_recomputed'] is True
    assert {x['pair'] for x in root['contexts']}==set(KAMA)
    assert all(x['output_and_presence_match'] for x in root['contexts'])
    bundle=json.loads(evidence(plan['worker_bridge_expected']))
    assert set(bundle)==set(KAMA)
    assert index['source']==NEW and index['source_fingerprint']==plan['candidate']['source_fingerprint']
    assert index['cpu_root_review_sha256']==plan['candidate']['cpu_root_review']['sha256']
    assert index['gpu_root_review_sha256']==plan['candidate']['gpu_root_review']['sha256']
    assert index['child_sha256']==plan['worker_proof_helper']['sha256']
    indexed={r['pair']:r for r in index['contexts']}
    models={r['pair']:r for r in json.loads(evidence(plan['model_input']))['rows'] if r['bars']==10000}
    for pair,record in bundle.items():
        ready=record['ready']
        assert sha(record['ready_raw'].encode())==record['ready_sha256']==indexed[pair]['hashes']['ready.json']
        assert json.loads(record['ready_raw'])==ready
        assert ready['pair']==pair and ready['source']==NEW and ready['bars']==10000
        assert ready['loaded_modules']['OoplesFinance.StockIndicators']['sha256']==plan['candidate']['production_sha256']
        assert ready['loaded_modules']['OoplesFinance.StockIndicators.CompetitorBenchmarks']['sha256']==plan['candidate']['actual_harness_sha256']
        assert ready['full_returned_output_hash']==ready['independent_reference_output_hash']
        assert ready['presence_sha256']==ready['independent_reference_presence_hash']
        model=record['model']
        original_model=models[pair]
        assert model['actual_input_hashes']==original_model['actual_input_hashes']
        assert model['actual_input_hashes']==ready['actual_data_bits']
        for name,column in model['independent_reference_outputs'].items():
            assert column['values_sha256']==original_model['independent_reference_outputs'][name]['values_sha256']
            assert column['presence_base64']==original_model['independent_reference_outputs'][name]['presence_base64']
            assert column['values_sha256']==json.loads(ready['full_returned_output_hash'])[name]
            import base64
            assert sha(base64.b64decode(column['presence_base64']))==json.loads(ready['presence_sha256'])[name]
    assert evidence(plan['worker_proof_helper']) and evidence(plan['model_input'])
    c=plan['candidate'];frozen=c['evidence_source']
    assert common.source_fingerprint(frozen)==c['source_fingerprint']
    assert not p.git('diff','--name-only',NEW,frozen,'--','src','tests',p.BENCH,p.TESTS)
    assert blob(frozen,'benchmarks/results/shared-rollout/local-gpu-contracts/receipt.json')==evidence(c['gpu_receipt'])
    assert blob(frozen,'benchmarks/results/shared-rollout/local-gpu-contracts/gpu.trx.gz')==evidence(c['gpu_trx'])


def validate_scope(plan, *, require_ready=True):
    verify_workflow_entrypoint()
    assert plan['diagnostic_kind']==KIND and plan['diagnostic_only'] is True
    assert plan['bootstrap_policy']==dict(version=3,framework='Microsoft.NETCore.App',fx_version='10.0.15',
        reflection_runner=True,package_dependencies=0,measurement_runtime_unchanged=True)
    assert plan['sources']==dict(published=OLD,old=OLD,successor=NEW,variants=OLD)
    assert plan['gpu_sources']==dict(old=OLD,successor=NEW,variants=OLD)
    assert (plan['cases'],plan['decisions'])==(60,12)
    assert plan['matrix']==dict(shards=1,max_parallel=1,timeout_minutes_per_shard=120)
    assert plan['targeted_shards']==plan['diagnostic_shards']==shards()
    assert plan['measurement_policy']==common.POLICY and plan['runtime_policy_overrides']=={}
    assert plan['runtime_dependency_alignment']==dict(tensor_version='0.151.1')
    assert plan['require_fixture_inventory'] is True
    assert (plan['proposed_dotnet_sdk'],plan['expected_runtime_version'])==('10.0.403','.NET 10.0.15')
    assert plan['bridge_policy']==bridge_contract(plan)
    assert runner_source_pins()==plan['runner_source_pins']
    assert plan['native_progress_gate']==NATIVE_POLICY
    policy.verify_native_policy(plan)
    assert plan['closure_ledger_new_rows']==0 and plan['automatic_qualification'] is False and plan['report_import_permitted'] is False
    assert plan['full_consumer_registry']==consumer_registry()
    assert plan['requested_case_inventory']==case_inventory()
    assert plan['harness_alias_policy']==dict(common_artifact_role='old',variants_artifact_role='old',baseline_source=OLD,candidate_source=NEW,
        require_scoped_source_identity=True,claim_artifact_binary_identity=False,excluded_sibling_test=PARITY_FILE)
    assert plan['old_candidate']['source']==OLD and plan['candidate']['source']==NEW
    assert plan['old_candidate']['unit_classes']==BASE_UNIT and plan['old_candidate']['parity_classes']==BASE_PARITY
    assert plan['candidate']['unit_classes']==UNIT and plan['candidate']['parity_classes']==PARITY
    assert (plan['candidate']['unit_passed'],plan['candidate']['parity_passed'])==(62,43)
    assert (plan['old_candidate']['unit_passed'],plan['old_candidate']['parity_passed'])==(53,48)
    assert plan['cpu_test_classes']==dict(old=BASE_UNIT,successor=UNIT,variants=BASE_UNIT)
    assert type(plan['inputs_ready']) is bool and type(plan['parent_review_approved']) is bool
    if require_ready:
        assert plan['inputs_ready'] is True and plan['parent_review_approved'] is True, 'HELD: root review before SDK/input I/O'
        assert plan['current_baseline_reconciliation']['status']=='complete','HELD: reconcile accepted publication baseline before hosted timing'
    verify_source_boundary();verify_registry();verify_publication(plan);verify_root_evidence(plan)

def checked_inputs(folder,c):
    """Reviewed package/GPU verification, with all source and evidence passed explicitly."""
    folder=Path(folder).resolve()
    def read(name):
        path=(folder/name).resolve();assert path.is_relative_to(folder) and path.is_file();return path.read_bytes()
    spec=importlib.util.spec_from_file_location('kama_candidate_ci',p.REPO/'scripts/build-candidate-correctness.py')
    assert Path(spec.origin).read_bytes().replace(b'\r\n',b'\n')==blob(c['producer'],'scripts/build-candidate-correctness.py')
    ci=importlib.util.module_from_spec(spec);spec.loader.exec_module(ci)
    source=c['source'];fingerprint=common.source_fingerprint(source)
    assert fingerprint==c['source_fingerprint'] and sha(read('manifest.json'))==c['manifest_sha256']
    manifest=json.loads(read('manifest.json'))
    assert (manifest['source'],manifest['run_id'],manifest['workflow_head'])==(source,c['ci_run'],c['producer'])
    assert manifest['classes']==dict(unit=c['unit_classes'],parity=c['parity_classes'])
    production=ci.validate_layout(manifest,read,source,fingerprint);assert production==c['production_sha256']
    assert manifest['runtime_files'][HARNESS_PATH]==c['actual_harness_sha256']
    for label,classes in [('unit',c['unit_classes']),('parity',c['parity_classes'])]:
        with tempfile.TemporaryDirectory(prefix='kama-trx-') as temp:
            path=Path(temp)/'checks.trx';path.write_bytes(read('evidence/'+label+'.trx'));checks=p.verify_trx(path,classes)
        assert checks==manifest['checks'][label] and checks['passed']==c[label+'_passed']
    receipt_raw,trx_raw=evidence(c['gpu_receipt']),evidence(c['gpu_trx'])
    receipt=json.loads(receipt_raw)
    assert ci.gpu_module(p.REPO).verify_evidence(receipt,gzip.decompress(trx_raw),json.loads(blob(source,'scripts/competitor-gpu-methods.json')),fingerprint)==100
    origin=receipt['build_origin']
    assert (origin['source'],origin['run_id'],origin['workflow_head'],str(origin['artifact_id']))==(source,c['ci_run'],c['producer'],c['artifact_id'])
    assert receipt['test_assembly_sha256']==manifest['runtime_files'][ci.ROOTS[-1]+'/OoplesFinance.StockIndicators.CompetitorTests.dll']
    from .runtime_dependencies import tensor_closure
    with tempfile.TemporaryDirectory(prefix='kama-closure-') as temp:
        runtime=Path(temp);prefix=ci.ROOTS[1]+'/'
        for name,value in manifest['runtime_files'].items():
            if name.startswith(prefix):
                path=runtime/name[len(prefix):];path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(read('blobs/'+value))
        closure=tensor_closure(runtime,'OoplesFinance.StockIndicators.Tests.Unit.deps.json')
    assert closure['packages']['AiDotNet.Tensors']=='0.151.1'
    return dict(manifest=manifest,read=read,production=production,closure=closure,gpu_receipt=receipt_raw,gpu_trx=trx_raw)

def source_authentication(plan):
    return dict(old=plan['old_candidate'],successor=plan['candidate'],variants_alias_of='old',additional_cpu_executions=0,additional_gpu_executions=0)

def harness_role_alias(plan,old,new):
    verify_source_boundary()
    actual={role:auth['manifest']['runtime_files'][HARNESS_PATH] for role,auth in [('old',old),('successor',new)]}
    assert actual==dict(old=plan['old_candidate']['actual_harness_sha256'],successor=plan['candidate']['actual_harness_sha256'])
    assert actual['old']==plan['harness_sha256']
    trees={role:p.git('rev-parse',source+':'+p.BENCH) for role,source in [('old',OLD),('successor',NEW)]}
    assert trees['old']==trees['successor']
    return dict(policy=plan['harness_alias_policy'],actual_artifact_harness_sha256=actual,
                actual_artifact_manifest_sha256={role:plan[key]['manifest_sha256'] for role,key in [('old','old_candidate'),('successor','candidate')]},
                common_harness_sha256=actual['old'],common_artifact_role='old',benchmark_source_trees=trees,
                source_scopes=HARNESS_SCOPES,scoped_source_identical=True,actual_artifact_binary_identical=actual['old']==actual['successor'])

def verify_harness_alias(folder,plan,proof):
    assert proof['source_authentication']==source_authentication(plan)
    for suffix in ('.trx','-parity.trx','-original-gpu-receipt.json','-original-gpu-gpu.trx.gz'):
        assert (folder/'evidence'/('old'+suffix)).read_bytes()==(folder/'evidence'/('variants'+suffix)).read_bytes()
    for field in ('production_sha256','checks','tested_runtime_dependencies'):
        assert proof[field]['variants']==proof[field]['old']

def bridge_contract(plan):
    return dict(fixture=FIXTURE,pairs=KAMA,bars=10000,setup_calls=4,explicit_ooples_calls=4,
        builds=0,profiles=0,warmups=0,timings=0,temporary_common_runtime=True,replace_core_only=True,
        common_harness_sha256=plan['harness_sha256'],candidate_core_sha256=plan['candidate']['production_sha256'])

def verify_bridge_record(plan,record):
    assert plan['bridge_policy']==bridge_contract(plan)
    assert record['status']=='common-harness-four-adapter-preflight-passed' and record['source']==NEW
    assert record['actual_clr']=='10.0.15'
    assert record['core_sha256']==plan['candidate']['production_sha256'] and record['harness_sha256']==plan['harness_sha256']
    assert record['cpu_engine']=='AiDotNet.Tensors.Engines.CpuEngine'
    assert record['default_backend']=='Auto' and record['default_history']=='Full'
    assert type(record['default_workers']) is int and record['default_workers']>0
    for key in ('setup_calls','explicit_ooples_calls','builds','profiles','warmups','timings'):
        assert record[key]==plan['bridge_policy'][key]
    assert record['qualification'] is False and record['performance_claim'] is False
    expected=json.loads(evidence(plan['worker_bridge_expected']))
    assert len(record['contexts'])==4 and {r['pair'] for r in record['contexts']}==set(KAMA)
    for row in record['contexts']:
        ready=expected[row['pair']]['ready']
        assert row['bars']==10000 and row['period']==20
        assert row['factory']==ready['actual_factory'] and row['parameters']==ready['actual_indicator_parameters']
        assert row['actual_data_bits']==ready['actual_data_bits']
        assert row['full_returned_output_hash']==json.loads(ready['full_returned_output_hash'])
        assert row['presence_sha256']==json.loads(ready['presence_sha256'])

TOOL_FILES = ('KamaPreflight.dll','KamaPreflight.pdb','KamaPreflight.deps.json','KamaPreflight.runtimeconfig.json')

def runner_source_pins():
    return {name:p.digest(p.REPO/name) for name in ('scripts/kama-preflight/Program.cs','scripts/kama-preflight/KamaPreflight.csproj')}

def verify_runner_runtime(folder,plan,*,require_receipt=True):
    folder=Path(folder).resolve()
    expected=set(TOOL_FILES)|({'build-receipt.json'} if require_receipt else set())
    assert {path.name for path in folder.iterdir()}==expected
    runtime=p.read(folder/'KamaPreflight.runtimeconfig.json')['runtimeOptions']
    assert runtime['framework']==dict(name='Microsoft.NETCore.App',version='10.0.15')
    assert runtime['rollForward']=='Disable' and 'includedFrameworks' not in runtime
    deps=p.read(folder/'KamaPreflight.deps.json')
    assert set(deps['libraries'])=={'KamaPreflight/1.0.0'},'Reflection runner must have no package dependencies'
    manifest={name:p.digest(folder/name) for name in TOOL_FILES}
    if require_receipt:
        receipt=p.read(folder/'build-receipt.json')
        assert receipt['runner_source_pins']==runner_source_pins()==plan['runner_source_pins']
        assert receipt['runtime_manifest']==manifest and receipt['exit_code']==0
        assert receipt['production_or_benchmark_builds']==0 and receipt['packages_added']==0
    return manifest

def verify_bootstrap_evidence(folder,plan,proof):
    assert proof['runner_source_pins']==runner_source_pins()==plan['runner_source_pins']
    assert verify_runner_runtime(folder/'evidence/reflection-runner',plan)==proof['runner_manifest']
    assert p.digest(folder/'evidence/reflection-runner/build-receipt.json')==proof['runner_build_receipt_sha256']
    raw=(folder/'evidence/common-harness-bootstrap.json').read_bytes();assert sha(raw)==proof['bootstrap_sha256']
    bootstrap=json.loads(raw)
    assert bootstrap['actual_clr']=='10.0.15' and bootstrap['disable_gpu']=='1'
    assert bootstrap['runner_assembly_sha256']==proof['runner_manifest']['KamaPreflight.dll']
    assert bootstrap['runner_deps_sha256']==proof['runner_manifest']['KamaPreflight.deps.json']
    assert bootstrap['runner_runtimeconfig_sha256']==proof['runner_manifest']['KamaPreflight.runtimeconfig.json']
    assert bootstrap['process_executable'] and bootstrap['corelib_path']
    assert Path(bootstrap['corelib_path']).parts[-4:]==('shared','Microsoft.NETCore.App','10.0.15','System.Private.CoreLib.dll')
    for key in ('process_executable_sha256','corelib_sha256'):
        assert re.fullmatch('[a-f0-9]{64}',bootstrap[key])
    assert proof['fx_version']=='10.0.15'

def common_harness_bridge(out,plan):
    """One input-preparation preflight; temporary common runtime, candidate core only."""
    baseline=p.manifest(out/'common')
    tool=Path(os.environ.get('KAMA_PREFLIGHT_RUNNER',str(p.REPO/'kama-preflight-runner'))).resolve()
    runner_manifest=verify_runner_runtime(tool,plan)
    shutil.copytree(tool,out/'evidence/reflection-runner')
    temp_parent=Path(tempfile.gettempdir()).resolve()
    temp_required=sum(path.stat().st_size for path in (out/'common').rglob('*') if path.is_file())+64*1024**2
    assert shutil.disk_usage(temp_parent).free>temp_required,'Insufficient bounded temporary-copy space'
    with tempfile.TemporaryDirectory(prefix='kama-common-bridge-') as temp:
        temp=Path(temp).resolve();assert temp.parent==temp_parent
        runtime=temp/'runtime';shutil.copytree(out/'common',runtime)
        core=runtime/'OoplesFinance.StockIndicators.dll'
        core.write_bytes((out/'production/successor.dll').read_bytes())
        assert p.digest(core)==plan['candidate']['production_sha256']
        assert p.manifest(runtime)==dict(baseline,**{'OoplesFinance.StockIndicators.dll':plan['candidate']['production_sha256']})
        result=temp/'result';result.mkdir()
        p.write(result/'invocation.json',dict(runtime_file_pins=p.manifest(runtime)))
        expected=p.REPO/plan['worker_bridge_expected']['path']
        command=['dotnet','exec','--fx-version','10.0.15','--runtimeconfig',str(tool/'KamaPreflight.runtimeconfig.json'),
            str(tool/'KamaPreflight.dll'),str(runtime),str(expected),str(result),NEW]
        env=dict(os.environ,AIDOTNET_DISABLE_GPU='1')
        process=subprocess.run(command,env=env,capture_output=True,text=True,timeout=120)
        (out/'evidence/common-harness-bridge.log').write_text(process.stdout+process.stderr,encoding='utf-8')
        if (result/'bootstrap.json').exists():(out/'evidence/common-harness-bootstrap.json').write_bytes((result/'bootstrap.json').read_bytes())
        assert process.returncode==0,'Common-harness bridge failed; no retry or timing'
        raw=(result/'bridge.json').read_bytes();record=json.loads(raw);verify_bridge_record(plan,record)
        assert p.manifest(runtime)==dict(baseline,**{'OoplesFinance.StockIndicators.dll':plan['candidate']['production_sha256']})
        (out/'evidence/common-harness-bridge.json').write_bytes(raw)
    assert p.manifest(out/'common')==baseline
    assert not temp.exists(),'Only the verified disposable copy should be removed'
    proof=dict(receipt_sha256=sha(raw),runner_source_pins=runner_source_pins(),expected_sha256=plan['worker_bridge_expected']['sha256'],
        common_runtime_unchanged=True,temporary_core_only_replacement=True,
        runner_manifest=runner_manifest,runner_build_receipt_sha256=p.digest(tool/'build-receipt.json'),
        temp_required_bytes=temp_required,verified_temp_parent=str(temp_parent),verified_temp_copy_removed=True,
        bootstrap_sha256=p.digest(out/'evidence/common-harness-bootstrap.json'),fx_version='10.0.15')
    verify_bootstrap_evidence(out,plan,proof)
    return proof

def verify_authentication(folder,plan,proof):
    validate_scope(plan)
    assert proof['workflow_event']=='push' and proof['workflow_ref']=='refs/heads/'+WORKFLOW_BRANCH
    assert proof['preparation_mode']=='authenticated-kama-fused-prefix-diagnostic'
    assert proof['published_baseline']==verify_publication(plan)
    raw=(folder/'case-selection-inventory.json').read_bytes()
    assert sha(raw)==proof['case_selection_inventory_sha256'] and json.loads(raw)==case_inventory()
    verify_harness_alias(folder,plan,proof)
    bridge=proof['common_harness_bridge']
    raw=(folder/'evidence/common-harness-bridge.json').read_bytes()
    assert sha(raw)==bridge['receipt_sha256'] and bridge['runner_source_pins']==plan['runner_source_pins']
    assert bridge['expected_sha256']==plan['worker_bridge_expected']['sha256']
    assert bridge['common_runtime_unchanged'] is True and bridge['temporary_core_only_replacement'] is True
    verify_bootstrap_evidence(folder,plan,bridge)
    verify_bridge_record(plan,json.loads(raw))
    authenticated={}
    for label,key in [('old','old_candidate'),('successor','candidate')]:
        auth=checked_inputs(folder/(label+'-authentication'),plan[key]);authenticated[label]=auth
        assert proof['production_sha256'][label]==auth['production'] and proof['tested_runtime_dependencies'][label]==auth['closure']
        assert proof['checks'][label]==dict(auth['manifest']['checks']['unit'],original_gpu_source=plan[key]['source'])
        for suffix,raw in [('.trx',auth['read']('evidence/unit.trx')),('-parity.trx',auth['read']('evidence/parity.trx')),('-original-gpu-receipt.json',auth['gpu_receipt']),('-original-gpu-gpu.trx.gz',auth['gpu_trx'])]:
            assert (folder/'evidence'/(label+suffix)).read_bytes()==raw
    assert proof['harness_role_alias']==harness_role_alias(plan,authenticated['old'],authenticated['successor'])
    prefix=p.BENCH+'/bin/Release/net10.0/'
    assert proof['common_manifest']=={name[len(prefix):]:value for name,value in authenticated['old']['manifest']['runtime_files'].items() if name.startswith(prefix)}

def prepare(old_candidate,candidate,out):
    plan=p.read(p.PLAN);validate_scope(plan)
    verify_entrypoint_environment(os.environ)
    publication=verify_publication(plan,current=True)
    assert subprocess.check_output(['dotnet','--version'],text=True).strip()==plan['proposed_dotnet_sdk']
    old_candidate,candidate,out=map(lambda x:Path(x).resolve(),(old_candidate,candidate,out))
    assert not out.exists() and not out.is_relative_to(old_candidate) and not out.is_relative_to(candidate)
    old,new=checked_inputs(old_candidate,plan['old_candidate']),checked_inputs(candidate,plan['candidate'])
    assert old['closure']==new['closure']
    alias=harness_role_alias(plan,old,new)
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
        for suffix,raw in [('.trx',auth['read']('evidence/unit.trx')),('-parity.trx',auth['read']('evidence/parity.trx')),('-original-gpu-receipt.json',auth['gpu_receipt']),('-original-gpu-gpu.trx.gz',auth['gpu_trx'])]:
            (out/'evidence'/(label+suffix)).write_bytes(raw)
    proof=dict(plan_sha256=p.digest(p.PLAN),sources=plan['sources'],source_trees={label:p.git('rev-parse',plan['sources'][label]+':src') for label in ('old','successor','variants')},
        sdk=plan['proposed_dotnet_sdk'],dotnet_info=subprocess.check_output(['dotnet','--info'],text=True),workflow_head=p.git('rev-parse','HEAD'),
        run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],workflow_event=os.environ['GITHUB_EVENT_NAME'],workflow_ref=os.environ['GITHUB_REF'],preparation_mode='authenticated-kama-fused-prefix-diagnostic',
        source_authentication=source_authentication(plan),harness_role_alias=alias,published_baseline=publication,
        common_manifest=p.manifest(out/'common'),evidence_manifest=p.manifest(out/'evidence'),production_sha256=production,checks=checks,tested_runtime_dependencies=dependencies)
    proof['common_harness_bridge']=common_harness_bridge(out,plan)
    proof['evidence_manifest']=p.manifest(out/'evidence')
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256']=record_inventory(out,plan,p.digest(p.PLAN))
    raw=(json.dumps(case_inventory(),indent=2)+'\n').encode()
    (out/'case-selection-inventory.json').write_bytes(raw)
    proof['case_selection_inventory_sha256']=sha(raw)
    p.write(out/'provenance.json',proof);p.verify_inputs(out)
    return proof

def native_rows(folder):
    from .verify import archived,sample
    from .checkpoints import case_key
    roots=[x.parent for x in Path(folder).glob('*/provenance.json')];assert len(roots)==1
    root=roots[0];proof=p.read(root/'provenance.json');state=p.read(root/'status.json')
    tasks={t['fixture']:t for t in shards()[0]['tasks']};rounds={}
    metadata={r['fixture']:r for r in consumer_registry()}
    for record in state['rounds']:
        fixture,index=record['fixture'],record['index'];assert fixture in tasks and index in (0,1,2,3)
        assert (fixture,index) not in rounds
        path=archived(root,proof['raw_root'],record['report']);assert p.digest(path)==record['sha256']
        rows={}
        for case in p.read(path)['Benchmarks']:
            assert case['Type']==fixture
            key=case_key(case);assert key not in rows
            contract=metadata[fixture]
            assert re.search(r'\b'+contract['pair_parameter']+r': "'+re.escape(key[0])+r'"',case['FullName'])
            assert re.search(r'\b'+contract['size_parameter']+r': '+str(key[1])+r'(?:,|\))',case['FullName'])
            assert case['FullName'].split('(',1)[0].endswith('.'+case['Method'])
            rows[key]=sample(case)
        task=tasks[fixture]
        assert set(rows)=={(o['raw'],n,m) for o in task['operations'] for n in COUNTS for m in p.methods_for(task,index)}
        rounds[fixture,index]=rows
    assert set(rounds)=={(f,i) for f in tasks for i in range(4)}
    result=[]
    for fixture,task in tasks.items():
        for operation in task['operations']:
            pair=operation['raw']
            for bars in COUNTS:
                native={m:rounds[fixture,2][pair,bars,m] for m in task['methods'] if m not in p.OWNED}
                for method in (m for m in task['methods'] if m in p.OWNED):
                    result.append(dict(fixture=fixture,pair=pair,bars=bars,method=method,
                        old_rounds={str(i):rounds[fixture,i][pair,bars,method] for i in (0,3)},
                        candidate_rounds={str(i):rounds[fixture,i][pair,bars,method] for i in (1,2)},native_controls=native))
    return result

def adjudicate_feasibility(rows,decisions):
    expected={(FIXTURE,pair,n,'Ooples') for pair in PAIRS for n in COUNTS}
    indexed={(r['fixture'],r['pair'],r['bars'],r['method']):r for r in rows}
    own={(d['fixture'],d['pair'],d['bars'],d['method']):d['verdict'] for d in decisions}
    assert len(rows)==len(indexed)==len(decisions)==len(own)==12 and set(indexed)==set(own)==expected
    assert set(own.values())<={'faster','slower','inconclusive'}
    details=[];new_wins=[];protected=[];reopened=[]
    old_counts={'0':0,'3':0};new_counts={'1':0,'2':0};shared_controls={}
    for key in sorted(expected):
        row=indexed[key]
        assert set(row['old_rounds'])=={'0','3'} and set(row['candidate_rounds'])=={'1','2'}
        assert set(row['native_controls'])=={'Competitor'}
        control_key=key[:3]
        if control_key in shared_controls: assert shared_controls[control_key]==row['native_controls']
        else: shared_controls[control_key]=row['native_controls']
        native=list(row['native_controls'].values())
        best_mean=min(v['mean_ns'] for v in native);best_lower=min(v['lower_ns'] for v in native)
        old=list(row['old_rounds'].values());new=list(row['candidate_rounds'].values())
        eligible=all(v['mean_ns']>=best_mean for v in old)
        prior=all(v['upper_ns']<best_lower for v in old)
        separated=all(v['upper_ns']<best_lower for v in new)
        target=row['pair'] in TARGETS and row['bars']==10000
        won=target and eligible and own[key]=='faster' and separated
        if row['bars']==10000:
            for i,v in row['old_rounds'].items():old_counts[i]+=int(v['mean_ns']<best_mean)
            for i,v in row['candidate_rounds'].items():new_counts[i]+=int(v['mean_ns']<best_mean)
        detail=dict(**row,source_verdict=own[key],best_native_mean_ns=best_mean,best_native_lower_ns=best_lower,
                    baseline_new_win_eligible=eligible,baseline_interval_native_winner=prior,candidate_statistical_native_win=separated,new_10000_target_win=won)
        details.append(detail)
        if won:new_wins.append({k:row[k] for k in ('fixture','pair','bars','method')})
        if prior:
            check=dict(**{k:row[k] for k in ('fixture','pair','bars','method')},preserved=all(v['mean_ns']<best_mean for v in new))
            protected.append(check)
            if not check['preserved']:reopened.append(check)
    no_slower=all(v!='slower' for v in own.values());net=min(new_counts.values())>max(old_counts.values())
    return dict(passed=no_slower and bool(new_wins) and not reopened and net,
        all_twelve_source_comparisons_no_regression=no_slower,new_10000_target_wins=new_wins,
        old_10000_point_win_counts=old_counts,candidate_10000_point_win_counts=new_counts,net_10000_point_progress=net,
        protected_prior_native_wins=protected,prior_native_win_reopenings=reopened,all_context_rows=details,
        native_control_round=2,minimum_native_margin=None,qualification=False,closure_credit=0,automatic_qualification=False)

def collect(folder):
    from .verify import _collect
    original.verify_runtime(folder,p.read(p.PLAN)['expected_runtime_version'])
    result=_collect(Path(folder),'diagnostic',preflight=False,diagnostic=True)
    assert not result['accepted'] and (result['case_count'],result['decision_count'])==(60,12)
    result.update(diagnostic_only=True,qualification=False,report_imported=False,closure_ledger_new_rows=0,
                  automatic_qualification=False,feasibility_screen=adjudicate_feasibility(native_rows(folder),result['decisions']))
    return result
