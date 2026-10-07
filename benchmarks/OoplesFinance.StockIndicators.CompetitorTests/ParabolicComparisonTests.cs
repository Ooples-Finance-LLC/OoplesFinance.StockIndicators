using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ParabolicComparisonTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void IndependentReferencesCoverDefaultsAndRepeatedCalls(int kind)
    {
        var data = CompetitorData.Create(200);
        var p = new SarParameters();
        ComparisonVerifier.Check(ParabolicComparison.Pair(kind), data, 20);
        ComparisonVerifier.Compare(
            ParabolicComparison.Owned(data.IndicatorBars, kind, p),
            ParabolicComparison.Owned(data.IndicatorBars, kind, p),
            "fresh SAR",
            IndicatorErrorBudget.Exact
        );
    }

    [Fact]
    public void FirstTrendRemovalAndFourBarSeedHaveGoldens()
    {
        var falling = Enumerable
            .Range(0, 8)
            .Select(i => new Bar(
                DateTime.UnixEpoch.AddDays(i),
                10 - i * 2,
                10 - i * 2,
                9 - i * 2,
                10 - i * 2,
                1
            ))
            .ToArray();
        var confirmed = ParabolicStopSnapshots.Confirmed(falling);
        Assert.Null(confirmed[0].Sar);
        Assert.Null(confirmed[1].Sar);
        Assert.Equal(10, confirmed[2].Sar);
        Assert.False(confirmed[2].IsReversal);
        var four = ParabolicStopSnapshots.FourBar(falling);
        Assert.All(four.Take(4), v => Assert.Null(v));
        Assert.Equal(10, four[4]);
        var rising = falling.Reverse().ToArray();
        Assert.All(ParabolicStopSnapshots.Confirmed(rising), v => Assert.Null(v.Sar));
    }

    [Fact]
    public void WideRangesAndTinyStopsRetainFiniteClampDecisions()
    {
        foreach (var scale in new[] { double.MaxValue, double.Epsilon, 1e-200 })
        {
            var bars = Enumerable
                .Range(0, 12)
                .Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, scale, -scale, 0, 0))
                .ToArray();
            Assert.Equal(scale, ParabolicStopSnapshots.Classic(bars)[1]);
            Assert.Equal(-scale, ParabolicStopSnapshots.Extended(bars)[1]);
            Assert.Equal(-scale, ParabolicStopSnapshots.Classic(bars)[2]);
            var confirmed = ParabolicStopSnapshots.Confirmed(bars);
            if (scale == double.Epsilon)
                Assert.All(confirmed, r => Assert.Null(r.Sar));
            else
            {
                Assert.All(confirmed.Take(2), r => Assert.Null(r.Sar));
                Assert.All(
                    confirmed.Skip(2),
                    r =>
                    {
                        Assert.Equal(scale, r.Sar);
                        Assert.False(r.IsReversal);
                    }
                );
            }
            Assert.All(
                ParabolicStopSnapshots.FourBar(bars).Skip(4),
                v => Assert.True(double.IsFinite(v!.Value))
            );
        }
        var extreme = Enumerable
            .Range(0, 5)
            .Select(i => new Bar(
                DateTime.UnixEpoch.AddDays(i),
                0,
                double.MaxValue,
                -double.MaxValue,
                0,
                0
            ))
            .ToArray();
        Assert.Throws<OverflowException>(() =>
            ParabolicStopSnapshots.Extended(extreme, offsetOnReverse: 1)
        );
    }

    [Fact]
    public void ParameterValidationAndNativeSmallInputBoundariesAreExplicit()
    {
        Assert.Throws<ArgumentNullException>(() => ParabolicStopSnapshots.Classic(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParabolicStopSnapshots.Classic([], -.01));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ParabolicStopSnapshots.Extended([], offsetOnReverse: -1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => ParabolicStopSnapshots.Confirmed([], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ParabolicStopSnapshots.Confirmed([], initialFactor: 1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => ParabolicStopSnapshots.FourBar([], 2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ParabolicStopSnapshots.Classic([], double.NaN)
        );
        Assert.Empty(ParabolicStopSnapshots.FourBar([], -.1, -.01));
        var code = Functions.Sar<double>(
            new[] { 1d },
            new[] { 0d },
            System.Range.All,
            new double[1],
            out _
        );
        Assert.Equal(TALib.Core.RetCode.OutOfRangeParam, code);
    }

    [Fact]
    public void ExtendedSignedStartOffsetsAndIndependentAccelerationsAreVerified()
    {
        var data = CompetitorData.Create(120);
        foreach (
            var p in new[]
            {
                new SarParameters(.04, .15, .03, 95, .01, .06, .02, .3),
                new SarParameters(.5, .2, .7, -105, .02, .5, .4, .1),
                new SarParameters(0, 0, 0, 0, 0, 0, 0, 0),
            }
        )
            ComparisonVerifier.Check(ParabolicComparison.Pair(1, p), data, 20);
        foreach (
            var p in new[]
            {
                new SarParameters(0, 0),
                new SarParameters(-.01, .2),
                new SarParameters(.1, .4),
            }
        )
            ComparisonVerifier.Check(ParabolicComparison.Pair(3, p), data, 20);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EveryOutputAndMaskMutationFails(int kind)
    {
        var data = CompetitorData.Create(250);
        var p = new SarParameters();
        var expected = ParabolicComparison.Owned(data.IndicatorBars, kind, p);
        foreach (var name in expected.Outputs.Keys)
        foreach (var missing in new[] { false, true })
        {
            var changed = ParabolicComparison.Owned(data.IndicatorBars, kind, p);
            Assert.True(changed.Outputs[name].Present![^1]);
            if (missing)
                changed.Outputs[name].Present![^1] = false;
            else
                changed.Outputs[name].Values[^1] += 1;
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Compare(expected, changed, name, IndicatorErrorBudget.Exact)
            );
        }
    }

    [Fact]
    public void NativeQuoteAndTupleRoutesRetainTheirPublishedValues()
    {
        var data = CompetitorData.Create(70);
        var candle = new Trady.Analysis.Indicator.ParabolicStopAndReverse(data.Candles)
            .Compute()
            .Select(r => r.Tick);
        var tuple = new Trady.Analysis.Indicator.ParabolicStopAndReverseByTuple(
            data.Candles.Select(c => (c.High, c.Low))
        ).Compute();
        Assert.Equal(candle, tuple);
        Assert.Equal(
            data.Quotes.GetParabolicSar().Select(r => (r.Sar, r.IsReversal)),
            data.Quotes.ToList().GetParabolicSar().Select(r => (r.Sar, r.IsReversal))
        );
    }
}
