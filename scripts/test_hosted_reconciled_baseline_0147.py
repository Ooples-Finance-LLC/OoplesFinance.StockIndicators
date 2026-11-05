import copy
import gzip
import hashlib
import json
import unittest
from unittest.mock import patch

from hosted_timing import reconciled_baseline as check


class ReconciledBaseline0147Tests(unittest.TestCase):
    baseline = 'ed2dfdfddb4ac64c136b690047c1e1104b2adb98'
    directory = 'benchmarks/results/remaining-indicators/dependency-reconciliation-20261105'
    publication = 'benchmarks/plans/evidence/pr266-ci-ed2dfdfdd-20261105.json'

    def setUp(self):
        self.raw_publication = (check.REPO / self.publication).read_bytes()
        evidence = self.directory + '/verification.json'
        self.reference = dict(qualified_source='a942c58ce4829e5e723ba664840f034fc083129d',
            dependency_reconciliation=dict(kind='master-tensors-0.146.0-to-0.147.0',
                timings_reused=False, evidence=evidence,
                sha256=hashlib.sha256(check.blob(self.baseline, evidence)).hexdigest(),
                publication_ci=dict(path=self.publication,
                    sha256=hashlib.sha256(self.raw_publication).hexdigest())))

    def test_real_0147_source_and_gpu_evidence_pass(self):
        check.verify_reconciliation(self.reference, self.baseline)

    def test_publication_failures_cannot_authorize_the_baseline(self):
        receipt = json.loads(self.raw_publication)
        changes = [lambda r: r.update(head='0' * 40),
            lambda r: r['checks'][0].update(status='in_progress'),
            lambda r: r['checks'][0].update(conclusion='failure'),
            lambda r: r['counts'].update(success=999),
            lambda r: r.update(suites=[]), lambda r: r.update(statuses=[]),
            lambda r: r['pr'].update(draft=False)]
        for mutate in changes:
            with self.subTest(mutate=mutate):
                changed = copy.deepcopy(receipt)
                mutate(changed)
                with self.assertRaises(AssertionError):
                    check.verify_publication(json.dumps(changed).encode(), self.baseline)

    def test_incomplete_or_wrong_ci_manifest_rejected(self):
        original = check.blob
        path = self.directory + '/ci-manifest.json.gz'
        manifest = json.loads(gzip.decompress(original(self.baseline, path)))
        for key, value in [('status', 'failed'), ('source', '0' * 40),
                           ('workflow_head', '0' * 40), ('run_id', '0'), ('checks', {})]:
            changed = dict(manifest, **{key: value})
            raw = gzip.compress(json.dumps(changed).encode())
            with self.subTest(key=key), patch.object(check, 'blob', side_effect=lambda source, name:
                    raw if name == path else original(source, name)):
                with self.assertRaises(AssertionError):
                    check.verify_reconciliation(self.reference, self.baseline)

    def test_publication_hash_and_path_are_enforced(self):
        proof = self.reference['dependency_reconciliation']['publication_ci']
        proof['sha256'] = '0' * 64
        with self.assertRaises(AssertionError):
            check.verify_reconciliation(self.reference, self.baseline)
        proof['path'] = '../outside-publication-receipt.json'
        with self.assertRaises(AssertionError):
            check.verify_reconciliation(self.reference, self.baseline)

    def test_old_version_cannot_be_substituted_for_the_new_proof(self):
        self.reference['dependency_reconciliation']['kind'] = 'master-tensors-0.144.0-to-0.146.0'
        with self.assertRaises(AssertionError):
            check.verify_reconciliation(self.reference, self.baseline)


if __name__ == '__main__':
    unittest.main()
