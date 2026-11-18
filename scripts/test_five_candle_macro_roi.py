"""Bounded orchestration checks; no production builds, timing or network calls."""
import itertools,os,re,subprocess,tempfile,unittest
from pathlib import Path
import yaml
ROOT=Path(__file__).resolve().parents[1]
WORKFLOW=ROOT/'.github/workflows/five-candle-macro-roi.yml'
DRIVER=ROOT/'scripts/screen-five-candle-pinned-runtime.cs'
PAIRS={'MatHold','Breakaway','ConcealingBabySwallow','LadderBottom','ThreeStarsInSouth','RisingFallingThreeMethods','AdvanceBlock','Stalled'}
class RoiScopeTests(unittest.TestCase):
    def setUp(self):self.text=WORKFLOW.read_text();self.driver=DRIVER.read_text();self.workflow=yaml.safe_load(self.text)
    def test_four_serial_jobs_eighty_complete_cases(self):
        job=self.workflow['jobs']['screen'];matrix=job['strategy']['matrix']
        self.assertEqual(job['strategy']['max-parallel'],1);self.assertEqual(job['timeout-minutes'],10)
        self.assertEqual(list(itertools.product(matrix['bars'],matrix['group'])),[(1000,'primary'),(1000,'shared'),(10000,'primary'),(10000,'shared')])
        self.assertEqual(set(re.findall(r'TaLib.Candles.([A-Za-z]+)',self.driver)),PAIRS)
        self.assertIn("@('old','new','new','old','native')",self.text)
        self.assertIn('overall_diagnostic_cases=80',self.text);self.assertIn('$cases.Count -ne 20 -or $compared -ne 160',self.text)
    def test_unbound_guard_precedes_all_input_output_actions(self):
        step=self.workflow['jobs']['screen']['steps'][0];self.assertIn('UNBOUND candidate/GPU pins',step['run'])
        self.assertLess(step['run'].index('UNBOUND actual CPU receipt counts'),step['run'].index('Test-Path'))
        self.assertIn("always() && env.SCREEN_ROOT != ''",self.workflow['jobs']['screen']['steps'][-1]['if'])
        self.assertLess(self.driver.index('UNBOUND actual CPU receipt counts'),self.driver.index('Path.GetFullPath(args[0])'))
        self.assertLess(self.driver.index('UNBOUND candidate/GPU pins'),self.driver.index('Directory.CreateDirectory(output)'))
        self.assertIn('CandidateUnitPassed > 0 && CandidateParityPassed > 0',self.driver)
        # Exercise the real preflight with explicit unbound fixtures. Actual
        # checked-in pins may be unbound or receipt-bound without changing tests.
        ready = dict(self.workflow['env'])
        for key in ('EXPECTED_NEW_CORE_SHA','EXPECTED_MANIFEST_SHA','EXPECTED_SOURCE_FINGERPRINT','EXPECTED_GPU_RECEIPT_SHA','EXPECTED_GPU_TRX_GZ_SHA','EXPECTED_CANDIDATE_ZIP_SHA'):
            ready[key] = 'a'*64
        for key in ('EXPECTED_CANDIDATE_SOURCE','EXPECTED_CANDIDATE_WORKFLOW_HEAD','GPU_RECEIPT_COMMIT'):ready[key] = 'b'*40
        for key in ('EXPECTED_CANDIDATE_ARTIFACT_ID','EXPECTED_CANDIDATE_RUN_ID','EXPECTED_UNIT_PASSED','EXPECTED_PARITY_PASSED'):ready[key] = '1'
        for unbound_key,message in (('EXPECTED_NEW_CORE_SHA','UNBOUND candidate/GPU pins'),('GPU_RECEIPT_COMMIT','UNBOUND GPU-only commit'),('EXPECTED_UNIT_PASSED','UNBOUND actual CPU receipt counts')):
            with self.subTest(unbound_key=unbound_key), tempfile.TemporaryDirectory(prefix='five-candle-preflight-') as temporary:
                root=Path(temporary);script=root/'preflight.ps1';script.write_text(step['run'])
                fixture=dict(ready);fixture[unbound_key]='UNBOUND'
                environment=dict(os.environ);environment.update(fixture)
                environment.update(STUDY_GROUP='primary',STUDY_BARS='1000',RUNNER_TEMP=str(root/'runner'),GITHUB_WORKSPACE=str(root/'workspace'),GITHUB_ENV=str(root/'github-env'))
                result=subprocess.run(['pwsh','-NoProfile','-NonInteractive','-File',str(script)],env=environment,capture_output=True,text=True,timeout=30)
                self.assertNotEqual(result.returncode,0);self.assertIn(message,result.stderr)
                self.assertEqual(set(root.iterdir()),{script},'Unbound preflight performed input/output I/O')
    def test_original_caps_and_all_samples_outputs_remain_required(self):
        for required in ['-ge 120','60000','104857600','$i -lt 12','$_ -ge 15','@($case.samples_ms).Count -ne 10','$case.warmups -ne 1000','@($case.source_outputs).Count -ne 10']:
            self.assertIn(required,self.text)
        self.assertIn('for (int i = 0; i < 1000; i++)',self.driver);self.assertIn('for (int i = 0; i < 10; i++)',self.driver)
        self.assertIn('source_outputs',self.driver);self.assertNotIn('RemoveAll(',self.driver)
    def test_actual_factory_defaults_and_properties_are_authenticated(self):
        for family in ['FiveCandleContinuationComparison','ExtendedReversalComparison','ExhaustionComparison']:self.assertIn(family,self.driver)
        for prop in ['LongPeriod','ShortPeriod','ShadowPeriod','NearPeriod','FarPeriod','Penetration','Period']:self.assertIn('"'+prop+'"',self.driver)
        self.assertIn('parameter.HasDefaultValue',self.driver);self.assertIn('create.Invoke(null, factoryArgs)',self.driver)
        self.assertIn('Equals(actual, expected)',self.driver);self.assertIn('type.GetMethod("Setup")!.Invoke(fixture, null)',self.driver)
        self.assertNotIn('Fixture("walk"',self.driver)
    def test_frozen_assets_and_binary_receipt_transport_preserved(self):
        for sha in ['4d571648599e567bb6df0cda1f823d79e95712241078c2ab1721cf7409c10173','48229494a1ff1e4e59bddd5a018cbd590cae24523d52dd2f3effdd20bd802723','9013cb468cec2d505c0eb2fb6050517d2a1a5e28b441ade2cd55523be298bf78']:self.assertIn(sha,self.driver)
        self.assertIn('common.Count == 94',self.driver);self.assertIn('results.Length == 100',self.driver)
        self.assertIn('BaseStream.CopyTo($blobFile)',self.text);self.assertIn('git cat-file blob stdout BaseStream to FileStream',self.text)
if __name__=='__main__':unittest.main()
