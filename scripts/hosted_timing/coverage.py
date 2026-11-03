"""Measure missing comparisons of qualified source without qualifying new code."""
import hashlib
import json
import re
import subprocess

from .pipeline import BENCH, REPO, TESTS, git


def harness_additions(plan):
    additions = {BENCH + '/LibraryComparisons/SharedClassicalWindowVariantBenchmarks.cs',
                 TESTS + '/SharedClassicalWindowVariantBenchmarkTests.cs'}
    if plan.get('coverage_harness'):
        additions.update(BENCH + '/LibraryComparisons/' + name for name in (
            'SlowNativeTimingConfig.cs', 'SharedDynamicMomentumBenchmarks.cs', 'SharedTradyNativeCandleBenchmarks.cs'))
    return additions


def tasks():
    methods = ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']
    return [dict(fixture=fixture, group=0, operations=[dict(raw=raw, pair=pair)],
                 methods=methods, implicit_pair=False, cases=20, decisions=4)
            for fixture, raw, pair in [
                ('SharedDynamicMomentumBenchmarks', 'Trady.Indicator.DynamicMomentumIndex',
                 'Trady.Indicator.DynamicMomentumIndex'),
                ('SharedTradyNativeCandleBenchmarks', 'RisingThreeMethods',
                 'Trady.Candlestick.RisingThreeMethods')]]


def verify_reference(plan):
    reference = plan.get('coverage_reference')
    if reference is None:
        assert not plan.get('coverage_harness')
        return
    assert not plan.get('qualify_variants') and not plan.get('standalone_variants')
    assert not plan.get('trix_preflight')
    assert plan.get('coverage_shards', 1) in (1, 2)
    raw = subprocess.check_output(['git', 'show', reference['published'] + ':' + reference['decision']], cwd=REPO)
    assert hashlib.sha256(raw).hexdigest() == reference['sha256']
    decision = json.loads(raw)
    assert decision['accepted'] and decision['phase'] == 'successor'
    assert (decision['case_count'], decision['decision_count']) == (1860, 372)
    assert not decision['verdicts'].get('slower', 0)
    assert str(decision['run_id']) == str(reference['run_id'])
    assert decision['sources']['new'] == plan['sources']['successor']
    assert not git('diff', '--name-only', plan['sources']['successor'],
                   reference['published'], '--', 'src', 'tests', BENCH, TESTS)
    harness = plan.get('coverage_harness')
    if harness:
        assert not plan.get('reuse_inputs')
        assert plan['coverage_shards'] == 2
        assert plan['matrix'] == dict(shards=2, max_parallel=2, timeout_minutes_per_shard=240)
        assert harness['execution_timeout_minutes'] == 120
        native_count = harness.get('native_invocation_count')
        if native_count is not None:
            assert native_count == 1
            assert plan['measurement_policy']['operation_timeout_seconds'] == 10800
        assert not git('diff', '--name-only', harness['baseline'], plan['sources']['variants'], '--', 'src', 'tests', TESTS)
        names = ('SharedDynamicMomentumBenchmarks.cs', 'SharedTradyNativeCandleBenchmarks.cs')
        expected = {BENCH + '/LibraryComparisons/' + name for name in (*names, 'SlowNativeTimingConfig.cs')}
        assert set(git('diff', '--name-only', harness['baseline'], plan['sources']['variants'], '--', BENCH).splitlines()) == expected
        for name in names:
            path = BENCH + '/LibraryComparisons/' + name
            before = git('show', harness['baseline'] + ':' + path)
            after = git('show', plan['sources']['variants'] + ':' + path)
            if native_count is not None:
                assert after.count('[InvocationCount(1)]') == 2
                assert len(re.findall(r'\[InvocationCount\(1\)\]\s+\[Benchmark(?:\(Baseline = true\))?\]\s+public[^\r\n]*\bNative(?:Single|Parallel)\(', after)) == 2
                after = after.replace('    [InvocationCount(1)]\n', '')
            assert before.replace('Config(typeof(TensorsGpuTimingConfig))', 'Config(typeof(SlowNativeTimingConfig))') == after
        config = subprocess.check_output(['git','show',plan['sources']['variants'] + ':' + BENCH + '/LibraryComparisons/SlowNativeTimingConfig.cs'],cwd=REPO)
        assert hashlib.sha256(config).hexdigest() == harness['config_sha256']
