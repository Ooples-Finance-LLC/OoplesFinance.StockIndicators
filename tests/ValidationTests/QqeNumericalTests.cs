using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class QqeNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("QQE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 1;
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(QuantitativeQualitativeEstimation)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentWidths(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.QqeOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length = 3, int smooth = 2, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage, double fast = 2.618, double slow = 4.236)
    {
        var expected = BuiltInFormulaReferences.QqeValues(bars, length, smooth, Kind(kind), fast, slow);
        var batch = Data(bars).CalculateQuantitativeQualitativeEstimation(kind, length, smooth, fast, slow);
        Assert.Equal(expected.Outputs["FastAtrRsi"], batch.OutputValues["FastAtrRsi"]); Assert.Equal(expected.Outputs["SlowAtrRsi"], batch.OutputValues["SlowAtrRsi"]); Assert.Equal(expected.Signals, batch.SignalsList);
        var direct = QqeWindow.Calculate(Data(bars), kind, length, smooth, fast, slow, true);
        Assert.Equal(expected.Outputs["FastAtrRsi"], direct.Fast); Assert.Equal(expected.Outputs["SlowAtrRsi"], direct.Slow);
        using var state = new QuantitativeQualitativeEstimationState(kind, length, smooth, fast, slow);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Bars(3, -1, 8)) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(double.MaxValue)[0]), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true);
                    Assert.Equal(expected.Outputs["FastAtrRsi"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["FastAtrRsi"]);
                    Assert.Equal(expected.Outputs["SlowAtrRsi"][i], point.Outputs!["SlowAtrRsi"]);
                }
            }
        }
        return expected.Outputs["FastAtrRsi"];
    }
    [Fact]
    public void HandWidthsUseBothStagesAndOpeningMovement()
    {
        Assert.Equal(new[] { 200d, 0, 200 }, Check(Bars(2, 4, 2), 1, 1, fast: 2, slow: 3));
        Assert.Equal(50, Check(Bars(1), 2, 2, MovingAvgType.WeightedMovingAverage, 3, 6)[0]);
        Assert.Equal(4294967293L, QqeWindow.WidthPeriod(int.MaxValue));
    }
    [Fact]
    public void WeightedFractionIsRetainedUntilFinalScaling()
    {
        // Exact opening width is (100*2/3)*(3/6)*(3/6) = 50/3.
        foreach (var factor in new[] { double.Epsilon, 3d, Math.BitIncrement(3d), double.MaxValue })
        {
            var expected = (new ReferenceFraction(50) * ReferenceFraction.FromDouble(factor) / new ReferenceFraction(3)).ToDouble();
            Assert.Equal(expected, Check(Bars(1), 2, 2, MovingAvgType.WeightedMovingAverage, factor, 0)[0]);
        }
    }
    [Fact]
    public void ExtremeAndSubnormalPricesPreserveWidthsAndSignals()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var scale in new[] { double.Epsilon, 1d, Math.Pow(2, 1020) })
            Check(Bars(new[] { 0d, 3, -2, 1, 1, -4, 2, 0, 0, 3 }.Select(v => v * scale).ToArray()), 3, 2, kind, .5, 2);
        Check(Bars(-double.MaxValue, double.MaxValue, 0, double.Epsilon, -double.Epsilon), fast: double.MaxValue, slow: 0);
    }
    [Fact]
    public void ExtremePeriodsAreLazyAndDoNotWrap()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, 2, 7, 1073741825, int.MaxValue })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Array.Empty<Bar>(), period, period, kind); Check(Bars(1, 4, -2, 3, 0), period, period, kind); }
    }
    [Fact]
    public void InvalidFactorsAreRejectedBeforeAnyCalculation()
    {
        foreach (var factor in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var pair in new[] { (factor, 1d), (1d, factor) })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new QuantitativeQualitativeEstimationState(fastFactor: pair.Item1, slowFactor: pair.Item2));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateQuantitativeQualitativeEstimation(fastFactor: pair.Item1, slowFactor: pair.Item2));
        }
    }
    [Fact]
    public void InvalidBarsCannotAdvanceState()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new QuantitativeQualitativeEstimationState(length: 2); using var control = new QuantitativeQualitativeEstimationState(length: 2);
            foreach (var b in Bars(1, 3, -1)) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 1d, 3, -1, 2, 1 }; v[field] = invalid; var bad = new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4]);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(bad), final, true));
            foreach (var b in Bars(0, 3, -2)) Assert.Equal(control.Update(Native(b), true, false).Value, state.Update(Native(b), true, false).Value);
        }
    }
    [Fact]
    public void FiveCallbacksPreservePeriodsAndStageInputs()
    {
        var requests = new List<(double[] Values, int Period)>();
        var answers = new[] { new[] { 1d, 1, 1 }, new[] { 1d, 1, 1 }, new[] { .25, .5, .75 }, new[] { 1d, 2, 3 }, new[] { 0d, 1, 2 } };
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { requests.Add((values.ToArray(), period)); return answers[requests.Count - 1]; };
        var data = Data(Bars(2, 4, 2)); var prior = data.ChainedValues.ToArray();
        using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 5).ToArray());
        var result = QqeWindow.Calculate(data, MovingAvgType.ExponentialMovingAverage, 2, 3, 2, 3, true);
        Assert.Equal(5, ComponentAverage.Substitutions); Assert.Equal(new[] { 2, 2, 3, 3, 3 }, requests.Select(v => v.Period));
        Assert.Equal(new[] { 50d, 50, 50 }, requests[2].Values); Assert.Equal(new[] { .25, .25, .25 }, requests[3].Values); Assert.Equal(answers[3], requests[4].Values);
        Assert.Equal(new[] { 0d, 2, 4 }, result.Fast); Assert.Equal(new[] { 0d, 3, 6 }, result.Slow); Assert.Equal(prior, data.ChainedValues);
    }
    [Fact]
    public void LegacySmoothingRetainsBatchFallback()
    {
        var bars = Bars(1, 4, 2, -1, 3, 0); var kind = MovingAvgType.LinearWeightedMovingAverage;
        var batch = Data(bars).CalculateQuantitativeQualitativeEstimation(kind, 3, 2);
        var fast = QqeWindow.Calculate(Data(bars), kind, 3, 2, 2.618, 4.236, true);
        Assert.Equal(batch.OutputValues["FastAtrRsi"], fast.Fast); Assert.Equal(batch.OutputValues["SlowAtrRsi"], fast.Slow);
        Assert.Throws<NotSupportedException>(() => new QuantitativeQualitativeEstimationState(kind, 3, 2));
    }
}
