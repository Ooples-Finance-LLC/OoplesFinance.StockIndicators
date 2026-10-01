using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MassThrustNumericalTests
{
    private static Bar[] Candles(double[] prices, double[]? volumes = null) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, volumes?[i] ?? 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("MASS", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(MassThrust) || c.IndicatorType == typeof(MassThrustIndicator) || c.IndicatorType == typeof(MassThrustOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRatios(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.MassThrustOutputs(bars, (IBuiltInIndicator)c.Factory()), BuiltInFormulaReferences.MassThrustBudget);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Equal(double expected, double actual)
    {
        Assert.False(double.IsNaN(actual));
        if (expected == 0 || double.IsInfinity(expected)) Assert.Equal(expected, actual);
        else { Assert.Equal(Math.Sign(expected), Math.Sign(actual)); Assert.True(Math.Abs((actual - expected) / expected) <= 4e-15, $"Expected {expected:R}, actual {actual:R}"); }
    }
    private static Dictionary<string, double[]> Check(Bar[] bars, bool oscillator, int length = 2, MovingAvgType mean = MovingAvgType.ExponentialMovingAverage)
    {
        var kind = mean == MovingAvgType.SimpleMovingAverage ? 1 : mean == MovingAvgType.WeightedMovingAverage ? 2 : mean == MovingAvgType.WildersSmoothingMethod ? 6 : 3;
        var expected = BuiltInFormulaReferences.MassThrustValues(bars, length, kind, oscillator); var key = oscillator ? "Mto" : "Mti";
        var batch = oscillator ? Data(bars).CalculateMassThrustOscillator(mean, length) : Data(bars).CalculateMassThrustIndicator(mean, length);
        foreach (var output in expected.Outputs) for (var i = 0; i < bars.Length; i++) Equal(output.Value[i], batch.OutputValues[output.Key][i]);
        Assert.Equal(expected.Signals, batch.SignalsList);
        var core = new double[bars.Length]; var prices = bars.Select(b => b.Close).ToArray(); var volumes = bars.Select(b => b.Volume).ToArray();
        if (oscillator) OscillatorCore.MassThrustOscillator(prices, volumes, core, length); else TrendCore.MassThrust(prices, volumes, core, length);
        using var context = new ComputeContext(); using var fast = oscillator ? IndicatorCompute.ComputeMassThrustOscillatorFast(Data(bars), context, length) : IndicatorCompute.ComputeMassThrustIndicatorFast(Data(bars), context, length);
        using var signalFast = IndicatorCompute.ComputeMassThrustSignalFast(Data(bars), context, length, mean, oscillator);
        var fastValues = fast.ToArray(); var fastSignals = signalFast.ToArray();
        IStreamingIndicatorState native = oscillator ? new MassThrustOscillatorState(mean, length) : new MassThrustIndicatorState(mean, length);
        using var lifetime = (IDisposable)native;
        using var window = new MassThrustWindow(oscillator, mean, length);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var seed in Candles(new[] { 8d, -2, 5, 3, 9 })) { native.Update(Native(seed), true, false); window.Next(seed.Close, seed.Volume, true); }
            native.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                Equal(expected.Outputs[key][i], core[i]); Equal(core[i], fastValues[i]); Equal(expected.Outputs["Signal"][i], fastSignals[i]);
                var decoy = Candles(new[] { -99d })[0]; native.Update(Native(decoy), false, false); window.Next(-99, 1, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, bars[i].Volume, final);
                    Equal(expected.Outputs[key][i], point.Value); Equal(point.Value, point.Outputs![key]); Equal(expected.Outputs["Signal"][i], point.Outputs["Signal"]);
                    Equal(point.Value, direct.Value); Assert.Equal(expected.Signals[i], direct.Trade);
                }
            }
        }
        return expected.Outputs;
    }
    [Theory, InlineData(false), InlineData(true)]
    public void HandValuesAndTinyOrOverflowingMoves(bool oscillator)
    {
        var key = oscillator ? "Mto" : "Mti";
        foreach (var prices in new[] { new[] { 0d, 1, 0 }, new[] { 0d, double.Epsilon, 0 }, new[] { -double.MaxValue, double.MaxValue, -double.MaxValue } })
        {
            var actual = Check(Candles(prices), oscillator);
            Assert.Equal(new[] { 0d, oscillator ? 100 : .000001, 0 }, actual[key]);
            Equal(oscillator ? 50 : .0000005, actual["Signal"][1]);
        }
        var wideVolume = Check(Candles(new[] { 0d, 1, 0 }, new[] { double.MaxValue, double.MaxValue, double.MaxValue }), oscillator);
        Assert.Equal(oscillator ? 100 : double.MaxValue / 1000000, wideVolume[key][1]);
    }
    [Theory, InlineData(false), InlineData(true)]
    public void SignedVolumesZeroDenominatorsAndWindowExpiry(bool oscillator)
    {
        var prices = new[] { 2d, 4, 1, 5, 5, 0, 3, 8, 1, 1, 4, 6, 3, 2, 9, 1 };
        foreach (var mean in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var length in new[] { 1, 2, 3, 7 })
        {
            Check(Candles(prices), oscillator, length, mean);
            Check(Candles(prices, new[] { 0d, 2, -3, 0, 5, -7, 1, 9, 2, 0, -4, 6, 8, 1, -2, 3 }), oscillator, length, mean);
        }
        Check(Candles(new[] { 0d, 1, 0, 0, 1, 0 }, new[] { 1d, 1, -1, 0, 1, -1 }), oscillator);
    }
    [Theory, InlineData(false), InlineData(true)]
    public void OverflowOutputRecoversAndSignalsKeepExtendedValues(bool oscillator)
    {
        var m = double.MaxValue; var e = double.Epsilon;
        foreach (var mean in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
            Check(Candles(new[] { 0d, e, m, -m, e, -e, 1, 2, 0, 1, 2, 3 }, new[] { 0d, m, m, m, 1, -1, 2, 3, 1, 1, 1, 1 }), oscillator, 3, mean);
    }
    [Theory, InlineData(false), InlineData(true)]
    public void ExtremePeriodsOnlyAllocateObservedHistory(bool oscillator)
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue })
        foreach (var mean in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Candles(new[] { 1d, 2, 0, 3 }), oscillator, length, mean); Check(Array.Empty<Bar>(), oscillator, length, mean); }
    }
    [Fact]
    public void SignedVolumePoleRetainsSubnormalDenominatorAndRecovers()
    {
        var bars = Candles(new[] { 0d, 1, 0, 1, 1, 2, 1, 3 }, new[] { 0d, .5, -1, double.Epsilon, 1, 1, 1, 1 });
        var values = Check(bars, true, 3, MovingAvgType.SimpleMovingAverage);
        Assert.Equal(double.PositiveInfinity, values["Mto"][3]);
        Assert.Equal(-100, values["Mto"][4]); Assert.Equal(100, values["Mto"][5]);
        Assert.True(double.IsFinite(values["Signal"][6]));
    }
    [Theory, InlineData(false), InlineData(true)]
    public void PriceScaleCancelsFromNestedWeights(bool oscillator)
    {
        var prices = new[] { 0d, 1, 3, 2, 4, 0, 1, 3, 2 };
        var volumes = new[] { 1d, 2, 3, 1, 5, 7, 2, 4, 1 }; var key = oscillator ? "Mto" : "Mti";
        var expected = Check(Candles(prices, volumes), oscillator, 3);
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), Math.Pow(2, 1021) })
        {
            var actual = Check(Candles(prices.Select(v => v * scale).ToArray(), volumes), oscillator, 3);
            Assert.Equal(expected[key], actual[key]); Assert.Equal(expected["Signal"], actual["Signal"]);
        }
    }
    [Theory, InlineData(false), InlineData(true)]
    public void RecursiveDecayMatchesExactClosedForm(bool oscillator)
    {
        var prices = new double[400]; prices[1] = 1;
        var result = Check(Candles(prices), oscillator, 2);
        // After index3 the rolling thrust is zero; EMA(2) multiplies its
        // previous state by exactly one third on each following bar.
        var amplitude = oscillator ? new ReferenceFraction(100) : new ReferenceFraction(1) / new ReferenceFraction(1000000);
        var expected = new ReferenceFraction(-11) * amplitude / new ReferenceFraction(18);
        for (var i = 3; i < prices.Length; i++)
        {
            Equal(expected.ToDouble(), result["Signal"][i]);
            expected /= new ReferenceFraction(3);
        }
    }
    [Fact]
    public void AliasFactoriesRetainPeriodAndBothOutputs()
    {
        var bars = Candles(new[] { 2d, 4, 1, 5, 0, 3, 8, 1 }, new[] { 1d, 2, 3, 4, 1, 3, 2, 5 });
        foreach (IBuiltInIndicator indicator in new IBuiltInIndicator[] { new MassThrust(length: 2), new MassThrustIndicator(length: 2), new MassThrustOscillator(length: 2) })
        {
            var expected = BuiltInFormulaReferences.MassThrustOutputs(bars, indicator);
            var spec = new IndicatorSpec(indicator.BatchName, indicator.CreateOptions());
            foreach (var native in new[] { true, false })
            {
                var state = native ? StatefulIndicatorFactory.Create(spec) : StreamingIndicatorFactory.CreateState(spec);
                Assert.NotNull(state); using var lifetime = state as IDisposable;
                for (var i = 0; i < bars.Length; i++)
                {
                    var point = state.Update(Native(bars[i]), true, true); Assert.NotNull(point.Outputs);
                    foreach (var output in expected) Equal(output.Value[i], point.Outputs[output.Key]);
                    Equal(expected[indicator.BatchName == IndicatorName.MassThrustOscillator ? "Mto" : "Mti"][i], point.Value);
                }
            }
        }
    }
    [Fact]
    public void CoreSpanContractsAndUnitVolumeOverload()
    {
        var prices = new[] { 0d, 1, 0 }; var volume = new[] { 1d, 1, 1 }; var output = new[] { 777d, 777, 777, 777 };
        OscillatorCore.MassThrustOscillator(prices, output, 2); Assert.Equal(new[] { 0d, 100, 0, 777 }, output);
        var inPlace = prices.ToArray(); OscillatorCore.MassThrustOscillator(inPlace, volume, inPlace, 2); Assert.Equal(new[] { 0d, 100, 0 }, inPlace);
        inPlace = prices.ToArray(); TrendCore.MassThrust(inPlace, volume, inPlace, 2); Assert.Equal(new[] { 0d, .000001, 0 }, inPlace);
        Assert.Throws<ArgumentException>(() => TrendCore.MassThrust(prices, volume, new double[2]));
        Assert.Throws<ArgumentException>(() => OscillatorCore.MassThrustOscillator(prices, volume, new double[2]));
        Assert.Throws<ArgumentException>(() => TrendCore.MassThrust(prices, new double[2], output));
        Assert.Throws<ArgumentException>(() => OscillatorCore.MassThrustOscillator(prices, new double[2], output));
        TrendCore.MassThrust(Array.Empty<double>(), Array.Empty<double>(), output); Assert.Equal(777, output[3]);
    }
    [Theory, InlineData(false), InlineData(true)]
    public void NonfiniteInputIsRejectedBeforeStateChanges(bool oscillator)
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var output = new[] { 777d, 777 };
            Assert.Throws<ArgumentOutOfRangeException>(() => TrendCore.MassThrust(new[] { 1d, bad }, new[] { 1d, 1 }, output));
            Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.MassThrustOscillator(new[] { 1d, 2 }, new[] { 1d, bad }, output));
            Assert.Equal(new[] { 777d, 777 }, output);
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                IStreamingIndicatorState actual = oscillator ? new MassThrustOscillatorState() : new MassThrustIndicatorState();
                IStreamingIndicatorState expected = oscillator ? new MassThrustOscillatorState() : new MassThrustIndicatorState();
                using var a = (IDisposable)actual; using var b = (IDisposable)expected;
                var first = Native(Candles(new[] { 1d })[0]); actual.Update(first, true, true); expected.Update(first, true, true);
                var values = new[] { 2d, 2, 2, 2, 1 }; values[field] = bad;
                var broken = new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4]);
                Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(broken), final, true));
                var next = Native(Candles(new[] { 2d })[0]); Equal(expected.Update(next, true, true).Value, actual.Update(next, true, true).Value);
            }
        }
    }
}
