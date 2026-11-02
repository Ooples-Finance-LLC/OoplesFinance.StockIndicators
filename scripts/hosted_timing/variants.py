"""Bind standalone WMA/TRIMA timing to a previously accepted full baseline."""
import hashlib
import json
import subprocess

from .pipeline import REPO, git


def verify_baseline(plan):
    reference = plan.get('standalone_variants')
    if reference is None:
        return
    assert plan['qualify_variants'] and not plan.get('trix_preflight')
    assert plan['sources']['published'] == plan['sources']['successor']
    raw = subprocess.check_output(['git', 'show', plan['sources']['published'] + ':' + reference['decision']], cwd=REPO)
    assert hashlib.sha256(raw).hexdigest() == reference['sha256']
    decision = json.loads(raw)
    assert decision['accepted'] and decision['phase'] == 'successor'
    assert (decision['case_count'], decision['decision_count']) == (1860, 372)
    assert not decision['verdicts'].get('slower', 0)
    assert str(decision['run_id']) == str(reference['run_id'])
    assert decision['sources']['new'] == reference['qualified_source']
    assert not git('diff', '--name-only', reference['qualified_source'],
                   plan['sources']['successor'], '--', 'src', 'tests')
    changed = git('diff', '--name-only', plan['sources']['successor'],
                  plan['sources']['variants'], '--', 'src', 'tests').splitlines()
    assert changed == ['src/Indicators/WideClassicalWindowState.cs'], changed
