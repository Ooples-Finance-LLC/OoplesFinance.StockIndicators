"""Explicit harness baseline refresh; never production qualification."""
import copy
import os
from pathlib import Path
import re
import shutil
import subprocess
from . import pipeline as p, candle_family_roi as common
from .arithmetic_warmup_policy import PAIRS as ARITHMETIC, PROPOSAL as WARMUP_PROPOSAL, SHA as WARMUP_SHA

PUBLISHED='ce02581b1f55400fd674b5496ee659fa0e8654fa'
CANONICAL='2ea1fa379b0c7d05fdf881768b4b92908adcf9bc'
VARIANTS='ce994435f670a1c2c8d5424e167316914cfe9b51'
HARNESS='01f83663a8e7580a2941cc8bf2b0e92c1dd4a296'
CI_RUN='43795309989'
PRODUCER='0857eefc242585a9f44bfa3c134ea0bd3eb1b30a'
ASSEMBLY='OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'
HARNESS_SHA='9b901f6bd31f72e2bc494f9b25d85127b8f4937311b0427c87f9ab2db874e755'
UNIT=['SharedPointwiseExecutionTests','SharedArithmeticExecutionTests']
PARITY=['PointwiseComparisonPublisherTests','ElementaryMathComparisonTests','CircularComparisonTests','TranscendentalComparisonTests']
PAIRS=['TaLib.Functions.'+name for name in ('Add','Sub','Mult','Div','Ceil','Floor','Sqrt','Sin','Cos','Tan','Asin','Acos','Atan','Ln','Log10','Exp','Sinh','Cosh','Tanh')]
APPROVAL='benchmarks/plans/evidence/pointwise-family-refresh-scope-20261116.json'
SEED_PLAN='benchmarks/plans/hosted-pointwise-projection-native-pilot-20261116.json'
SEED_SHA='4811e70ccecf412e50cd644883e8ddac7334cd769bb6b04addc8e827d0ccf436'
SEED_PROOF='026d56b6ede5f32f3a949053ac24c03a51dfd8a893fc60d66cad2d2426a1857f'
POLICY=dict(order=['old','new','new','old'],quiet_samples=12,max_cpu_percent=15,warmups=10,iterations=10,
    fixed_invocation_warmups=1000,fixed_invocation_pair_warmups={pair:20000 for pair in sorted(ARITHMETIC)})
WARMUP_APPROVAL=dict(approved=True,proposal=WARMUP_PROPOSAL,sha256=WARMUP_SHA,diagnostic_run='43230319639')


def validate_scope(plan):
    approval=p.read(p.REPO/APPROVAL)
    assert approval['approved'] and approval['report_refresh_only'] and not approval['production_qualification']
    assert plan['refresh_approval']==dict(path=APPROVAL,sha256=p.digest(p.REPO/APPROVAL))
    assert plan['pointwise_family_refresh'] and plan['report_refresh_only'] and not plan['production_qualification']
    assert not plan.get('diagnostic_only')
    assert plan['sources']==dict(published=PUBLISHED,old=CANONICAL,successor=CANONICAL,variants=VARIANTS)
    assert plan['gpu_sources']==dict(old=CANONICAL,successor=CANONICAL,variants=VARIANTS)
    assert plan['cpu_test_classes']['old']==plan['cpu_test_classes']['successor']==UNIT
    assert plan['proposed_dotnet_sdk']=='10.0.403' and plan['runtime_policy_overrides']=={}
    assert plan['runtime_dependency_alignment']==dict(tensor_version='0.150.0')
    assert plan['measurement_policy']==POLICY
    assert plan['arithmetic_warmup_approval']==WARMUP_APPROVAL and p.digest(p.REPO/WARMUP_PROPOSAL)==WARMUP_SHA
    assert plan['require_fixture_inventory']
    assert (plan['cases'],plan['decisions'])==(190,38)
    assert plan['matrix']==dict(shards=19,max_parallel=2,timeout_minutes_per_shard=45)
    assert plan['affected_pairs']==PAIRS
    expected=[]
    for index,pair in enumerate(PAIRS):
        task=dict(fixture='LibraryPairBenchmarks',operations=[dict(raw=pair,pair=pair)],
            methods=['Ooples','Competitor'],implicit_pair=False,cases=10,decisions=2)
        expected.append(dict(index=index,tasks=[task],cases=10,decisions=2))
    assert plan['refresh_shards']==plan['targeted_shards']==expected
    assert not any(plan.get(k) for k in ('targeted_reference','coverage_reference','qualify_variants','standalone_variants','reuse_inputs','diagnostic_kind'))
    seed=plan['frozen_harness_inputs']
    assert seed==dict(plan=SEED_PLAN,plan_sha256=SEED_SHA,run_id='43791556704',
        workflow_head='2a3a90e14232f6c212e29ebc67c5650fcff78534',provenance_sha256=SEED_PROOF)
    candidate=plan['candidate']
    assert candidate['source']==CANONICAL and candidate['ci_run']==CI_RUN and candidate['producer']==PRODUCER
    assert candidate['unit_classes']==UNIT and candidate['parity_classes']==PARITY
    assert re.fullmatch('[a-f0-9]{40}',candidate['evidence_source'])
    assert all(re.fullmatch('[a-f0-9]{64}',candidate[key]) for key in ('manifest_sha256','gpu_receipt_sha256'))


