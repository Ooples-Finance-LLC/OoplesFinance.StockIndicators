import copy
import hashlib
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('candidate_inputs',Path(__file__).with_name('build-candidate-correctness.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class CorrectnessInputIntegrityTests(unittest.TestCase):
    def setUp(self):
        content = b'compiled candidate'
        sha = hashlib.sha256(content).hexdigest()
        self.blobs = {'blobs/'+sha:content,'evidence/unit.trx':b'unit evidence'}
        self.manifest = dict(schema=1,status='cpu-correctness-complete',source='a'*40,
            source_sha256='b'*64,sdk=module.SDK,
            runtime_files={root+'/'+module.DLL:sha for root in module.ROOTS},
            evidence={'evidence/unit.trx':hashlib.sha256(b'unit evidence').hexdigest()})

    def verify(self):
        return module.validate_layout(self.manifest,self.blobs.__getitem__,'a'*40,'b'*64)

    def test_complete_bound_package(self):
        self.verify()

    def test_wrong_identity_or_incomplete_status_is_rejected(self):
        for field,value in [('source','c'*40),('source_sha256','c'*64),('sdk','wrong'),('status','building')]:
            with self.subTest(field=field):
                original = copy.deepcopy(self.manifest)
                self.manifest[field] = value
                with self.assertRaises(AssertionError): self.verify()
                self.manifest = original

    def test_changed_runtime_or_evidence_is_rejected(self):
        for name in self.blobs:
            original = self.blobs[name]
            self.blobs[name] = b'changed'
            with self.assertRaises(AssertionError): self.verify()
            self.blobs[name] = original

    def test_missing_or_mixed_production_copies_are_rejected(self):
        name = module.ROOTS[0]+'/'+module.DLL
        original = self.manifest['runtime_files'].pop(name)
        with self.assertRaises(KeyError): self.verify()
        self.manifest['runtime_files'][name] = 'c'*64
        with self.assertRaises(AssertionError): self.verify()
        self.manifest['runtime_files'][name] = original

    def test_path_escape_and_alternate_streams_are_rejected(self):
        for name in ('../outside.dll','/outside.dll',module.ROOTS[0]+'/../outside.dll',
                     module.ROOTS[0]+'/file.dll:stream',module.ROOTS[0]+'\\file.dll'):
            with self.subTest(name=name):
                self.manifest['runtime_files'][name] = next(iter(self.manifest['runtime_files'].values()))
                with self.assertRaises(AssertionError): self.verify()
                self.manifest['runtime_files'].pop(name)


if __name__ == '__main__': unittest.main()
