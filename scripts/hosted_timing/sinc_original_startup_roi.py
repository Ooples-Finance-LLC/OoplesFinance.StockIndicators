"""Held actual Sinc builder feasibility screen; no automatic qualification or credit."""
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

KIND = 'sinc-original-startup-native-pilot'
OLD = 'd3d5c905a5d75b666b440aa6c5f6b4c24b42dc91'
NEW = '75259781f6efcd7d4403ae2b761790d0ca7e41cd'
PUBLISHED = '87405159446f3de0132ce49800b52bd0af00e234'
SINC = 'SharedWindowedSincBenchmarks'
PAIR = 'QuanTAlib.Afirma'
PRIOR = 'QuanTAlib.Gma'
COUNTS = (1000, 10000)
HARNESS_PATH = p.BENCH + '/bin/Release/net10.0/' + original.HARNESS_ASSEMBLY
HARNESS_SCOPES = [p.BENCH,p.TESTS,'Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.Config','nuget.config']
PATHS = {'src/Indicators/WindowedSinc.cs','src/Indicators/ValuesBarExecution.AnalyticGrid.cs',
         'src/Indicators/ValuesBarExecution.BoundedWindows.cs','src/Indicators/ValuesBarExecution.SincGrid.cs',
         'tests/ModelsTests/SincGridBatchTests.cs'}
UNIT = ['SincGridBatchTests','WindowedSincCompactTests','SharedWeightedWindowTests','AnalyticGridBatchTests']
PARITY = ['SincBuilderComparisonTests','AnalyticKernelComparisonTests','FixedWeightedComparisonTests','ConvolutionComparisonTests']
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
    tasks=[dict(fixture='LibraryPairBenchmarks',operations=[dict(raw=PRIOR,pair=PRIOR)],
                methods=['Ooples','Competitor'],implicit_pair=False,cases=10,decisions=2),
           dict(fixture=SINC,operations=[dict(raw=PAIR,pair=PAIR)],
                methods=['BuilderFull','BuilderLatestOnly','NativeSingle','NativeParallel'],
                implicit_pair=False,cases=20,decisions=4)]
    return [dict(index=0,tasks=tasks,cases=30,decisions=6)]

def consumer_registry():
    """Expand the existing paired task registry, after ordered source dispatch."""
    return [
        dict(fixture='LibraryPairBenchmarks',pair_parameter='PairId',size_parameter='Bars',
             operations=['QuanTAlib.Gma','QuanTAlib.Sinema','QuanTAlib.Convolution'],
             methods=['Ooples','Competitor'],sizes=list(COUNTS)),
        dict(fixture='SharedWeightedWindowBenchmarks',pair_parameter='Operation',size_parameter='Count',
             operations=['Gma','Sinema','Convolution'],
             methods=['BuilderFull','BuilderLatestOnly','NativeSingle','NativeParallel'],sizes=list(COUNTS)),
        dict(fixture='SharedFibonacciWeightedBenchmarks',pair_parameter='Operation',size_parameter='Count',
             operations=['QuanTAlib.Fwma'],
             methods=['BuilderFull','BuilderLatestOnly','NativeSingle','NativeParallel'],sizes=list(COUNTS)),
        dict(fixture=SINC,pair_parameter='Operation',size_parameter='Count',operations=[PAIR],
             methods=['BuilderFull','BuilderLatestOnly','NativeSingle','NativeParallel'],sizes=list(COUNTS)),
    ]

def case_inventory():
    from .checkpoints import filters
    records=[]
    metadata={r['fixture']:r for r in consumer_registry()}
    for index,arm in enumerate(common.POLICY['order']):
        for task in shards()[0]['tasks']:
            row=metadata[task['fixture']]
            for operation in task['operations']:
                for method in p.methods_for(task,index):
                    selection=filters(task['fixture'],operation['raw'],[method])
                    for bars in COUNTS:
                        records.append(dict(round=index,arm=arm,fixture=task['fixture'],raw=operation['raw'],
                            pair=operation['pair'],method=method,bars=bars,filters=selection,
                            parameters={row['pair_parameter']:operation['raw'],row['size_parameter']:bars}))
    assert len(records)==30 and [sum(r['round']==i for r in records) for i in range(4)]==[6,6,12,6]
    return records

