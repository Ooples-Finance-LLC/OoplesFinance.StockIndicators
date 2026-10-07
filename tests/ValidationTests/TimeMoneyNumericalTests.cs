using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TimeMoneyNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("TM", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(TimeAndMoneyChannel)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentDelayedVariance(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.TimeMoneyOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveAllSevenOutputs(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void OverflowingReturnsAndWidthsStillProduceFiniteBands()
    {
        var bars = new[] { double.Epsilon, double.Epsilon, double.MaxValue, double.MaxValue, double.Epsilon, double.Epsilon, 1d }
            .Select((v, i) => B(v, i)).ToArray();
        var expected = BuiltInFormulaReferences.TimeMoneyOutputs(bars, 1, 2, 1);
        var actual = Data(bars).CalculateTimeAndMoneyChannel(length1: 1, length2: 2);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], actual.OutputValues[key]);
        Assert.True(double.IsPositiveInfinity(actual.OutputValues["Median"][4]));
        Assert.True(double.IsFinite(actual.OutputValues["Ch+1"][4]));
        Assert.True(actual.OutputValues["Ch+1"][4] > double.MaxValue / 4);
        using var context = new ComputeContext();
        using var fast = IndicatorCompute.ComputeTimeAndMoneyChannelFast(Data(bars), context, 1, 2, outputKey: "Ch+1");
        Assert.Equal(expected["Ch+1"], fast.ToArray());
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage, 1)]
    [InlineData(MovingAvgType.WeightedMovingAverage, 2)]
    [InlineData(MovingAvgType.ExponentialMovingAverage, 3)]
    [InlineData(MovingAvgType.WildersSmoothingMethod, 6)]
    public void MeansVarianceSignalsPreviewAndResetRetainTheirStages(MovingAvgType kind, int code)
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 16 })
        {
            var bars = Enumerable.Range(0, 18).Select(i => B((i % 5 + 1) * (i % 3 == 0 ? -scale : scale), i)).ToArray();
            var signals = new List<Signal>(); var expected = BuiltInFormulaReferences.TimeMoneyOutputs(bars, 3, 4, code, signals);
            var actual = Data(bars).CalculateTimeAndMoneyChannel(kind, 3, 4);
            foreach (var key in expected.Keys) Assert.Equal(expected[key], actual.OutputValues[key]);
            Assert.Equal(signals, actual.SignalsList);
            using var state = new TimeAndMoneyChannelState(kind, 3, 4);
            for (var pass = 0; pass < 2; pass++)
            {
                state.Update(Native(B(17)), true, false); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(B(-23)), false, false);
                    Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(B(double.NaN)), true, false));
                    foreach (var final in new[] { false, false, true })
                    {
                        var point = state.Update(Native(bars[i]), final, true);
                        foreach (var key in expected.Keys) Assert.Equal(expected[key][i], point.Outputs![key]);
                        Assert.Equal(expected["Median"][i], point.Value);
                    }
                }
            }
        }
    }
    [Fact]
    public void SingleObservationVarianceLeavesThePriceBandsUnchanged()
    {
        var bars = new[] { B(1), B(3, 1), B(-2, 2), B(7, 3), B(2, 4) };
        var actual = Data(bars).CalculateTimeAndMoneyChannel(length1: 1, length2: 1);
        Assert.Equal(new double[bars.Length], actual.OutputValues["Median"]);
        foreach (var key in TimeMoneyWindow.Keys.Where(k => k != "Median")) Assert.Equal(bars.Select(b => b.Close), actual.OutputValues[key]);
    }
    [Fact]
    public void MaximumPeriodsUseObservedHistoryAndTheExistingLagCap()
    {
        var bars = Enumerable.Range(0, 1065).Select(i => B(1 + i % 7, i)).ToArray();
        var expected = BuiltInFormulaReferences.TimeMoneyOutputs(bars, int.MaxValue, 2, 3);
        using var state = new TimeMoneyWindow(MovingAvgType.ExponentialMovingAverage, int.MaxValue, 2);
        var actual = bars.Select(b => state.Next(b.Close, true).Outputs[6]).ToArray();
        Assert.Equal(expected["Median"], actual); Assert.Contains(actual, value => value > 0);
        using var flat = new TimeMoneyWindow(MovingAvgType.SimpleMovingAverage, int.MaxValue, int.MaxValue);
        Assert.All(flat.Next(3, true).Outputs, value => Assert.Equal(0, value));
    }
    [Fact]
    public void FourCallbackStagesRetainPeriodsAndFastCallerState()
    {
        var bars = Enumerable.Range(0, 12).Select(i => B(1 + i % 5, i)).ToArray();
        foreach (var fast in new[] { false, true })
        {
            var periods = new List<int>(); var data = Data(bars); var input = data.CustomValuesList;
            var outputs = data.OutputValues; var signals = data.SignalsList; var name = data.IndicatorName;
            Func<IReadOnlyList<double>,int,IReadOnlyList<double>> callback = (values, period) =>
            { periods.Add(period); return values.Select(_ => 3d).ToArray(); };
            using var scope = ComponentAverage.Arm(Enumerable.Repeat(callback, 4).ToArray());
            if (fast)
            {
                using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeTimeAndMoneyChannelFast(data, context, 3, 5);
                Assert.Equal(Enumerable.Repeat(3d, bars.Length), result.ToArray());
                Assert.Same(input, data.CustomValuesList); Assert.Same(outputs, data.OutputValues);
                Assert.Same(signals, data.SignalsList); Assert.Equal(name, data.IndicatorName);
            }
            else Assert.Equal(Enumerable.Repeat(3d, bars.Length), data.CalculateTimeAndMoneyChannel(length1: 3, length2: 5).OutputValues["Median"]);
            Assert.Equal(new[] { 3, 5, 5, 3 }, periods); Assert.Equal(4, ComponentAverage.Substitutions);
        }
    }
}
