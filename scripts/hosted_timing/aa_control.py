"""Approved, diagnostic-only routing of both arms to one complete old profile."""
from pathlib import Path
import os

ORIGINAL = 'benchmarks/plans/hosted-pvo-adjacent-certificate-20261105.json'
ORIGINAL_SHA = 'bece7c00fef12c147da613b8bc934a1dc322033f3bc7d7fb4c8efa922cc936a4'
SOURCE = 'e9f7106541a010fe3d0ce72fb0c925483691165e'
PAIR = 'Trady.Indicator.MovingAverageConvergenceDivergence'
ORDER = ['A', 'B', 'B', 'A']


def shards():
    return [dict(index=0, cases=10, comparisons=2, tasks=[dict(
        fixture='LibraryPairBenchmarks', operations=[dict(raw=PAIR, pair=PAIR)],
        methods=['Ooples','Competitor'], implicit_pair=False, cases=10, decisions=2)]),
        dict(index=1, cases=20, comparisons=4, tasks=[dict(
        fixture='SharedTradyMacdBenchmarks',
        operations=[dict(raw='MovingAverageConvergenceDivergence', pair=PAIR)],
        methods=['BuilderFull','BuilderLatestOnly','NativeSingle','NativeParallel'],
        implicit_pair=False, cases=20, decisions=4)])]


def runtime_controls():
    names = ('TieredPGO','TieredCompilation','ReadyToRun','TC_QuickJit','JitStress','JitDisasm')
    return {prefix+name:os.environ[prefix+name] for prefix in ('DOTNET_','COMPlus_')
            for name in names if os.environ.get(prefix+name)}


def validate_plan(path):
    from . import pipeline as p
    path = Path(path).resolve()
    d = p.read(path)
    assert p.PLAN.resolve() == (p.REPO/ORIGINAL).resolve()
    assert p.digest(p.PLAN) == ORIGINAL_SHA
    original = p.read(p.PLAN)
    assert d['diagnostic_only'] is True and d['status'] == 'frozen'
    assert d['original_plan'] == dict(path=ORIGINAL, sha256=ORIGINAL_SHA)
    assert d['original_input']['run_id'] == '41988339055'
    assert d['original_input']['run_attempt'] == '1'
    assert d['original_input']['workflow_head'] == '9c8096a10bba8ffcca43c4da81a53fe922a4b7a8'
    assert d['original_input']['artifact_id'] == 13398353405
    assert d['original_input']['artifact_digest'] == 'sha256:72a7cc4bc4af990bf5318802fb940da4847861afbc43a1dcc209fa3f04d47ec7'
    assert d['sources'] == original['sources']
    assert d['proposed_dotnet_sdk'] == original['proposed_dotnet_sdk'] == '10.0.403'
    assert d['cpu_test_classes'] == {k:p.cpu_test_classes(original,k) for k in ('old','successor','variants')}
    assert d['gpu_sources'] == {k:p.original_gpu_source(original,k) for k in ('old','successor','variants')}
    assert d['measurement_policy'] == original['measurement_policy']
    assert d['measurement_policy']['warmups'] == d['measurement_policy']['iterations'] == 10
    assert d['measurement_policy']['fixed_invocation_warmups'] == 1000
    assert d['measurement_policy']['quiet_samples'] == 12 and d['measurement_policy']['max_cpu_percent'] == 15
    assert d['order'] == ORDER and d['shards'] == shards()
    assert (d['cases'],d['comparisons']) == (30,6)
    assert d['matrix'] == dict(max_parallel=2, timeout_minutes_per_job=180)
    assert d['profile_label'] == 'old' and d['actual_source'] == SOURCE == original['sources']['old']
    assert d['production_sha256'] == d['production_hashes']['old']
    assert d['runtime_overrides'] == {}
    assert d['rejected_qualifications'] == ['41969039432','41988339055']
    return dict(d, _plan_sha256=p.digest(path))


def validate(path, inputs):
    from . import pipeline as p
    d = validate_plan(path)
    inputs = Path(inputs)
    assert p.digest(inputs/'provenance.json') == d['original_input']['provenance_sha256']
    proof = p.verify_inputs(inputs)  # Complete original tuple, classes, GPU receipts and every file.
    assert proof['sources'] == d['sources']
    assert proof['sdk'] == d['proposed_dotnet_sdk']
    for key in ('run_id','run_attempt','workflow_head'):
        assert proof[key] == d['original_input'][key]
    assert proof['production_sha256'] == d['production_hashes']
    assert p.digest(inputs/'production/old.dll') == d['production_sha256']
    return dict(d, _verified_inputs=proof)