NATIVE_POLICY = dict(fixture=SINC,owned_methods=['BuilderFull','BuilderLatestOnly'],
    native_methods=['NativeSingle','NativeParallel'],native_round=2,old_rounds=[0,3],candidate_rounds=[1,2],
    minimum_margin=None,require_separated_intervals_both_rounds=True,new_closure_bars=10000,
    minimum_new_closures=1,require_own_source_faster=True,require_net_10000_point_progress=True,
    protect_prior_confident_wins_both_sizes=True,prior_control=PRIOR)

def verify_source_boundary():
    assert set(p.git('diff','--name-only',OLD,NEW,'--','src','tests',p.BENCH,p.TESTS).splitlines())==PATHS
    assert not p.git('diff','--name-only',OLD,NEW,'--',*HARNESS_SCOPES)

def verify_registry():
    expected={'FibonacciWeightedAverage','NormalizedConvolution','GaussianWeightedAverage','SineWeightedAverage','WindowedSinc'}
    for source in (OLD,NEW):
        bounded=blob(source,'src/Indicators/ValuesBarExecution.BoundedWindows.cs').decode()
        period=bounded.split('private static int WeightedWindowPeriod',1)[1].split('private static int BoundedParallelMinimum',1)[0]
        dispatch=bounded.split('private static Bar FillBoundedWindow(',1)[1].split('_ when WeightedWindowPeriod(indicator) > 0 =>',1)[0]
        types=set(re.findall(r'^\s*(\w+)(?:\s+\{|\s+\w+\s*=>)',period,re.M))
        intercepted=set(re.findall(r'^\s*(\w+)(?:\s+\{|\s+\w+\s*=>)',dispatch,re.M))
        assert types-intercepted==expected, (types,intercepted)
        assert 'FillBoundedWindow<StateWindowKernel>(source, output, indicator, WeightedWindowPeriod(indicator)' in bounded
        for row in consumer_registry():
            text=blob(source,p.BENCH+'/LibraryComparisons/'+row['fixture']+'.cs').decode()
            assert re.search(r'\[Params\(1_?000,\s*10_?000\)\]\s*public int '+row['size_parameter'],text)
            assert 'public string '+row['pair_parameter'] in text
            for method in row['methods']:
                assert re.search(r'\b'+method+r'\s*\(',text), (row['fixture'],method)
            if row['fixture']!='LibraryPairBenchmarks':
                for operation in row['operations']: assert '"'+operation+'"' in text
        sinc=blob(source,p.BENCH+'/LibraryComparisons/'+SINC+'.cs').decode()
        assert 'private const int Period = 20;' in sinc and 'private const int Taps = 21;' in sinc
        assert 'FixedKernelBenchmarkInput.Create(Count' in sinc
        snapshot=blob(source,p.BENCH+'/LibraryComparisons/FixedKernelSnapshotComparison.cs').decode()
        assert 'WindowedSincSnapshot' in snapshot
    # This is the existing paired-profile task registry, not every diagnostic
    # constructor elsewhere in the benchmark project.
    registry=p.read(p.REPO/'benchmarks/plans/hosted-analytic-grid-qualification-20261119.json')
    tasks={}
    for shard in registry['targeted_shards']:
        for task in shard['tasks']:
            row=tasks.setdefault(task['fixture'],dict(operations=set(),methods=set()))
            row['operations'].update(o['raw'] for o in task['operations'])
            row['methods'].update(task['methods'])
    for row in consumer_registry():
        task=tasks[row['fixture']]
        assert set(row['operations'])<=task['operations']
        assert set(row['methods'])<=task['methods']
    factories={
        'SharedFibonacciWeightedBenchmarks.cs':['new FibonacciWeightedAverage(Period)'],
        'SharedWeightedWindowBenchmarks.cs':['new GaussianWeightedAverage(Period)','new SineWeightedAverage(Period)','new NormalizedConvolution(Kernel())'],
        'AnalyticKernelComparison.cs':['new GaussianWeightedAverage(period, sigma)','new SineWeightedAverage(period)'],
        'ConvolutionComparison.cs':['new NormalizedConvolution(Weights(period, false, kernel))'],
    }
    for name,constructors in factories.items():
        text=blob(NEW,p.BENCH+'/LibraryComparisons/'+name).decode()
        assert all(constructor in text for constructor in constructors)

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
    for key,source,unit,parity in [('old_candidate',OLD,53,48),('candidate',NEW,95,48)]:
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
    root=json.loads(evidence(plan['builder_proof_root_review']))
    proof=json.loads(evidence(plan['builder_proof']))
    assert root['status']=='root-independent-actual-builder-proof-authenticated' and root['source']==NEW
    assert root['source_fingerprint']==plan['candidate']['source_fingerprint']
    assert root['cpu_root_review_sha256']==plan['candidate']['cpu_root_review']['sha256']
    assert root['gpu_root_review_sha256']==plan['candidate']['gpu_root_review']['sha256']
    assert root['proof_sha256']==plan['builder_proof']['sha256']
    assert root['helper_sha256']==plan['builder_proof_helper']['sha256'] and evidence(plan['builder_proof_helper'])
    assert root['full_outputs_equal_original_both_modes'] is True and root['hosted_worker_count_not_inferred'] is True
    assert proof['core_sha256']==plan['candidate']['production_sha256'] and proof['harness_sha256']==plan['candidate']['actual_harness_sha256']
    assert proof['status']=='actual-builder-correctness-proof-only' and proof['fixture_dop']==8
    assert (proof['period'],proof['taps'],proof['window'])==(20,21,'Hann')
    assert proof['builder_calls']==2 and proof['setup_calls']==proof['oopless_calls']==proof['profiles']==proof['timings']==proof['local_builds']==0
    assert len(proof['calls'])==2 and {x['method'] for x in proof['calls']}=={'BuilderFull','BuilderLatestOnly'}
    assert all(x['bar_count']==10000 and x['output_sha256']==proof['reference_output_sha256'] for x in proof['calls'])

