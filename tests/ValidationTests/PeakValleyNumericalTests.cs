using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PeakValleyNumericalTests
{
    private static Bar[] Bars(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("PVE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 1;
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(PeakValleyEstimation)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentEvents(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.PeakValleyOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Check(double[] prices, int length = 3, int smooth = 4, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var bars = Bars(prices); var expected = BuiltInFormulaReferences.PeakValleyValues(bars, length, smooth, Kind(kind));
        var batch = Data(bars).CalculatePeakValleyEstimation(kind, length, smooth);
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key], batch.OutputValues[key]);
            using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputePeakValleyEstimationFast(Data(bars), context, length, smooth, kind, key);
            Assert.Equal(expected[key], fast.ToArray());
        }
        Assert.Equal(expected["Sign1"], batch.CustomValuesList);
        Assert.Equal(expected["Sign1"].Select(v => v > 0 ? Signal.Buy : v < 0 ? Signal.Sell : Signal.None), batch.SignalsList);
        if (kind == MovingAvgType.SimpleMovingAverage)
        {
            var output = Enumerable.Repeat(123d, prices.Length + 2).ToArray();
            OscillatorCore.PeakValleyEstimation(prices, output.AsSpan(1, prices.Length), length, smooth);
            Assert.Equal(expected["Sign1"], output.Skip(1).Take(prices.Length)); Assert.Equal(123, output[0]); Assert.Equal(123, output[output.Length - 1]);
        }
        using var state = new PeakValleyEstimationState(kind, length, smooth); using var window = new PeakValleyWindow(kind, length, smooth);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var b in Bars(new[] { 7d, -2, 1 })) { state.Update(Native(b), true, false); window.Next(b.Close, true); }
            state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -100d })[0]), false, false); window.Next(-100, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(prices[i], final);
                    Assert.Equal(expected["Sign1"][i], point.Value);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], point.Outputs![key]);
                    Assert.Equal((expected["Sign1"][i], expected["Sign2"][i], expected["Sign3"][i]), direct);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void IndependentThreeEventHands()
    {
        var values = Check(new[] { 2d, 4, 2, 8, 0, 1 }, 2, 1);
        Assert.Equal(new[] { -1d, 0, 1, 0, 0, 0 }, values["Sign1"]);
        Assert.Equal(new[] { 0d, -1, 0, 0, 0, -1 }, values["Sign2"]);
        Assert.Equal(new[] { 0d, -1, 0, 0, 0, -1 }, values["Sign3"]);
    }
    [Fact]
    public void NegativeRegressionMaximumIsNotClippedToZero()
    {
        var values = Check(new[] { 0d, 4, 0, 0, 0, 1 }, 2, 4);
        Assert.Equal(new[] { 0d, -1, 0, 0, 0, -1 }, values["Sign1"]);
        Assert.All(values["Sign2"], v => Assert.Equal(0, v)); Assert.All(values["Sign3"], v => Assert.Equal(0, v));
    }
    [Fact]
    public void OverflowingResidualsAndFitsStillProduceExactVotes()
    {
        var pattern = new[] { 1d, -1, 1, 0, -1, 1, 1, -1 };
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            var baseline = Check(pattern, 3, 4, kind);
            foreach (var scale in new[] { double.MaxValue, double.Epsilon })
            {
                var actual = Check(pattern.Select(v => v * scale).ToArray(), 3, 4, kind);
                foreach (var key in baseline.Keys) Assert.Equal(baseline[key], actual[key]);
            }
        }
    }
    [Fact]
    public void ThresholdAndPeakTiesRemainExact()
    {
        foreach (var price in new[] { Math.BitDecrement(4d), 4d, Math.BitIncrement(4d), Math.BitDecrement(5d), 5d })
        {
            var bars = Bars(new[] { 5d, price }); var expected = BuiltInFormulaReferences.PeakValleyValues(bars, 2, 1, externalMean: new double[2]);
            foreach (var key in expected.Keys)
            {
                using var armed = ComponentAverage.Arm(new List<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>> { (_, _) => new double[2] });
                using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputePeakValleyEstimationFast(Data(bars), context, 2, 1, outputKey: key);
                Assert.Equal(expected[key], actual.ToArray());
                if (key == "Sign2") Assert.Equal(price < 4 ? -1d : 0, actual.ToArray()[1]);
                if (key == "Sign3") Assert.Equal(price < 5 ? -1d : 0, actual.ToArray()[1]);
            }
        }
    }
    [Fact]
    public void OneUlpBelowPeakDoesNotStartAnEvent()
    {
        foreach (var last in new[] { Math.BitDecrement(5d), 5d })
        {
            var bars = Bars(new[] { 5d, 3, last });
            using var armed = ComponentAverage.Arm(new List<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>> { (_, _) => new double[3] });
            using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputePeakValleyEstimationFast(Data(bars), context, 3, 1);
            Assert.Equal(new[] { -1d, 0, last == 5 ? -1d : 0 }, actual.ToArray());
        }
    }
    [Fact]
    public void ExpiryPreviewResetAndLazyPeriods()
    {
        var prices = new[] { 0d, 4, 0, 0, 0, 1, -2, 7, 1, 5, -3, 0 };
        foreach (var period in new[] { int.MinValue, 0, 1, 2, 5, int.MaxValue - 1, int.MaxValue })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Array.Empty<double>(), period, period, kind); Check(prices, period, period, kind); }
        var prefix = Check(prices.Take(7).ToArray()); var full = Check(prices);
        foreach (var key in prefix.Keys) Assert.Equal(prefix[key], full[key].Take(7));
    }
    [Fact]
    public void CoreSpanValidationPrecedesOutputWrites()
    {
        var output = new[] { 123d, 456d };
        Assert.Throws<ArgumentException>(() => OscillatorCore.PeakValleyEstimation(new[] { 1d, 2 }, new double[1]));
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var prices = new[] { 1d, bad };
            Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.PeakValleyEstimation(prices, output)); Assert.Equal(new[] { 123d, 456d }, output);
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Bars(prices)).CalculatePeakValleyEstimation());
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputePeakValleyEstimationFast(Data(Bars(prices)), context));
        }
    }
    [Fact]
    public void RejectedCandlesCannotAdvanceHistory()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new PeakValleyEstimationState(length: 2, smoothLength: 4); using var control = new PeakValleyEstimationState(length: 2, smoothLength: 4);
            foreach (var b in Bars(new[] { 0d, 4, 0 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var input = new[] { 1d, 3, -1, 1, 1 }; input[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, input[0], input[1], input[2], input[3], input[4])), final, true));
            foreach (var b in Bars(new[] { 0d, 0, 1, 3 }))
            { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]); }
        }
    }
    [Fact]
    public void ComponentMeanReceivesSelectedPricesAndPeriod()
    {
        var bars = Bars(new[] { 1d, 7, 2, 5, 4 }); var selected = new[] { 3d, -1, 5, 2, 7 }; var mean = new[] { 0d, .5, -1, 2, 4 };
        var expected = BuiltInFormulaReferences.PeakValleyValues(bars, 2, 3, selected: selected, externalMean: mean);
        foreach (var key in expected.Keys)
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList());
            using var armed = ComponentAverage.Arm(new List<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>> { (values, period) => { Assert.Equal(selected, values); Assert.Equal(2, period); return mean; } });
            using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputePeakValleyEstimationFast(data, context, 2, 3, outputKey: key);
            Assert.Equal(expected[key], actual.ToArray()); Assert.Equal(1, ComponentAverage.Requests); Assert.Equal(1, ComponentAverage.Substitutions); Assert.Equal(selected, data.ChainedValues);
        }
    }
}
