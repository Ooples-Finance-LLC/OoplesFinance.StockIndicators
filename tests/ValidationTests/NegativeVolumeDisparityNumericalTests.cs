using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class NegativeVolumeDisparityNumericalTests
{
    private static Bar[] Bars(double[] prices, double[]? volumes = null) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, volumes?[i] ?? i % 7 + 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b, double? selected = null) => new("NVDI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, selected ?? b.Close, b.Volume, true);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 1;
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(NegativeVolumeDisparityIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCoordinates(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.NegativeVolumeDisparityOutputs(bars, (IBuiltInIndicator)c.Factory()), BuiltInFormulaReferences.NegativeVolumeDisparityBudget);
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
    private static Dictionary<string, double[]> Check(Bar[] bars, int length = 3, int signalLength = 2,
        MovingAvgType kind = MovingAvgType.SimpleMovingAverage, double top = 1.1, double bottom = .9, double[]? selected = null)
    {
        var expected = BuiltInFormulaReferences.NegativeVolumeDisparityValues(bars, length, signalLength, Kind(kind), top, bottom, selected);
        var data = Data(bars); if (selected is not null) data.SetCustomValues(selected.ToList());
        var batch = data.CalculateNegativeVolumeDisparityIndicator(kind, length, signalLength, top, bottom);
        Assert.Equal(expected.Signals, batch.SignalsList);
        var fastData = Data(bars); if (selected is not null) fastData.SetCustomValues(selected.ToList());
        using var context = new ComputeContext();
        using var primary = IndicatorCompute.ComputeNegativeVolumeDisparityFast(fastData, context, length, kind, top, bottom);
        using var signal = IndicatorCompute.ComputeNegativeVolumeDisparitySignalFast(fastData, context, length, signalLength, kind, top, bottom);
        var fastLine = primary.ToArray(); var fastSignal = signal.ToArray();
        if (selected is not null) Assert.Equal(selected, fastData.ChainedValues);
        using var native = new NegativeVolumeDisparityIndicatorState(kind, length, signalLength, top, bottom);
        using var direct = new NegativeVolumeDisparityWindow(kind, length, signalLength, top, bottom);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var b in Bars(new[] { 7d, 2, 9 })) { native.Update(Native(b), true, false); direct.Next(b.Close, b.Volume, true); }
            native.Reset(); direct.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                Equal(expected.Outputs["Nvdi"][i], batch.CustomValuesList[i]); Equal(expected.Outputs["Nvdi"][i], batch.OutputValues["Nvdi"][i]);
                Equal(expected.Outputs["Signal"][i], batch.OutputValues["Signal"][i]); Equal(expected.Outputs["Nvdi"][i], fastLine[i]); Equal(expected.Outputs["Signal"][i], fastSignal[i]);
                var decoy = Bars(new[] { -9d }, new[] { 100d })[0]; native.Update(Native(decoy), false, false); direct.Next(-9, 100, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i], selected?[i]), final, true); var kernel = direct.Next(selected?[i] ?? bars[i].Close, bars[i].Volume, final);
                    Equal(expected.Outputs["Nvdi"][i], point.Value); Equal(expected.Outputs["Nvdi"][i], point.Outputs!["Nvdi"]); Equal(expected.Outputs["Signal"][i], point.Outputs["Signal"]);
                    Equal(expected.Outputs["Nvdi"][i], kernel.Line.Publish()); Equal(expected.Outputs["Signal"][i], kernel.SignalLine.Publish()); Assert.Equal(expected.Signals[i], kernel.Trade);
                }
            }
        }
        return expected.Outputs;
    }
    [Fact]
    public void ProportionalCoordinatesAndSimpleBandsHaveHandValues()
    {
        var proportional = Bars(new[] { 1d, 7, 2, 3, 1, 9 }, new[] { 6d, 5, 4, 3, 2, 1 });
        var line = Check(proportional, 3, 2); Assert.All(line["Nvdi"], value => Assert.Equal(1, value)); Assert.Equal(new[] { 0d, 1, 1, 1, 1, 1 }, line["Signal"]);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.Buy, Signal.Buy, Signal.Buy, Signal.Buy, Signal.Buy }, Data(proportional).CalculateNegativeVolumeDisparityIndicator(length: 3, signalLength: 2).SignalsList);
        Assert.Equal(new[] { 1d, 1.75 }, Check(Bars(new[] { 1d, 3 }, new[] { 1d, 2 }), 2, 1)["Nvdi"]);
    }
    [Fact]
    public void SignedPricesUseThePublicNviReturnConvention()
    {
        var bars = Bars(new[] { -2d, -1, 1 }, new[] { 3d, 2, 1 });
        Assert.Equal(new[] { 1000d, 1500, 4500 }, BuiltInFormulaReferences.NegativeVolumeDisparityValues(bars, 2, 2).Index); Check(bars, 2, 2);
        Check(Bars(new[] { 2d, 0, 7, -3, 1 }, new[] { 5d, 4, 3, 2, 1 }), 2, 2);
        Check(bars, 2, 2, selected: new[] { 3d, 5, 2 });
    }
    [Fact]
    public void OverflowingNviCanHaveFiniteDisparity()
    {
        var bars = Bars(new[] { double.Epsilon, double.MaxValue }, new[] { 2d, 1 });
        Assert.True(double.IsPositiveInfinity(BuiltInFormulaReferences.NegativeVolumeDisparityValues(bars, 2, 1).Index[1]));
        Assert.Equal(new[] { 1d, 1 }, Check(bars, 2, 1)["Nvdi"]);
        Check(Bars(new[] { double.Epsilon, double.MaxValue, 1d, 0, -1, 2 }, new[] { 6d, 5, 4, 3, 2, 1 }), 3, 2);
    }
    [Fact]
    public void ExactZeroAndOneUlpPerturbationRemainDistinct()
    {
        var prices = Enumerable.Repeat(1d, 37).ToArray(); prices[36] = 0;
        Assert.Equal(0, Check(Bars(prices, Enumerable.Range(1, 37).Select(i => (double)i).ToArray()), 37, 1)["Nvdi"][36]);
        prices[0] = Math.BitDecrement(1d);
        var result = Check(Bars(prices, Enumerable.Range(1, 37).Select(i => (double)i).ToArray()), 37, 1)["Nvdi"][36];
        // Independent 120-digit evaluation of the normalized population coordinate.
        Equal(9.237330659190631e-33, result); Assert.True(result > 0);
    }
    [Fact]
    public void ExactDenominatorZeroDoesNotPoisonTheFollowingBar()
    {
        var prices = Enumerable.Repeat(1d, 38).ToArray(); prices[36] = 0;
        var volumes = Enumerable.Repeat(2d, 38).ToArray(); volumes[36] = 1;
        var line = Check(Bars(prices, volumes), 37, 2)["Nvdi"];
        Assert.Equal(0, line[36]); Assert.True(line[37] > 0 && double.IsFinite(line[37]));
    }
    [Fact]
    public void ExpiryScalingAndAverageKindsPreserveTheFormula()
    {
        var pattern = Enumerable.Range(0, 24).Select(i => (double)(i * 7 % 13 - 6)).ToArray();
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var scale in new[] { 1d, double.Epsilon, Math.Pow(2, 1019) })
            Check(Bars(pattern.Select(v => v * scale).ToArray()), 3, 2, kind);
        Check(Bars(pattern), 7, 5); Check(Bars(pattern), 2, 3);
    }
    [Fact]
    public void ExtremePeriodsAreLazyAndPrefixesAreCausal()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Array.Empty<Bar>(), period, period, kind); Check(Bars(new[] { 1d, -2, 0, 4 }), period, period, kind); }
        var bars = Bars(new[] { 1d, 3, 2, 5, 1, 9, 4, 7 });
        var prefix = Data(bars.Take(5).ToArray()).CalculateNegativeVolumeDisparityIndicator(length: 3, signalLength: 2);
        var full = Data(bars).CalculateNegativeVolumeDisparityIndicator(length: 3, signalLength: 2);
        foreach (var key in prefix.OutputValues.Keys) Assert.Equal(prefix.OutputValues[key], full.OutputValues[key].Take(5));
    }
    [Fact]
    public void FiniteThresholdArgumentsKeepTheirOrderedBranchSemantics()
    {
        var bars = Bars(new[] { 1d, 8, 2, 7, -1, 3, 0, 5 });
        Check(bars, 3, 2, top: .4, bottom: 1.3); Check(bars, 3, 2, top: double.MaxValue, bottom: -double.MaxValue);
    }
    [Fact]
    public void NonfiniteArgumentsAndCandlesCannotAdvanceState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NegativeVolumeDisparityIndicatorState(top: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NegativeVolumeDisparityIndicatorState(bottom: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateNegativeVolumeDisparityIndicator(top: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateNegativeVolumeDisparityIndicator(bottom: bad));
            using var context = new ComputeContext();
            foreach (var thresholds in new[] { (Top: bad, Bottom: .9), (Top: 1.1, Bottom: bad) })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeNegativeVolumeDisparityFast(Data(Array.Empty<Bar>()), context, 2, MovingAvgType.SimpleMovingAverage, thresholds.Top, thresholds.Bottom));
                Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeNegativeVolumeDisparitySignalFast(Data(Array.Empty<Bar>()), context, 2, 2, MovingAvgType.SimpleMovingAverage, thresholds.Top, thresholds.Bottom));
                foreach (var key in new[] { "Nvdi", "Signal" })
                {
                    var spec = new IndicatorSpec(IndicatorName.NegativeVolumeDisparityIndicator,
                        new NegativeVolumeDisparityIndicatorSpecOptions(2, 2, thresholds.Top, thresholds.Bottom, MovingAvgType.SimpleMovingAverage), key);
                    Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeArm(Data(Array.Empty<Bar>()), spec, context));
                }
            }
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                using var state = new NegativeVolumeDisparityIndicatorState(length: 2, signalLength: 2); using var control = new NegativeVolumeDisparityIndicatorState(length: 2, signalLength: 2);
                foreach (var b in Bars(new[] { 1d, 4, 2 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
                var input = new[] { 1d, 3, -1, 1, 1 }; input[field] = bad;
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, input[0], input[1], input[2], input[3], input[4])), final, false));
                foreach (var b in Bars(new[] { 7d, -1, 0, 3 }))
                { var actual = state.Update(Native(b), true, true); var expected = control.Update(Native(b), true, true); foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void CallbacksReceivePriceNviAndSignalInTheirActualSlots()
    {
        var bars = Bars(new[] { 1d, 7, 2, 5, 4 }, new[] { 5d, 4, 3, 2, 1 }); var selected = bars.Select(b => b.Close + 5).ToArray();
        var priceMeans = Enumerable.Repeat(.25, bars.Length).ToArray(); var indexMeans = Enumerable.Repeat(100d, bars.Length).ToArray(); var signals = new[] { 0d, .2, 2, -1, .5 };
        var reference = BuiltInFormulaReferences.NegativeVolumeDisparityValues(bars, 2, 3, selected: selected, externalPriceMean: priceMeans, externalIndexMean: indexMeans, externalSignalMean: signals);
        foreach (var includeSignal in new[] { false, true })
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList());
            var callbacks = new List<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>>
            {
                (values, length) => { Assert.Equal(2, length); Assert.Equal(selected, values); return priceMeans; },
                (values, length) => { Assert.Equal(2, length); Assert.Equal(reference.Index, values); return indexMeans; }
            };
            if (includeSignal) callbacks.Add((values, length) => { Assert.Equal(3, length); for (var i = 0; i < values.Count; i++) Equal(reference.Outputs["Nvdi"][i], values[i]); return signals; });
            using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
            using var result = includeSignal ? IndicatorCompute.ComputeNegativeVolumeDisparitySignalFast(data, context, 2, 3, MovingAvgType.SimpleMovingAverage)
                : IndicatorCompute.ComputeNegativeVolumeDisparityFast(data, context, 2);
            var actual = result.ToArray(); var expected = reference.Outputs[includeSignal ? "Signal" : "Nvdi"];
            for (var i = 0; i < actual.Length; i++) Equal(expected[i], actual[i]);
            Assert.Equal(includeSignal ? 3 : 2, ComponentAverage.Requests); Assert.Equal(ComponentAverage.Requests, ComponentAverage.Substitutions); Assert.Equal(selected, data.ChainedValues);
        }
    }
}