def validate_scope(plan, *, require_ready=True):
    assert plan['diagnostic_kind']==KIND and plan['diagnostic_only'] is True
    assert plan['sources']==dict(published=OLD,old=OLD,successor=NEW,variants=OLD)
    assert plan['gpu_sources']==dict(old=OLD,successor=NEW,variants=OLD)
    assert (plan['cases'],plan['decisions'])==(30,6)
    assert plan['matrix']==dict(shards=1,max_parallel=1,timeout_minutes_per_shard=120)
    assert plan['targeted_shards']==plan['diagnostic_shards']==shards()
    assert plan['measurement_policy']==common.POLICY and plan['runtime_policy_overrides']=={}
    assert plan['runtime_dependency_alignment']==dict(tensor_version='0.151.1')
    assert plan['require_fixture_inventory'] is True
    assert (plan['proposed_dotnet_sdk'],plan['expected_runtime_version'])==('10.0.403','.NET 10.0.15')
    assert plan['native_progress_gate']==NATIVE_POLICY
    policy.verify_native_policy(plan)
    assert plan['closure_ledger_new_rows']==0 and plan['automatic_qualification'] is False and plan['report_import_permitted'] is False
    assert plan['full_consumer_registry']==consumer_registry()
    assert plan['requested_case_inventory']==case_inventory()
    assert plan['harness_alias_policy']==dict(common_artifact_role='old',variants_artifact_role='old',baseline_source=OLD,candidate_source=NEW,
        require_scoped_source_identity=True,claim_artifact_binary_identity=False)
    assert plan['old_candidate']['source']==OLD and plan['candidate']['source']==NEW
    assert plan['old_candidate']['unit_classes']==BASE_UNIT and plan['old_candidate']['parity_classes']==BASE_PARITY
    assert plan['candidate']['unit_classes']==UNIT and plan['candidate']['parity_classes']==PARITY
    assert (plan['candidate']['unit_passed'],plan['candidate']['parity_passed'])==(95,48)
    assert (plan['old_candidate']['unit_passed'],plan['old_candidate']['parity_passed'])==(53,48)
    assert plan['cpu_test_classes']==dict(old=BASE_UNIT,successor=UNIT,variants=BASE_UNIT)
    assert type(plan['inputs_ready']) is bool and type(plan['parent_review_approved']) is bool
    if require_ready:
        assert plan['inputs_ready'] is True and plan['parent_review_approved'] is True, 'HELD: root review before SDK/input I/O'
    verify_source_boundary();verify_registry();verify_publication(plan);verify_root_evidence(plan)

