"""Bound Hilbert timing to its complete mapped consumers and an accepted baseline."""
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


def verify_reference(plan):
    reference = plan.get('targeted_reference')
    if reference is None:
        return
    assert reference['scope'] == 'hilbert-fir'
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
    assert changed == {'src/Helpers/HilbertRoundedKernel.cs', 'src/Indicators/HilbertPhaseState.cs',
                       'tests/ModelsTests/HilbertRoundedKernelTests.cs'}, changed
    pairs = set().union(*FIXTURES.values())
    seen = {'batch': set(), 'builder': set()}
    cases = decisions = 0
    assert len(plan['targeted_shards']) == 2
    for index, shard in enumerate(plan['targeted_shards']):
        assert shard['index'] == index
        fixtures = [task['fixture'] for task in shard['tasks']]
        assert fixtures and len(fixtures) == len(set(fixtures))
        for task in shard['tasks']:
            batch = task['fixture'] == 'LibraryPairBenchmarks'
            allowed = pairs if batch else FIXTURES[task['fixture']]
            expected_methods = ['Ooples', 'Competitor'] if batch else [
                'BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']
            assert task['methods'] == expected_methods
            assert bool(task.get('implicit_pair')) == (task['fixture'] == 'SharedTaHilbertTrendModeBenchmarks')
            category = 'batch' if batch else 'builder'
            for operation in task['operations']:
                pair = operation['pair']
                assert operation['raw'] == pair and pair in allowed and pair not in seen[category]
                seen[category].add(pair)
            owned = len(set(task['methods']) & OWNED)
            expected_decisions = len(task['operations']) * 2 * owned
            expected_cases = len(task['operations']) * 2 * (3 * owned + len(task['methods']))
            assert (task['cases'], task['decisions']) == (expected_cases, expected_decisions)
            cases += expected_cases
            decisions += expected_decisions
    assert seen['batch'] == seen['builder'] == pairs
    assert (cases, decisions) == (300, 60)
