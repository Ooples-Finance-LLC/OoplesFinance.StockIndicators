"""Only the selected accepted consumers may enter the combined qualification."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO, read

PLAN=REPO/'benchmarks/plans/hosted-combined-local-certificates-20261103.json'
EXPECTED={'combined-local-directional':(30,6),'combined-local-directional-body':(180,36)}

class CombinedLocalTests(test_hosted_stoch_compact.StochCompactTests):
    def setUp(self):
        self.expected_totals=EXPECTED[read(PLAN)['targeted_reference']['scope']]
        replacement=patch('test_hosted_stoch_compact.PLAN',PLAN)
        replacement.start()
        self.addCleanup(replacement.stop)
