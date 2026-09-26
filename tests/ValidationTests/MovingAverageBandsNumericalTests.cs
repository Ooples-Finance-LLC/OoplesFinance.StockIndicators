using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MovingAverageBandsNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void RootMeanSquareBandsPreservePreviewResetAndExtendedRange()
    {
        foreach (var periods in new[] { (1, 2), (2, 7), (7, 3), (1063, 2) })
        foreach (var mult in new[] { 0d, 1, -1, double.MaxValue })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 2d, 5, 0, 3, 8, -3, 1, -5, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, 3 * double.Epsilon, 0, 0 }, Enumerable.Repeat(double.MaxValue, 20).ToArray() })
        {
            var bars = BarsOf(prices); IBuiltInIndicator indicator = new MovingAverageBands(periods.Item1, periods.Item2, mult);
            var expected = BuiltInFormulaReferences.MovingAverageBandOutputs(bars, indicator);
            var batch = Data(bars).CalculateMovingAverageBands(fastLength: periods.Item1, slowLength: periods.Item2, mult: mult);
            foreach (var key in new[] { "UpperBand", "MiddleBand", "LowerBand", "FastMa" }) Assert.Equal(expected[key], batch.OutputValues[key]);
            Assert.Equal(expected["Mabw"], Data(bars).CalculateMovingAverageBandWidth(fastLength: periods.Item1, slowLength: periods.Item2, mult: mult).CustomValuesList);
            using var state = new MovingAverageBandsState(fastLength: periods.Item1, slowLength: periods.Item2, mult: mult);
            using var width = new MovingAverageBandWidthState(fastLength: periods.Item1, slowLength: periods.Item2, mult: mult);
            for (var replay = 0; replay < 2; replay++)
            {
                foreach (var seed in BarsOf(new[] { 99d, -99d })) { state.Update(Native(seed), true, true); width.Update(Native(seed), true, true); } state.Reset(); width.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var preview = Native(BarsOf(new[] { -double.MaxValue })[0]); state.Update(preview, false, true); width.Update(preview, false, true);
                    foreach (var final in new[] { false, false, true })
                    {
                        var actual = state.Update(Native(bars[i]), final, true);
                        foreach (var key in new[] { "UpperBand", "MiddleBand", "LowerBand", "FastMa" }) Assert.Equal(expected[key][i], actual.Outputs![key]);
                        Assert.Equal(expected["Mabw"][i], width.Update(Native(bars[i]), final, true).Value);
                    }
                }
            }
        }
    }

    [Fact]
    public void HiddenDeviationPreservesFiniteWidthsAndNarrowBands()
    {
        foreach (var pair in new[] { (double.MaxValue, -double.MaxValue), (Math.BitDecrement(1d), 1d), (0d, double.Epsilon) })
        foreach (var mult in new[] { 0d, .25, 1, -1 })
        {
            var bars = BarsOf(new[] { 1d }); var indicator = new MovingAverageBands(1, 2, mult);
            var expected = BuiltInFormulaReferences.MovingAverageBandOutputs(bars, indicator, new[] { pair.Item1 }, new[] { pair.Item2 });
            using var window = new MovingAverageBandWindow(MovingAvgType.ExponentialMovingAverage, 1, 2, mult, true);
            var actual = window.Next(1, true, pair.Item1, pair.Item2);
            Assert.Equal(expected["UpperBand"][0], actual.Upper); Assert.Equal(expected["LowerBand"][0], actual.Lower); Assert.Equal(expected["Mabw"][0], actual.Width);
        }
    }

    [Fact]
    public void CustomerAveragesReceiveOriginalPricesAtBothPeriods()
    {
        var bars = BarsOf(new[] { 1d, 3, 2, 4 });
        var callbacks = new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
            (values, period) => { Assert.Equal(2, period); Assert.Equal(bars.Select(b => b.Close), values); return values.Select(_ => 12d).ToArray(); },
            (values, period) => { Assert.Equal(3, period); Assert.Equal(bars.Select(b => b.Close), values); return values.Select(_ => 8d).ToArray(); } };
        foreach (IBuiltInIndicator indicator in new IBuiltInIndicator[] { new MovingAverageBands(2, 3), new MovingAverageBandWidth(2, 3) })
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
            if (batch)
            {
                var actual = indicator.BatchName == IndicatorName.MovingAverageBands ? Data(bars).CalculateMovingAverageBands(fastLength: 2, slowLength: 3) : Data(bars).CalculateMovingAverageBandWidth(fastLength: 2, slowLength: 3);
                if (indicator.BatchName == IndicatorName.MovingAverageBands) { Assert.All(actual.OutputValues["UpperBand"], v => Assert.Equal(12, v)); Assert.All(actual.OutputValues["LowerBand"], v => Assert.Equal(4, v)); }
                else Assert.All(actual.CustomValuesList, v => Assert.Equal(100, v));
            }
            else
            {
                var key = indicator.BatchName == IndicatorName.MovingAverageBands ? "UpperBand" : "Mabw";
                using var actual = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), key), context);
                Assert.NotNull(actual); Assert.All(actual.Value.Span.ToArray(), v => Assert.Equal(key == "Mabw" ? 100 : 12, v));
            }
            Assert.Equal(2, ComponentAverage.Substitutions);
        }
    }

    [Fact]
    public void NonfiniteMultipliersAreRejectedEvenForEmptyInput()
    {
        foreach (var mult in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovingAverageBandsState(mult: mult));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovingAverageBandWidthState(mult: mult));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateMovingAverageBands(mult: mult));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateMovingAverageBandWidth(mult: mult));
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var bandwidth in new[] { false, true })
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = bandwidth ? new MovingAverageBandWidthState(fastLength: 3) : new MovingAverageBandsState(fastLength: 3);
            IStreamingIndicatorState control = bandwidth ? new MovingAverageBandWidthState(fastLength: 3) : new MovingAverageBandsState(fastLength: 3);
            using var lifetime = (IDisposable)state; using var controlLifetime = (IDisposable)control;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(MovingAverageBands) || c.IndicatorType == typeof(MovingAverageBandWidth)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.MovingAverageBandOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
