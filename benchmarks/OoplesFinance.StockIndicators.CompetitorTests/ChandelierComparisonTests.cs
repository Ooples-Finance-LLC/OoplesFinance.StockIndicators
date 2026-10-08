using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ChandelierComparisonTests
{
    [Theory]
    [InlineData(1, 0, ChandelierExitSelection.Both, false)]
    [InlineData(3, 3, ChandelierExitSelection.Both, false)]
    [InlineData(3, .5, ChandelierExitSelection.Long, true)]
    [InlineData(3, -1, ChandelierExitSelection.Short, false)]
    [InlineData(int.MaxValue, 3, ChandelierExitSelection.Both, false)]
    public async Task IndependentLifecycleContracts(
        int period,
        double multiplier,
        ChandelierExitSelection selection,
        bool floor
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WindowChandelierExit),
                "window chandelier",
                () => new WindowChandelierExit(period, multiplier, selection, floor)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    private static Bar[] Raw(double[] highs, double[] lows, double[] closes) =>
        highs
            .Select(
                (v, i) =>
                    new Bar(DateTime.UnixEpoch.AddDays(i), closes[i], v, lows[i], closes[i], 0)
            )
            .ToArray();

    [Fact]
    public void KnownLevelsAndNegativeHighConvention()
    {
        var bars = Raw([2, 2, 2], [0, 0, 0], [1, 1, 1]);
        var result = ChandelierComparison.Owned(bars, 2, true).Outputs;
        Assert.Equal(new[] { false, false, true }, result["Long"].Present);
        Assert.Equal(new[] { false, false, true }, result["Short"].Present);
        Assert.Equal(-4, result["Long"].Values[2]);
        Assert.Equal(6, result["Short"].Values[2]);
        var negative = Raw([-2, -2, -2], [-4, -4, -4], [-3, -3, -3]);
        Assert.Equal(-6, ChandelierComparison.Owned(negative, 2, false).Outputs["Value"].Values[2]);
        Assert.Equal(-8, ChandelierComparison.Owned(negative, 2, true).Outputs["Long"].Values[2]);
        Assert.Equal(
            2,
            ChandelierComparison.Owned(negative, 2, false, true).Outputs["Value"].Values[2]
        );
    }

    [Fact]
    public void ExtendedAtrAndOffsetCancellationPreserveFiniteLevels()
    {
        var bars = Raw(
            [double.MaxValue, double.MaxValue, double.MaxValue],
            [-double.MaxValue, -double.MaxValue, -double.MaxValue],
            [0, 0, 0]
        );
        var result = ChandelierComparison.Owned(bars, 2, true, multiplier: .5);
        Assert.Equal(0, result.Outputs["Long"].Values[2]);
        Assert.Equal(0, result.Outputs["Short"].Values[2]);
        var oneSide = Raw(
            [double.MaxValue, double.MaxValue, double.MaxValue],
            [0, 0, 0],
            [0, 0, 0]
        );
        Assert.Equal(
            -double.MaxValue,
            ChandelierComparison.Owned(oneSide, 2, false, multiplier: 2).Outputs["Value"].Values[2]
        );
        Assert.Throws<IndicatorOutputException>(() =>
            ChandelierComparison.Owned(oneSide, 2, true, multiplier: 2)
        );
        foreach (
            var fixture in new[]
            {
                bars,
                Raw(
                    [double.Epsilon, 2 * double.Epsilon, double.Epsilon],
                    [0, 0, 0],
                    [0, double.Epsilon, 0]
                ),
                Raw([1, Math.BitIncrement(1), 1], [1, 1, Math.BitDecrement(1)], [1, 1, 1]),
            }
        )
        foreach (var period in new[] { 1, 2, 3, int.MaxValue })
        {
            var expected = RetrospectivePriceComparison.Series(
                ["Long", "Short"],
                ChandelierComparison.Reference(fixture, period, .5, false, 0)
            );
            ComparisonVerifier.Compare(
                expected,
                ChandelierComparison.Owned(fixture, period, true, multiplier: .5),
                "wide/tiny chandelier",
                IndicatorErrorBudget.Exact
            );
        }
    }

    [Fact]
    public void NativeDefaultsSortingSelectionsAndValidationHoles()
    {
        var data = CompetitorData.Create(50);
        Assert.Equal(
            data.Quotes.GetChandelier(22, 3, ChandelierType.Long).Select(r => r.ChandelierExit),
            data.Quotes.GetChandelier().Select(r => r.ChandelierExit)
        );
        foreach (var side in new[] { ChandelierType.Long, ChandelierType.Short })
            Assert.Equal(
                data.Quotes.GetChandelier(3, 3, side).Select(r => (r.Date, r.ChandelierExit)),
                data.Quotes.AsEnumerable()
                    .Reverse()
                    .GetChandelier(3, 3, side)
                    .Select(r => (r.Date, r.ChandelierExit))
            );
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetChandelier(1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetChandelier(3, 0).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            data.Quotes.GetChandelier(3, -1).ToArray()
        );
        Assert.All(
            data.Quotes.Take(2).GetChandelier(3, 3, (ChandelierType)99),
            r => Assert.Null(r.ChandelierExit)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            data.Quotes.GetChandelier(3, 3, (ChandelierType)99).ToArray()
        );
        Assert.True(
            double.IsNaN(data.Quotes.GetChandelier(3, double.NaN).Last().ChandelierExit!.Value)
        );
        Assert.True(
            double.IsNegativeInfinity(
                data.Quotes.GetChandelier(3, double.PositiveInfinity).Last().ChandelierExit!.Value
            )
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowChandelierExit(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowChandelierExit(3, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WindowChandelierExit(3, 3, (ChandelierExitSelection)99)
        );
        foreach (var shortSide in new[] { false, true })
        foreach (var multiplier in new[] { .5, 3 })
            ComparisonVerifier.Check(
                ChandelierComparison.Create(false, shortSide, multiplier),
                data,
                3
            );
    }

    [Fact]
    public void TradyGenericTupleIndexRangeAndRepeatedRoutes()
    {
        var data = CompetitorData.Create(25);
        var inputs = data.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray();
        foreach (var multiplier in new[] { -1m, 0m, .5m, 3m })
        foreach (var period in new[] { 1, 3 })
        {
            var expected = ChandelierComparison.TradyReference(inputs, period, multiplier);
            var tuple = new T.ChandelierExitByTuple(inputs, period, multiplier);
            Assert.Equal(expected, tuple.Compute());
            Assert.Equal(expected, tuple.Compute());
            Assert.Equal(
                expected.Select(v => (double?)v.Long),
                tuple.Compute().Select(v => (double?)v.Long)
            );
            Assert.Equal(
                expected.Select(v => (double?)v.Short),
                tuple.Compute().Select(v => (double?)v.Short)
            );
            Assert.Equal(
                expected,
                new T.ChandelierExit(data.Candles, period, multiplier).Compute().Select(r => r.Tick)
            );
            Assert.Equal(
                expected,
                new T.ChandelierExit<int, (decimal? Long, decimal? Short)>(
                    Enumerable.Range(0, inputs.Length),
                    i => inputs[i],
                    period,
                    multiplier
                ).Compute()
            );
            var indexes = new[] { 8, 2, 2, 0, 5 };
            Assert.Equal(
                indexes.Select(i => expected[i]),
                tuple.Compute((IEnumerable<int>)indexes)
            );
            Assert.Equal(expected.Skip(2).Take(6), tuple.Compute(startIndex: 2, endIndex: 7));
            foreach (var i in indexes)
                Assert.Equal(expected[i], tuple[i]);
            ComparisonVerifier.Check(
                ChandelierComparison.Create(true, multiplier: (double)multiplier),
                data,
                period
            );
        }
    }

    [Fact]
    public void NativeDecimalOverflowAndTinyQuoteCollapseRemainVisible()
    {
        var tuples = Enumerable.Repeat((decimal.MaxValue, -decimal.MaxValue, 0m), 3).ToArray();
        Assert.Throws<OverflowException>(() =>
            new T.ChandelierExitByTuple(tuples, 1, .5m).Compute().ToArray()
        );
        var tiny = CompetitorData.FromOhlc(
            [0, 0, 0],
            [double.Epsilon, 2 * double.Epsilon, double.Epsilon],
            [0, 0, 0],
            [0, double.Epsilon, 0]
        );
        Assert.Equal(0, tiny.Quotes.GetChandelier(2).Last().ChandelierExit);
        Assert.NotEqual(
            0,
            ChandelierComparison.Owned(tiny.IndicatorBars, 2, false).Outputs["Value"].Values[2]
        );
        var decimalTiny = new[]
        {
            (1m, 0m, 0m),
            (1.0000000000000000000000000001m, 0m, 0m),
            (1m, 0m, 0m),
        };
        Assert.Equal(
            ChandelierComparison.TradyReference(decimalTiny, 1, 3),
            new T.ChandelierExitByTuple(decimalTiny, 1, 3).Compute()
        );
    }

    [Fact]
    public async Task ChainingRetainsOriginalRangesAndSelectedPrimaryOutput()
    {
        var data = CompetitorData.Create(30);
        var closes = FixedWeightedComparison.Stage(data.Closes, 3, false);
        var bars = data
            .IndicatorBars.Select(
                (b, i) => new Bar(b.Time, b.Open, b.High, b.Low, closes[i], b.Volume)
            )
            .ToArray();
        foreach (
            var selection in new[]
            {
                ChandelierExitSelection.Long,
                ChandelierExitSelection.Short,
                ChandelierExitSelection.Both,
            }
        )
        {
            var indicator = new WindowChandelierExit(3, 3, selection);
            indicator.Of(new FixedPeriodWma(3));
            Assert.Same(
                selection == ChandelierExitSelection.Short
                    ? indicator.ShortExit
                    : indicator.LongExit,
                indicator.PrimaryOutput
            );
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var expected = ChandelierComparison.Reference(bars, 3, 3, false, 0);
            for (var slot = 0; slot < 2; slot++)
            {
                var selected = selection == ChandelierExitSelection.Both || (int)selection == slot;
                Assert.Equal(
                    expected[slot].Select(v => selected ? v ?? 0 : 0),
                    run[indicator.Outputs[slot]].ToArray()
                );
                Assert.Equal(
                    expected[slot].Select(v => selected && v.HasValue ? 1d : 0),
                    run[indicator.Outputs[slot + 2]].ToArray()
                );
            }
        }
    }

    [Fact]
    public void EveryValuePresenceAndHighFloorMutationIsDetected()
    {
        foreach (var pair in ChandelierComparison.Pairs)
        foreach (var name in pair.OutputNames!)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                if (presence)
                    result.Outputs[name].Present![^1] = false;
                else
                    result.Outputs[name].Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    CompetitorData.Create(30),
                    3
                )
            );
        }
        var skender = ChandelierComparison.Create(false);
        var negative = CompetitorData.FromOhlc(
            [-3, -3, -3],
            [-2, -2, -2],
            [-4, -4, -4],
            [-3, -3, -3]
        );
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                skender with
                {
                    Library = (d, p) =>
                        VolumePriceComparison.Mask(
                            ChandelierComparison.Reference(d.IndicatorBars, p, 3, false, 0)[0]
                        ),
                },
                negative,
                2
            )
        );
    }
}
