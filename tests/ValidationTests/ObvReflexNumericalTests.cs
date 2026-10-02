using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ObvReflexNumericalTests
{
    private static Bar[] Bars(double[] prices, double[]? volumes = null) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, volumes?[i] ?? i % 7 + 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b, double? selected = null) => new("NVDI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, selected ?? b.Close, b.Volume, true);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 1;
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(OnBalanceVolumeReflex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCoordinates(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.ObvReflexOutputs(bars, (IBuiltInIndicator)c.Factory()), BuiltInFormulaReferences.ObvReflexBudget);
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
        MovingAvgType kind = MovingAvgType.SimpleMovingAverage, double[]? selected = null)
    {
        var expected = BuiltInFormulaReferences.ObvReflexValues(bars, length, signalLength, Kind(kind), selected);
        var data = Data(bars); if (selected is not null) data.SetCustomValues(selected.ToList());
        var batch = data.CalculateOnBalanceVolumeReflex(kind, length, signalLength);
        Assert.Equal(expected.Signals, batch.SignalsList);
        var fastData = Data(bars); if (selected is not null) fastData.SetCustomValues(selected.ToList());
        using var context = new ComputeContext();
        using var primary = IndicatorCompute.ComputeOnBalanceVolumeReflexFast(fastData, context, length);
        using var signal = IndicatorCompute.ComputeOnBalanceVolumeReflexSignalFast(fastData, context, length, signalLength, kind);
        var fastLine = primary.ToArray(); var fastSignal = signal.ToArray();
        if (selected is not null) Assert.Equal(selected, fastData.ChainedValues);
        using var native = new OnBalanceVolumeReflexState(kind, length, signalLength);
        using var direct = new ObvReflexWindow(kind, length, signalLength);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var b in Bars(new[] { 7d, 2, 9 })) { native.Update(Native(b), true, false); direct.Next(b.Close, b.Volume, true); }
            native.Reset(); direct.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                Equal(expected.Outputs["Obvr"][i], batch.CustomValuesList[i]); Equal(expected.Outputs["Obvr"][i], batch.OutputValues["Obvr"][i]);
                Equal(expected.Outputs["Signal"][i], batch.OutputValues["Signal"][i]); Equal(expected.Outputs["Obvr"][i], fastLine[i]); Equal(expected.Outputs["Signal"][i], fastSignal[i]);
                var decoy = Bars(new[] { -9d }, new[] { 100d })[0]; native.Update(Native(decoy), false, false); direct.Next(-9, 100, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i], selected?[i]), final, true); var kernel = direct.Next(selected?[i] ?? bars[i].Close, bars[i].Volume, final);
                    Equal(expected.Outputs["Obvr"][i], point.Value); Equal(expected.Outputs["Obvr"][i], point.Outputs!["Obvr"]); Equal(expected.Outputs["Signal"][i], point.Outputs["Signal"]);
                    Equal(expected.Outputs["Obvr"][i], kernel.Line.Publish()); Equal(expected.Outputs["Signal"][i], kernel.SignalLine.Publish()); Assert.Equal(expected.Signals[i], kernel.Trade);
                }
            }
        }
        return expected.Outputs;
    }
    [Fact]
    public void LaggedPriceDirectionHasIndependentHandValues()
    {
        var bars = Bars(new[] { 1d, 2, 0, -1, 3 }, new[] { 1d, 2, 3, 4, 5 });
        var result = Check(bars, 2, 2);
        Assert.Equal(new[] { 1d, 3, 0, -4, 1 }, result["Obvr"]);
        Assert.Equal(new[] { 0d, 2, 1.5, -2, -1.5 }, result["Signal"]);
        // For lag 3 the first three comparisons use zero, not the first
        // available price. Independent signed increments are +2,+3,-5,+7,-11.
        // A separate two-bar SMA averages the resulting cumulative totals.
        var distinctPeriods = Check(Bars(new[] { 4d, 1, -1, 5, 0 }, new[] { 2d, 3, 5, 7, 11 }), 3, 2);
        Assert.Equal(new[] { 2d, 5, 0, 7, -4 }, distinctPeriods["Obvr"]);
        Assert.Equal(new[] { 0d, 3.5, 2.5, 3.5, 1.5 }, distinctPeriods["Signal"]);
    }
    [Fact]
    public void ZeroPricesAndSignedVolumesKeepTheirDirections()
    {
        var bars = Bars(new[] { 0d, 2, 2, -1, 3 }, new[] { 7d, -2, 9, -3, 5 });
        Assert.Equal(new[] { 0d, -2, -2, 1, 6 }, Check(bars, 1, 2)["Obvr"]);
        Check(bars, 2, 2, selected: new[] { 3d, 5, 2, -1, 0 });
    }
    [Fact]
    public void OverflowingTotalsAndSignalsRecoverToFiniteValues()
    {
        var bars = Bars(new[] { 1d, 2, 1, 0 }, Enumerable.Repeat(double.MaxValue, 4).ToArray());
        var result = Check(bars, 1, 3);
        Assert.Equal(new[] { double.MaxValue, double.PositiveInfinity, double.MaxValue, 0 }, result["Obvr"]);
        Assert.Equal(new[] { 0d, 0, double.PositiveInfinity, double.MaxValue }, result["Signal"]);
    }
    [Fact]
    public void ExactZeroAndOneUlpPerturbationRemainDistinct()
    {
        var prices = new[] { 1d, 2, 3 };
        Assert.Equal(0, Check(Bars(prices, new[] { double.MaxValue, 0, -double.MaxValue }), 1, 2)["Obvr"][2]);
        Assert.Equal(double.Epsilon, Check(Bars(prices, new[] { double.MaxValue, double.Epsilon, -double.MaxValue }), 1, 2)["Obvr"][2]);
        Assert.Equal(new[] { 0d, 0 }, Check(Bars(new[] { 1d, 1 }, new[] { 0d, 7 }), 1, 1)["Obvr"]);
        Assert.Equal(new[] { 0d, 7 }, Check(Bars(new[] { 1d, Math.BitIncrement(1d) }, new[] { 0d, 7 }), 1, 1)["Obvr"]);
    }
    [Fact]
    public void LongLagUsesZeroUntilTheRequestedHistoryExists()
    {
        var bars = Bars(new[] { 1d, 0, -1, 2, -3 }, Enumerable.Repeat(1d, 5).ToArray());
        Assert.Equal(new[] { 1d, 1, 0, 1, 0 }, Check(bars, int.MaxValue, 1)["Obvr"]);
        Check(bars, 3, 2);
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
        var prefix = Data(bars.Take(5).ToArray()).CalculateOnBalanceVolumeReflex(length: 3, signalLength: 2);
        var full = Data(bars).CalculateOnBalanceVolumeReflex(length: 3, signalLength: 2);
        foreach (var key in prefix.OutputValues.Keys) Assert.Equal(prefix.OutputValues[key], full.OutputValues[key].Take(5));
    }
    [Fact]
    public void SignalComparisonsUseExactMargins()
    {
        var bars = Bars(new[] { 1d, 2, 0, -1, 3 }, new[] { 1d, 2, 3, 4, 5 });
        Check(bars, 2, 2);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.Buy, Signal.StrongSell, Signal.StrongSell, Signal.StrongBuy }, Data(bars).CalculateOnBalanceVolumeReflex(length: 2, signalLength: 2).SignalsList);
        Assert.All(Data(bars).CalculateOnBalanceVolumeReflex(length: 2, signalLength: 1).SignalsList, value => Assert.Equal(Signal.None, value));
    }
    [Fact]
    public void NonfiniteArgumentsAndCandlesCannotAdvanceState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            foreach (var volumes in new[] { false, true })
            {
                var bars = Bars(new[] { volumes ? 1d : bad }, new[] { volumes ? bad : 1d });
                Assert.Throws<ArgumentOutOfRangeException>(() => Data(bars).CalculateOnBalanceVolumeReflex());
                using var context = new ComputeContext();
                Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeOnBalanceVolumeReflexFast(Data(bars), context));
                Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeOnBalanceVolumeReflexSignalFast(Data(bars), context, 2, 2, MovingAvgType.SimpleMovingAverage));
            }
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                using var state = new OnBalanceVolumeReflexState(length: 2, signalLength: 2); using var control = new OnBalanceVolumeReflexState(length: 2, signalLength: 2);
                foreach (var b in Bars(new[] { 1d, 4, 2 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
                var input = new[] { 1d, 3, -1, 1, 1 }; input[field] = bad;
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, input[0], input[1], input[2], input[3], input[4])), final, false));
                foreach (var b in Bars(new[] { 7d, -1, 0, 3 }))
                { var actual = state.Update(Native(b), true, true); var expected = control.Update(Native(b), true, true); foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void SignalCallbackReceivesTheLaggedVolumeTotal()
    {
        var bars = Bars(new[] { 1d, 7, 2, 5, 4 }); var selected = new[] { 3d, -1, 5, 2, 7 }; var replacement = new[] { 0d, .5, -1, 2, 4 };
        var reference = BuiltInFormulaReferences.ObvReflexValues(bars, 2, 3, selected: selected, externalSignal: replacement);
        foreach (var includeSignal in new[] { false, true })
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList());
            using var armed = ComponentAverage.Arm(new List<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>>
            { (values, period) => { Assert.Equal(3, period); Assert.Equal(reference.Outputs["Obvr"], values); return replacement; } });
            using var context = new ComputeContext();
            using var result = includeSignal ? IndicatorCompute.ComputeOnBalanceVolumeReflexSignalFast(data, context, 2, 3, MovingAvgType.SimpleMovingAverage)
                : IndicatorCompute.ComputeOnBalanceVolumeReflexFast(data, context, 2);
            Assert.Equal(reference.Outputs[includeSignal ? "Signal" : "Obvr"], result.ToArray());
            Assert.Equal(includeSignal ? 1 : 0, ComponentAverage.Requests); Assert.Equal(ComponentAverage.Requests, ComponentAverage.Substitutions); Assert.Equal(selected, data.ChainedValues);
        }
    }
}
