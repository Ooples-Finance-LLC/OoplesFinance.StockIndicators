using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class BayesianNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(BayesianOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentBandEvidence(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.BayesianOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static readonly MovingAvgType[] Kinds = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod };
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static readonly string[] Keys = { "SigmaProbsDown", "SigmaProbsUp", "ProbPrime" };
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, double multiplier = 2.5, double threshold = 15)
    {
        var expected = BuiltInFormulaReferences.BayesianValues(bars, length, Kind(kind), multiplier, threshold); var batch = Data(bars).CalculateBayesianOscillator(kind, length, multiplier, threshold);
        foreach (var key in Keys) Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); Assert.Empty(batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); foreach (var key in Keys) { using var output = IndicatorCompute.ComputeBayesianOscillatorFast(Data(bars), context, length, kind, multiplier, key); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        using var state = new BayesianOscillatorState(kind, length, multiplier, threshold);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -4, 7, 1, -2, 8 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -91d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["ProbPrime"][i], point.Value); foreach (var key in Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); } }
        }
        return expected;
    }
    [Fact]
    public void WideBandsEvidenceAndAllProbabilityStagesMatchRationalWindows()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 16 }) foreach (var kind in Kinds) foreach (var length in new[] { 2, 5 }) foreach (var multiplier in new[] { -2.5, 0d, 2.5, double.MaxValue })
        {
            var bars = Bars(Enumerable.Range(0, 15).Select(i => (i % 7 - 3) * scale)); var actual = Check(bars, length, kind, multiplier);
            foreach (var key in Keys) Assert.All(actual.Outputs[key], value => Assert.InRange(value, 0, 1));
        }
    }
    [Fact]
    public void HandTiesEmptyEvidenceAndBuyPriorityKeepProbabilitySeeds()
    {
        var bars = Bars(new[] { 2d, 4, 2, 4 }); var normal = Check(bars, 2);
        Assert.Equal(new[] { 1d, 1, 0, 0 }, normal.Outputs["SigmaProbsDown"]); Assert.Equal(new[] { 0d, 0, 1, 1 }, normal.Outputs["SigmaProbsUp"]); Assert.Equal(new[] { 0d, 0, 0, 0 }, normal.Outputs["ProbPrime"]);
        Assert.Equal(new[] { Signal.None, Signal.None, Signal.Sell, Signal.None }, normal.Signals);
        var collapsed = Check(bars, 2, multiplier: 0); Assert.Equal(new[] { 1d, 1, .5, .5 }, collapsed.Outputs["SigmaProbsDown"]); Assert.Equal(new[] { 0d, 0, .5, .5 }, collapsed.Outputs["ProbPrime"]); Assert.Equal(Signal.Buy, collapsed.Signals[2]);
        foreach (var values in new[] { new[] { 0d, 0, 0 }, new[] { 1d, 2, -3 } }) foreach (var key in Keys) Assert.All(Check(Bars(values), 1).Outputs[key], value => Assert.Equal(0, value));
        Check(Bars(new[] { -2d, 0, 2, 0, -2, 0, 2 }), 2, multiplier: 0);
    }
    [Fact]
    public void ExtremePeriodsGrowWithHistoryAndExpiredWideBandsRecover()
    {
        foreach (var kind in Kinds) foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) { Check(Array.Empty<Bar>(), length, kind); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), length, kind); }
        var bars = Bars(new[] { -double.MaxValue, double.MaxValue, -double.MaxValue }.Concat(Enumerable.Repeat(0d, 12)));
        foreach (var kind in Kinds) { var result = Check(bars, 2, kind, double.MaxValue); if (kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage) foreach (var key in Keys) Assert.Equal(0, result.Outputs[key][^1]); }
    }
    [Fact]
    public void SelectedPricesAndSingleMeanCallbackPreserveEveryOutput()
    {
        var selected = new[] { -2d, 0, 4, 3, -1, 8 }; var bars = Enumerable.Range(0, 6).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, 3, 1, 2, i + 1)).ToArray();
        var effective = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray(); var supplied = new[] { 0d, -2, 1, 4, 3, -1 };
        foreach (var callbackEnabled in new[] { false, true }) foreach (var fast in new[] { false, true }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage }) foreach (var key in Keys)
        {
            var expected = BuiltInFormulaReferences.BayesianValues(effective, 2, Kind(kind), externalMean: callbackEnabled ? supplied : null); var calls = 0;
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(2, period); Assert.Equal(selected, values); calls++; return supplied; };
            using var armed = callbackEnabled ? ComponentAverage.Arm(new[] { callback }) : null; using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected.ToList());
            if (fast) { using var output = IndicatorCompute.ComputeBayesianOscillatorFast(data, context, 2, kind, key: key); Assert.Equal(expected.Outputs[key], output.ToArray()); }
            else { data.CalculateBayesianOscillator(kind, 2); Assert.Equal(expected.Outputs[key], data.OutputValues[key]); Assert.Equal(expected.Signals, data.SignalsList); }
            Assert.Equal(callbackEnabled ? 1 : 0, calls); if (callbackEnabled) { Assert.Equal(1, ComponentAverage.Requests); Assert.Equal(1, ComponentAverage.Substitutions); }
        }
    }
    [Fact]
    public void LegacyMeansRetainBatchFastAndNativeProbabilityParity()
    {
        var bars = Bars(new[] { 2d, -4, 7, -2, 1, 8, -3 }); var kind = MovingAvgType.DoubleExponentialMovingAverage; var batch = Data(bars).CalculateBayesianOscillator(kind, 3); using var context = new ComputeContext(); using var state = new BayesianOscillatorState(kind, 3);
        foreach (var key in Keys) { using var output = IndicatorCompute.ComputeBayesianOscillatorFast(Data(bars), context, 3, kind, key: key); Assert.Equal(batch.OutputValues[key], output.ToArray()); }
        for (var i = 0; i < bars.Length; i++) { var point = state.Update(Native(bars[i]), true, true); foreach (var key in Keys) Assert.Equal(batch.OutputValues[key][i], point.Outputs![key]); }
    }
    [Fact]
    public void CoreAndInPlaceSpansUseBandEvidenceWithDefaultSigmaOutput()
    {
        var bars = Bars(new[] { 2d, 4, 2, -3, 4, 0, -1, 8 }); var prices = bars.Select(b => b.Close).ToArray();
        foreach (var length in new[] { 1, 2, 7, int.MaxValue })
        {
            var expected = BuiltInFormulaReferences.BayesianValues(bars, length, 1).Outputs["SigmaProbsDown"]; var output = new double[prices.Length]; OoplesFinance.StockIndicators.Core.OscillatorCore.BayesianOscillator(prices, output, length); Assert.Equal(expected, output);
            var inPlace = prices.ToArray(); OoplesFinance.StockIndicators.Core.OscillatorCore.BayesianOscillator(inPlace, inPlace, length); Assert.Equal(expected, inPlace);
        }
        OoplesFinance.StockIndicators.Core.OscillatorCore.BayesianOscillator(Array.Empty<double>(), Array.Empty<double>());
        Assert.Throws<ArgumentException>(() => OoplesFinance.StockIndicators.Core.OscillatorCore.BayesianOscillator(prices, Array.Empty<double>()));
    }
    [Fact]
    public void ThresholdTiesSignedMultipliersAndSignalMemoryStayOrdered()
    {
        var bars = Bars(new[] { 2d, 4, 2, 4, -3, 4, -2, -4, 0, 4 });
        foreach (var threshold in new[] { -double.MaxValue, 0d, 15, 50, 100, double.MaxValue }) foreach (var multiplier in new[] { -double.MaxValue, -2.5, 0d, 2.5, double.MaxValue }) Check(bars, 2, multiplier: multiplier, threshold: threshold);
        Assert.Equal(Signal.Sell, Check(Bars(new[] { 2d, 4, 2 }), 2, multiplier: 0, threshold: 50).Signals[2]);
    }
    [Fact]
    public void InvalidCandlesAndParametersCannotAdvanceEvidence()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BayesianOscillatorState(stdDevMult: invalid)); Assert.Throws<ArgumentOutOfRangeException>(() => new BayesianOscillatorState(lowerThreshold: invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Bars(new[] { 1d })).CalculateBayesianOscillator(stdDevMult: invalid)); Assert.Throws<ArgumentOutOfRangeException>(() => Data(Bars(new[] { 1d })).CalculateBayesianOscillator(lowerThreshold: invalid));
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                using var state = new BayesianOscillatorState(length: 2); using var control = new BayesianOscillatorState(length: 2);
                foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
                var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
                foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) { var a = state.Update(Native(bar), true, true); var b = control.Update(Native(bar), true, true); foreach (var key in Keys) Assert.Equal(b.Outputs![key], a.Outputs![key]); }
            }
        }
    }
}