def checked_inputs(folder,c):
    """Reviewed package/GPU verification, with all source and evidence passed explicitly."""
    folder=Path(folder).resolve()
    def read(name):
        path=(folder/name).resolve();assert path.is_relative_to(folder) and path.is_file();return path.read_bytes()
    spec=importlib.util.spec_from_file_location('sinc_candidate_ci',p.REPO/'scripts/build-candidate-correctness.py')
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
        with tempfile.TemporaryDirectory(prefix='sinc-trx-') as temp:
            path=Path(temp)/'checks.trx';path.write_bytes(read('evidence/'+label+'.trx'));checks=p.verify_trx(path,classes)
        assert checks==manifest['checks'][label] and checks['passed']==c[label+'_passed']
    receipt_raw,trx_raw=evidence(c['gpu_receipt']),evidence(c['gpu_trx'])
    receipt=json.loads(receipt_raw)
    assert ci.gpu_module(p.REPO).verify_evidence(receipt,gzip.decompress(trx_raw),json.loads(blob(source,'scripts/competitor-gpu-methods.json')),fingerprint)==100
    origin=receipt['build_origin']
    assert (origin['source'],origin['run_id'],origin['workflow_head'],str(origin['artifact_id']))==(source,c['ci_run'],c['producer'],c['artifact_id'])
    assert receipt['test_assembly_sha256']==manifest['runtime_files'][ci.ROOTS[-1]+'/OoplesFinance.StockIndicators.CompetitorTests.dll']
    from .runtime_dependencies import tensor_closure
    with tempfile.TemporaryDirectory(prefix='sinc-closure-') as temp:
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

def verify_authentication(folder,plan,proof):
    validate_scope(plan)
    assert proof['preparation_mode']=='authenticated-sinc-original-startup-diagnostic'
    assert proof['published_baseline']==verify_publication(plan)
    raw=(folder/'case-selection-inventory.json').read_bytes()
    assert sha(raw)==proof['case_selection_inventory_sha256'] and json.loads(raw)==case_inventory()
    verify_harness_alias(folder,plan,proof)
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
        run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],preparation_mode='authenticated-sinc-original-startup-diagnostic',
        source_authentication=source_authentication(plan),harness_role_alias=alias,published_baseline=publication,
        common_manifest=p.manifest(out/'common'),evidence_manifest=p.manifest(out/'evidence'),production_sha256=production,checks=checks,tested_runtime_dependencies=dependencies)
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
        pair=task['operations'][0]['raw']
        for bars in COUNTS:
            native={m:rounds[fixture,2][pair,bars,m] for m in task['methods'] if m not in p.OWNED}
            for method in (m for m in task['methods'] if m in p.OWNED):
                result.append(dict(fixture=fixture,pair=pair,bars=bars,method=method,
                    old_rounds={str(i):rounds[fixture,i][pair,bars,method] for i in (0,3)},
                    candidate_rounds={str(i):rounds[fixture,i][pair,bars,method] for i in (1,2)},native_controls=native))
    return result

