"""The fast gate must remain explicitly partial; exhaustive selection loses nothing."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from unit_test_shards import select_methods


class UnitProfileTests(unittest.TestCase):
    def test_focused_includes_hands_and_regressions_but_not_full_library_sweeps(self):
        selected = [
            'Tests.CalculationsTests.RsiTests.CalculateRelativeStrengthIndex_CustomLengthMatchesNaive',
            'Tests.ValidationTests.FormulaContractCoverageTests.MamaHasEightHandCalculatedInitialOutputs',
            'Tests.ValidationTests.SymmetricKernelNumericalTests.EverySymmetricTapPairCancelsOppositeInputs',
            'Tests.ValidationTests.WaveTrendNumericalTests.EqualLineAndSignalHaveNoDirectionalMargin',
        ]
        exhaustive = selected + [
            'Tests.CalculationsTests.RoundingDigitsTests.RoundingChangesOnlyThePublishedDigits',
            'Tests.CalculationsTests.IncludeCustomValuesTests.TurningCustomValuesOffChangesNoOutput',
            'Tests.CalculationsTests.IncludeOutputValuesTests.TurningOutputValuesOffChangesNoResult',
            'Tests.CalculationsTests.BuilderArmTests.EveryTypedSpecComputesItsBatchIndicator',
            'Tests.CalculationsTests.BuilderStreamingArmTests.EveryTypedSpecComputesItsStreamingIndicator',
            'Tests.CalculationsTests.EhlersTests.CalculateEhlersMotherOfAdaptiveMovingAverages_ReturnsProperValues',
            'Tests.ValidationTests.FormulaContractCoverageTests.EveryReferencedConfigurationPassesItsFormula00',
            'Tests.ValidationTests.WaveTrendNumericalTests.NumericalClassesAreEnrolled',
        ]
        self.assertEqual(selected, select_methods(exhaustive, 'focused'))
        self.assertEqual(exhaustive, select_methods(exhaustive, 'exhaustive'))

    def test_empty_or_unknown_profiles_fail(self):
        with self.assertRaises(ValueError):
            select_methods(['Other.Unselected.Method'], 'focused')
        with self.assertRaises(ValueError):
            select_methods(['Other.Unselected.Method'], 'typo')
