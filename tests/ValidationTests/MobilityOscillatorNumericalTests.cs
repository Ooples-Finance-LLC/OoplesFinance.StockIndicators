using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using MacdSeries = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.MacdSeries;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MobilityOscillatorNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(MobilityOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
        bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static Bar[] Points(params double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static OhlcvBar Native(Bar b) => new("MO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCandleCdf(IndicatorValidationCase c, string route)
    {
        var o = (MobilityOscillatorSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.MobilityValues(bars, o.Length, o.MaType), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourceRetainsOriginalCandles(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void BothPublishedOutputsRejectFaults(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EveryNumericalClassIsEnrolled(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    [Fact]
    public void FirstExactModeAndTwoWeightedStagesHaveIndependentHand()
    {
        var bars = Points(1, 0, 2);
        var data = Data(bars).CalculateMobilityOscillator(length2: 2);
        Assert.Equal(new[] { 0d, 0, -25 }, data.OutputValues["Mo"]);
        Assert.Equal(new[] { 0d, 0, -6.25 }, data.OutputValues["Signal"]);
        using var state = new MobilityOscillatorState(length2: 2);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Points(91)[0]), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var result = state.Update(Native(bars[i]), final, true);
                    Assert.Equal(data.OutputValues["Mo"][i], result.Value);
                    Assert.Equal(data.OutputValues["Signal"][i], result.Outputs!["Signal"]);
                }
            }
        }
    }

    [Fact]
    public void ExtremeBinCountsAndPeriodsDoNotAllocateTheirDeclaredSize()
    {
        var bars = Points(1, 0, 2);
        var data = Data(bars).CalculateMobilityOscillator(length1: int.MaxValue, length2: 2, signalLength: 1);
        Assert.Equal(new[] { 0d, 0, -100 }, data.OutputValues["Mo"]);
        using var state = new MobilityOscillatorState(length1: int.MaxValue, length2: 2, signalLength: 1);
        Assert.Equal(data.OutputValues["Mo"], bars.Select(b => state.Update(Native(b), true, false).Value));
        using var lazy = new MobilityOscillatorState(length1: int.MaxValue, length2: int.MaxValue, signalLength: int.MaxValue);
        Assert.All(bars, b => Assert.Equal(0d, lazy.Update(Native(b), true, false).Value));
        Assert.Empty(Data([]).CalculateMobilityOscillator(length1: int.MaxValue, length2: int.MaxValue).OutputValues["Mo"]);
    }

    [Fact]
    public void ChainedComparisonPriceDoesNotReplaceCandleRanges()
    {
        var data = Data(Points(9, 0, 2)); data.SetCustomValues(new List<double> { -1, 0, 2 });
        data.CalculateMobilityOscillator(length2: 2, signalLength: 1);
        Assert.Equal(new[] { 0d, 0, 100 }, data.OutputValues["Mo"]);
        Assert.Equal(new[] { 9d, 0, 2 }, data.HighPrices);
        Assert.Equal(new[] { 9d, 0, 2 }, data.LowPrices);
    }

    [Fact]
    public void CoreUsesPublicDensityFormulaAndSupportsAliasedOutput()
    {
        var highs = new[] { 1d, 0, 2 }; var lows = highs.ToArray(); var closes = highs.ToArray();
        OscillatorCore.MobilityOscillator(highs, lows, closes, highs, 2);
        Assert.Equal(new[] { 0d, 0, -25 }, highs);
        var sentinel = new[] { 91d, 92, 93 };
        Assert.Throws<ArgumentException>(() => OscillatorCore.MobilityOscillator(new[] { 1d }, lows, closes, sentinel, 2));
        Assert.Equal(new[] { 91d, 92, 93 }, sentinel);
        Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.MobilityOscillator(new[] { 0d }, new[] { 1d }, new[] { 0d }, sentinel));
        Assert.Equal(new[] { 91d, 92, 93 }, sentinel);
    }

    [Fact]
    public void CustomerAveragesReceiveRawThenLineAndPreserveCallerInputs()
    {
        foreach (var batch in new[] { false, true })
        foreach (var series in new[] { MacdSeries.Line, MacdSeries.Signal })
        {
            var data = Data(Points(9, 0, 2)); var selected = new[] { -1d, 0, 2 };
            data.SetCustomValues(selected.ToList());
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (values, period) => { Assert.Equal(3, period); Assert.Equal(new[] { 0d, 0, 100 }, values); return new[] { 7d, 8, 9 }; },
                (values, period) => { Assert.Equal(3, period); Assert.Equal(new[] { 7d, 8, 9 }, values); Assert.Equal(selected, data.ChainedValues); return new[] { 2d, 3, 4 }; } });
            var expected = series == MacdSeries.Line ? new[] { 7d, 8, 9 } : new[] { 2d, 3, 4 };
            if (batch) Assert.Equal(expected, data.CalculateMobilityOscillator(length2: 2, signalLength: 3).OutputValues[series == MacdSeries.Line ? "Mo" : "Signal"]);
            else
            {
                using var context = new ComputeContext();
                using var output = IndicatorCompute.ComputeMobilityOscillatorFast(data, context, length2: 2, signalLength: 3, series: series);
                Assert.Equal(expected, output.ToArray()); Assert.Equal(selected, data.ChainedValues);
            }
            Assert.Equal(2, ComponentAverage.Substitutions);
            Assert.Equal(new[] { 9d, 0, 2 }, data.HighPrices);
        }
    }

    [Fact]
    public void ThrowingCustomerAverageRestoresCallerInputs()
    {
        var data = Data(Points(9, 0, 2)); var selected = new[] { -1d, 0, 2 }; data.SetCustomValues(selected.ToList());
        using var armed = ComponentAverage.Arm((_, _) =>
        {
            data.SetCustomValues(new List<double> { 91, 92, 93 });
            throw new InvalidOperationException("customer failure");
        });
        Assert.Throws<InvalidOperationException>(() => data.CalculateMobilityOscillator(length2: 2));
        Assert.Equal(selected, data.ChainedValues);
    }

    [Fact]
    public void RejectedCandlesDoNotAdvanceNativeHistory()
    {
        using var actual = new MobilityOscillatorState(length2: 2);
        using var expected = new MobilityOscillatorState(length2: 2);
        var seed = Native(Points(1)[0]); actual.Update(seed, true, false); expected.Update(seed, true, false);
        foreach (var invalid in new[]
        {
            new Bar(DateTime.UnixEpoch, 0, double.NaN, 0, 0, 1),
            new Bar(DateTime.UnixEpoch, 0, 0, double.NegativeInfinity, 0, 1),
            new Bar(DateTime.UnixEpoch, 0, 0, 0, double.PositiveInfinity, 1),
            new Bar(DateTime.UnixEpoch, 0, 0, 1, 0, 1)
        })
        foreach (var final in new[] { false, true })
            Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(invalid), final, true));
        foreach (var bar in Points(0, 2, -1, 3, 0))
            Assert.Equal(expected.Update(Native(bar), true, false).Value, actual.Update(Native(bar), true, false).Value);
    }
}
