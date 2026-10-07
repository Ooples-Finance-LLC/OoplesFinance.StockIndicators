using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class PivotTrendComparisonTests
{
    private static Bar[] Bars(double[] highs) =>
        highs.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, -20, v, 1)).ToArray();

    [Fact]
    public void GoldensShowEndpointLinesTrendOffsetsAndFutureConfirmation()
    {
        var bars = Bars([1, 2, 5, 2, 1, 2, 9, 2, 1, 2, 3]);
        var result = PivotTrendSnapshot.Calculate(bars);
        Assert.Equal(5, result[2].HighPoint);
        Assert.Equal(9, result[6].HighPoint);
        Assert.Equal(
            new double?[] { 5, 6, 7, 8, 9 },
            result.Skip(2).Take(5).Select(r => r.HighLine)
        );
        Assert.Null(result[2].HighTrend);
        Assert.All(
            result.Skip(3).Take(4),
            r => Assert.Equal(PivotTrendDirection.HigherHigh, r.HighTrend)
        );
        Assert.All(
            PivotTrendSnapshot.Calculate(bars.Take(8).ToArray()),
            r => Assert.Null(r.HighLine)
        );
        Assert.All(result, r => Assert.Null(r.LowPoint));
    }

    [Fact]
    public void EqualPivotsResetAnchorAndExpiredPairsAreNotConnected()
    {
        var equal = Bars([1, 2, 5, 2, 1, 2, 5, 2, 1, 2, 9, 2, 1]);
        var rows = PivotTrendSnapshot.Calculate(equal);
        Assert.Null(rows[2].HighLine);
        Assert.Equal(5, rows[6].HighLine);
        Assert.Equal(9, rows[10].HighLine);
        Assert.All(
            PivotTrendSnapshot.Calculate(equal, maxTrendPeriods: 3),
            r => Assert.Null(r.HighLine)
        );
    }

    [Fact]
    public void InvalidHugeSpansAndWideInterpolationAreExplicit()
    {
        Assert.Throws<ArgumentNullException>(() => PivotTrendSnapshot.Calculate(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => PivotTrendSnapshot.Calculate([], 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PivotTrendSnapshot.Calculate([], 2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PivotTrendSnapshot.Calculate([], maxTrendPeriods: 2)
        );
        var bars = Bars([1, 2, 5, 2, 1, 2, 9, 2, 1]);
        Assert.All(
            PivotTrendSnapshot.Calculate(bars, int.MaxValue - 1, int.MaxValue, int.MaxValue),
            r => Assert.Null(r.HighPoint)
        );
        var max = double.MaxValue;
        var wide = Bars([-max, -max, -max / 2, -max, -max, -max, max, -max, -max]);
        var result = PivotTrendSnapshot.Calculate(wide);
        Assert.Equal(-max / 8, result[3].HighLine);
        Assert.Equal(max / 4, result[4].HighLine);
    }

    [Fact]
    public void NativeHighLowAndCloseRoutesAndRepeatedCallsMatchReferences()
    {
        var data = CompetitorData.Create(150);
        foreach (var close in new[] { false, true })
            ComparisonVerifier.Check(PivotTrendComparison.Pair(2, 3, 20, close), data, 20);
        Assert.Equal(
            PivotTrendSnapshot.Calculate(data.IndicatorBars),
            PivotTrendSnapshot.Calculate(data.IndicatorBars)
        );
    }

    [Fact]
    public void AllSixOutputsAndPresenceFlagsDetectCorruption()
    {
        var data = CompetitorData.Create(250);
        var expected = PivotTrendComparison.Owned(data.IndicatorBars, 2, 2, 100, false);
        foreach (var name in PivotTrendComparison.Names)
        foreach (var missing in new[] { false, true })
        {
            var changed = PivotTrendComparison.Owned(data.IndicatorBars, 2, 2, 100, false);
            var index = Array.FindIndex(changed.Outputs[name].Present!, v => v);
            Assert.True(index >= 0, name);
            if (missing)
                changed.Outputs[name].Present![index] = false;
            else
                changed.Outputs[name].Values[index] += 1;
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Compare(expected, changed, name, IndicatorErrorBudget.Exact)
            );
        }
    }
}
