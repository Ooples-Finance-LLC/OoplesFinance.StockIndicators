using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class DynamicMomentumComparisonTests
{
    [Fact]
    public void CompleteReferencesAndNativeRoutesAgree()
    {
        var data = CompetitorData.Create(120);
        ComparisonVerifier.Check(DynamicMomentumComparison.Pair(), data, 20);
        var inputs = data.Candles.Select(c => (decimal?)c.Close).ToArray();
        var tuple = new Trady.Analysis.Indicator.DynamicMomentumIndexByTuple(
            inputs,
            5,
            10,
            14,
            30,
            5
        ).Compute();
        Assert.Equal(DynamicMomentumComparison.NativeReference(inputs, 5, 10, 14, 30, 5), tuple);
        var generic = new Trady.Analysis.Indicator.DynamicMomentumIndex<int, decimal?>(
            Enumerable.Range(0, inputs.Length),
            i => inputs[i],
            5,
            10,
            14,
            30,
            5
        ).Compute();
        Assert.Equal(tuple, generic);
    }

    [Fact]
    public void NullableGapsWideAndTinyValuesHaveIndependentReferences()
    {
        foreach (var scale in new[] { 1d, 1e-200, double.Epsilon, double.MaxValue / 8 })
        {
            var inputs = Enumerable
                .Range(0, 70)
                .Select(i => i == 3 || i == 48 ? null : (double?)((i % 7 - 3) * scale))
                .ToArray();
            Assert.Equal(
                DynamicMomentumComparison.Reference(inputs, 3, 5, 4, 8, 1),
                DynamicMomentumSnapshot.FromValues(inputs, 3, 5, 4, 8, 1)
            );
        }
        var empty = new double?[] { null, null, null, null };
        Assert.All(DynamicMomentumSnapshot.FromValues(empty, 2, 2, 1, 2, 1), x => Assert.Null(x));
        Assert.All(
            DynamicMomentumSnapshot.FromValues(
                Enumerable.Repeat((double?)5, 20).ToArray(),
                2,
                2,
                2,
                5,
                1
            ),
            x => Assert.Null(x)
        );
    }

    [Fact]
    public void ClampedSinglePeriodEqualsOriginalHistoryRsi()
    {
        var values = CompetitorData.Create(80).Closes.Select(x => (double?)x).ToArray();
        var expected = NullableStrengthOscillator.FromValues(values, 5).ToArray();
        var actual = DynamicMomentumSnapshot.FromValues(values, 2, 2, int.MaxValue, 5, 5);
        Assert.Equal(expected.Skip(5), actual.Skip(5));
        Assert.All(
            DynamicMomentumSnapshot.FromValues(values, int.MaxValue, int.MaxValue),
            x => Assert.Null(x)
        );
    }

    [Fact]
    public void InvalidParametersAndNonfiniteInputAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => DynamicMomentumSnapshot.FromValues(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => DynamicMomentumSnapshot.FromValues([], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DynamicMomentumSnapshot.FromValues([], lowerLimit: 31)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DynamicMomentumSnapshot.FromValues([double.NaN])
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OutputAndPresenceMutationsAreDetected(bool mask)
    {
        var values = CompetitorData.Create(80).Closes.Select(x => (double?)x).ToArray();
        var expected = VolumePriceComparison.Mask(
            DynamicMomentumSnapshot.FromValues(values).ToArray()
        );
        var actual = VolumePriceComparison.Mask(
            DynamicMomentumSnapshot.FromValues(values).ToArray()
        );
        var output = actual.Outputs.Values.Single();
        Assert.True(output.Present![^1]);
        if (mask)
            output.Present[^1] = false;
        else
            output.Values[^1] += 1;
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Compare(expected, actual, "DMI mutation", IndicatorErrorBudget.Exact)
        );
    }
}
