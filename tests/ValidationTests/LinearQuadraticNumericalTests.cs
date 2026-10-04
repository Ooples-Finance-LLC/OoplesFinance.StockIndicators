using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class LinearQuadraticNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("LQCD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(LinearQuadraticConvergenceDivergenceOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentFitDifferences(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.LinearQuadraticOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputPreservesFormula(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage, 1)]
    [InlineData(MovingAvgType.WeightedMovingAverage, 2)]
    [InlineData(MovingAvgType.ExponentialMovingAverage, 3)]
    [InlineData(MovingAvgType.WildersSmoothingMethod, 6)]
    public void ExtendedEndpointsPreserveIndependentHistogramAndPreview(MovingAvgType kind, int code)
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 })
        {
            var bars = new[] { -8, 8, 8, -1, 7, -6, 2, 3 }.Select((v, i) => B(v * scale, i)).ToArray();
            var expected = BuiltInFormulaReferences.LinearQuadraticOutputs(bars, 3, 2, code)["Lqcdo"];
            Assert.Equal(expected, Data(bars).CalculateLinearQuadraticConvergenceDivergenceOscillator(kind, 3, 2).CustomValuesList);
            using var context = new ComputeContext();
            using var fast = IndicatorCompute.ComputeLinearQuadraticConvergenceDivergenceOscillatorFast(Data(bars), context, 3, 2, kind);
            Assert.Equal(expected, fast.ToArray());
            using var state = new LinearQuadraticConvergenceDivergenceOscillatorState(kind, 3, 2);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Update(Native(B(17)), true, false); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(B(-39)), false, false);
                    Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(B(double.NaN)), true, false));
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                }
            }
        }
    }
    [Fact]
    public void LinearAndQuadraticHandsPreserveDoubleSignalSubtraction()
    {
        var bars = new[] { B(1), B(2, 1), B(4, 2) };
        // Three-point quadratic ends at 4; OLS ends at 23/6, each rounded first.
        var difference = 4 - 23d / 6;
        var expected = (difference - difference) - difference;
        Assert.Equal(expected, Data(bars).CalculateLinearQuadraticConvergenceDivergenceOscillator(length: 3, signalLength: 1).CustomValuesList[2]);
    }
    [Fact]
    public void MaximumPeriodsUseOnlyObservedHistory()
    {
        var bars = new[] { B(1), B(3, 1), B(2, 2), B(-1, 3) };
        var expected = BuiltInFormulaReferences.LinearQuadraticOutputs(bars, int.MaxValue, int.MaxValue, 1)["Lqcdo"];
        using var state = new LinearQuadraticWindow(MovingAvgType.SimpleMovingAverage, int.MaxValue, int.MaxValue);
        Assert.Equal(expected, bars.Select(b => state.Next(b.Close, true)).ToArray());
    }
    [Fact]
    public void SharedObservedFitsMatchExistingRingPaths()
    {
        using var linear = new ExactLinearFitWindow(3); using var lazyLinear = new ExactLinearFitWindow(3, true);
        using var quadratic = new QuadraticProjectionWindow(MovingAvgType.WeightedMovingAverage, 3);
        using var lazyQuadratic = new QuadraticProjectionWindow(MovingAvgType.WeightedMovingAverage, 3, observedHistory: true);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var value in new[] { 1d, 3d, -7d, 9d, 2d, -4d })
                foreach (var final in new[] { false, false, true })
                {
                    Assert.Equal(linear.Next(value, final).Last, lazyLinear.Next(value, final).Last);
                    Assert.Equal(quadratic.Next(value, final), lazyQuadratic.Next(value, final));
                }
            linear.Reset(); lazyLinear.Reset(); quadratic.Reset(); lazyQuadratic.Reset();
        }
    }
    [Fact]
    public void FastCallbacksRetainThreeCentersAndSignalRequest()
    {
        var bars = new[] { B(1), B(3, 1), B(7, 2), B(2, 3) };
        var calls = new List<int>();
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) =>
        { calls.Add(period); return values.Select(_ => 3d).ToArray(); };
        using var scope = ComponentAverage.Arm(Enumerable.Repeat(callback, 4).ToArray());
        using var context = new ComputeContext();
        using var result = IndicatorCompute.ComputeLinearQuadraticConvergenceDivergenceOscillatorFast(Data(bars), context, 3, 2);
        Assert.Equal(new[] { 3, 3, 3, 2 }, calls); Assert.Equal(4, ComponentAverage.Substitutions);
        Assert.Equal(bars.Length, result.Length);
    }
    [Fact]
    public void CoreMatchesPublicHistogramWithOverlappingSpans()
    {
        var bars = new[] { B(-3), B(7, 1), B(2, 2), B(-5, 3), B(11, 4) };
        var expected = BuiltInFormulaReferences.LinearQuadraticOutputs(bars, 3, 25, 1)["Lqcdo"];
        var input = bars.Select(b => b.Close).ToArray();
        OscillatorCore.LinearQuadraticConvergenceDivergenceOscillator(input, input, 3); Assert.Equal(expected, input);
        var sentinel = new[] { 17d, 19d };
        Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.LinearQuadraticConvergenceDivergenceOscillator(new[] { 1d, double.NaN }, sentinel, 3));
        Assert.Equal(new[] { 17d, 19d }, sentinel);
    }
}
