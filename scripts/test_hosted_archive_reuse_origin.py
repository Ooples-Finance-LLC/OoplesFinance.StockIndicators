"""Reused preparation archives its verified tooling plan into a separate checkout."""
import gzip
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from hosted_timing import archive as module


class ArchiveReuseOriginTests(unittest.TestCase):
    def test_original_plan_is_read_from_tooling_and_retained_in_publication(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);tooling=root/'tooling';publishing=root/'publishing'
            old=tooling/'benchmarks/plans/origin.json';old.parent.mkdir(parents=True)
            original=b'{"source":"original preparation"}\n';old.write_bytes(original)
            plan=old.with_name('reused.json');plan.write_text(json.dumps({'reuse_inputs':{'plan':'benchmarks/plans/origin.json'}}))
            raw=root/'raw';raw.mkdir();(raw/'measurement.txt').write_text('complete')
            prepared=root/'inputs';(prepared/'evidence').mkdir(parents=True)
            (prepared/'provenance.json').write_bytes(b'{}')
            (prepared/'original-preparation.json').write_bytes(b'{}')
            summary=dict(input_provenance_sha256=module.digest(prepared/'provenance.json'),accepted=True,
                sources={'old':'old','new':'new'},case_count=270,decision_count=54,verdicts={'faster':54})
            with patch.object(module,'PLAN',plan),patch.object(module,'REPO',tooling), \
                 patch.object(module,'verify_inputs',return_value={'run_id':'12','run_attempt':'1','workflow_head':'tooling'}), \
                 patch.object(module,'collect',return_value=summary), \
                 patch.object(module.subprocess,'check_output',return_value=b'candidate patch'):
                destination=module.archive(raw,prepared,publishing,'targeted')
            self.assertFalse((publishing/'benchmarks/plans/origin.json').exists())
            self.assertEqual(original,gzip.decompress((destination/'original-plan.json.gz').read_bytes()))
