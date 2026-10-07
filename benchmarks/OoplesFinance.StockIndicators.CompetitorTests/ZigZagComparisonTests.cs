using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ZigZagComparisonTests
{
    private static CompetitorData Fixture() =>
        CompetitorData.FromCloses([100, 120, 100, 130, 90, 140, 80, 150, 70, 160, 60]);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothPriceModesMatchEveryNativeOutput(bool highLow) =>
        ComparisonVerifier.Check(ZigZagComparison.Pair(5, highLow), Fixture(), 20);

    [Fact]
    public void KnownPivotsRetractionsAndFinalEndpointAreExplicit()
    {
        var result = ZigZagSnapshot.Calculate(Fixture().IndicatorBars);
        Assert.Null(result[0].ZigZag);
        Assert.Equal(120, result[1].ZigZag);
        Assert.Equal(ZigZagPointKind.High, result[1].PointType);
        Assert.Equal(100, result[2].ZigZag);
        Assert.Equal(ZigZagPointKind.Low, result[2].PointType);
        Assert.Equal(125, result[2].RetraceHigh);
        Assert.Equal(95, result[3].RetraceLow);
        Assert.Equal(60, result[^1].ZigZag);
        Assert.Null(result[^1].PointType);
    }

    [Fact]
    public void WideAndTinyValuesRetainExactPivotsAndFiniteLines()
    {
        foreach (var scale in new[] { double.Epsilon, 1e-200, double.MaxValue / 200 })
        {
            var bars = Fixture()
                .Closes.Select(
                    (v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), 0, 0, 0, v * scale, 0)
                )
                .ToArray();
            var result = ZigZagSnapshot.Calculate(bars);
            Assert.Equal(ZigZagPointKind.High, result[1].PointType);
            Assert.All(result.Skip(1), r => Assert.True(double.IsFinite(r.ZigZag!.Value)));
        }
        Assert.Empty(ZigZagSnapshot.Calculate([]));
        Assert.Throws<ArgumentOutOfRangeException>(() => ZigZagSnapshot.Calculate([], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ZigZagSnapshot.Calculate([], double.NaN));
        Assert.Throws<ArgumentNullException>(() => ZigZagSnapshot.Calculate(null!));
    }

    [Fact]
    public void EveryOutputAndPresenceMutationFails()
    {
        var bars = Fixture().IndicatorBars;
        var expected = ZigZagComparison.Series(ZigZagSnapshot.Calculate(bars));
        foreach (var name in expected.Outputs.Keys)
        foreach (var mask in new[] { false, true })
        {
            var actual = ZigZagComparison.Series(ZigZagSnapshot.Calculate(bars));
            var output = actual.Outputs[name];
            var index = Array.FindLastIndex(output.Present!, v => v);
            Assert.True(index >= 0);
            if (mask)
                output.Present![index] = false;
            else
                output.Values[index] += 1;
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Compare(expected, actual, name, IndicatorErrorBudget.Exact)
            );
        }
    }

    [Fact]
    public void UnavailableTradyMapsToTheCompleteVerifiedAlternate()
    {
        var row = ComparisonManifest.Create().Single(r => r.Id == "Trady.Indicator.ZigZag");
        Assert.Equal("unavailable", row.Status);
        Assert.Equal("Skender.GetZigZag", row.AlternateComparison);
        Assert.Equal(nameof(ZigZagSnapshot), row.OoplesIndicator);
        Assert.Equal(4, row.Outputs.Length);
        ComparisonVerifier.Check(
            ZigZagComparison.Pair(1e-25),
            CompetitorData.FromCloses([1e-20, 2e-20, 1e-20, 3e-20]),
            20
        );
    }

    [Fact]
    public void TradyZigZagHasNoExecutableFormula() =>
        Assert.Throws<NotImplementedException>(() => new TradyProbe().Compute());

    private sealed class TradyProbe()
        : Trady.Analysis.Indicator.ZigZag<decimal, decimal?>(new[] { 1m, 2m }, v => v);
}
