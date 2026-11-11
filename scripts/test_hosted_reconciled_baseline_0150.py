"""Real 0.150 source/GPU evidence with explicit synthetic publication fixtures."""
import copy
import gzip
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from hosted_timing import reconciled_baseline as check


class ReconciledBaseline0150Tests(unittest.TestCase):
    baseline='54785ad2035d2fe3d4a41dfa49334887362459ee'
    directory='benchmarks/results/remaining-indicators/runtime-0150-alignment-20261111'

    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix='runtime0150-fixture-',dir=check.REPO)
        self.addCleanup(self.temp.cleanup)
        self.path=Path(self.temp.name)/'publication.json'
        self.publication=dict(head=self.baseline,counts=dict(success=70,skipped=0),
            checks=[dict(name='synthetic-publication-fixture-'+str(i),status='completed',conclusion='success') for i in range(70)],
            suites=[dict(app=app,status='completed',conclusion='success') for app in ('github-actions','sonarqubecloud')],
            statuses=[dict(context='CodeRabbit',state='success')],
            pr=dict(number=266,draft=True,auto_merge=None,base='master'))
        self.write_publication()
        evidence=self.directory+'/verification.json'
        self.reference=dict(qualified_source='73118b5eb5439ad113f8f4afa99a932a944887e4',
            dependency_reconciliation=dict(kind='master-tensors-0.149.0-to-0.150.0-and-net461-json11',timings_reused=False,
                evidence=evidence,sha256=hashlib.sha256(check.blob(self.baseline,evidence)).hexdigest(),
                publication_ci=dict(path=self.path.relative_to(check.REPO).as_posix(),
                    sha256=hashlib.sha256(self.path.read_bytes()).hexdigest())))

    def write_publication(self):
        self.path.write_text(json.dumps(self.publication))
        if hasattr(self,'reference'):
            self.reference['dependency_reconciliation']['publication_ci']['sha256']=hashlib.sha256(self.path.read_bytes()).hexdigest()

    def test_real_0150_source_gpu_and_manifest_with_synthetic_publication_pass(self):
        check.verify_reconciliation(self.reference,self.baseline)

    def test_old_transition_and_reused_timings_rejected(self):
        self.reference['dependency_reconciliation']['kind']='master-tensors-0.147.0-to-0.148.0'
        with self.assertRaises(AssertionError): check.verify_reconciliation(self.reference,self.baseline)
        self.reference['dependency_reconciliation']['kind']='master-tensors-0.149.0-to-0.150.0-and-net461-json11'
        self.reference['dependency_reconciliation']['timings_reused']=True
        with self.assertRaises(AssertionError): check.verify_reconciliation(self.reference,self.baseline)

    def test_incomplete_correctness_and_gpu_rejected(self):
        original=check.blob; path=self.directory+'/verification.json'
        evidence=json.loads(original(self.baseline,path))
        for field in ('unit','parity','gpu_passed'):
            changed=copy.deepcopy(evidence)
            if field=='gpu_passed': changed[field]=99
            else: changed[field]['passed']-=1
            raw=json.dumps(changed).encode()
            self.reference['dependency_reconciliation']['sha256']=hashlib.sha256(raw).hexdigest()
            with self.subTest(field=field),patch.object(check,'blob',side_effect=lambda source,name: raw if name==path else original(source,name)):
                with self.assertRaises(AssertionError): check.verify_reconciliation(self.reference,self.baseline)

    def test_changed_project_and_tampered_evidence_rejected(self):
        original=check.blob
        with patch.object(check,'blob',side_effect=lambda source,name:
                original(source,name)+b'<!-- extra -->' if source==self.baseline and name==check.PROJECT else original(source,name)):
            with self.assertRaises(AssertionError): check.verify_reconciliation(self.reference,self.baseline)
        self.reference['dependency_reconciliation']['sha256']='0'*64
        with self.assertRaises(AssertionError): check.verify_reconciliation(self.reference,self.baseline)

    def test_wrong_manifest_origin_or_checks_rejected(self):
        original=check.blob; path=self.directory+'/ci-manifest.json.gz'
        manifest=json.loads(gzip.decompress(original(self.baseline,path)))
        for key,value in [('status','failed'),('source','0'*40),('workflow_head','0'*40),('run_id','0'),('checks',{})]:
            raw=gzip.compress(json.dumps(dict(manifest,**{key:value})).encode())
            with self.subTest(key=key),patch.object(check,'blob',side_effect=lambda source,name: raw if name==path else original(source,name)):
                with self.assertRaises(AssertionError): check.verify_reconciliation(self.reference,self.baseline)

    def test_missing_json_update_rejected(self):
        original=check.blob
        with patch.object(check,'blob',side_effect=lambda source,name:
                original(source,name).replace(b'Include="System.Text.Json" Version="11.0.0"',
                    b'Include="System.Text.Json" Version="10.0.14"')
                if source==self.baseline and name==check.PROJECT else original(source,name)):
            with self.assertRaises(AssertionError): check.verify_reconciliation(self.reference,self.baseline)

    def test_stale_legacy_source_hash_or_build_rejected(self):
        original=check.blob; path=self.directory+'/legacy/verification.json'
        evidence=json.loads(original(self.baseline,path))
        for key,value in [('source','0'*40),('project_sha256','0'*64),
                          ('project_bytes_sha256','0'*64),('log_sha256','0'*64),
                          ('result','failed'),('run_id','0'),('workflow_head','0'*40)]:
            raw=json.dumps(dict(evidence,**{key:value})).encode()
            with self.subTest(key=key),patch.object(check,'blob',side_effect=lambda source,name:
                    raw if name==path else original(source,name)):
                with self.assertRaises(AssertionError): check.verify_reconciliation(self.reference,self.baseline)

    def test_publication_failure_hash_and_escape_rejected(self):
        self.publication['checks'][0]['conclusion']='failure'; self.write_publication()
        with self.assertRaises(AssertionError): check.verify_reconciliation(self.reference,self.baseline)
        self.reference['dependency_reconciliation']['publication_ci']['sha256']='0'*64
        with self.assertRaises(AssertionError): check.verify_reconciliation(self.reference,self.baseline)
        self.reference['dependency_reconciliation']['publication_ci']['path']='../outside.json'
        with self.assertRaises(AssertionError): check.verify_reconciliation(self.reference,self.baseline)


if __name__=='__main__': unittest.main()
