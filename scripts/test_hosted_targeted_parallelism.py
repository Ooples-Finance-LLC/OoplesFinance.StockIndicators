"""Transport the frozen targeted concurrency cap into the GitHub strategy."""
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from hosted_timing import pipeline


class TargetedParallelismTests(unittest.TestCase):
    def test_preparation_exports_the_actual_plan_limit_and_workflow_uses_it(self):
        plan = json.loads((pipeline.REPO/'benchmarks/plans/hosted-combined-directional-accumulation-20261104.json').read_text())
        for limit in (3, 6):
            plan['matrix']['max_parallel'] = limit
            with tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                path = root/'plan.json'; path.write_text(json.dumps(plan))
                output = root/'github-output'
                with patch.object(pipeline, 'PLAN', path), patch.object(pipeline, 'prepare') as prepare, \
                     patch.dict(os.environ, {'GITHUB_OUTPUT':str(output)}), \
                     patch('sys.argv', ['run-hosted-timing.py','prepare','--out',str(root/'inputs')]):
                    pipeline.main()
                    prepare.assert_called_once()
                lines = dict(line.split('=',1) for line in output.read_text().splitlines())
                self.assertEqual(str(limit), lines['targeted_max_parallel'])
                self.assertEqual(list(range(9)), json.loads(lines['targeted_matrix']))
        workflow = (pipeline.REPO/'.github/workflows/cpu-kernel-performance.yml').read_text()
        self.assertIn('targeted_max_parallel: ${{ steps.prepare.outputs.targeted_max_parallel }}', workflow)
        targeted = workflow.split('  hosted-targeted:',1)[1].split('\n  hosted-collect-targeted:',1)[0]
        self.assertIn('max-parallel: ${{ fromJSON(needs.hosted-prepare.outputs.targeted_max_parallel) }}', targeted)

    def test_invalid_limits_fail_instead_of_silently_using_unbounded_parallelism(self):
        for limit in (0, -1, 257, '6', True):
            with self.assertRaises(AssertionError):
                pipeline.targeted_max_parallel({'matrix':{'max_parallel':limit}})
        self.assertEqual(2, pipeline.targeted_max_parallel({}))