def verify_runtime_identity(plan,manifests):
    validate_scope(plan)
    assert manifests['old']==manifests['new'], 'Baseline refresh requires identical production and harness under both labels'


def verify_authentication(folder,plan,proof):
    validate_scope(plan)
    seed=plan['frozen_harness_inputs'];candidate=plan['candidate']
    assert p.digest(folder/'evidence/family-frozen-harness-provenance.json')==seed['provenance_sha256']
    original=p.read(folder/'evidence/family-frozen-harness-provenance.json')
    assert original['run_id']==seed['run_id'] and original['workflow_head']==seed['workflow_head']
    assert proof['common_manifest']==original['common_manifest']
    assert p.digest(folder/'common'/ASSEMBLY)==HARNESS_SHA
    assert proof['frozen_harness_origin']==seed and proof['canonical_source_authentication']==candidate
    path=folder/'evidence/family-canonical-ci-manifest.json'
    assert p.digest(path)==candidate['manifest_sha256']
    manifest=p.read(path)
    assert manifest['source']==CANONICAL and manifest['run_id']==CI_RUN and manifest['workflow_head']==PRODUCER
    assert manifest['classes']==dict(unit=UNIT,parity=PARITY)
    for name,sha in manifest['evidence'].items():
        assert p.digest(folder/'evidence/family-canonical-ci-evidence'/Path(name).name)==sha
    production=manifest['runtime_files']['src/bin/Release/net10.0/'+p.DLL]
    assert production==plan['source_correctness']['production_sha256']
    assert manifest['source_sha256']==plan['source_correctness']['source_fingerprint']
    for label in ('old','successor'):
        assert proof['production_sha256'][label]==production
        assert proof['checks'][label]['original_gpu_source']==CANONICAL
        checks=p.verify_trx(folder/'evidence'/(label+'.trx'),UNIT)
        assert checks==manifest['checks']['unit'] and checks['passed']==111
        assert p.verify_trx(folder/'evidence'/(label+'-parity.trx'),PARITY)==manifest['checks']['parity']
        assert manifest['checks']['parity']['passed']==73
        receipt_path=folder/'evidence'/(label+'-original-gpu-receipt.json')
        assert p.digest(receipt_path)==candidate['gpu_receipt_sha256']
        receipt=p.read(receipt_path)
        assert receipt['source_sha256']==manifest['source_sha256']
        assert (receipt['build_origin']['source'],receipt['build_origin']['run_id'],receipt['build_origin']['workflow_head'])==(CANONICAL,CI_RUN,PRODUCER)
        assert receipt['test_assembly_sha256']==manifest['runtime_files'][p.TESTS+'/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorTests.dll']
    assert proof['production_sha256']['variants']==original['production_sha256']['variants']
    assert proof['checks']['variant-harness']==original['checks']['variant-harness']
    assert proof['checks']['variant-harness']['passed']==13


