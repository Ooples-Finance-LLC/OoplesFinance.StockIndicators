using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RmoNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RMO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(RahulMohindarOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCascade(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.RmoOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Check(Bar[] bars, int first = 2, int range = 10, int swing = 30, int output = 81)
    {
        var expected = BuiltInFormulaReferences.RmoValues(bars, first, range, swing, output);
        var batch = Data(bars).CalculateRahulMohindarOscillator(first, range, swing, output);
        foreach (var entry in expected.Outputs) Assert.Equal(entry.Value, batch.OutputValues[entry.Key]);
        Assert.Equal(expected.Signals, batch.SignalsList); Assert.Equal(expected.Outputs["Rmo"], batch.CustomValuesList);
        foreach (var series in Enum.GetValues<IndicatorCompute.RahulMohindarSeries>())
        {
            using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeRahulMohindarOscillatorFast(Data(bars), context, range, first, swing, output, series);
            Assert.Equal(expected.Outputs[series.ToString()], actual.ToArray());
        }
        using var state = new RahulMohindarOscillatorState(first, range, swing, output);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Bars(3, -1, 8)) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(double.MaxValue)[0]), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Rmo"][i], point.Value);
                    foreach (var entry in expected.Outputs) Assert.Equal(entry.Value[i], point.Outputs![entry.Key]);
                }
            }
        }
        return expected.Outputs;
    }
    [Fact]
    public void OpeningConvolutionHasIndependentFourOutputHand()
    {
        var values = Check(Bars(0, 2));
        Assert.Equal(46085d / 512, values["SwingTrade1"][1]); Assert.Equal(46085d / 1024, values["SwingTrade2"][1]);
        Assert.Equal(46085d / 2048, values["SwingTrade3"][1]); Assert.Equal(46085d / 1024, values["Rmo"][1]);
        Assert.All(values.Values, v => Assert.Equal(0, v[0]));
    }
    [Fact]
    public void UnitCascadePeriodCancelsBeforePublication()
    {
        foreach (var range in new[] { 1, 2, 7 })
        {
            var outputs = Check(Bars(double.MaxValue, -double.MaxValue, 1, double.Epsilon, -double.Epsilon, 0), 1, range, 2, 3);
            foreach (var values in outputs.Values) Assert.All(values, v => Assert.Equal(0, v));
        }
    }
    [Fact]
    public void ExtremeAndSubnormalRangesRetainCompleteRatios()
    {
        var prices = new[] { 0d, 3, -2, 1, 1, -4, 2, 0, 0, 3 };
        var ordinary = Check(Bars(prices), 2, 3, 2, 4);
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, 1020) })
        {
            var scaled = Check(Bars(prices.Select(v => v * scale).ToArray()), 2, 3, 2, 4);
            foreach (var entry in ordinary) Assert.Equal(entry.Value, scaled[entry.Key]);
        }
        Check(Bars(double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, double.Epsilon), 3, 2, 3, 2);
        Check(Bars(1, Math.BitIncrement(1), 1, Math.BitDecrement(1), 1), 3, 2, 2, 2);
    }
    [Fact]
    public void ExtremePeriodsAreLazyAndNormalizeConsistently()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, 2, 5, int.MaxValue })
        { Check(Array.Empty<Bar>(), period, period, period, period); Check(Bars(1, 4, -2, 3, 0), period, period, period, period); }
    }
    [Fact]
    public void ExpiryAndEmaTransitionsPreservePreviewsAndReset()
    {
        Check(Bars(Enumerable.Range(0, 180).Select(i => (double)(i % 13 - 5)).ToArray()), 3, 4, 7, 81);
        Check(Bars(2, 2, 2, 2, 2, 2), 2, 2, 1, 1);
    }
    [Fact]
    public void CallbackGraphUsesOnlyRequestedOutputStages()
    {
        foreach (var series in Enum.GetValues<IndicatorCompute.RahulMohindarSeries>())
        {
            var requests = new List<(double[] Values, int Period)>();
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { requests.Add((values.ToArray(), period)); return Enumerable.Repeat((double)requests.Count, values.Count).ToArray(); };
            var data = Data(Bars(0, 2, 1)); var prior = data.ChainedValues.ToArray();
            using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 12).ToArray()); using var context = new ComputeContext();
            using var actual = IndicatorCompute.ComputeRahulMohindarOscillatorFast(data, context, 3, 3, 4, 5, series);
            var count = series == IndicatorCompute.RahulMohindarSeries.SwingTrade1 ? 10 : series == IndicatorCompute.RahulMohindarSeries.SwingTrade3 ? 12 : 11;
            Assert.Equal(count, ComponentAverage.Substitutions); Assert.Equal(count, requests.Count); Assert.All(requests.Take(10), v => Assert.Equal(3, v.Period));
            Assert.Equal(new[] { 0d, 2, 1 }, requests[0].Values);
            for (var stage = 1; stage < 10; stage++) Assert.All(requests[stage].Values, v => Assert.Equal(stage, v));
            var raw = new[] { 0d, -175, -225 };
            if (count == 10) Assert.Equal(raw, actual.ToArray());
            else
            {
                Assert.Equal(raw, requests[10].Values); Assert.Equal(series == IndicatorCompute.RahulMohindarSeries.Rmo ? 5 : 4, requests[10].Period);
                if (count == 12) { Assert.Equal(4, requests[11].Period); Assert.All(requests[11].Values, v => Assert.Equal(11, v)); }
                Assert.All(actual.ToArray(), v => Assert.Equal(count, v));
            }
            Assert.Equal(prior, data.ChainedValues);
        }
    }
    [Fact]
    public void CallbackCascadeSumCannotOverflowBeforeCancellation()
    {
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, _) => Enumerable.Repeat(double.MaxValue, values.Count).ToArray();
        using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 10).ToArray()); using var context = new ComputeContext();
        using var actual = IndicatorCompute.ComputeRahulMohindarOscillatorFast(Data(Bars(-double.MaxValue, double.MaxValue)), context, series: IndicatorCompute.RahulMohindarSeries.SwingTrade1);
        Assert.Equal(new[] { 0d, 0 }, actual.ToArray()); Assert.Equal(10, ComponentAverage.Substitutions);
    }
    [Fact]
    public void InvalidBarsCannotAdvanceState()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new RahulMohindarOscillatorState(); using var control = new RahulMohindarOscillatorState();
            foreach (var b in Bars(1, 3, -1)) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 1d, 3, -1, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(0, 3, -2)) Assert.Equal(control.Update(Native(b), true, false).Value, state.Update(Native(b), true, false).Value);
        }
    }
}
