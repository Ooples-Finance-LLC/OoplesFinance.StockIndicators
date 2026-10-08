using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AutonomousRecursiveNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(AutonomousRecursiveMa)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentLaggedPartialMeans(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.AutonomousRecursiveOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static double[] Check(Bar[] bars, int length = 3, int momentumLength = 7, double gamma = 3)
    {
        var expected = BuiltInFormulaReferences.AutonomousRecursiveValues(bars, length, momentumLength, gamma); var batch = Data(bars).CalculateAutonomousRecursiveMovingAverage(length, momentumLength, gamma);
        Assert.Equal(expected.Outputs["Arma"], batch.CustomValuesList); Assert.Equal(expected.Outputs["Arma"], batch.OutputValues["Arma"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeAutonomousRecursiveMaFast(Data(bars), context, length, momentumLength, gamma); Assert.Equal(expected.Outputs["Arma"], output.ToArray());
        using var state = new AutonomousRecursiveMovingAverageState(length, momentumLength, gamma);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -4, 7, 1, -2, 8 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -91d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Arma"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Arma"]); } }
        }
        return expected.Outputs["Arma"];
    }
    [Fact]
    public void WideLagDeviationAndBothPartialMeansMatchRationalStages()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 }) foreach (var length in new[] { 1, 3, 7 }) foreach (var lag in new[] { 1, 7 }) foreach (var gamma in new[] { -1d, 0, 3 })
            Check(Bars(Enumerable.Range(0, 19).Select(i => (i % 7 - 3) * scale)), length, lag, gamma);
    }
    [Fact]
    public void HandLagAndPartialWindowWarmupStayIndependent()
    {
        Assert.Equal(new[] { 1d, 1, 4.375 }, Check(Bars(new[] { 1d, 2, 10 }), 2));
        Assert.Equal(new[] { 1d, 1.25, 2 }, Check(Bars(new[] { 1d, 2, 3 }), 2, 7, 0));
        Assert.Equal(new[] { 5d, 5, 5, 5 }, Check(Bars(Enumerable.Repeat(5d, 4)), 3));
        Check(Bars(new[] { 1d, 2, 10, -4, 3, 8, 1, 20, -9, 1, 2, 3 }), 2, 7);
    }
    [Fact]
    public void ExtendedDeviationDoesNotPoisonZeroGainOrOverflowedState()
    {
        var prices = new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue }.Concat(Enumerable.Repeat(0d, 16));
        var values = Check(Bars(prices), 3, 2, 0); Assert.All(values, value => Assert.True(double.IsFinite(value))); Assert.Equal(0, values[^1]);
        var extended = Check(Bars(new[] { 1d, 2, 3, 0 }), 1, 7, -double.MaxValue); Assert.Contains(extended, double.IsInfinity); Assert.All(extended, value => Assert.False(double.IsNaN(value)));
    }
    [Fact]
    public void BothExtremePeriodsGrowOnlyWithObservedHistory()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var position in new[] { 0, 1 })
        { var length = position == 0 ? period : 3; var lag = position == 1 ? period : 7; Check(Array.Empty<Bar>(), length, lag); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), length, lag); }
    }
    [Fact]
    public void StrictRoundedThresholdsAndSignedGammaKeepBranchOrder()
    {
        Assert.Equal(new[] { 1d, 1 }, Check(Bars(new[] { 1d, 7 }), 1));
        Assert.Equal(new[] { 1d, 1 }, Check(Bars(new[] { 1d, -5 }), 1));
        foreach (var gamma in new[] { double.Epsilon, -double.Epsilon, -3d, double.MaxValue }) Check(Bars(new[] { 1d, 2, -1, 3, 0 }), 2, 3, gamma);
    }
    [Fact]
    public void SelectedCallerPricesNeedNoCustomAverageCallbacks()
    {
        var bars = Bars(new[] { 2d, 4, 6, 8, 10, 12 }); var selected = new[] { -2d, 0, 4, 3, -1, 8 }; var expected = BuiltInFormulaReferences.AutonomousRecursiveValues(Bars(selected), 2, 3, 1);
        foreach (var fast in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm((_, _) => throw new InvalidOperationException("The two partial means are internal formula stages.")); using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected.ToList());
            if (fast) { using var output = IndicatorCompute.ComputeAutonomousRecursiveMaFast(data, context, 2, 3, 1); Assert.Equal(expected.Outputs["Arma"], output.ToArray()); }
            else { data.CalculateAutonomousRecursiveMovingAverage(2, 3, 1); Assert.Equal(expected.Outputs["Arma"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); }
            Assert.Equal(0, ComponentAverage.Requests); Assert.Equal(0, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void CoreRegistryAndInPlaceSpansUseTheSameDefaultLag()
    {
        foreach (var length in new[] { 1, 2, 7, int.MaxValue }) foreach (var bars in new[] { Array.Empty<Bar>(), Bars(new[] { 1d, 2, 10, -4, 3, 8, 1, 20, -9 }), Bars(new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, 0 }) })
        {
            var prices = bars.Select(b => b.Close).ToArray(); var expected = BuiltInFormulaReferences.AutonomousRecursiveValues(bars, length, 7, 3).Outputs["Arma"]; var output = new double[prices.Length];
            OoplesFinance.StockIndicators.Core.MovingAverageCore.AutonomousRecursiveMovingAverage(prices, output, length); Assert.Equal(expected, output);
            var inPlace = prices.ToArray(); OoplesFinance.StockIndicators.Core.MovingAverageCore.AutonomousRecursiveMovingAverage(inPlace, inPlace, length); Assert.Equal(expected, inPlace);
            Assert.Equal(expected, CalculationsHelper.GetMovingAverageList(Data(bars), MovingAvgType.AutonomousRecursiveMovingAverage, length, prices.ToList()));
        }
        Assert.Throws<ArgumentException>(() => OoplesFinance.StockIndicators.Core.MovingAverageCore.AutonomousRecursiveMovingAverage(new[] { 1d }, Array.Empty<double>(), 2));
    }
    [Fact]
    public void InvalidCandlesAndGammaCannotAdvanceLagOrMeans()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AutonomousRecursiveMovingAverageState(gamma: invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateAutonomousRecursiveMovingAverage(gamma: invalid));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeAutonomousRecursiveMaFast(Data(Array.Empty<Bar>()), context, gamma: invalid));
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                using var state = new AutonomousRecursiveMovingAverageState(2, 3, 1); using var control = new AutonomousRecursiveMovingAverageState(2, 3, 1);
                foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
                var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
                foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, true).Value, state.Update(Native(bar), true, true).Value);
            }
        }
    }
}