def adjudicate_feasibility(rows,decisions):
    expected={(SINC,PAIR,n,m) for n in COUNTS for m in ('BuilderFull','BuilderLatestOnly')}|{('LibraryPairBenchmarks',PRIOR,n,'Ooples') for n in COUNTS}
    indexed={(r['fixture'],r['pair'],r['bars'],r['method']):r for r in rows}
    own={(d['fixture'],d['pair'],d['bars'],d['method']):d['verdict'] for d in decisions}
    assert len(rows)==len(indexed)==len(decisions)==len(own)==6 and set(indexed)==set(own)==expected
    assert set(own.values())<={'faster','slower','inconclusive'}
    details=[];new_wins=[];protected=[];reopened=[]
    old_counts={'0':0,'3':0};new_counts={'1':0,'2':0};shared_controls={}
    for key in sorted(expected):
        row=indexed[key]
        assert set(row['old_rounds'])=={'0','3'} and set(row['candidate_rounds'])=={'1','2'}
        assert set(row['native_controls'])==({'NativeSingle','NativeParallel'} if row['fixture']==SINC else {'Competitor'})
        control_key=key[:3]
        if control_key in shared_controls: assert shared_controls[control_key]==row['native_controls']
        else: shared_controls[control_key]=row['native_controls']
        native=list(row['native_controls'].values())
        best_mean=min(v['mean_ns'] for v in native);best_lower=min(v['lower_ns'] for v in native)
        old=list(row['old_rounds'].values());new=list(row['candidate_rounds'].values())
        eligible=all(v['mean_ns']>=best_mean for v in old)
        prior=all(v['upper_ns']<best_lower for v in old)
        separated=all(v['upper_ns']<best_lower for v in new)
        target=row['fixture']==SINC and row['bars']==10000
        won=target and eligible and own[key]=='faster' and separated
        if row['bars']==10000:
            for i,v in row['old_rounds'].items():old_counts[i]+=int(v['mean_ns']<best_mean)
            for i,v in row['candidate_rounds'].items():new_counts[i]+=int(v['mean_ns']<best_mean)
        detail=dict(**row,source_verdict=own[key],best_native_mean_ns=best_mean,best_native_lower_ns=best_lower,
                    baseline_new_win_eligible=eligible,baseline_interval_native_winner=prior,candidate_statistical_native_win=separated,new_10000_builder_win=won)
        details.append(detail)
        if won:new_wins.append({k:row[k] for k in ('fixture','pair','bars','method')})
        if prior:
            check=dict(**{k:row[k] for k in ('fixture','pair','bars','method')},preserved=all(v['mean_ns']<best_mean for v in new))
            protected.append(check)
            if not check['preserved']:reopened.append(check)
    no_slower=all(v!='slower' for v in own.values());net=min(new_counts.values())>max(old_counts.values())
    return dict(passed=no_slower and bool(new_wins) and not reopened and net,
        all_six_source_comparisons_no_regression=no_slower,new_10000_builder_wins=new_wins,
        old_10000_point_win_counts=old_counts,candidate_10000_point_win_counts=new_counts,net_10000_point_progress=net,
        protected_prior_native_wins=protected,prior_native_win_reopenings=reopened,all_context_rows=details,
        native_control_round=2,minimum_native_margin=None,qualification=False,closure_credit=0,automatic_qualification=False)

def collect(folder):
    from .verify import _collect
    original.verify_runtime(folder,p.read(p.PLAN)['expected_runtime_version'])
    result=_collect(Path(folder),'diagnostic',preflight=False,diagnostic=True)
    assert not result['accepted'] and (result['case_count'],result['decision_count'])==(30,6)
    result.update(diagnostic_only=True,qualification=False,report_imported=False,closure_ledger_new_rows=0,
                  automatic_qualification=False,feasibility_screen=adjudicate_feasibility(native_rows(folder),result['decisions']))
    return result
