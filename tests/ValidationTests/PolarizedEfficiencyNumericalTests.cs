using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PolarizedEfficiencyNumericalTests
{
    private static Bar[] Bars(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("PFE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 1;
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(Pfe) || c.IndicatorType == typeof(PolarizedFractalEfficiency)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    private static IReadOnlyDictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) => indicator.CreateOptions() switch
    {
        PfeSpecOptions alias => BuiltInFormulaReferences.PolarizedEfficiencyOutputs(bars, alias.Length, 5),
        PolarizedFractalEfficiencySpecOptions full => BuiltInFormulaReferences.PolarizedEfficiencyOutputs(bars, full.Length, full.SmoothLength, Kind(full.MaType)),
        _ => throw new InvalidOperationException()
    };
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentGeometry(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourcePreservesFormula(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var indicator = c.Factory(); var builtIn = (IBuiltInIndicator)indicator;
        var (length, smooth, kind) = builtIn.CreateOptions() switch
        {
            PfeSpecOptions alias => (alias.Length, 5, MovingAvgType.ExponentialMovingAverage),
            PolarizedFractalEfficiencySpecOptions full => (full.Length, full.SmoothLength, full.MaType),
            _ => throw new InvalidOperationException()
        };
        var count = Math.Max(64, indicator.WarmupBars + 8);
        var selected = Enumerable.Range(0, count).Select(i => i % 2 == 0 ? -(double)i : i + 1d).ToArray();
        var bars = Bars(Enumerable.Repeat(100d, count).ToArray());
        var expected = Expected(Bars(selected), builtIn)["Pfe"];
        Assert.Contains(expected, value => value != 0); // Constant original closes have zero directional efficiency.
        var data = Data(bars); data.SetCustomValues(selected.ToList());
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputePolarizedFractalEfficiencyFast(data, context, length, smooth, kind);
        Assert.Equal(expected, fast.ToArray()); Assert.Equal(selected, data.ChainedValues);
        data.CalculatePolarizedFractalEfficiency(kind, length, smooth);
        Assert.Equal(expected, data.CustomValuesList); Assert.Equal(expected, data.OutputValues["Pfe"]);
        Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(double[] prices, int length = 3, int smooth = 2, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var bars = Bars(prices); var expected = BuiltInFormulaReferences.PolarizedEfficiencyOutputs(bars, length, smooth, Kind(kind))["Pfe"];
        var batch = Data(bars).CalculatePolarizedFractalEfficiency(kind, length, smooth);
        Assert.Equal(expected, batch.OutputValues["Pfe"]); Assert.Equal(expected, batch.CustomValuesList);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.InRange(expected[i], -100, 100); var v = expected[i]; var p = i == 0 ? 0 : expected[i - 1];
            Assert.Equal(v > 0 && v > p ? Signal.StrongBuy : v < 0 && v < p ? Signal.StrongSell : v > 0 ? Signal.Buy : v < 0 ? Signal.Sell : Signal.None, batch.SignalsList[i]);
        }
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputePolarizedFractalEfficiencyFast(Data(bars), context, length, smooth, kind); Assert.Equal(expected, fast.ToArray());
        if (kind == MovingAvgType.ExponentialMovingAverage)
        { var core = new double[prices.Length]; OscillatorCore.PolarizedFractalEfficiency(prices, core, length, smooth); Assert.Equal(expected, core); }
        using var state = new PolarizedFractalEfficiencyState(kind, length, smooth);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var b in Bars(new[] { 7d, -2, 1 })) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -double.MaxValue })[0]), false, false);
                foreach (var final in new[] { false, false, true })
                { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], point.Value); Assert.Equal(expected[i], point.Outputs!["Pfe"]); }
            }
        }
        return expected;
    }
    [Fact]
    public void IndependentGeometryHandsAndTriangleBound()
    {
        Assert.Equal(new[] { 0d, 100, -100, 0 }, Check(new[] { 0d, 4, 0, 0 }, 1, 1));
        Assert.Equal(new[] { 0d, 0, 0, 100 }, Check(new[] { 0d, 5, 10, 15 }, 3, 1));
        Assert.Equal(new[] { 0d, 0, 0, -100 }, Check(new[] { 0d, -5, -10, -15 }, 3, 1));
        // Direct distance is5; walked path is1+1+sqrt(17).
        var expected = (new ReferenceFraction(500) / (new ReferenceFraction(2) + ReferenceFraction.FromDouble(Math.Sqrt(17)))).ToDouble();
        Assert.Equal(expected, Check(new[] { 0d, 0, 0, 4 }, 3, 1)[3]);
        Assert.Equal(ExactVarianceWindow.Units(5), PolarizedEfficiencyWindow.Distance(3, 0, 4));
        Assert.Equal(ExactVarianceWindow.Units(double.MaxValue) << 1, PolarizedEfficiencyWindow.Distance(double.MaxValue, -double.MaxValue, 1));
    }
    [Fact]
    public void ExtremeAndSubnormalPricesPreserveDirectionAndFinitePaths()
    {
        foreach (var scale in new[] { 1d, double.Epsilon, Math.Pow(2, 1020), double.MaxValue / 4 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
            Check(new[] { 0d, 3, -1, 1, 2, -2, 0, 1 }.Select(v => v * scale).ToArray(), 3, 2, kind);
        Check(new[] { double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue, 0, double.MaxValue }, 2, 1);
        Assert.Equal(100, Check(new[] { 0d, double.Epsilon }, 1, 1)[1]);
        Assert.Equal(-100, Check(new[] { 0d, -double.Epsilon }, 1, 1)[1]);
        Assert.All(Check(Enumerable.Repeat(double.MaxValue, 16).ToArray()), v => Assert.Equal(0, v));
        Assert.True(Check(new[] { 1d, 1, 1, Math.BitIncrement(1d) }, 3, 1)[3] > 0);
        Assert.True(Check(new[] { 1d, 1, 1, Math.BitDecrement(1d) }, 3, 1)[3] < 0);
    }
    [Fact]
    public void ExpiryPreviewResetAndExtremePeriodsStayLazy()
    {
        var prices = new[] { 0d, 4, -1, 1, 7, -3, 0, 2, 1, -2 };
        foreach (var period in new[] { int.MinValue, 0, 1, 2, 5, int.MaxValue - 1, int.MaxValue })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Array.Empty<double>(), period, period, kind); Check(prices, period, period, kind); Check(prices, 2, period, kind); }
        var prefix = Check(prices.Take(6).ToArray()); Assert.Equal(prefix, Check(prices).Take(6));
    }
    [Fact]
    public void RejectedPricesAndCandlesCannotAdvanceState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var badBars = Bars(new[] { 1d, bad });
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(badBars).CalculatePolarizedFractalEfficiency());
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputePolarizedFractalEfficiencyFast(Data(badBars), context));
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                using var state = new PolarizedFractalEfficiencyState(length: 3, smoothLength: 2); using var control = new PolarizedFractalEfficiencyState(length: 3, smoothLength: 2);
                foreach (var b in Bars(new[] { 0d, 3, -1 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
                var input = new[] { 1d, 3, -1, 1, 1 }; input[field] = bad;
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, input[0], input[1], input[2], input[3], input[4])), final, true));
                foreach (var b in Bars(new[] { 1d, -3, 0, 4 })) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
            }
        }
    }
    [Fact]
    public void CallbackReceivesRawEfficiencyAndSmoothingPeriod()
    {
        var bars = Bars(new[] { 1d, 7, 2, 5, 4 }); var selected = new[] { 3d, -1, 5, 2, 7 }; var replacement = new[] { 0d, .5, -1, 2, 4 };
        var raw = BuiltInFormulaReferences.PolarizedEfficiencyOutputs(Bars(selected), 3, 1)["Pfe"];
        var data = Data(bars); data.SetCustomValues(selected.ToList());
        using var armed = ComponentAverage.Arm(new List<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>> { (values, period) => { Assert.Equal(raw, values); Assert.Equal(2, period); return replacement; } });
        using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputePolarizedFractalEfficiencyFast(data, context, 3, 2);
        Assert.Equal(replacement, actual.ToArray()); Assert.Equal(1, ComponentAverage.Requests); Assert.Equal(1, ComponentAverage.Substitutions); Assert.Equal(selected, data.ChainedValues);
    }
    [Fact]
    public void CoreSpanBoundsAndInPlaceReplay()
    {
        var input = new[] { 2d, -4, 7, 0, 1 }; var output = Enumerable.Repeat(999d, input.Length + 2).ToArray();
        Assert.Throws<ArgumentException>(() => OscillatorCore.PolarizedFractalEfficiency(input, output.AsSpan(0, 2), 2, 2)); Assert.All(output, v => Assert.Equal(999, v));
        var expected = BuiltInFormulaReferences.PolarizedEfficiencyOutputs(Bars(input), 2, 2)["Pfe"];
        OscillatorCore.PolarizedFractalEfficiency(input, output, 2, 2); Assert.Equal(expected, output.Take(input.Length)); Assert.Equal(999, output[^1]);
        var replay = input.ToArray(); OscillatorCore.PolarizedFractalEfficiency(replay, replay, 2, 2); Assert.Equal(expected, replay);
        OscillatorCore.PolarizedFractalEfficiency(Array.Empty<double>(), Array.Empty<double>(), int.MaxValue, int.MaxValue);
    }
}
