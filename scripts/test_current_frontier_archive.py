import gzip
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from hosted_timing import archive as a, current_frontier_refresh as q
import test_current_baseline_archive as legacy


class CurrentFrontierArchiveTests(unittest.TestCase):
    def test_current_frontier_retains_verified_metadata_and_honest_refresh_only_claims(self):
        fixture=legacy.CurrentBaselineArchiveTests('runTest')
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary)
            repo,raw,prepared,proof,plan_path,refresh,_=fixture.fixture(root,False)
            plan=a.read(plan_path);plan['current_frontier_refresh']=True;a.write(plan_path,plan)
            original_collect=refresh.collect
            summary=original_collect(None);summary.update(case_count=1540,decision_count=308)
            refresh.collect=lambda _:summary
            retained={'scope-evidence/frontier-scope.json':b'prospective scope\n',
                      'prepared/baseline-authentication/manifest.json':b'accepted manifest\n'}
            refresh.archive_authentication=lambda *_:retained
            with patch.object(a,'PLAN',plan_path),patch.object(a,'REPO',repo),patch.object(a,'verify_inputs',return_value=proof), \
                 patch('hosted_timing.baseline_refresh_dispatch.implementation',return_value=refresh):
                with patch.object(a.subprocess,'check_output',side_effect=[b'',b'changed harness']),self.assertRaises(AssertionError):
                    a.archive(raw,prepared,repo,'baseline-refresh')
                with patch.object(a.subprocess,'check_output',side_effect=[b'',b'']):
                    destination=a.archive(raw,prepared,repo,'baseline-refresh')
            decision=json.loads((destination/'decision.json').read_bytes())
            self.assertEqual('baseline-refresh-verified',decision['status'])
            self.assertFalse(decision['accepted']);self.assertFalse(decision['production_qualification'])
            self.assertEqual(0,decision['algorithm_credit']);self.assertEqual(0,decision['closure_credit'])
            self.assertEqual(247,decision['remaining_unmeasured_primary_pairs'])
            self.assertIn('Existing authenticated CPU134/parity86/GPU100',decision['note'])
            self.assertNotIn('fresh consumer validation',decision['note'])
            for name,value in retained.items():
                self.assertEqual(value,gzip.decompress((destination/(name+'.gz')).read_bytes()))

    def test_authentication_archive_excludes_runtime_and_binds_manifest(self):
        plan=q.p.read(q.p.REPO/'benchmarks/plans/hosted-current-frontier-refresh-20261119.json')
        with tempfile.TemporaryDirectory() as temporary,patch.object(q,'validate_scope'):
            prepared=Path(temporary);auth=prepared/'baseline-authentication';(auth/'evidence').mkdir(parents=True);(auth/'blobs').mkdir()
            manifest=b'{"synthetic":"archive metadata only"}\n'
            (auth/'manifest.json').write_bytes(manifest);plan['candidate']['manifest_sha256']=hashlib.sha256(manifest).hexdigest()
            (auth/'evidence/unit.trx').write_bytes(b'unit metadata')
            (auth/'blobs/ignored-runtime').write_bytes(b'not archived')
            result=q.archive_authentication(prepared,plan)
            self.assertEqual(manifest,result['prepared/baseline-authentication/manifest.json'])
            self.assertFalse(any('/blobs/' in name for name in result))
            self.assertIn('scope-evidence/current-frontier-scope-20261119.json',result)
            (auth/'manifest.json').write_bytes(manifest+b'changed')
            with self.assertRaises(AssertionError):q.archive_authentication(prepared,plan)
            (auth/'manifest.json').write_bytes(manifest)
            (auth/'evidence/runtime.dll').write_bytes(b'forbidden')
            with self.assertRaisesRegex(AssertionError,'No runtime'):q.archive_authentication(prepared,plan)


if __name__=='__main__':unittest.main()
