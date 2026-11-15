import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

from hosted_timing import pipeline as p

spec=importlib.util.spec_from_file_location('aligned_cpu_profile',p.REPO/'scripts/profile-aligned-adosc-cpu.py')
cpu=importlib.util.module_from_spec(spec);spec.loader.exec_module(cpu)


class AlignedCpuProfileTests(unittest.TestCase):
    def test_perfview_zero_exit_with_error_log_is_not_success(self):
        with tempfile.TemporaryDirectory() as work:
            path=Path(work)/'log';path.write_text('[Error: failed]\n[DONE FAIL]\n')
            with self.assertRaises(AssertionError):cpu.successful_log(path)

    def test_release_hash_is_verified_before_tool_execution(self):
        with tempfile.TemporaryDirectory() as work:
            root=Path(work);out=root/'out';out.mkdir()
            def download(url,path):path.write_bytes(b'untrusted')
            with patch.object(p,'verify_inputs',return_value=dict(production_sha256=dict(successor='expected'))), \
                 patch.object(cpu.ctypes.windll.shell32,'IsUserAnAdmin',return_value=True), \
                 patch.object(cpu.urllib.request,'urlretrieve',side_effect=download),patch.object(cpu.subprocess,'run') as run:
                with self.assertRaisesRegex(AssertionError,'hash mismatch'):cpu.profile(root,out)
                run.assert_not_called()

    def test_non_elevated_collection_is_explicitly_unavailable_without_claims(self):
        with tempfile.TemporaryDirectory() as work:
            root=Path(work);out=root/'out'
            with patch.object(sys,'argv',['profile','--inputs',str(root),'--out',str(out)]), \
                 patch.object(p,'verify_inputs',return_value=dict(production_sha256=dict(successor='expected'))), \
                 patch.object(cpu.ctypes.windll.shell32,'IsUserAnAdmin',return_value=False):
                cpu.main()
            result=json.loads((out/'receipt.json').read_text())
            self.assertFalse(result['available'] or result['performance_claim'] or result['qualification'])
            self.assertTrue(result['diagnostic_only'])

    def test_optional_profile_runs_after_measurement_and_before_evidence_upload(self):
        text=(p.REPO/'.github/workflows/cpu-kernel-performance.yml').read_text()
        measure=text.index('      - name: Measure approved diagnostic cases')
        profile=text.index('      - name: Retain optional post-measurement kernel CPU diagnostic')
        upload=text.index('          name: adosc-roi-diagnostic',profile)
        self.assertLess(measure,profile);self.assertLess(profile,upload)
