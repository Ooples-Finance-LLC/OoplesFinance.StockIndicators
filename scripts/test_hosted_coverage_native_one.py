"""Approved native exception preserves adaptive builders and all sample gates."""
import copy
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import test_hosted_timing
from hosted_timing.coverage import verify_reference
from hosted_timing.pipeline import REPO, operation_timeout_for, read
from hosted_timing.verify import check_log, collect

PLAN = REPO/'benchmarks/plans/hosted-builder-coverage-native-one-20261103.json'


class NativeOneCoverageTests(unittest.TestCase):
    def test_real_source_boundary(self):
        verify_reference(read(PLAN))

    def test_timeout_is_coverage_only_and_requires_exception(self):
        plan = read(PLAN)
        self.assertEqual(10800, operation_timeout_for(plan, 'coverage'))
        for phase in ('targeted', 'successor', 'variants'):
            self.assertEqual(3600, operation_timeout_for(plan, phase))
        del plan['coverage_harness']['native_invocation_count']
        with self.assertRaises(AssertionError): operation_timeout_for(plan, 'coverage')
        plan['measurement_policy'].pop('operation_timeout_seconds')
        self.assertEqual(3600, operation_timeout_for(plan, 'coverage'))

    def test_invalid_native_policy_rejected(self):
        for count, timeout in ((2,10800),(1,3600)):
            plan = copy.deepcopy(read(PLAN))
            plan['coverage_harness']['native_invocation_count'] = count
            plan['measurement_policy']['operation_timeout_seconds'] = timeout
            with self.assertRaises(AssertionError): verify_reference(plan)

    def test_native_samples_enforced_builders_adaptive(self):
        log = '// Benchmark: Fixture.NativeSingle: IterationCount=10 WarmupCount=10 InvocationCount=1\n' + ''.join(
            f'Workload{kind} {i}: 1 op, sample\n' for kind in ('Warmup','Actual') for i in range(1,11))
        check_log(log, 1, native_invocations=1)
        for bad in (log.replace('1 op,','4 op,'),log.replace(' InvocationCount=1',''),log.replace('WorkloadActual 10:','WorkloadActual 9:')):
            with self.assertRaises(AssertionError): check_log(bad, 1, native_invocations=1)
        check_log(log.replace('NativeSingle','BuilderFull').replace(' InvocationCount=1','').replace('1 op,','4 op,'),1,native_invocations=1)

    def test_full_fresh_evidence(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            with patch('hosted_timing.pipeline.PLAN',PLAN), patch('hosted_timing.verify.PLAN',PLAN), patch('test_hosted_timing.PLAN',PLAN):
                test_hosted_timing.make_evidence(root,'coverage')
                result = collect(root,'coverage')
            self.assertEqual((40,8),(result['case_count'],result['decision_count']))
            self.assertTrue(result['coverage_complete'])
            self.assertFalse(result['accepted'])
