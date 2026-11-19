"""Exercise partial/zero admission against complete report-shaped fixtures."""
import copy
import hashlib
import json
from pathlib import Path
import re
import tempfile
import types
import unittest
from unittest.mock import patch

import test_hosted_timing
from hosted_timing import pipeline as p, current_baseline_ten_refresh as refresh
from hosted_timing.report import stage


class CurrentBaselineReportAdmissionTests(unittest.TestCase):
    def check_admission(self, admitted_count, wrong_report_hash=False, *, refresh_module=None,
                        refresh_flag='current_baseline_ten_refresh', plan_extra=None):
        refresh = refresh_module or globals()['refresh']
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            raw = root/'raw'; raw.mkdir()
            pairs = refresh.PAIRS
            admitted = set(pairs[:admitted_count])
            extras = {f'unchanged-{i:03}' for i in range(401-len(pairs))}
            dataset = dict(evidence=dict(sourceSha='old'), rows=[
                dict(pair=pair, bars=bars, source_sha='old', measurement_run_url='old-url',
                     competitor_over_ooples=1, raw={})
                for pair in sorted(set(pairs) | extras) for bars in (1000, 10000)])
            stats = dict(mean_ns=100, lower_ns=99, upper_ns=101, samples=10, allocated_bytes=8)
            comparisons = [dict(pair=f'builder-{i:03}', bars=bars, mode=mode,
                ooples=copy.deepcopy(stats), native=dict(NativeSingle=copy.deepcopy(stats),
                    NativeParallel=copy.deepcopy(stats)), verdict='inconclusive')
                for i in range(386) for bars in (1000, 10000)
                for mode in ('BuilderFull', 'BuilderLatestOnly')]
            builder = dict(full_comparisons=1544,
                verdicts=dict(faster=0, slower=0, inconclusive=1544),
                receipts=[dict(run_id='old-run', source_sha='old', comparisons=comparisons)],
                status=dict(completed=[]))
            status = dict(published_head='old', measurement_totals=dict(faster=0, slower=0,
                inconclusive=1544, expected=1604, pending=60), builder_measurement_sources=[])
            document = ''.join(f'<script id="{key}" type="application/json">{json.dumps(value)}</script>'
                for key, value in [('dataset', dataset), ('current-competitor-data', builder),
                                  ('rollout-status-data', status)])
            document += '<section id="explorer"><p class="sub">old</p></section>'
            document += '<section id="rollout-status"><p class="sub">Rollout published <code>old</code>. old</p></section>'
            document += '<table id="builder-current-results"><tbody></tbody></table>'
            document += """<script>
$('source').innerHTML='old';
const keys=Object.keys(D.rows[0]).filter(k=>k!=='raw');
const detail=`${esc(r.raw.Ooples.host.ProcessorName)}`;
</script>"""
            live = root/'live.html'; live.write_bytes(document.encode())
            plan = p.read(p.REPO/globals()['refresh'].BASE_PLAN)
            plan.pop('named_output_projection_refresh')
            plan.update({refresh_flag:True}, cases=10*len(pairs), decisions=2*len(pairs),
                sources=dict(published=refresh.PUBLISHED, old=refresh.CANONICAL,
                    successor=refresh.CANONICAL, variants=refresh.HARNESS),
                expected_runtime_version='test runtime',
                report_before_sha256='wrong' if wrong_report_hash else hashlib.sha256(live.read_bytes()).hexdigest())
            plan.update(plan_extra or {})
            plan['refresh_shards'] = plan['targeted_shards'] = [dict(index=i, cases=10, decisions=2,
                tasks=[dict(fixture='LibraryPairBenchmarks', operations=[dict(raw=pair, pair=pair)],
                    methods=['Ooples', 'Competitor'], implicit_pair=False, cases=10, decisions=2)])
                for i, pair in enumerate(pairs)]
            plan_file = root/'plan.json'; p.write(plan_file, plan)
            for module in ('hosted_timing.pipeline', 'hosted_timing.verify',
                           'hosted_timing.report', 'test_hosted_timing'):
                self.enterContext(patch(module+'.PLAN', plan_file))
            # Scope authentication is tested by the leaf module's own tests.
            # Here real raw collectors/admission run on report-sized synthetic data.
            self.enterContext(patch.object(refresh, 'validate_scope'))
            for i, pair in enumerate(pairs):
                with patch.object(test_hosted_timing, 'phase_shards', return_value=[i]):
                    test_hosted_timing.make_evidence(raw, 'baseline-refresh',
                        new_mean=100 if pair in admitted else 150, same_production=True)
            prepared = root/'prepared'; prepared.mkdir()
            (prepared/'provenance.json').write_bytes((raw/'input.json').read_bytes())
            self.enterContext(patch.object(p, 'verify_inputs', return_value=p.read(prepared/'provenance.json')))
            repo = root/'repo'; p.write(repo/'benchmarks/results/remaining-indicators/inventory.json', {})
            primary = types.ModuleType('primary_baseline_report')
            primary.replace_primary = lambda text, data: text
            with patch('hosted_timing.report.subprocess.check_output', return_value='head'), \
                 patch('hosted_timing.report.subprocess.run'), \
                 patch.dict('sys.modules', {'primary_baseline_report': primary}):
                if not admitted_count or wrong_report_hash:
                    expected = 'No A/A-admitted pairs' if not admitted_count else 'Frozen report changed'
                    with self.assertRaisesRegex(AssertionError, expected):
                        stage(live, raw, repo, root/'staged', phase='baseline-refresh', inputs=prepared)
                    self.assertFalse((root/'staged').exists())
                    self.assertEqual(live.read_bytes(), document.encode())
                    return
                candidate = stage(live, raw, repo, root/'staged', phase='baseline-refresh', inputs=prepared)
            rendered = candidate.read_text()
            def embedded(key):
                return json.loads(re.search(r'<script[^>]*id="'+key+r'"[^>]*>(.*?)</script>', rendered, re.S)[1])
            after = embedded('dataset')
            self.assertEqual(len(after['rows']), 802)
            changed = [(before, now) for before, now in zip(dataset['rows'], after['rows'], strict=True)
                       if before != now]
            self.assertEqual(len(changed), 2*admitted_count)
            self.assertEqual({row['pair'] for _, row in changed}, admitted)
            self.assertEqual([row for row in after['rows'] if row['pair'] not in admitted],
                             [row for row in dataset['rows'] if row['pair'] not in admitted])
            self.assertEqual(embedded('current-competitor-data'), builder)
            evidence = after['evidence']['harnessBaselineRefreshes'][-1]
            self.assertEqual(set(evidence['admitted_pairs']), admitted)
            self.assertEqual({row['pair'] for row in evidence['quarantined_pairs']}, set(pairs)-admitted)
            self.assertFalse(evidence['harness_changed'])
            self.assertTrue(evidence['admission_is_not_equivalence'])
            self.assertIn('does not establish a current-source gap count for all 401 pairs', rendered)
            self.assertEqual(live.read_bytes(), document.encode())
            staged = p.read(root/'staged/staged.json')
            self.assertEqual(staged['changed_baseline_rows'], 2*admitted_count)
            self.assertEqual(staged['changed_builder_slots'], 0)
            return after

    def test_partial_admission_preserves_802_rows_and_all_1544_builders(self):
        self.check_admission(1)

    def test_full_admission_updates_exactly_twenty_rows(self):
        self.check_admission(10)

    def test_zero_admission_never_stages_a_report(self):
        self.check_admission(0)

    def test_changed_frozen_report_is_rejected_before_staging(self):
        self.check_admission(1, wrong_report_hash=True)


if __name__ == '__main__':
    unittest.main()
