import hashlib
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO,read
from hosted_timing.reuse import origin

class AwesomeNativeFixtureRepairTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals=(30,6)
    target_shard_index=0
    def setUp(self):
        p=patch('test_hosted_stoch_compact.PLAN',REPO/'benchmarks/plans/hosted-awesome-native-fixture-repair-20261113.json')
        p.start();self.addCleanup(p.stop)
    def test_real_fixture_inventory_and_authenticated_reuse(self):
        p=read(REPO/'benchmarks/plans/hosted-awesome-native-fixture-repair-20261113.json');i=read(REPO/'benchmarks/plans/evidence/awesome-native-fixture-inventory-20261113.json')
        self.assertTrue(i['passed'])
        self.assertEqual('321827d7ad9b3e153c6b57c8cb83f64bde40792c65a9df731a4938c05b073cfc',i['productionSha256'])
        self.assertEqual('SharedSkenderFiniteWindowBenchmarks',p['targeted_shards'][0]['tasks'][1]['fixture'])
        self.assertEqual(set(['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']),set(i['correctedMethods']))
        self.assertEqual([1000, 10000],[x['count'] for x in i['checks']])
        source,proof=origin(p,REPO/'benchmarks/plans/evidence/awesome-preparation-43447185609.json')
        self.assertEqual(source['sources'],p['sources'])
        self.assertEqual('43447185609',proof['run_id'])
        self.assertEqual((14,19,100),tuple(p['source_correctness'][k] for k in ('unit','comparator','actual_local_gpu')))
        self.assertTrue(p['qualification_context']['partial_timings_excluded'])
