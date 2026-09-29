using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CoralTrendNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CORAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(CoralTrendIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentSixStagePolynomial(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.CoralTrendOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length = 5, double cd = .4)
    {
        var expected = BuiltInFormulaReferences.CoralTrendValues(bars, length, cd); var values = expected.Outputs["Cti"];
        var data = Data(bars).CalculateCoralTrendIndicator(length, cd); Assert.Equal(values, data.CustomValuesList); Assert.Equal(values, data.OutputValues["Cti"]); Assert.Equal(expected.Signals, data.SignalsList);
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeCoralTrendIndicatorFast(Data(bars), context, length, cd); Assert.Equal(values, output.ToArray());
        var core = new double[bars.Length]; MovingAverageCore.CoralTrendIndicator(bars.Select(b => b.Close).ToArray(), core, length, cd); Assert.Equal(values, core);
        var state = new CoralTrendIndicatorState(length, cd); var window = new CoralTrendWindow(length, cd);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 5d, -3, 1, 7 })) { state.Update(Native(bar), true, false); window.Next(bar.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -91d })[0]), false, false); window.Next(-91, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(values[i], point.Value); Assert.Equal(values[i], point.Outputs!["Cti"]); var direct = window.Next(bars[i].Close, final); Assert.Equal(values[i], direct.Value); Assert.Equal(expected.Signals[i], direct.Signal); } }
        }
        return values;
    }
    [Fact]
    public void WidePricesAndSignedCoefficientsMatchExactPolynomial()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 8 }) foreach (var cd in new[] { -1d, -.4, 0, .4, 1, 2.5 })
            Assert.All(Check(Bars(Enumerable.Range(0, 23).Select(i => (i % 7 - 3) * scale)), 5, cd), value => Assert.False(double.IsNaN(value)));
        Check(Bars(new[] { -double.MaxValue, double.MaxValue, double.MaxValue, -double.MaxValue, 0, 1, -1, 0, 0, 0 }), 2);
    }
    [Fact]
    public void HandOpeningPolynomialAndUnitPeriodCancellationArePreserved()
    {
        foreach (var pair in new[] { (0d, .125), (.4, .216), (1d, .421875), (-1d, .015625) }) Assert.Equal(new[] { pair.Item2 }, Check(Bars(new[] { 1d }), 5, pair.Item1));
        var prices = new[] { 1d, 2, 1, -double.MaxValue, double.MaxValue, double.Epsilon, -double.Epsilon, 0 };
        foreach (var cd in new[] { -double.MaxValue, -1d, 0, .4, double.MaxValue }) Assert.Equal(prices, Check(Bars(prices), 1, cd));
    }
    [Fact]
    public void OverflowingCoefficientCubeStillProducesFiniteScaledOutput()
    {
        var price = Math.Pow(2, -500); var coefficient = Math.Pow(2, 500);
        Assert.Equal(new[] { Math.Pow(2, 994) }, Check(Bars(new[] { price }), 5, coefficient));
        Assert.Equal(new[] { -Math.Pow(2, 994) }, Check(Bars(new[] { price }), 5, -coefficient));
    }
    [Fact]
    public void TrueOutputOverflowDoesNotPoisonTheSixStageHistory()
    {
        var values = Check(Bars(new[] { 1d }.Concat(Enumerable.Repeat(0d, 700))), 5, Math.Pow(2, 500));
        Assert.True(double.IsPositiveInfinity(values[0])); Assert.False(double.IsInfinity(values[^1])); Assert.False(double.IsNaN(values[^1])); Assert.NotEqual(0, values[^1]);
    }
    [Fact]
    public void ExtremePeriodsRequireOnlySixHistorySlots()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var bars in new[] { Array.Empty<Bar>(), Bars(new[] { 1d, -3, 7, 0, double.Epsilon }) }) Check(bars, length);
    }
    [Fact]
    public void SelectedPricesReachTheKernelWithoutAverageCallbacks()
    {
        var bars = Bars(new[] { 2d, 4, 6, 8, 10, 12 }); var selected = new[] { -2d, 0, 4, 3, -1, 8 }; var expected = BuiltInFormulaReferences.CoralTrendValues(Bars(selected), 5, .4);
        using var armed = ComponentAverage.Arm((_, _) => throw new InvalidOperationException("Coral has no custom moving-average component.")); using var context = new ComputeContext();
        foreach (var fast in new[] { false, true }) { var data = Data(bars); data.SetCustomValues(selected.ToList()); if (fast) { using var output = IndicatorCompute.ComputeCoralTrendIndicatorFast(data, context, 5); Assert.Equal(expected.Outputs["Cti"], output.ToArray()); } else { data.CalculateCoralTrendIndicator(5); Assert.Equal(expected.Outputs["Cti"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); } }
        Assert.Equal(0, ComponentAverage.Requests); Assert.Equal(0, ComponentAverage.Substitutions);
    }
    [Fact]
    public void NonfiniteCoefficientsRejectEvenEmptyInputBeforeCallbacks()
    {
        using var context = new ComputeContext(); using var armed = ComponentAverage.Arm((_, _) => throw new InvalidOperationException("No callback is expected."));
        foreach (var cd in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var bars in new[] { Array.Empty<Bar>(), Bars(new[] { 1d }) })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoralTrendIndicatorState(5, cd));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(bars).CalculateCoralTrendIndicator(5, cd));
            Assert.Throws<ArgumentOutOfRangeException>(() => { using var output = IndicatorCompute.ComputeCoralTrendIndicatorFast(Data(bars), context, 5, cd); });
            Assert.Throws<ArgumentOutOfRangeException>(() => MovingAverageCore.CoralTrendIndicator(bars.Select(b => b.Close).ToArray(), new double[bars.Length], 5, cd));
        }
        Assert.Equal(0, ComponentAverage.Requests);
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceAnySmoothingStage()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new CoralTrendIndicatorState(5); var control = new CoralTrendIndicatorState(5);
            foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, true).Value, state.Update(Native(bar), true, true).Value);
        }
    }
    [Fact]
    public void CoreRejectsShortOutputAndKeepsTrailingSpaceUntouched()
    {
        Assert.Throws<ArgumentException>(() => MovingAverageCore.CoralTrendIndicator(new[] { 1d, 2 }, new double[1], 5));
        var output = new[] { -7d, -7, -7 }; MovingAverageCore.CoralTrendIndicator(new[] { 1d }, output, 5); Assert.Equal(new[] { .216, -7, -7 }, output);
    }
}
