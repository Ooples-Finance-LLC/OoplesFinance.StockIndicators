using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SupportResistanceNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void CascadesAndAllBandsPreservePreviewResetAndCancellation()
    {
        foreach (var length in new[] { 1, 2, 3, 7, 1063 })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 2d, 5, 0, 3, 8, -3, 1, -5, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, 3 * double.Epsilon, 0, 0 }, Enumerable.Repeat(double.MaxValue, 90).ToArray() })
        {
            var bars = BarsOf(prices); IBuiltInIndicator indicator = new MovingAverageSupportResistance(length);
            var expected = BuiltInFormulaReferences.SupportResistanceOutputs(bars, indicator);
            var batch = Data(bars).CalculateMovingAverageSupportResistance(length: length);
            foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
            using var state = new MovingAverageSupportResistanceState(length: length);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Update(Native(BarsOf(new[] { 99d })[0]), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -double.MaxValue })[0]), false, true);
                    foreach (var final in new[] { false, false, true })
                    {
                        var actual = state.Update(Native(bars[i]), final, true);
                        foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]);
                        Assert.Equal(expected["MiddleBand"][i], actual.Value);
                    }
                }
            }
        }
    }

    [Fact]
    public void CustomerAverageReceivesPrices()
    {
        var bars = BarsOf(new[] { 1d, 3, 2, 4 });
        var callbacks = new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (values, period) => {
            Assert.Equal(2, period); Assert.Equal(bars.Select(b => b.Close), values); return values.Select(_ => 20d).ToArray(); } };
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
            if (batch)
            {
                var actual = Data(bars).CalculateMovingAverageSupportResistance(length: 2, factor: 25);
                Assert.All(actual.OutputValues["UpperBand"], v => Assert.Equal(25, v)); Assert.All(actual.OutputValues["MiddleBand"], v => Assert.Equal(20, v)); Assert.All(actual.OutputValues["LowerBand"], v => Assert.Equal(16, v));
            }
            else { using var actual = IndicatorCompute.ComputeMovingAverageSupportResistanceFast(Data(bars), context, 2); Assert.All(actual.Span.ToArray(), v => Assert.Equal(20, v)); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }

    [Fact]
    public void PercentageShiftRoundsOnlyTheFinalResult()
    {
        foreach (var price in new[] { double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 13d })
        foreach (var shift in new[] { 0d, 1, -1, 7.25, -7.25, 100, -100, 99, -99, double.MaxValue, -double.MaxValue })
        {
            var hundred = new ReferenceFraction(100); var p = ReferenceFraction.FromDouble(price); var s = ReferenceFraction.FromDouble(shift);
            var upper = (p * (hundred + s) / hundred).ToDouble(); var divisor = hundred + s; var lower = divisor.Sign == 0 ? 0 : (p * hundred / divisor).ToDouble();
            var data = Data(BarsOf(new[] { price })).CalculateMovingAverageSupportResistance(length: 1, factor: shift);
            Assert.Equal(upper, data.OutputValues["UpperBand"][0]); Assert.Equal(lower, data.OutputValues["LowerBand"][0]);
            using var state = new MovingAverageSupportResistanceState(length: 1, factor: shift); var actual = state.Update(Native(BarsOf(new[] { price })[0]), true, true);
            Assert.Equal(upper, actual.Outputs!["UpperBand"]); Assert.Equal(lower, actual.Outputs["LowerBand"]);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeMovingAverageSupportResistanceFast(Data(BarsOf(new[] { price })), context, 1, band: IndicatorCompute.ChannelBand.Upper, factor: shift);
            Assert.Equal(upper, raw.Span[0]);
            using var rawLower = IndicatorCompute.ComputeMovingAverageSupportResistanceFast(Data(BarsOf(new[] { price })), context, 1, band: IndicatorCompute.ChannelBand.Lower, factor: shift);
            Assert.Equal(lower, rawLower.Span[0]);
        }
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MovingAverageSupportResistanceState(factor: invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateMovingAverageSupportResistance(factor: invalid));
            using var context = new ComputeContext();
            Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeMovingAverageSupportResistanceFast(Data(Array.Empty<Bar>()), context, factor: invalid));
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = new MovingAverageSupportResistanceState(length: 3);
            IStreamingIndicatorState control = new MovingAverageSupportResistanceState(length: 3);
            using var lifetime = (IDisposable)state; using var controlLifetime = (IDisposable)control;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(MovingAverageSupportResistance)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.SupportResistanceOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
