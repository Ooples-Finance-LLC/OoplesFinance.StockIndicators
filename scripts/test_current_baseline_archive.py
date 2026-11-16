"""Archive-only regression cases for report-only versus legacy harness refresh."""
import gzip
import json
from pathlib import Path
from types import SimpleNamespace
import tempfile
import unittest
from unittest.mock import patch

from hosted_timing import archive as a


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes((json.dumps(value, indent=2)+'\n').encode())


class CurrentBaselineArchiveTests(unittest.TestCase):
    def fixture(self, root, current):
        repo = root/'repo'; repo.mkdir()
        raw = root/'raw'; raw.mkdir(); (raw/'samples.json').write_bytes(b'raw four rounds\n')
        prepared = root/'prepared'; (prepared/'evidence').mkdir(parents=True)
        (prepared/'evidence/receipt.json').write_bytes(b'original GPU receipt\n')
        proof = dict(run_id='ten-run',run_attempt='1',workflow_head='tooling')
        write(prepared/'provenance.json',proof)
        plan = dict(current_baseline_ten_refresh=current)
        retained = {}
        if current:
            write(prepared/'origin-provenance.json',dict(run_id='original-run'))
            old_approval = repo/'old-approval.json'; write(old_approval,dict(approved=True))
            base_plan = repo/'old-plan.json'; write(base_plan,dict(refresh_approval=dict(path='old-approval.json',sha256=a.digest(old_approval))))
            approval = repo/'approval.json'; write(approval,dict(approved=True))
            preflight = prepared/'consumer-preflight'; preflight.mkdir()
            for name in ('receipt.json','authentication.json','canonical-comparators.trx.gz','frozen-canonical-alignment.json'):
                (preflight/name).write_bytes(('actual proof '+name).encode())
            plan.update(baseline_inputs=dict(plan='old-plan.json',plan_sha256=a.digest(base_plan),
                    provenance_sha256=a.digest(prepared/'origin-provenance.json')),
                refresh_approval=dict(path='approval.json',sha256=a.digest(approval)),
                consumer_preflight=dict(files={p.name:a.digest(p) for p in preflight.iterdir()}))
            retained = {'prepared/origin-provenance.json':prepared/'origin-provenance.json',
                'original-plan.json':base_plan,'scope-approval.json':approval,'original-scope-approval.json':old_approval}
            retained.update({'prepared/consumer-preflight/'+p.name:p for p in preflight.iterdir()})
        path = repo/'plan.json'; write(path,plan)
        summary = dict(baseline_refresh_verified=True,accepted=False,production_qualification=False,
            input_provenance_sha256=a.digest(prepared/'provenance.json'),sources=dict(old='canonical',new='canonical'),
            case_count=100 if current else 80,decision_count=20 if current else 16,verdicts={'inconclusive':20 if current else 16},
            native_review=dict(confirmed_native_wins={'1000':0,'10000':0}))
        refresh = SimpleNamespace(PUBLISHED='published',CANONICAL='canonical',collect=lambda _:summary)
        return repo, raw, prepared, proof, path, refresh, retained

    def test_current_empty_harness_patch_retains_origin_and_all_pinned_proofs(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo,raw,prepared,proof,plan,refresh,retained = self.fixture(Path(tmp),True)
            with patch.object(a,'PLAN',plan), patch.object(a,'REPO',repo), \
                    patch.object(a,'verify_inputs',return_value=proof), \
                    patch('hosted_timing.baseline_refresh_dispatch.implementation',return_value=refresh):
                with patch.object(a.subprocess,'check_output',side_effect=[b'',b'unapproved harness change']), self.assertRaises(AssertionError):
                    a.archive(raw,prepared,repo,'baseline-refresh')
                with patch.object(a.subprocess,'check_output',side_effect=[b'',b'']):
                    destination = a.archive(raw,prepared,repo,'baseline-refresh')
            decision = json.loads((destination/'decision.json').read_text())
            self.assertFalse(decision['accepted'] or decision['production_integrated'] or decision['report_imported'])
            self.assertIn('without new compilation or GPU execution',decision['note'])
            self.assertEqual(b'',gzip.decompress((destination/'harness-source.patch.gz').read_bytes()))
            for name, source in retained.items():
                with self.subTest(name=name):
                    self.assertEqual(source.read_bytes(),gzip.decompress((destination/(name+'.gz')).read_bytes()))
                    self.assertEqual(a.digest(source),decision['archives'][name]['sha256'])

    def test_legacy_still_requires_and_retains_nonempty_harness_patch(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo,raw,prepared,proof,plan,refresh,_ = self.fixture(Path(tmp),False)
            with patch.object(a,'PLAN',plan), patch.object(a,'REPO',repo), \
                    patch.object(a,'verify_inputs',return_value=proof), \
                    patch('hosted_timing.baseline_refresh_dispatch.implementation',return_value=refresh):
                with patch.object(a.subprocess,'check_output',side_effect=[b'',b'']), self.assertRaises(AssertionError):
                    a.archive(raw,prepared,repo,'baseline-refresh')
                with patch.object(a.subprocess,'check_output',side_effect=[b'',b'actual legacy harness diff']):
                    destination = a.archive(raw,prepared,repo,'baseline-refresh')
            self.assertEqual(b'actual legacy harness diff',gzip.decompress((destination/'harness-source.patch.gz').read_bytes()))
            self.assertNotIn('original-plan.json',json.loads((destination/'decision.json').read_text())['archives'])


if __name__ == '__main__':
    unittest.main()
