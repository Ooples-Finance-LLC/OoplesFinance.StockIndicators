"""Exercise scope, provenance and incomplete-evidence rejection for the diagnostic."""
import importlib.util,json,tempfile,unittest,fnmatch
from pathlib import Path
from unittest.mock import patch
file=Path(__file__).with_name('run-log10-profile-diagnostic.py')
s=importlib.util.spec_from_file_location('log10_profile',file);m=importlib.util.module_from_spec(s);s.loader.exec_module(m)

class DiagnosticTests(unittest.TestCase):
    def test_frozen_source_and_scope(self):
        d=m.validate_plan();self.assertEqual(42734662811,d['input_artifact']['run_id']);self.assertEqual({'old','successor','variants','published'},set(d['sources']));self.assertEqual({},d['runtime_policy_overrides'])
    def test_changed_plan_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            p=Path(folder)/'plan.json';p.write_text('{}')
            with patch.object(m,'PLAN',p):
                with self.assertRaises(AssertionError):m.validate_plan()
    def test_filters_select_only_exact_pair_size_and_methods(self):
        for profile in ('batch','builder'):
            fixture,pair,methods,param,_=m.spec(profile)
            for size in (1000,10000,100000):
                for op in (pair,'Ln','Log10X'):
                    for method in methods+['BuilderLatestOnly']:
                        for parameters in (f'{param}: {size}, Operation: "{op}"',f'Operation: "{op}", {param}: {size}'):
                            name=f'{fixture}.{method}({parameters})'
                            self.assertEqual(size==10000 and op==pair and method in methods,any(fnmatch.fnmatchcase(name,p) for p in m.filters(profile)),name)
    def test_existing_full_compressed_export_discovery(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);(root/'results').mkdir()
            with self.assertRaises(AssertionError):m.find_report(root)
            real=root/'results/Fixture-report-full-compressed.json';real.write_text('{}')
            (root/'results/Fixture-report.csv').write_text('csv')
            self.assertEqual(real,m.find_report(root))
            (root/'results/Other-report-full-compressed.json').write_text('{}')
            with self.assertRaises(AssertionError):m.find_report(root)
    def test_runtime_overrides_detected_case_insensitively(self):
        self.assertFalse(m.controls({'DOTNET_ROOT':'sdk','DOTNET_CLI_TELEMETRY_OPTOUT':'1'}))
        for key in ('DOTNET_TieredPGO','COMPlus_TieredCompilation','dotnet_tc_quickjit','DOTNET_JitDisasm'):
            self.assertIn(key,m.controls({key:'0'}))
    def test_complete_bdn_and_actual_iteration_sequence(self):
        for profile in ('batch','builder'):
            fixture,pair,methods,param,warmups=m.spec(profile)
            cases=[dict(FullName=f'{fixture}.{method}({param}: 10000, Operation: "{pair}")',Method=method,Statistics=dict(N=10,OriginalValues=[100.]*10,Mean=100.,ConfidenceInterval=dict(Lower=99.,Upper=101.)),Memory=dict(BytesAllocatedPerOperation=8)) for method in methods]
            log=''.join(f'// Benchmark: {fixture}.{method}: IterationCount=10 WarmupCount={warmups}\n'+''.join(f'WorkloadWarmup {i}: 1 op, x\n' for i in range(1,warmups+1))+''.join(f'WorkloadActual {i}: 1 op, x\n' for i in range(1,11)) for method in methods)
            self.assertEqual(len(methods),len(m.validate_cases(profile,[{'Benchmarks':cases}],log)))
            with self.assertRaises(AssertionError):m.validate_cases(profile,[{'Benchmarks':cases[:-1]}],log)
            with self.assertRaises(AssertionError):m.validate_cases(profile,[{'Benchmarks':cases}],log.replace('WorkloadActual 10:','WrongActual 10:'))
            cases[0]['FullName']=cases[0]['FullName'].replace('10000','100000')
            with self.assertRaises(AssertionError):m.validate_cases(profile,[{'Benchmarks':cases}],log)
    def test_sample_attribution_does_not_claim_cpu_share(self):
        with tempfile.TemporaryDirectory() as folder:
            p=Path(folder)/'sample.json';data=dict(shared=dict(frames=[{'name':'OoplesFinance.StockIndicators!Kernel'}]),profiles=[dict(type='evented',unit='milliseconds',startValue=0,events=[dict(type='O',at=0,frame=0),dict(type='C',at=5,frame=0)])]);p.write_text(json.dumps(data))
            result=m.summarize_speedscope(p);self.assertEqual(5,result['attributed_milliseconds']);self.assertIn('NOT CPU share',result['kind'])
            data['shared']['frames'][0]['name']='Unrelated';p.write_text(json.dumps(data))
            with self.assertRaises(AssertionError):m.summarize_speedscope(p)
if __name__=='__main__':unittest.main()
