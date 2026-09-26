using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AbsoluteStrengthMtfNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void SymmetricChangeAndBothSmoothersPreserveAllFiniteScales()
    {
        foreach (var length in new[] { 1, 2, 3, 7, 20 })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 1d, 3, -2, 0, 9, 9, 9, 9 }, new[] { double.MaxValue, double.MaxValue / 2, -double.MaxValue, double.MaxValue, 0 }, new[] { double.Epsilon, 3 * double.Epsilon, -double.Epsilon, 0, 0 }, new[] { -4d, -2, -1, 1, -1, 2 } })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            var bars = BarsOf(prices); var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
            var expected = BuiltInFormulaReferences.AbsoluteStrengthMtfOutputs(bars, length, 3, code); var batch = Data(bars).CalculateAbsoluteStrengthMTFIndicator(kind, length, 3);
            foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeAbsoluteStrengthMTFFast(Data(bars), context, length, 3, kind);
            using var signal = IndicatorCompute.ComputeAbsoluteStrengthMTFFast(Data(bars), context, length, 3, kind, IndicatorCompute.BullBearSeries.Bears);
            Assert.Equal(expected["Bulls"], raw.Span.ToArray()); Assert.Equal(expected["Bears"], signal.Span.ToArray());
            using var state = new AbsoluteStrengthMTFIndicatorState(kind, length, 3);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                foreach (var final in new[] { false, false, true })
                {
                    var actual = state.Update(Native(bars[i]), final, true);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void OverflowingDifferenceCanProduceFiniteSmoothedStrength()
    {
        var result = Data(BarsOf(new[] { double.MaxValue, -double.MaxValue, double.MaxValue, double.MaxValue }))
            .CalculateAbsoluteStrengthMTFIndicator(length: 1, smoothLength: 2);
        Assert.Equal(new[] { 0d, double.MaxValue / 2, double.MaxValue, double.MaxValue }, result.OutputValues["Bulls"]);
        Assert.Equal(new[] { 0d, double.MaxValue, double.MaxValue, 0d }, result.OutputValues["Bears"]);
    }
    [Fact]
    public void SelectedInputAndCustomerStagesReceiveTheDerivedSeries()
    {
        var prices = new[] { 1d, 2, 4 }; var data = Data(BarsOf(new[] { 9d, 9, 9 })); data.SetCustomValues(prices.ToList());
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeAbsoluteStrengthMTFFast(data, context, 1, 1);
        Assert.Equal(new[] { 1d, 1, 2 }, raw.Span.ToArray());
        foreach (var batch in new[] { false, true })
        {
            var callbacks = new List<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>> {
                (values, period) => { Assert.Equal(3, period); Assert.Equal(prices, values); return new[] { 3d, 6, 9 }; },
                (values, period) => { Assert.Equal(3, period); Assert.Equal(new[] { 0d, 1, 2 }, values); return new[] { 4d, 2, 10 }; },
                (values, period) => { Assert.Equal(2, period); Assert.Equal(batch ? new[] { 0d, 4, 0 } : new[] { 1d, 0, 1 }, values); return new[] { 5d, 6, 7 }; }
            };
            if (batch) callbacks.Add((values, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 1d, 0, 1 }, values); return new[] { 8d, 9, 10 }; });
            using var armed = ComponentAverage.Arm(callbacks.ToArray());
            if (batch) Assert.Equal(new[] { 8d, 9, 10 }, Data(BarsOf(prices)).CalculateAbsoluteStrengthMTFIndicator(length: 3, smoothLength: 2).OutputValues["Bears"]);
            else { using var bears = IndicatorCompute.ComputeAbsoluteStrengthMTFFast(Data(BarsOf(prices)), context, 3, 2, MovingAvgType.SimpleMovingAverage, IndicatorCompute.BullBearSeries.Bears); Assert.Equal(new[] { 5d, 6, 7 }, bears.Span.ToArray()); }
            Assert.Equal(batch ? 4 : 3, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = new AbsoluteStrengthMTFIndicatorState(length: 3);
            IStreamingIndicatorState control = new AbsoluteStrengthMTFIndicatorState(length: 3);
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }


    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(AbsoluteStrengthMTFIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.AbsoluteStrengthMtfOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
