"""Pure bounded-scope/identity/runtime fixtures; no builds or measurements."""
import copy
from pathlib import Path
import tempfile
import unittest
from unittest import mock
from hosted_timing import pipeline as p
from hosted_timing import candle_predicate_qualification as q


class PredicateQualificationTests(unittest.TestCase):
    def plan(self):
        old=p.read(p.REPO/q.OLD_PLAN)
        plan=copy.deepcopy(old)
        for key in ('matchinglow_grid_qualification','native_crossover_gate','baseline_continuation','preflight'):
            plan.pop(key,None)
        plan.update(candle_predicate_order_qualification=True,inputs_ready=False,
            sources=dict(published=q.OLD,old=q.OLD,successor=q.NEW,variants=q.VARIANTS),
            gpu_sources=dict(old=q.OLD_COMPILED,successor=q.NEW,variants=q.VARIANTS),
            cases=180,decisions=36,matrix=dict(shards=1,max_parallel=1,timeout_minutes_per_shard=180),
            targeted_shards=q.shards(),old_candidate=old['candidate'],
            cpu_test_classes=dict(old=q.previous.UNIT,successor=q.UNIT,variants=['OwnedHistoryBatchTests']),
            scope_approval=dict(path='fixture-only-approval.json',sha256='1'*64))
        plan['targeted_reference'].update(scope=q.SCOPE,decision=q.REFERENCE,run_id='43992359476',qualified_source=q.OLD_COMPILED)
        plan['candidate'].update(source=q.NEW,unit_classes=q.UNIT,parity_classes=q.PARITY,unit_passed=24,parity_passed=65,
            ci_run='44005551788',producer='2a1737f30e5e1164189d0209b39ea46bf38c1239',
            evidence_source='f63b014560a4978b7b47755fcd73f5d39c531e12',
            manifest_sha256='9a211abdc551bfa6f0b900ef17e5f305ea417d8546f9f3ba2ec8d9c643391156',
            gpu_receipt_sha256='4f0abd4a553fe0ab1985e1b8faa610d4c680a84084e199d5ae27600ad1366eb9',
            production_sha256='8bac548a15cf862067fffcaab85b0abbabadd2964948ceb0d160db29c3f85f52')
        return plan

    def validate(self,plan,ready=False):
        original_read,original_digest=p.read,p.digest
        approval=(p.REPO/'fixture-only-approval.json').resolve()
        with mock.patch.object(p,'read',side_effect=lambda path:dict(approved=True,scope=q.SCOPE,cases=180)
                if Path(path).resolve()==approval else original_read(path)),\
             mock.patch.object(p,'digest',side_effect=lambda path:'1'*64 if Path(path).resolve()==approval else original_digest(path)):
            q.validate_scope(plan,require_ready=ready)

    def test_exact_six_pair_tasks_and_standard_policy(self):
        plan=self.plan();self.validate(plan)
        self.assertEqual((180,36),(sum(s['cases'] for s in q.shards()),sum(s['decisions'] for s in q.shards())))
        penetration=q.shards()[0]['tasks'][2]
        self.assertEqual('SharedTaReversalContinuationBenchmarks',penetration['fixture'])
        self.assertEqual(['PiercingLine','DarkCloudCover'],[x['raw'] for x in penetration['operations']])
        self.assertEqual(q.METHODS,penetration['methods'])

    def test_qualified_classes_are_allowlisted_only_for_this_scope(self):
        plan=self.plan()
        for label,expected in [('old',q.previous.UNIT),('successor',q.UNIT),('variants',['OwnedHistoryBatchTests'])]:
            self.assertEqual(expected,p.cpu_test_classes(plan,label))
        for kind in ('extra-class','other-scope','no-scope'):
            bad=copy.deepcopy(plan)
            if kind=='extra-class':bad['cpu_test_classes']['successor'].append('Other.Full.Name')
            elif kind=='other-scope':bad['targeted_reference']['scope']='other'
            else:bad.pop('candle_predicate_order_qualification')
            with self.assertRaises(AssertionError):p.cpu_test_classes(bad,'successor')

    def test_ready_requires_actual_ci_gpu_identities(self):
        plan=self.plan()
        with self.assertRaises(AssertionError):self.validate(plan,ready=True)
        plan['inputs_ready']=True;self.validate(plan,ready=True)
        plan['candidate']['gpu_receipt_sha256']='pending'
        with self.assertRaises(AssertionError):self.validate(plan,ready=True)

    def test_old_compiled_core_cannot_be_replaced_by_original_frozen_baseline(self):
        for kind in ('gpu-source','core','class-count'):
            plan=self.plan()
            if kind=='gpu-source':plan['gpu_sources']['old']=q.previous.CANONICAL
            elif kind=='core':plan['old_candidate']['production_sha256']=q.previous.CANONICAL_DLL
            else:plan['candidate']['unit_passed']=20
            with self.assertRaises(AssertionError):self.validate(plan)

    def test_runtime_guard_checks_original_operation_reports_not_only_merged(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder)/'shard0';root.mkdir()
            p.write(root/'provenance.json',dict(raw_root=r'C:\raw'))
            p.write(root/'status.json',dict(rounds=[dict(report=r'C:\raw\merged.json',
                operation_checkpoints=[dict(report=r'C:\raw\original.json')])]))
            for name in ('merged.json','original.json'):
                p.write(root/name,dict(HostEnvironmentInfo=dict(RuntimeVersion='.NET 10.0.15 (10.0.15)')))
            q.verify_runtime(Path(folder),'.NET 10.0.15')
            p.write(root/'original.json',dict(HostEnvironmentInfo=dict(RuntimeVersion='.NET 10.0.14')))
            with self.assertRaises(AssertionError):q.verify_runtime(Path(folder),'.NET 10.0.15')

    def test_matchinglow_frozen_module_and_plan_remain_byte_identical(self):
        for name in ('scripts/hosted_timing/matchinglow_grid_qualification.py',q.OLD_PLAN):
            expected=p.subprocess.check_output(['git','show','fab5e0179feee36f3e0d9205dd0323d1b1e9c2f7:'+name],cwd=p.REPO)
            self.assertEqual(expected,(p.REPO/name).read_bytes().replace(b'\r\n',b'\n'))


if __name__=='__main__':unittest.main()
