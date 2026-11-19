"""Actual workflow CLI/parser and downstream dispatch checks; no indicator workloads."""
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
from hosted_timing import pipeline as p,current_frontier_refresh as q
import test_current_frontier_preparation as preparation_tests

class CurrentFrontierCliTests(unittest.TestCase):
    def test_actual_entrypoint_help_for_measure_and_collect(self):
        for mode in ('measure','collect'):
            result=subprocess.run([sys.executable,str(p.REPO/'scripts/run-hosted-timing.py'),mode,
                '--out','unused','--phase','baseline-refresh','--help'],cwd=p.REPO,text=True,capture_output=True)
            self.assertEqual(0,result.returncode,result.stderr)
            self.assertIn('baseline-refresh',result.stdout)

    def test_actual_cli_measure_and_collect_dispatch_before_workloads(self):
        from hosted_timing import verify
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);inputs=root/'inputs';inputs.mkdir()
            (inputs/'provenance.json').write_bytes(b'UNIT CLI dispatch provenance');out=root/'output.json'
            args=['run-hosted-timing.py','measure','--inputs',str(inputs),'--out',str(out),'--phase','baseline-refresh','--shard','0']
            with patch.object(sys,'argv',args),patch.object(p,'measure') as measure:p.main()
            measure.assert_called_once_with(inputs.resolve(),out.resolve(),'baseline-refresh',0,None)
            args=['run-hosted-timing.py','collect','--inputs',str(root/'raw'),'--prepared',str(inputs),'--out',str(out),'--phase','baseline-refresh']
            summary=dict(case_count=1540,decision_count=308,accepted=False,verdicts={},input_provenance_sha256=p.digest(inputs/'provenance.json'))
            # Keep real verify.collect -> frontier.collect dispatch, stop at its workload boundary.
            with patch.object(sys,'argv',args),patch.object(p,'verify_inputs') as inputs_check,patch.object(verify,'PLAN',preparation_tests.PLAN),patch.object(q,'collect',return_value=summary) as collector:p.main()
            inputs_check.assert_called_once_with(inputs.resolve());collector.assert_called_once_with((root/'raw').resolve())
            self.assertEqual(summary,p.read(out))

    def test_real_measure_setup_runtime_dispatch_reaches_checkpoint_boundary(self):
        fixture=preparation_tests.CurrentFrontierPreparationTests()
        with fixture.fixture() as (root,candidate,plan):
            inputs=root/'prepared';q.prepare(candidate,inputs);records=[]
            def checkpoint(**kwargs):
                records.append(kwargs);self.assertEqual('baseline-refresh',kwargs['context']['phase']);self.assertEqual(1000,kwargs['warmups'])
                return dict(cases=2*len(kwargs['pairs'])*len(kwargs['methods']))
            with patch.object(p,'run_checkpointed',side_effect=checkpoint):p.measure(inputs,root/'measurement'/'shard0','baseline-refresh',0)
            self.assertEqual(4,len(records))
            self.assertEqual([['Ooples'],['Ooples'],['Ooples','Competitor'],['Ooples']],[r['methods'] for r in records])
            state=p.read(root/'measurement'/'shard0'/'status.json');self.assertEqual('raw-measurements-complete',state['status'])
            self.assertEqual(130,sum(r['cases'] for r in state['rounds']))

if __name__=='__main__':unittest.main()
