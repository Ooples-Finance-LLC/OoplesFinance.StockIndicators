using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DeviationScaledNumericalTests
{
    private static readonly IndicatorErrorBudget FisherBudget = new(0, 8e-15, true);
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersDeviationScaledMovingAverage) || c.IndicatorType == typeof(EhlersFisherizedDeviationScaledOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentLagDifferences(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, c.Factory()), c.IndicatorType == typeof(EhlersFisherizedDeviationScaledOscillator) ? FisherBudget : Budget);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var o = ((IBuiltInIndicator)indicator).CreateOptions(); var fisher = o is EhlersFisherizedDeviationScaledOscillatorSpecOptions; var fast = fisher ? ((EhlersFisherizedDeviationScaledOscillatorSpecOptions)o).Length : ((EhlersDeviationScaledMovingAverageSpecOptions)o).Length; return new() { { fisher ? "Efdso" : "Edsma", BuiltInFormulaReferences.DeviationScaledValues(bars, fast, fisher ? 40 : 2 * Math.Max(1, fast), fisher) } }; }
    private static void Match(double expected, double actual, bool fisher) => Assert.True((fisher ? FisherBudget : Budget).Accepts(expected, actual), $"{expected:R} != {actual:R}");
    private static void Check(Bar[] bars, int fast = 3, int slow = 6, bool fisher = false)
    {
        var expected = BuiltInFormulaReferences.DeviationScaledValues(bars, fast, slow, fisher);
        var batch = fisher ? Data(bars).CalculateEhlersFisherizedDeviationScaledOscillator(fastLength: fast, slowLength: slow) : Data(bars).CalculateEhlersDeviationScaledMovingAverage(fastLength: fast, slowLength: slow);
        var key = fisher ? "Efdso" : "Edsma";
        for (var i = 0; i < bars.Length; i++) { Assert.True(double.IsFinite(expected[i])); Match(expected[i], batch.CustomValuesList[i], fisher); Match(expected[i], batch.OutputValues[key][i], fisher); }
        using var context = new ComputeContext();
        if (fisher || slow == 2 * Math.Max(1, fast))
        { using var arm = fisher ? IndicatorCompute.ComputeEhlersFisherizedDeviationScaledOscillatorFast(Data(bars), context, fast, slow) : IndicatorCompute.ComputeEhlersDeviationScaledMovingAverageFast(Data(bars), context, fast); var line = arm.ToArray(); for (var i = 0; i < line.Length; i++) Match(expected[i], line[i], fisher); }
        if (!fisher)
        { var core = new double[bars.Length]; MovingAverageCore.EhlersDeviationScaledMovingAverage(bars.Select(b => b.Close).ToArray(), core, fast, slow); Assert.Equal(expected, core); }
        IStreamingIndicatorState state = fisher ? new EhlersFisherizedDeviationScaledOscillatorState(fastLength: fast, slowLength: slow) : new EhlersDeviationScaledMovingAverageState(fastLength: fast, slowLength: slow);
        using var disposable = (IDisposable)state;
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(1)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Match(expected[i], point.Value, fisher); Match(expected[i], point.Outputs![key], fisher); }
            }
        }
    }
    [Fact]
    public void WideAndTinyChangesPreservePopulationDeviationAndFeedback()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue }) foreach (var fast in new[] { 1, 3, 20 })
        { var bars = Enumerable.Range(0, 50).Select(i => Candle(i % 3 == 0 ? -scale : scale)).ToArray(); Check(bars, fast, 2 * fast); Check(bars, fast, 7, true); }
        Check(Enumerable.Range(0, 60).Select(i => Candle(i < 25 ? (i % 2 == 0 ? double.MaxValue : -double.MaxValue) : double.Epsilon)).ToArray(), 3, 5);
    }
    [Fact]
    public void StartupAndZeroVarianceUseOnePercentZeroSeededAverage()
    {
        foreach (var slow in new[] { 1, 6 })
        { var bars = Enumerable.Repeat(Candle(1), 20).ToArray(); Check(bars, 3, slow); var line = Data(bars).CalculateEhlersDeviationScaledMovingAverage(fastLength: 3, slowLength: slow).CustomValuesList; Assert.Equal(.01, line[0]); Assert.InRange(line[1], .01989999999999999, .01990000000000001); }
        Check(Enumerable.Range(0, 30).Select(i => Candle(i % 7)).ToArray(), 3, 1);
    }
    [Fact]
    public void SubnormalFeedbackAccumulatesIntoARepresentableStep()
    {
        var bars = Enumerable.Range(0, 110).Select(i => Candle(i < 4 ? 0 : double.Epsilon)).ToArray(); Check(bars, 3, 1);
        Assert.Equal(double.Epsilon, Data(bars).CalculateEhlersDeviationScaledMovingAverage(fastLength: 3, slowLength: 1).CustomValuesList[^1]);
    }
    [Fact]
    public void IndependentNormalizedPeriodsUseOnlyConsumedHistory()
    {
        var bars = Enumerable.Range(0, 12).Select(i => Candle(i % 4 - 1)).ToArray();
        foreach (var fast in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var slow in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var fisher in new[] { false, true }) { Check(bars, fast, slow, fisher); Check(Array.Empty<Bar>(), fast, slow, fisher); }
    }
    [Fact]
    public void PublicDoubledPeriodRejectsOverflowBeforeAllocation()
    {
        using var context = new ComputeContext();
        Assert.Throws<OverflowException>(() => IndicatorCompute.ComputeEhlersDeviationScaledMovingAverageFast(Data(Array.Empty<Bar>()), context, int.MaxValue));
        Assert.Throws<OverflowException>(() => MovingAverageCore.EhlersDeviationScaledMovingAverage(Array.Empty<double>(), Array.Empty<double>(), int.MaxValue));
        var options = new EhlersDeviationScaledMovingAverageSpecOptions(int.MaxValue); var spec = new IndicatorSpec(IndicatorName.EhlersDeviationScaledMovingAverage, options, "Edsma");
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        Assert.Throws<OverflowException>(() => BuilderArmBinding.Compute(Data(Array.Empty<Bar>()), spec, target));
    }
    [Fact]
    public void FisherRetainsTinySignalsAndHoldsOutsideItsDomain()
    {
        foreach (var scale in new[] { Math.Pow(2, -1000), 1e-20, 1d }) Check(Enumerable.Range(0, 50).Select(i => Candle((i % 5 - 2) * scale)).ToArray(), 3, 6, true);
        var bars = new[] { 1d, 1, 1, 400, 400, 400, -400, -400, .01, .01 }.Select(v => Candle(v)).ToArray(); Check(bars, 3, 1, true);
        var line = Data(bars).CalculateEhlersFisherizedDeviationScaledOscillator(fastLength: 3, slowLength: 1).CustomValuesList; Assert.NotEqual(0, line[2]); Assert.Equal(line[2], line[3]);
    }
    [Fact]
    public void SelectedPricesReachBothFastArms()
    {
        var selected = Enumerable.Range(0, 50).Select(i => (i % 5 - 2) / 10d).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        foreach (var fisher in new[] { false, true })
        {
            var expected = BuiltInFormulaReferences.DeviationScaledValues(selected.Select(v => Candle(v)).ToArray(), 3, fisher ? 40 : 6, fisher);
            var data = Data(bars); data.SetCustomValues(selected); var batch = fisher ? data.CalculateEhlersFisherizedDeviationScaledOscillator(fastLength: 3) : data.CalculateEhlersDeviationScaledMovingAverage(fastLength: 3, slowLength: 6);
            using var context = new ComputeContext(); data = Data(bars); data.SetCustomValues(selected);
            using var fast = fisher ? IndicatorCompute.ComputeEhlersFisherizedDeviationScaledOscillatorFast(data, context, 3) : IndicatorCompute.ComputeEhlersDeviationScaledMovingAverageFast(data, context, 3);
            var line = fast.ToArray(); for (var i = 0; i < bars.Length; i++) { Match(expected[i], line[i], fisher); Match(expected[i], batch.CustomValuesList[i], fisher); }
        }
    }
    [Fact]
    public void InvalidFieldsLeaveDeviationAndFeedbackUnchanged()
    {
        foreach (var fisher in new[] { false, true }) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => fisher ? new EhlersFisherizedDeviationScaledOscillatorState(fastLength: 3, slowLength: 6) : new EhlersDeviationScaledMovingAverageState(fastLength: 3, slowLength: 6);
            var state = Create(); var control = Create(); using var first = (IDisposable)state; using var second = (IDisposable)control;
            state.Update(Native(Candle(1)), true, false); control.Update(Native(Candle(1)), true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 10).Select(i => Native(Candle(i % 3 / 10d)))) Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
