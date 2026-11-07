"""Frozen diagnostic routing; never a performance qualification."""
from pathlib import Path
import os

ORIGINAL = 'benchmarks/plans/hosted-body-shadow-comparison-weights-20261106.json'
ORIGINAL_SHA = 'e6cec4820db09d5dd75b86916fcc528f9f915c2a461d8b309f6a29f3751530ca'
PLAN_SHA = '5560fbd7e2323b6e93eba842437e5f1513b6524aabf2d9ab93a0e6da7e584c6a'
WARMUP_PLAN_SHA = '911454843e1a6c5b3b5d923b043eba2b071275fc007430b4099934f09b75efd0'
ORDER = ['A', 'B', 'B', 'A']


class DiagnosticPlan(dict):
    """An in-memory plan type that ordinary qualification JSON cannot create."""


def measurement_plan(original, diagnostic):
    if diagnostic.get('warmup_experiment'):
        assert diagnostic['_plan_sha256']==WARMUP_PLAN_SHA
        return DiagnosticPlan(original,measurement_policy=diagnostic['measurement_policy'])
    return dict(original,measurement_policy=diagnostic['measurement_policy'])


def runtime_controls():
    names = ('TieredPGO', 'TieredCompilation', 'ReadyToRun', 'TC_QuickJit', 'JitStress', 'JitDisasm', 'JitDisasmAssemblies', 'JitPrintInlinedMethods')
    return {prefix+name:os.environ[prefix+name] for prefix in ('DOTNET_', 'COMPlus_') for name in names if os.environ.get(prefix+name)}


def validate_plan(path):
    from . import pipeline as p
    path = Path(path).resolve()
    sha=p.digest(path)
    assert sha in (PLAN_SHA,WARMUP_PLAN_SHA), 'Frozen diagnostic plan changed'
    d = p.read(path)
    assert p.PLAN.resolve() == (p.REPO/ORIGINAL).resolve()
    assert p.digest(p.PLAN) == ORIGINAL_SHA
    original = p.read(p.PLAN)
    assert d['diagnostic_only'] is True and d['status'] == 'frozen'
    assert d['sources'] == original['sources']
    assert d['cpu_test_classes'] == {k:p.cpu_test_classes(original,k) for k in ('old','successor','variants')}
    assert d['gpu_sources'] == {k:p.original_gpu_source(original,k) for k in ('old','successor','variants')}
    policy=dict(original['measurement_policy'])
    if sha==WARMUP_PLAN_SHA:
        assert d['warmup_experiment'] is True and d['instrumentation_only']=={}
        policy['fixed_invocation_warmups']=20000
    assert d['measurement_policy'] == policy
    assert d['proposed_dotnet_sdk'] == original['proposed_dotnet_sdk'] == '10.0.403'
    assert d['runtime_dependency_alignment'] == original['runtime_dependency_alignment'] == dict(tensor_version='0.148.0')
    assert d['runtime_policy_overrides'] == {} and d['order'] == ORDER
    assert (d['cases'],d['comparisons']) == ((20,4) if sha==WARMUP_PLAN_SHA else (30,6))
    return dict(d, _plan_sha256=p.digest(path))


def profiles(d, shard):
    job = d['jobs'][shard]
    assert job['index'] == shard
    return tuple(job['label_profiles'][k] for k in ('A','B'))


def environment(d, shard):
    return d['instrumentation_only'] if d['jobs'][shard]['kind'] == 'observational-AB' else {}


def validate(path, inputs):
    from . import pipeline as p
    d = validate_plan(path)
    inputs = Path(inputs)
    assert p.digest(inputs/'provenance.json') == d['original_input']['provenance_sha256']
    proof = p.verify_inputs(inputs)
    assert proof['sources'] == d['sources'] and proof['sdk'] == d['proposed_dotnet_sdk']
    for key in ('run_id','run_attempt','workflow_head'):
        assert proof[key] == d['original_input'][key]
    assert proof['production_sha256'] == d['production_hashes']
    return dict(d, _verified_inputs=proof)


def collection_receipt(path, diagnostic):
    from . import pipeline as p
    assert p.digest(path) == 'ae95064cda9ff190d471596cd1cd12abf463455a3640fcff4b6198c055a1f9c3'
    d=p.read(path)
    assert d['diagnostic_only'] is True and d['diagnostic_plan_sha256']==diagnostic['_plan_sha256']
    assert d['measurement_workflow_head']=='b32555a46fc68c9fab1c456ffe626bd6bfb8167a'
    assert d['measurement_run']=='42325654760' and d['measurement_attempt']=='1'
    assert len(d['benchmark_logger_logs'])==4 and set(d['artifacts'])=={'0','1','2'}
    return dict(d, sha256=p.digest(path))
