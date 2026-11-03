"""Bound each arithmetic batch to complete mapped consumers and an accepted baseline."""
import hashlib
import json
import subprocess

from .pipeline import BENCH, OWNED, REPO, TESTS, git


FIXTURES = {
    'SharedHilbertBenchmarks': {'TaLib.Functions.HtDcPeriod', 'TaLib.Functions.HtPhasor', 'TaLib.Functions.Mama'},
    'SharedTaHilbertNativeBenchmarks': {'TaLib.Functions.HtDcPhase', 'TaLib.Functions.HtSine', 'TaLib.Functions.HtTrendline'},
    'SharedTaHilbertTrendModeBenchmarks': {'TaLib.Functions.HtTrendMode'},
    'SharedSeededPhaseBenchmarks': {'QuanTAlib.Mama', 'Skender.GetMama'},
    'SharedHilbertTrendlineBenchmarks': {'Skender.GetHtTrendline'},
}

SCOPES = {
    'hilbert-fir': dict(fixtures={f:{p:p for p in pairs} for f,pairs in FIXTURES.items()},
        paths={'src/Helpers/HilbertRoundedKernel.cs', 'src/Indicators/HilbertPhaseState.cs',
               'tests/ModelsTests/HilbertRoundedKernelTests.cs'}, totals=(300,60)),
    'ad-cumulative': dict(fixtures={
        'SharedRecursiveStateBenchmarks': {'TaLib.Functions.Ad':'Ad'},
        'SharedAdlDetailsBenchmarks': {'Skender.GetAdl':'Skender.GetAdl'},
        'SharedTradyNativeVolumeBenchmarks': {'Trady.Indicator.AccumulationDistributionLine':'AccumulationDistributionLine'}},
        paths={'src/Helpers/CompactAccumulationSum.cs', 'src/Helpers/MoneyFlowAccumulationWindow.cs',
               'src/Indicators/AccumulationDistributionWithAverage.cs',
               'src/Indicators/PriceAdjustedAccumulationDistribution.cs',
               'src/Indicators/ValuesBarExecution.AccumulationDistribution.cs',
               'tests/ModelsTests/CompactAccumulationSumTests.cs'}, totals=(90,18)),
}


def expected_totals(plan):
    return SCOPES[plan['targeted_reference']['scope']]['totals']


def verify_reference(plan):
    reference = plan.get('targeted_reference')
    if reference is None:
        return
    scope = SCOPES[reference['scope']]
    fixtures = scope['fixtures']
    assert not any(plan.get(k) for k in ('qualify_variants', 'standalone_variants', 'coverage_reference', 'trix_preflight'))
    assert plan['sources']['published'] == plan['sources']['old']
    raw = subprocess.check_output(['git', 'show', plan['sources']['published'] + ':' + reference['decision']], cwd=REPO)
    assert hashlib.sha256(raw).hexdigest() == reference['sha256']
    decision = json.loads(raw)
    assert decision['accepted'] and decision['phase'] == 'successor'
    assert (decision['case_count'], decision['decision_count']) == (1860, 372)
    assert decision['verdicts'].get('faster', 0) > 0
    assert not decision['verdicts'].get('slower', 0)
    assert str(decision['run_id']) == str(reference['run_id'])
    assert decision['sources']['new'] == reference['qualified_source']
    assert not git('diff', '--name-only', reference['qualified_source'], plan['sources']['old'],
                   '--', 'src', 'tests', BENCH, TESTS)
    changed = set(git('diff', '--name-only', plan['sources']['old'], plan['sources']['successor'],
                      '--', 'src', 'tests').splitlines())
    assert changed == scope['paths'], changed
    pairs = set().union(*(set(mapping) for mapping in fixtures.values()))
    seen = {'batch': set(), 'builder': set()}
    cases = decisions = 0
    assert len(plan['targeted_shards']) == 2
    for index, shard in enumerate(plan['targeted_shards']):
        assert shard['index'] == index
        shard_fixtures = [task['fixture'] for task in shard['tasks']]
        assert shard_fixtures and len(shard_fixtures) == len(set(shard_fixtures))
        for task in shard['tasks']:
            batch = task['fixture'] == 'LibraryPairBenchmarks'
            allowed = {p:p for p in pairs} if batch else fixtures[task['fixture']]
            expected_methods = ['Ooples', 'Competitor'] if batch else [
                'BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']
            assert task['methods'] == expected_methods
            assert bool(task.get('implicit_pair')) == (task['fixture'] == 'SharedTaHilbertTrendModeBenchmarks')
            category = 'batch' if batch else 'builder'
            for operation in task['operations']:
                pair = operation['pair']
                assert pair in allowed and operation['raw'] == allowed[pair] and pair not in seen[category]
                seen[category].add(pair)
            owned = len(set(task['methods']) & OWNED)
            expected_decisions = len(task['operations']) * 2 * owned
            expected_cases = len(task['operations']) * 2 * (3 * owned + len(task['methods']))
            assert (task['cases'], task['decisions']) == (expected_cases, expected_decisions)
            cases += expected_cases
            decisions += expected_decisions
    assert seen['batch'] == seen['builder'] == pairs
    assert (cases, decisions) == scope['totals']
