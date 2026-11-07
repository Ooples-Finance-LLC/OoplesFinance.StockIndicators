"""Frozen diagnostic routing; never a performance qualification."""
from pathlib import Path
import os

ORIGINAL = 'benchmarks/plans/hosted-body-shadow-comparison-weights-20261106.json'
ORIGINAL_SHA = 'e6cec4820db09d5dd75b86916fcc528f9f915c2a461d8b309f6a29f3751530ca'
PLAN_SHA = '5560fbd7e2323b6e93eba842437e5f1513b6524aabf2d9ab93a0e6da7e584c6a'
ORDER = ['A', 'B', 'B', 'A']


def runtime_controls():
    names = ('TieredPGO', 'TieredCompilation', 'ReadyToRun', 'TC_QuickJit', 'JitStress', 'JitDisasm', 'JitDisasmAssemblies', 'JitPrintInlinedMethods')
    return {prefix+name:os.environ[prefix+name] for prefix in ('DOTNET_', 'COMPlus_') for name in names if os.environ.get(prefix+name)}


def validate_plan(path):
    from . import pipeline as p
    path = Path(path).resolve()
    assert p.digest(path) == PLAN_SHA, 'Frozen diagnostic plan changed'
    d = p.read(path)
    assert p.PLAN.resolve() == (p.REPO/ORIGINAL).resolve()
    assert p.digest(p.PLAN) == ORIGINAL_SHA
    original = p.read(p.PLAN)
    assert d['diagnostic_only'] is True and d['status'] == 'frozen'
    assert d['sources'] == original['sources']
    assert d['cpu_test_classes'] == {k:p.cpu_test_classes(original,k) for k in ('old','successor','variants')}
    assert d['gpu_sources'] == {k:p.original_gpu_source(original,k) for k in ('old','successor','variants')}
    assert d['measurement_policy'] == original['measurement_policy']
    assert d['proposed_dotnet_sdk'] == original['proposed_dotnet_sdk'] == '10.0.403'
    assert d['runtime_dependency_alignment'] == original['runtime_dependency_alignment'] == dict(tensor_version='0.148.0')
    assert d['runtime_policy_overrides'] == {} and d['order'] == ORDER
    assert (d['cases'],d['comparisons']) == (30,6)
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
