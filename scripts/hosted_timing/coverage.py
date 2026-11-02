"""Measure missing comparisons of qualified source without qualifying new code."""
import hashlib
import json
import subprocess

from .pipeline import BENCH, REPO, TESTS, git


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
        return
    assert not plan.get('qualify_variants') and not plan.get('standalone_variants')
    assert not plan.get('trix_preflight')
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
