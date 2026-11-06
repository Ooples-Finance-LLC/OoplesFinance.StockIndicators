import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from hosted_timing.runtime_dependencies import tensor_closure, verify_alignment, verify_input_alignment


class RuntimeDependencyTests(unittest.TestCase):
    def make_runtime(self, root):
        root.mkdir()
        (root/'AiDotNet.Tensors.dll').write_bytes(b'tensor-148')
        (root/'Dependency.dll').write_bytes(b'child')
        (root/'deps.json').write_text(json.dumps(dict(runtimeTarget=dict(name='net10'), targets={'net10': {
            'AiDotNet.Tensors/0.148.0': dict(dependencies={'Dependency':'1.0'}, runtime={'lib/net10/AiDotNet.Tensors.dll': {}}),
            'Dependency/1.0': dict(runtime={'lib/net10/Dependency.dll': {}})}})))
        return tensor_closure(root, 'deps.json')

    def test_transitive_dependency_contents_are_bound(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            common=self.make_runtime(root/'common')
            tested=self.make_runtime(root/'tested')
            verify_alignment(common,tested,'0.148.0')
            (root/'tested/Dependency.dll').write_bytes(b'drift')
            with self.assertRaisesRegex(AssertionError,'differs'):
                verify_alignment(common,tensor_closure(root/'tested','deps.json'),'0.148.0')

    def test_wrong_version_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            closure=self.make_runtime(Path(directory)/'runtime')
            with self.assertRaises(AssertionError):verify_alignment(closure,closure,'0.147.0')

    def test_missing_dependency_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)/'runtime';self.make_runtime(root)
            (root/'Dependency.dll').unlink()
            with self.assertRaisesRegex(AssertionError,'Missing'):
                tensor_closure(root,'deps.json')

    def test_traversal_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)/'runtime';self.make_runtime(root)
            data=json.loads((root/'deps.json').read_text())
            data['targets']['net10']['Dependency/1.0']['runtime']={'../Dependency.dll':{}}
            (root/'deps.json').write_text(json.dumps(data))
            with self.assertRaises(AssertionError):tensor_closure(root,'deps.json')

    def test_all_tested_sources_must_be_bound(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);closure=self.make_runtime(root/'common')
            (root/'common/OoplesFinance.StockIndicators.CompetitorBenchmarks.deps.json').write_bytes((root/'common/deps.json').read_bytes())
            with self.assertRaises(AssertionError):
                verify_input_alignment(root,{'runtime_dependency_alignment':{'tensor_version':'0.148.0'}},
                    {'tested_runtime_dependencies':{'old':closure,'successor':closure}})

    def test_measurement_rejects_dependency_drift_before_launch(self):
        from hosted_timing import pipeline
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);closure=self.make_runtime(root/'common');(root/'evidence').mkdir()
            (root/'common/OoplesFinance.StockIndicators.CompetitorBenchmarks.deps.json').write_bytes((root/'common/deps.json').read_bytes())
            plan={'sources':{},'runtime_dependency_alignment':{'tensor_version':'0.147.0'}}
            proof={'plan_sha256':'plan','sources':{},'common_manifest':pipeline.manifest(root/'common'),
                'evidence_manifest':{},'tested_runtime_dependencies':dict.fromkeys(('old','successor','variants'),closure)}
            with patch.object(pipeline,'read',side_effect=[plan,proof]),patch.object(pipeline,'digest',return_value='plan'), \
                patch.object(pipeline,'command') as launch:
                with self.assertRaises(AssertionError):pipeline.measure(root,root/'output','targeted',0)
                launch.assert_not_called()
