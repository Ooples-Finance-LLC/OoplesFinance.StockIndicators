"""Complete Klinger/Schaff and dispersion scopes, including boolean parameters."""
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import test_hosted_timing
from hosted_timing.checkpoints import case_key, filters
from hosted_timing.pipeline import PLAN, REPO, read, write
from hosted_timing.targeted import SCOPES, verify_reference
from hosted_timing.verify import collect


def plan_for(scope):
    plan = copy.deepcopy(read(PLAN))
    plan.pop('coverage_reference',None);plan.pop('coverage_harness',None);plan.pop('reuse_inputs',None)
    plan['sources']['published'] = plan['sources']['old']
    plan['qualify_variants'] = plan['trix_preflight'] = False
    plan.pop('standalone_variants',None)
    plan['targeted_reference'] = dict(scope=scope,decision='receipt.json',run_id='original',qualified_source='qualified')
    fixtures = SCOPES[scope]['fixtures']
    pairs = set().union(*(set(mapping) for mapping in fixtures.values()))
    selected = [('LibraryPairBenchmarks',{p:p for p in sorted(pairs)}),*fixtures.items()]
    shards = [dict(index=i,tasks=[]) for i in range(SCOPES[scope].get('shards',2))]
    for index,(fixture,mapping) in enumerate(selected):
        batch = fixture == 'LibraryPairBenchmarks';owned = 1 if batch else 2
        methods = ['Ooples','Competitor'] if batch else ['BuilderFull','BuilderLatestOnly','NativeSingle','NativeParallel']
        task = dict(fixture=fixture,operations=[dict(pair=p,raw=r) for p,r in mapping.items()],
            methods=methods,implicit_pair=fixture == 'SharedTaHilbertTrendModeBenchmarks',cases=len(mapping)*2*(3*owned+len(methods)),decisions=len(mapping)*2*owned)
        if fixture == 'SharedDispersionBenchmarks':task['variance_parameter']=True
        shards[index%len(shards)]['tasks'].append(task)
    plan['targeted_shards']=shards
    return plan


class FollowupScopeTests(unittest.TestCase):
    def test_real_klinger_source_and_scope(self):
        verify_reference(read(REPO/'benchmarks/plans/hosted-klinger-schaff-20261103.json'))

    def verify(self,plan):
        receipt = dict(accepted=True,phase='successor',case_count=1860,decision_count=372,
            verdicts={'faster':13},run_id='original',sources={'new':'qualified'})
        raw = json.dumps(receipt).encode();plan['targeted_reference']['sha256']=hashlib.sha256(raw).hexdigest()
        with patch('hosted_timing.targeted.subprocess.check_output',return_value=raw), \
                patch('hosted_timing.targeted.git',side_effect=['','\n'.join(SCOPES[plan['targeted_reference']['scope']]['paths'])]):
            verify_reference(plan)

    def test_complete_scopes_and_evidence(self):
        for scope in ('klinger-schaff','rolling-dispersion','combined-hilbert-klinger'):
            with self.subTest(scope=scope), tempfile.TemporaryDirectory() as temp:
                plan = plan_for(scope);self.verify(plan)
                root=Path(temp);path=root/'plan.json';write(path,plan)
                with patch('hosted_timing.pipeline.PLAN',path),patch('hosted_timing.verify.PLAN',path), \
                        patch('test_hosted_timing.PLAN',path),patch('hosted_timing.targeted.verify_reference'):
                    test_hosted_timing.make_evidence(root,'targeted');result=collect(root,'targeted')
                self.assertEqual(SCOPES[scope]['totals'],(result['case_count'],result['decision_count']))
                self.assertTrue(result['accepted'])

    def test_missing_boolean_flag_or_consumer_rejected(self):
        for mode in ('flag','consumer'):
            plan=plan_for('rolling-dispersion')
            task=next(t for s in plan['targeted_shards'] for t in s['tasks'] if t['fixture']=='SharedDispersionBenchmarks')
            if mode=='flag':task.pop('variance_parameter')
            else:task['operations'].pop()
            with self.assertRaises(AssertionError):self.verify(plan)

    def test_boolean_keys_and_actual_filters(self):
        for value in ('True','False'):
            self.assertEqual((value,10000,'BuilderFull'),case_key(dict(
                FullName=f'Fixture.BuilderFull(Variance: {value}, Count: 10000)',Method='BuilderFull')))
            self.assertEqual([f'*SharedDispersionBenchmarks.BuilderFull*Variance: {value}*'],
                filters('SharedDispersionBenchmarks',value,['BuilderFull'],variance=True))
        with self.assertRaises(AssertionError):filters('Other','True',['BuilderFull'],variance=True)
