using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RobustWeightingNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RWO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(RobustWeightingOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentCenteredResidual(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.RobustWeightingOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveRegression(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void LinearHandHasOnlyThePreservedStartupResidual()
    {
        var bars = new[] { 1d, 3, 5, 7, 9 }.Select((v,i) => B(v,i)).ToArray();
        var result = Data(bars).CalculateRobustWeightingOscillator(length: 2);
        Assert.Equal(new[] { 0d, .5, 0, 0, 0 }, result.CustomValuesList);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.StrongSell, Signal.None, Signal.None }, result.SignalsList);
        Assert.Equal(result.CustomValuesList, BuiltInFormulaReferences.RobustWeightingOutputs(bars, 2, 1)["Rwo"]);
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage, 1)]
    [InlineData(MovingAvgType.WeightedMovingAverage, 2)]
    [InlineData(MovingAvgType.ExponentialMovingAverage, 3)]
    [InlineData(MovingAvgType.WildersSmoothingMethod, 6)]
    public void ExtremeResidualsPreserveCentersExpirySignalsAndPreview(MovingAvgType kind, int code)
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 })
        {
            var bars = Enumerable.Range(0, 23).Select(i => B((i % 7 + 1) * (i % 3 == 0 ? -scale : scale), i)).ToArray();
            var signals = new List<Signal>(); var expected = BuiltInFormulaReferences.RobustWeightingOutputs(bars, 3, code, signals)["Rwo"];
            var result = Data(bars).CalculateRobustWeightingOscillator(kind, 3);
            Assert.Equal(expected, result.CustomValuesList); Assert.Equal(signals, result.SignalsList);
            using var state = new RobustWeightingOscillatorState(kind, 3);
            for (var pass = 0; pass < 2; pass++)
            {
                state.Update(Native(B(17)), true, false); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(B(-19)), false, false);
                    Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(B(double.NaN)), true, false));
                    foreach (var final in new[] { false, false, true })
                    {
                        var point = state.Update(Native(bars[i]), final, true);
                        Assert.Equal(expected[i], point.Value); Assert.Equal(expected[i], point.Outputs!["Rwo"]);
                    }
                }
            }
        }
    }
    [Fact]
    public void CoreMatchesPublicFormulaAndProtectsOverlappingSpans()
    {
        var prices = new[] { double.MaxValue, -double.MaxValue, 1d, -1d, 5d, 2d, 7d };
        var expected = BuiltInFormulaReferences.RobustWeightingOutputs(prices.Select((v,i) => B(v,i)).ToArray(), 3, 1)["Rwo"];
        var overlap = new double[prices.Length + 1]; prices.CopyTo(overlap, 0);
        OscillatorCore.RobustWeightingOscillator(overlap.AsSpan(0, prices.Length), overlap.AsSpan(1), 3);
        Assert.Equal(expected, overlap.Skip(1));
        var output = Enumerable.Repeat(17d, 3).ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.RobustWeightingOscillator(new[] { 1d, double.NaN, 2 }, output, 3));
        Assert.Equal(new[] { 17d, 17d, 17d }, output);
    }
    [Fact]
    public void HugePeriodsAllocateOnlyObservedHistoryAndLengthOneIsZero()
    {
        using var huge = new RobustWeightingWindow(MovingAvgType.SimpleMovingAverage, int.MaxValue);
        Assert.Equal(0, huge.Next(double.MaxValue, true).Value);
        Assert.Equal(0, huge.Next(-double.MaxValue, true).Value);
        var bars = new[] { B(double.MaxValue), B(-double.MaxValue,1), B(double.Epsilon,2) };
        Assert.Equal(new double[3], Data(bars).CalculateRobustWeightingOscillator(length: 1).CustomValuesList);
    }
    [Fact]
    public void ThreeCallbacksKeepPeriodsAndFastCallerState()
    {
        var bars = Enumerable.Range(0, 8).Select(i => B(i+1,i)).ToArray();
        foreach (var fast in new[] { false, true })
        {
            var data = Data(bars); var values = data.CustomValuesList; var outputs = data.OutputValues; var signals = data.SignalsList; var name = data.IndicatorName;
            var periods = new List<int>(); var calls = 0;
            Func<IReadOnlyList<double>,int,IReadOnlyList<double>> callback = (input, period) =>
            {
                periods.Add(period); calls++;
                if (calls == 3) Assert.Equal(new[] { 0d, 1, 2, 2, 2, 2, 2, 2 }, input);
                return input.Select(_ => (double)calls).ToArray();
            };
            using var scope = ComponentAverage.Arm(Enumerable.Repeat(callback, 3).ToArray());
            if (fast)
            {
                using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeRobustWeightingOscillatorFast(data, context, 3);
                Assert.Equal(Enumerable.Repeat(3d, bars.Length), result.ToArray());
                Assert.Same(values,data.CustomValuesList); Assert.Same(outputs,data.OutputValues); Assert.Same(signals,data.SignalsList); Assert.Equal(name,data.IndicatorName);
            }
            else Assert.Equal(Enumerable.Repeat(3d, bars.Length), data.CalculateRobustWeightingOscillator(length:3).CustomValuesList);
            Assert.Equal(new[] {3,3,3},periods); Assert.Equal(3,ComponentAverage.Substitutions);
        }
    }
}