def prepare(seed,candidate,out):
    plan=p.read(p.PLAN);validate_scope(plan)
    seed,candidate,out=map(lambda path:Path(path).resolve(),(seed,candidate,out))
    assert not out.exists() and not out.is_relative_to(seed)
    assert p.digest(p.REPO/SEED_PLAN)==SEED_SHA
    with common.selected_plan(p.REPO/SEED_PLAN):original=p.verify_inputs(seed)
    assert p.digest(seed/'provenance.json')==SEED_PROOF
    assert original['run_id']=='43791556704' and p.digest(seed/'common'/ASSEMBLY)==HARNESS_SHA
    assert not p.git('diff','--name-only',PUBLISHED,CANONICAL,'--','src','tests')
    target='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/LibraryComparisons/'
    for name in ('PointwiseComparisonPublisher','CircularComparison','ElementaryMathComparison','TranscendentalComparison'):
        assert p.git('rev-parse',CANONICAL+':'+target+name+'.cs')==p.git('rev-parse',HARNESS+':'+target+name+'.cs')
    authenticated=common._candidate_inputs(candidate,plan,CANONICAL,UNIT,PARITY,111,73,CI_RUN)
    shutil.copytree(seed,out)
    shutil.copy2(seed/'provenance.json',out/'evidence/family-frozen-harness-provenance.json')
    (out/'evidence/family-canonical-ci-manifest.json').write_bytes((candidate/'manifest.json').read_bytes())
    retained=out/'evidence/family-canonical-ci-evidence';retained.mkdir()
    for name in authenticated['manifest']['evidence']:
        (retained/Path(name).name).write_bytes(authenticated['read'](name))
    for label in ('old','successor'):
        (out/'production'/(label+'.dll')).write_bytes(authenticated['read']('blobs/'+authenticated['production']))
        for source,target in [('unit',label),('parity',label+'-parity')]:
            (out/'evidence'/(target+'.trx')).write_bytes(authenticated['read']('evidence/'+source+'.trx'))
        (out/'evidence'/(label+'-original-gpu-receipt.json')).write_bytes(authenticated['gpu_receipt'])
        (out/'evidence'/(label+'-original-gpu-gpu.trx.gz')).write_bytes(authenticated['gpu_trx'])
        for source,target in [('unit-build.log',label+'-build.log'),('unit-tests.log',label+'-tests.log')]:
            (out/'evidence'/target).write_bytes(authenticated['read']('evidence/'+source))
    proof=copy.deepcopy(original)
    for label in ('old','successor'):
        proof['checks'][label]=dict(authenticated['manifest']['checks']['unit'],original_gpu_source=CANONICAL)
        proof['production_sha256'][label]=authenticated['production']
        proof['tested_runtime_dependencies'][label]=authenticated['closure']
    proof.pop('harness_input_reuse',None)
    proof.update(plan_sha256=p.digest(p.PLAN),sources=plan['sources'],
        source_trees={label:p.git('rev-parse',plan['sources'][label]+':src') for label in ('old','successor','variants')},
        evidence_manifest=p.manifest(out/'evidence'),workflow_head=p.git('rev-parse','HEAD'),
        run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],
        diagnostic_only=False,production_qualification=False,report_refresh_only=True,
        preparation_mode='authenticated-pointwise-family-baseline-refresh',
        canonical_source_authentication=plan['candidate'],frozen_harness_origin=plan['frozen_harness_inputs'])
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256']=record_inventory(out,plan,p.digest(p.PLAN))
    p.write(out/'provenance.json',proof);p.verify_inputs(out)
    return proof


def collect(folder):
    from .verify import _collect
    result=_collect(Path(folder),'baseline-refresh',preflight=False)
    assert not result['accepted'] and (result['case_count'],result['decision_count'])==(190,38)
    result.update(baseline_refresh_verified=True,report_refresh_only=True,production_qualification=False,
        production_optimization=False,report_imported=False,source_changed=False,same_production_under_both_labels=True)
    result['native_review']=native_review(Path(folder))
    return result


def native_review(folder):
    from .checkpoints import case_key
    from .verify import sample,archived
    controls={};owned={}
    for root in sorted(path.parent for path in Path(folder).glob('*/provenance.json')):
        proof=p.read(root/'provenance.json');records=p.read(root/'status.json')['rounds']
        assert len(records)==4
        for record in records:
            index=record['index']
            path=archived(root,proof['raw_root'],record['report'])
            for case in p.read(path)['Benchmarks']:
                pair,bars,method=case_key(case,None)
                key=pair,bars
                assert pair in PAIRS and bars in (1000,10000)
                row=dict(round=index,**sample(case))
                if method=='Competitor':
                    assert index==2 and key not in controls
                    controls[key]=row
                else:
                    assert method=='Ooples'
                    owned.setdefault(key,[]).append(row)
    assert len(controls)==len(owned)==38
    rows=[]
    for key in sorted(owned):
        rounds=sorted(owned[key],key=lambda row:row['round']);native=controls[key]
        assert [row['round'] for row in rounds]==[0,1,2,3]
        directions=['win' if row['upper_ns']<native['lower_ns'] else 'loss' if row['lower_ns']>native['upper_ns'] else 'inconclusive' for row in rounds]
        rows.append(dict(pair=key[0],bars=key[1],owned_rounds=rounds,native_control=native,
            native_round=2,round_classifications=directions,
            native_classification=directions[0] if len(set(directions))==1 else 'mixed',
            all_four_separated_native_wins=all(direction=='win' for direction in directions),
            final_round_native_over_owned=native['mean_ns']/rounds[2]['mean_ns']))
    return dict(rows=rows,native_controls_per_pair_size=1,
        limitation='Native controls occur in round2 only; native drift across rounds was not measured.',
        confirmed_native_wins={str(bars):sum(row['bars']==bars and row['all_four_separated_native_wins'] for row in rows) for bars in (1000,10000)})
