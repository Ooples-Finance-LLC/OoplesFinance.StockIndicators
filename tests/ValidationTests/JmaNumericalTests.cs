using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Core.Registry;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class JmaNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static Bar[] Prices(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static OhlcvBar Native(Bar b) => new("JMA", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(Jma)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRoundedStages(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.JmaOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length = 7, double phase = 50, double power = 2)
    {
        var expected = BuiltInFormulaReferences.JmaValues(bars, length, phase, power);
        var batch = Data(bars).CalculateJurikMovingAverage(length, phase, power);
        Assert.Equal(expected.Outputs["Jma"], batch.ChainedValues); Assert.Equal(expected.Signals, batch.SignalsList);
        Assert.Equal(expected.Outputs["Jma"], batch.OutputValues["Jma"]);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeJmaFast(Data(bars), context, length, phase, power); Assert.Equal(expected.Outputs["Jma"], fast.ToArray());
        var state = new JurikMovingAverageState(length, phase, power); var window = new JmaWindow(length, phase, power);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(Prices(new[] { 19d })[0]), true, false); window.Next(-7, true); state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Prices(new[] { 99d })[0]), false, false); window.Next(-99, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final);
                    Assert.Equal(expected.Outputs["Jma"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Jma"]); Assert.Equal(point.Value, direct.Value); Assert.Equal(expected.Signals[i], direct.Trade);
                }
            }
        }
        return expected.Outputs["Jma"];
    }
    [Fact]
    public void UnitPeriodAndZeroPowerHaveKnownValues()
    {
        var prices = new[] { 2d, -4, 0, 7, 1 }; var bars = Prices(prices);
        Assert.Equal(prices, Check(bars, 1));
        var wide = new[] { double.MaxValue, double.Epsilon, -double.MaxValue, -double.Epsilon, 1, 0 };
        Assert.Equal(wide, Check(Prices(wide), 1));
        Assert.Equal(wide, Check(Prices(wide), 7, power: double.MaxValue));
        foreach (var period in new[] { 1, 7, int.MaxValue }) Assert.All(Check(bars, period, power: 0), v => Assert.Equal(0, v));
        Assert.All(Check(Prices(new double[12])), v => Assert.Equal(0, v));
    }
    [Fact]
    public void FirstImpulseHasThreeDerivedStageWeights()
    {
        ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x);
        ReferenceFraction Round(ReferenceFraction x) => x.RoundExtendedBinary64();
        var beta = 2.7 / 4.7; var alpha = beta * beta;
        var first = R(1 - alpha); var residual = Round((R(1) - first) * R(1 - beta));
        var expected = Round((first + R(2) * residual) * Round(R(1 - alpha) * R(1 - alpha))).ToDouble();
        var line = Check(Prices(new[] { 1d, 0, 0, 0, 0, 0 })); Assert.Equal(expected, line[0]); Assert.NotEqual(0, line[1]);
    }
    [Fact]
    public void ExtendedStagesRecoverAfterWideAndSubnormalPrices()
    {
        var wide = Prices(new[] { double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue }.Concat(Enumerable.Range(0, 80).Select(i => (double)(i % 7 - 3))));
        foreach (var phase in new[] { -100d, 0, 50, 100 }) foreach (var length in new[] { 1, 3, 7, 50 }) Check(wide, length, phase);
        Check(Prices(Enumerable.Range(0, 35).Select(i => (i % 7 - 3) * double.Epsilon)));
        Check(Prices(Enumerable.Range(0, 35).Select(i => i % 3 == 0 ? Math.BitIncrement(double.MaxValue / 2) : double.MaxValue / 2)));
        Check(Prices(Enumerable.Repeat(double.MaxValue, 35)), 7, 100);
    }
    [Fact]
    public void PeriodNormalizationPhaseClampsAndFinitePowerRemainIndependent()
    {
        var bars = Prices(new[] { 1d, -2, 7, 4, 0, 1, -3, 5 });
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) { Check(Array.Empty<Bar>(), period); Check(bars, period); }
        Assert.Equal(Check(bars, phase: -100), Check(bars, phase: -double.MaxValue));
        Assert.Equal(Check(bars, phase: 100), Check(bars, phase: double.MaxValue));
        foreach (var power in new[] { -1d, .5, 1, 2, 3, double.MaxValue }) Check(bars, 7, -25, power);
    }
    [Fact]
    public void SelectedPricesAndNoAverageCallbacksArePreserved()
    {
        var bars = Prices(new[] { 1d, 2, 3, 4, 5 }); var selected = new[] { 7d, -3, 9, 0, 4 }; var expected = BuiltInFormulaReferences.JmaValues(Prices(selected), 7, 50, 2).Outputs["Jma"];
        using var armed = ComponentAverage.Arm((_, _) => throw new InvalidOperationException("JMA has no component average."));
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateJurikMovingAverage().ChainedValues);
        var rawData = Data(bars); rawData.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeJmaFast(rawData, context); Assert.Equal(expected, fast.ToArray()); Assert.Equal(selected, rawData.ChainedValues); Assert.Equal(0, ComponentAverage.Requests);
    }
    [Fact]
    public void InvalidParametersRejectEvenEmptyInput()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var phase in new[] { false, true })
        {
            var p = phase ? bad : 50; var power = phase ? 2 : bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateJurikMovingAverage(7, p, power));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeJmaFast(Data(Array.Empty<Bar>()), context, 7, p, power));
            Assert.Throws<ArgumentOutOfRangeException>(() => new JurikMovingAverageState(7, p, power));
        }
        foreach (var pair in new[] { (Length: 1, Power: -1d), (Length: 7, Power: -double.MaxValue) })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateJurikMovingAverage(pair.Length, 50, pair.Power));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeJmaFast(Data(Array.Empty<Bar>()), context, pair.Length, 50, pair.Power));
            Assert.Equal("power", Assert.Throws<ArgumentOutOfRangeException>(() => new JurikMovingAverageState(pair.Length, 50, pair.Power)).ParamName);
        }
    }
    [Fact]
    public void RejectedCandlesCannotAdvanceRecursiveStages()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            var state = new JurikMovingAverageState(); var control = new JurikMovingAverageState();
            foreach (var b in Prices(new[] { 2d, -1, 7 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 2d, 4, -1, 2, 1 }; values[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Prices(new[] { -4d, 3, 9, 0 })) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
    [Fact]
    public void RecursiveMemoryRetainsAllStagesAcrossLongTrajectories()
    {
        var bars = Prices(Enumerable.Range(0, 257).Select(i => (double)(i * 17 % 23 - 11)));
        foreach (var period in new[] { 2, 7, 31 }) Check(bars, period, 25, 3);
    }
    [Fact]
    public void CoreUsesPublicPowerStartupAndRegistryDefaults()
    {
        var prices = new[] { double.MaxValue, double.Epsilon, -double.MaxValue, 0, 4, -2, 7 };
        foreach (var length in new[] { 0, 1, 7, int.MaxValue }) foreach (var phase in new[] { -150d, 0, 50, 150 })
        {
            var expected = BuiltInFormulaReferences.JmaValues(Prices(prices), length, phase, 2).Outputs["Jma"]; var actual = new double[prices.Length];
            MovingAverageCore.JurikMovingAverage(prices, actual, length, phase); Assert.Equal(expected, actual);
        }
        var registry = MovingAverageRegistry.GetRequired(MovingAvgType.JurikMovingAverage); var output = new double[prices.Length];
        registry.Compute(prices, output, 7); Assert.Equal(BuiltInFormulaReferences.JmaValues(Prices(prices), 7, 50, 2).Outputs["Jma"], output);
        registry.ComputeWithVolume(prices, new double[prices.Length], output, 7); Assert.Equal(BuiltInFormulaReferences.JmaValues(Prices(prices), 7, 50, 2).Outputs["Jma"], output);
    }
    [Fact]
    public void CoreSpanBoundsAndExactInPlaceReplay()
    {
        var input = new[] { 2d, -4, 7, 0, 1 }; var output = Enumerable.Repeat(999d, input.Length + 2).ToArray();
        Assert.Throws<ArgumentException>(() => MovingAverageCore.JurikMovingAverage(input, output.AsSpan(0, 2), 7, 25)); Assert.All(output, v => Assert.Equal(999, v));
        MovingAverageCore.JurikMovingAverage(input, output, 7, 25);
        var expected = BuiltInFormulaReferences.JmaValues(Prices(input), 7, 25, 2).Outputs["Jma"]; Assert.Equal(expected, output.Take(input.Length)); Assert.Equal(999, output[^1]);
        var replay = input.ToArray(); MovingAverageCore.JurikMovingAverage(replay, replay, 7, 25); Assert.Equal(expected, replay);
        MovingAverageCore.JurikMovingAverage(Array.Empty<double>(), Array.Empty<double>());
        Assert.Throws<ArgumentOutOfRangeException>(() => MovingAverageCore.JurikMovingAverage(Array.Empty<double>(), Array.Empty<double>(), phase: double.NaN));
    }
}
