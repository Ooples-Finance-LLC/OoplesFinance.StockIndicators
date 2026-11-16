import copy
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import test_hosted_timing
from hosted_timing import pipeline as p,pointwise_family_refresh as refresh,verify
from hosted_timing.arithmetic_warmup_policy import validate as legacy_warmup_validate,SHA

PLAN=p.REPO/'benchmarks/plans/hosted-pointwise-family-refresh-20261116.json'


class FamilyRefreshTests(unittest.TestCase):
    def test_complete_family_is_refresh_only_even_if_every_aa_comparison_is_faster(self):
        with tempfile.TemporaryDirectory() as tmp,patch.object(p,'PLAN',PLAN),patch.object(verify,'PLAN',PLAN),patch.object(test_hosted_timing,'PLAN',PLAN):
            root=Path(tmp);test_hosted_timing.make_evidence(root,'baseline-refresh',same_production=True)
            result=refresh.collect(root)
            self.assertEqual((190,38),(result['case_count'],result['decision_count']))
            self.assertEqual({'faster':38},result['verdicts'])
            self.assertFalse(result['accepted'] or result['production_qualification'] or result['production_optimization'] or result['report_imported'])
            self.assertTrue(result['baseline_refresh_verified'])
            self.assertEqual(38,len(result['native_review']['rows']))
            self.assertTrue(all(len(row['owned_rounds'])==4 for row in result['native_review']['rows']))
            with self.assertRaisesRegex(AssertionError,'cannot qualify'):
                verify.collect(root,'successor')
            (root/'shard18/provenance.json').unlink()
            with self.assertRaises(AssertionError):refresh.collect(root)

    def test_all_nineteen_pairs_have_both_sizes_and_preserved_warmup_context(self):
        plan=p.read(PLAN);refresh.validate_scope(plan)
        self.assertEqual(list(range(19)),list(p.phase_shards(plan,'baseline-refresh')))
        for shard,pair in enumerate(refresh.PAIRS):
            task=p.phase_tasks(plan,'baseline-refresh',shard)[0]
            self.assertEqual([dict(raw=pair,pair=pair)],task['operations'])
            count=20000 if pair in refresh.ARITHMETIC else 1000
            self.assertEqual(count,p.warmups_for(plan,'LibraryPairBenchmarks',task))
            context=p.warmup_context(plan,'LibraryPairBenchmarks',task)
            self.assertEqual(count,context['warmup_count'])
            self.assertEqual(SHA if count==20000 else None,context.get('arithmetic_warmup_policy_sha256'))
        with self.assertRaises(AssertionError):legacy_warmup_validate(plan)

    def test_scope_rejects_missing_pair_new_source_policy_or_runner_expansion(self):
        original=p.read(PLAN)
        for key in ('source','pair','warmup','iteration','workers','gpu','qualification'):
            plan=copy.deepcopy(original)
            if key=='source':plan['sources']['successor']='a'*40
            elif key=='pair':plan['refresh_shards'].pop()
            elif key=='warmup':plan['measurement_policy']['fixed_invocation_pair_warmups']['TaLib.Functions.Sin']=20000
            elif key=='iteration':plan['measurement_policy']['iterations']=9
            elif key=='workers':plan['matrix']['max_parallel']=3
            elif key=='gpu':plan['gpu_sources']['old']=refresh.PUBLISHED
            else:plan['production_qualification']=True
            with self.subTest(key=key),self.assertRaises(AssertionError):refresh.validate_scope(plan)

    def test_runtime_identity_covers_harness_as_well_as_production(self):
        plan=p.read(PLAN);manifests=dict(old={p.DLL:'canonical','benchmark.dll':'frozen'},new={p.DLL:'canonical','benchmark.dll':'frozen'})
        refresh.verify_runtime_identity(plan,manifests)
        for name in (p.DLL,'benchmark.dll'):
            broken=copy.deepcopy(manifests);broken['new'][name]='changed'
            with self.subTest(name=name),self.assertRaises(AssertionError):refresh.verify_runtime_identity(plan,broken)

    def test_failed_cpu_gate_cannot_be_a_complete_refresh(self):
        with tempfile.TemporaryDirectory() as tmp,patch.object(p,'PLAN',PLAN),patch.object(verify,'PLAN',PLAN),patch.object(test_hosted_timing,'PLAN',PLAN):
            root=Path(tmp);test_hosted_timing.make_evidence(root,'baseline-refresh',same_production=True)
            path=root/'shard0/status.json';state=p.read(path)
            state['rounds'][0]['operation_checkpoints'][0]['quiet_cpu_samples'][0]=15.01
            p.write(path,state)
            with self.assertRaises(AssertionError):refresh.collect(root)
