using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AdaptiveZoneNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AZ", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    [Fact]
    public void DoubleSmoothingRangeAndFiniteCenterMatchIndependentFractions()
    {
        foreach (var length in new[] { 1, 2, 4, 7, 20 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var pct in new[] { 0d, .5, 2, double.MaxValue })
        foreach (var bars in new[] { Array.Empty<Bar>(), BarsOf(new[] { 1d, 4, -2, 3, 0, 0, 0, 0 }), BarsOf(new[] { double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue, 0, 0, 0, 0 }), BarsOf(new[] { double.Epsilon, 3 * double.Epsilon, -double.Epsilon, 0, 0, 0, 0 }), Enumerable.Range(0, 8).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, double.MaxValue, -double.MaxValue, 0, 1)).ToArray() })
        {
            var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
            var expected = BuiltInFormulaReferences.AdaptiveZoneOutputs(bars, length, code, pct);
            var batch = Data(bars).CalculateAdaptivePriceZoneIndicator(kind, length, pct); foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
            using var context = new ComputeContext();
            foreach (var band in new[] { IndicatorCompute.ChannelBand.Upper, IndicatorCompute.ChannelBand.Middle, IndicatorCompute.ChannelBand.Lower })
            {
                var key = band == IndicatorCompute.ChannelBand.Upper ? "UpperBand" : band == IndicatorCompute.ChannelBand.Lower ? "LowerBand" : "MiddleBand";
                using var raw = IndicatorCompute.ComputeAdaptivePriceZoneFast(Data(bars), context, length, pct, kind, band); Assert.Equal(expected[key], raw.Span.ToArray());
            }
            using var state = new AdaptivePriceZoneIndicatorState(kind, length, pct);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                foreach (var final in new[] { false, false, true }) { var actual = state.Update(Native(bars[i]), final, true); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]); }
            }
        }
        var hand = Data(new[] { new Bar(DateTime.UnixEpoch, 2, 4, 0, 2, 1) }).CalculateAdaptivePriceZoneIndicator(length: 4);
        Assert.Equal(10, hand.OutputValues["UpperBand"][0]); Assert.Equal(2, hand.OutputValues["MiddleBand"][0]); Assert.Equal(-6, hand.OutputValues["LowerBand"][0]);
    }
    [Fact]
    public void SelectedInputAndFourCustomerStagesKeepTheirOrder()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 1, 10, 0, 1, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 2, 10, 0, 2, 1), new Bar(DateTime.UnixEpoch.AddMinutes(2), 3, 10, 0, 3, 1) };
        var prices = new[] { 5d, 6, 7 }; var data = Data(bars); data.SetCustomValues(prices.ToList());
        var expected = BuiltInFormulaReferences.AdaptiveZoneOutputs(bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, prices[i], b.Volume)).ToArray(), 4, 3, 2);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeAdaptivePriceZoneFast(data, context, 4); Assert.Equal(expected["MiddleBand"], raw.Span.ToArray());
        var inputs = new[] { new[] { 1d, 2, 3 }, new[] { 1d, 1, 1 }, new[] { 10d, 10, 10 }, new[] { 1d, 1, 1 } };
        foreach (var batch in new[] { false, true })
        {
            var callbacks = inputs.Select(input => new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>((values, period) => { Assert.Equal(2, period); Assert.Equal(input, values); return new[] { 1d, 1, 1 }; })).ToArray();
            using var armed = ComponentAverage.Arm(callbacks);
            if (batch) Assert.Equal(new[] { 1d, 1, 1 }, Data(bars).CalculateAdaptivePriceZoneIndicator(length: 4).OutputValues["MiddleBand"]);
            else { using var value = IndicatorCompute.ComputeAdaptivePriceZoneFast(Data(bars), context, 4); Assert.Equal(new[] { 1d, 1, 1 }, value.Span.ToArray()); }
            Assert.Equal(4, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new AdaptivePriceZoneIndicatorState(length: 4); using var control = new AdaptivePriceZoneIndicatorState(length: 4);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("AZ", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(AdaptivePriceZoneIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.AdaptiveZoneOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory, MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory, MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
