"""Require complete exact-envelope consumers and authenticated source boundaries."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class EnvelopeBandCertificateTests(test_hosted_stoch_compact.StochCompactTests):
    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-envelope-band-certificate-20261103.json')
        replacement.start()
        self.addCleanup(replacement.stop)
