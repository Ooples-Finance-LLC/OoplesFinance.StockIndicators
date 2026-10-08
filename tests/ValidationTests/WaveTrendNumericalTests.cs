using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class WaveTrendNumericalTests
{
    private static readonly string[] Keys = { "Wto", "Signal" };
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
        bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("WAVE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(WaveTrendOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void BothOutputsMatchIndependentExactMeans(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.WaveTrendOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedPricesFeedBothDirectOutputs(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var options = ((IBuiltInIndicator)c.Factory()).CreateOptions(); var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var bars = Enumerable.Range(0, 32).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 100, 110, -20, i + 20, 2)).ToArray();
        var selected = Enumerable.Range(0, bars.Length).Select(i => (double)(i * 7 % 11 - 5)).ToArray();
        var expected = BuiltInFormulaReferences.WaveTrendValues(selected.Select((v, i) => B(v, i)).ToArray(), MovingAvgType.ExponentialMovingAverage, length, 21, 4);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        foreach (var key in Keys)
        {
            using var output = IndicatorCompute.ComputeWaveTrendOscillatorFast(data, context, length, outputKey: key);
            Assert.Equal(expected.Outputs[key], output.ToArray()); Assert.Equal(selected, data.ChainedValues);
        }
        data.CalculateWaveTrendOscillator(length1: length);
        foreach (var key in Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]);
        Assert.Equal(expected.Signals, data.SignalsList); Assert.Equal(bars.Select(b => b.Open), data.OpenPrices);
    }
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage,
        int channel = 2, int average = 2, int signal = 2)
    {
        var expected = BuiltInFormulaReferences.WaveTrendValues(bars, kind, channel, average, signal);
        var data = Data(bars).CalculateWaveTrendOscillator(kind, channel, average, signal);
        foreach (var key in Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]);
        Assert.Equal(expected.Outputs["Wto"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        using var context = new ComputeContext();
        foreach (var key in Keys.Concat(new string[] { null! }))
        {
            using var output = IndicatorCompute.ComputeWaveTrendOscillatorFast(Data(bars), context, channel, average, signal, kind, key);
            Assert.Equal(expected.Outputs[key ?? "Wto"], output.ToArray());
        }
        using var native = new WaveTrendOscillatorState(kind, channel, average, signal); using var kernel = new WaveTrendWindow(kind, channel, average, signal);
        for (var replay = 0; replay < 2; replay++)
        {
            native.Reset(); kernel.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                _ = native.Update(Native(B(-17)), false, false); _ = kernel.Next(MacZWindow.Number.Of(-17), false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i]), final, true);
                    var b = bars[i]; var raw = kernel.Next(WaveTrendWindow.Price(b.Open, b.High, b.Low, b.Close), final);
                    Assert.Equal(expected.Outputs["Wto"][i], point.Value); Assert.Equal(point.Value, raw.Line);
                    foreach (var key in Keys) { Assert.False(double.IsNaN(point.Outputs![key])); Assert.Equal(expected.Outputs[key][i], point.Outputs[key]); }
                    Assert.Equal(expected.Outputs["Signal"][i], raw.SignalLine); Assert.Equal(expected.Signals[i], raw.Trade);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void IndependentNormalizationAndSignalHands()
    {
        var result = Check(new[] { B(0), B(1), B(0) });
        // EMA means: 0,1/2,1/6. Deviations:0,1/4,7/36; normalized:0,2/c,-6/(7*c), c=.015.
        var c = ReferenceFraction.FromDouble(.015); var one = new ReferenceFraction(1);
        var line2 = (new ReferenceFraction(-5) / new ReferenceFraction(21)) / c;
        var signal2 = (one / new ReferenceFraction(126)) / c;
        Assert.Equal(new[] { 0d, (one / c).ToDouble(), line2.ToDouble() }, result.Outputs["Wto"]);
        Assert.Equal(new[] { 0d, (one / (new ReferenceFraction(2) * c)).ToDouble(), signal2.ToDouble() }, result.Outputs["Signal"]);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.StrongSell }, result.Signals);
    }
    [Fact]
    public void EqualLineAndSignalHaveNoDirectionalMargin()
    {
        // A one-bar signal equals the line: adding them would invent a buy.
        var result = Check(new[] { B(0), B(1) }, signal: 1);
        Assert.Equal(result.Outputs["Wto"], result.Outputs["Signal"]);
        Assert.Equal(new[] { Signal.None, Signal.None }, result.Signals);
    }
    [Fact]
    public void SubnormalInputsRetainNormalizedMovement()
    {
        var ordinary = Check(new[] { B(0), B(1), B(0), B(-1) });
        var tiny = Check(new[] { B(0), B(double.Epsilon), B(0), B(-double.Epsilon) });
        foreach (var key in Keys) Assert.Equal(ordinary.Outputs[key], tiny.Outputs[key]); Assert.Equal(ordinary.Signals, tiny.Signals);
    }
    [Fact]
    public void ExactOhlcMeanPreservesCancellationAndTinyMovement()
    {
        // Normalization cancels uniform price scaling, so also check OHLC4 itself.
        Assert.Equal(2.5, WaveTrendWindow.Price(1, 2, 3, 4).Publish());
        var bars = new[] { new Bar(DateTime.UnixEpoch, double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 1),
            new Bar(DateTime.UnixEpoch, double.MaxValue, double.MaxValue, -double.MaxValue, 0, 1),
            new Bar(DateTime.UnixEpoch, 0, double.Epsilon, 0, 0, 1) };
        Check(bars); Check(Enumerable.Repeat(B(double.MaxValue), 8).ToArray());
        var tiny = Check(new[] { B(0), new Bar(DateTime.UnixEpoch, 0, double.Epsilon, 0, 0, 1) });
        Assert.True(tiny.Outputs["Wto"][1] > 60); // The unpublished second OHLC4 is epsilon/4.
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)] [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)] [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void SupportedMeansPreserveExtremeInputs(MovingAvgType kind)
    {
        Check(Enumerable.Range(0, 24).Select(i => B((i * 7 % 13 - 6) * (double.MaxValue / 8), i)).ToArray(), kind, 3, 4, 2);
        Check(Enumerable.Range(0, 14).Select(i => B((i * 3 % 7 - 3) * double.Epsilon, i)).ToArray(), kind, 3, 4, 2);
    }
    [Theory]
    [InlineData(int.MinValue)] [InlineData(int.MaxValue)]
    public void ExtremePeriodsUseObservedHistory(int length)
    {
        foreach (var kind in new[] { MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
        {
            Check(Array.Empty<Bar>(), kind, length, length, length); Check(new[] { B(1), B(3), B(0), B(4) }, kind, length, length, length);
        }
    }
    [Fact]
    public void CoreOverloadsRetainDistinctPriceInputsAndAtomicAliasing()
    {
        var bars = Enumerable.Range(0, 12).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i * 3 % 7 - 2, i * 5 % 11 + 4, -i % 3 - 2, i * 7 % 13 - 3, 1)).ToArray();
        var open = bars.Select(b => b.Open).ToArray(); var high = bars.Select(b => b.High).ToArray(); var low = bars.Select(b => b.Low).ToArray(); var close = bars.Select(b => b.Close).ToArray();
        foreach (var hlc in new[] { false, true })
        {
            var expected = BuiltInFormulaReferences.WaveTrendValues(bars, MovingAvgType.ExponentialMovingAverage, 3, 4, 4, hlc: hlc).Outputs["Wto"];
            var output = Enumerable.Repeat(123d, bars.Length + 2).ToArray();
            if (hlc) OscillatorCore.WaveTrendOscillator(high, low, close, output, 3, 4); else OscillatorCore.WaveTrendOscillator(open, high, low, close, output, 3, 4);
            Assert.Equal(expected, output.Take(bars.Length)); Assert.Equal(new[] { 123d, 123d }, output.Skip(bars.Length));
            var alias = (double[])close.Clone();
            if (hlc) OscillatorCore.WaveTrendOscillator(high, low, alias, alias, 3, 4); else OscillatorCore.WaveTrendOscillator(open, high, low, alias, alias, 3, 4);
            Assert.Equal(expected, alias);
        }
        var sentinel = Enumerable.Repeat(55d, close.Length).ToArray(); var invalid = (double[])open.Clone(); invalid[3] = double.NaN;
        Assert.ThrowsAny<ArgumentException>(() => OscillatorCore.WaveTrendOscillator(invalid, high, low, close, sentinel)); Assert.All(sentinel, v => Assert.Equal(55d, v));
        Assert.ThrowsAny<ArgumentException>(() => OscillatorCore.WaveTrendOscillator(open, high[..^1], low, close, sentinel)); Assert.All(sentinel, v => Assert.Equal(55d, v));
        Assert.ThrowsAny<ArgumentException>(() => OscillatorCore.WaveTrendOscillator(high, low, close, new double[1]));
        OscillatorCore.WaveTrendOscillator(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());
        OscillatorCore.WaveTrendOscillator(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());
    }
    [Fact]
    public void CallbackSlotsPaddingAndEmaResidualBypassRemainExplicit()
    {
        var bars = new[] { B(1), B(3), B(0) }; var expected = BuiltInFormulaReferences.WaveTrendValues(bars, MovingAvgType.ExponentialMovingAverage, 2, 2, 2);
        using (ComponentAverage.Arm((_, _) => throw new InvalidOperationException("Batch bypasses fast hooks.")))
        {
            var batch = Data(bars).CalculateWaveTrendOscillator(length1: 2, length2: 2, smoothLength: 2);
            foreach (var key in Keys) Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); Assert.Equal(0, ComponentAverage.Requests);
        }
        foreach (var signal in new[] { false, true })
        {
            var requests = new List<double[]>();
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> Hook(double[] replacement) => (values, period) => { Assert.Equal(2, period); requests.Add(values.ToArray()); return replacement; };
            using var armed = ComponentAverage.Arm(new[] { Hook(new[] { 999d }), Hook(new[] { 2d, 2, 2 }), Hook(new[] { 7d, -9 }), Hook(new[] { 11d }) });
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeWaveTrendOscillatorFast(Data(bars), context, 2, 2, 2, outputKey: signal ? "Signal" : null);
            Assert.Equal(signal ? new[] { 11d, 0, 0 } : new[] { 7d, -9, 0 }, output.ToArray());
            Assert.Equal(signal ? 4 : 3, ComponentAverage.Requests); Assert.Equal(ComponentAverage.Requests, ComponentAverage.Substitutions);
            Assert.Equal(new[] { 1d, 3, 0 }, requests[0]);
            var c = ReferenceFraction.FromDouble(.015); var last = (new ReferenceFraction(-2) / new ReferenceFraction(3)) / (new ReferenceFraction(2) * c);
            Assert.Equal(new[] { 0d, (new ReferenceFraction(1) / (new ReferenceFraction(2) * c)).ToDouble(), last.ToDouble() }, requests[2]);
            if (signal) Assert.Equal(new[] { 7d, -9, 0 }, requests[3]);
        }
    }
    [Fact]
    public void InvalidCandlesDoNotAdvanceMeans()
    {
        using var state = new WaveTrendOscillatorState(length1: 2, length2: 2, smoothLength: 2); state.Update(Native(B(0)), true, false);
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, bad, 0, 0, 0, 1)), true, false));
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, 0, 0, 0, 0, bad)), true, false));
            var data = Data(new[] { B(bad) }); data.SetCustomValues(new List<double> { 1 });
            Assert.ThrowsAny<ArgumentException>(() => data.CalculateWaveTrendOscillator());
            using var context = new ComputeContext(); foreach (var key in Keys) Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeWaveTrendOscillatorFast(data, context, outputKey: key));
        }
        var expected = Check(new[] { B(0), B(1) }); Assert.Equal(expected.Outputs["Wto"][1], state.Update(Native(B(1)), true, true).Value);
    }
}
