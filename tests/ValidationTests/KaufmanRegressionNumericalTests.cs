using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class KaufmanRegressionNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(KaufmanAdaptiveCorrelationOscillator) || c.IndicatorType == typeof(KaufmanAdaptiveLeastSquaresMovingAverage))
        .Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesWeightedRegression(IndicatorValidationCase c, string route)
    {
        var indicator = (IBuiltInIndicator)c.Factory();
        var length = (int)indicator.CreateOptions().GetType().GetProperty("Length")!.GetValue(indicator.CreateOptions())!;
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.KaufmanRegressionOutputs(bars, length, indicator.BatchName == IndicatorName.KaufmanAdaptiveLeastSquaresMovingAverage),
            new IndicatorErrorBudget(0, 1e-12, requireSameSign: true));
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputPreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);

    private static Bar B(double x) => new(DateTime.UnixEpoch, x, x, x, x, 1);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("KREG", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static void Equal(double expected, double actual)
    {
        if (expected == 0 || double.IsInfinity(expected) || Math.Abs(expected) <= 16 * double.Epsilon) Assert.Equal(expected, actual);
        else Assert.True(double.IsFinite(actual) && Math.Abs((actual - expected) / expected) <= 2e-14, $"Expected {expected:R}, actual {actual:R}");
    }
    private static Dictionary<string, double[]> Check(double[] values, int length)
    {
        var bars = values.Select(B).ToArray(); var reference = BuiltInFormulaReferences.KaufmanRegressionValues(bars, length);
        var correlation = Data(bars).CalculateKaufmanAdaptiveCorrelationOscillator(length: length);
        var fit = Data(bars).CalculateKaufmanAdaptiveLeastSquaresMovingAverage(length: length);
        using var context = new ComputeContext();
        using var fastFit = IndicatorCompute.ComputeKaufmanAdaptiveLeastSquaresMovingAverageFast(Data(bars), context, length);
        Assert.Equal(fit.OutputValues["Kalsma"], fastFit.ToArray());
        foreach (var (key, series) in new[] { ("IndexSt", IndicatorCompute.KaufmanCorrelationSeries.IndexStandardised), ("SrcSt", IndicatorCompute.KaufmanCorrelationSeries.SourceStandardised), ("Kaco", IndicatorCompute.KaufmanCorrelationSeries.Correlation) })
        {
            using var fast = IndicatorCompute.ComputeKaufmanAdaptiveCorrelationOscillatorFast(Data(bars), context, length, series: series);
            Assert.Equal(correlation.OutputValues[key], fast.ToArray());
        }
        using var state = new KaufmanAdaptiveCorrelationOscillatorState(length: length);
        using var fitState = new KaufmanAdaptiveLeastSquaresMovingAverageState(length: length);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Reset(); fitState.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(777)), false, false); fitState.Update(Native(B(-777)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var fitted = fitState.Update(Native(bars[i]), final, true);
                    foreach (var key in new[] { "IndexSt", "SrcSt", "Kaco" })
                    { Equal(reference[key][i], point.Outputs![key]); Assert.Equal(correlation.OutputValues[key][i], point.Outputs[key]); }
                    Equal(reference["Kalsma"][i], fitted.Value); Assert.Equal(fit.OutputValues["Kalsma"][i], fitted.Value);
                }
            }
        }
        return reference;
    }
    [Fact]
    public void IndependentRootProvesExactSquaresAndHalfwayRounding()
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        foreach (var value in new[] { 0d, double.Epsilon, 2 * double.Epsilon, Math.Pow(2, -1022), 1d, Math.BitIncrement(1), 1e150, double.MaxValue })
            Assert.Equal(value, BuiltInFormulaReferences.KaufmanReferenceRoot(R(value) * R(value)));
        foreach (var lower in new[] { 0d, double.Epsilon, 1d, Math.BitIncrement(1) })
        {
            var upper = Math.BitIncrement(lower); var midpoint = (R(lower) + R(upper)) / new ReferenceFraction(2); var square = midpoint * midpoint;
            var expected = (BitConverter.DoubleToInt64Bits(lower) & 1) == 0 ? lower : upper;
            Assert.Equal(expected, BuiltInFormulaReferences.KaufmanReferenceRoot(square));
            Assert.Equal(square.SqrtToDouble(), BuiltInFormulaReferences.KaufmanReferenceRoot(square));
        }
    }
    [Fact]
    public void CompleteFitCanOverflowAndThenRecover()
    {
        var result = Check(new[] { double.MaxValue, -double.MaxValue, -double.MaxValue, 0, 2, -2, 1, 1, 1 }, 1);
        Assert.Equal(double.NegativeInfinity, result["Kalsma"][2]); Assert.True(double.IsFinite(result["Kalsma"][^1]));
    }
    [Theory]
    [InlineData(1d)]
    [InlineData(3d)]
    [InlineData(-2d)]
    [InlineData(0d)]
    public void ExactLinearFitsHaveNoTradingSignal(double step)
    {
        var prices = Enumerable.Range(-5, 10).Select(i => B(i * step)).ToArray();
        var fit = Data(prices).CalculateKaufmanAdaptiveLeastSquaresMovingAverage(length: 2);
        Assert.Equal(prices.Select(b => b.Close), fit.OutputValues["Kalsma"]);
        Assert.All(fit.SignalsList, signal => Assert.Equal(Signal.None, signal));
        prices[^1] = B(prices[^1].Close + .25);
        var changed = Data(prices).CalculateKaufmanAdaptiveLeastSquaresMovingAverage(length: 2);
        Assert.Equal(Signal.StrongBuy, changed.SignalsList[^1]);
    }
    [Fact]
    public void TwoObservationsAndLinearPriceHaveIndependentFits()
    {
        var result = Check(new[] { 1d, 3 }, 1); Assert.Equal(new[] { 1d, 3 }, result["Kalsma"]); Assert.Equal(new[] { 0d, 1 }, result["Kaco"]);
        Check(new[] { 1e100, 1e-100 }, 1); Check(new[] { -1e200, 3d }, 1);
        Check(new[] { -5d, -4, -3, -2, -1, 0, 1, 2 }, 2);
        Check(new[] { 2d, 8, -3, 4, -7, 9, 0, 1, 1, 1 }, 3);
    }
    [Fact]
    public void SquaredSubnormalsAndOverflowingMomentsRetainTheirRatios()
    {
        Check(new[] { double.Epsilon, 3 * double.Epsilon, -double.Epsilon, 0, 2 * double.Epsilon }, 1);
        Check(new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, -double.MaxValue / 4, 0, 1 }, 1);
        Check(new[] { 1e200, 1e200 + 1e185, 1e200 - 2e185, 1e200 + 4e185, 1e200 }, 2);
    }
    [Fact]
    public void ExtremePeriodsDoNotAllocateUnobservedHistory()
    {
        Check(new[] { -2d, 0, 4 }, int.MaxValue); Check(new[] { 1d, -1, 2 }, 0);
    }
    [Fact]
    public void ComponentOverridesRetainEveryMomentSlot()
    {
        var bars = new[] { 2d, 4, 3, 8 }.Select(B).ToArray();
        foreach (var regression in new[] { false, true })
        {
            var requests = new List<(double[] Values, int Period)>(); var means = new[] { 2d, 3, 7, 10, 8, 2, 3 };
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) =>
            { requests.Add((values.ToArray(), period)); return Enumerable.Repeat(means[requests.Count - 1], values.Count).ToArray(); };
            using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, regression ? 7 : 5).ToArray());
            using var context = new ComputeContext();
            using var output = regression ? IndicatorCompute.ComputeKaufmanAdaptiveLeastSquaresMovingAverageFast(Data(bars), context, 3)
                : IndicatorCompute.ComputeKaufmanAdaptiveCorrelationOscillatorFast(Data(bars), context, 3);
            var result = output.ToArray();
            Assert.Equal(regression ? new[] { -1d, 0, 1, 2 } : new[] { .5, .5, .5, .5 }, result);
            Assert.Equal(regression ? 7 : 5, requests.Count); Assert.All(requests, request => Assert.Equal(3, request.Period));
            Assert.Equal(bars.Select(b => b.Close), requests[0].Values); Assert.Equal(new[] { 0d, 1, 2, 3 }, requests[1].Values);
        }
    }
    [Fact]
    public void InvalidNativeBarCannotAdvanceAnyMoment()
    {
        var bars = new[] { (20d, 1d), (-10d, 4d), (40d, -2d), (0d, 3d), (10d, -5d), (-20d, 2d) }
            .Select(v => new Bar(DateTime.UnixEpoch, v.Item1, 50, -50, v.Item2, 2)).ToArray();
        foreach (var length in new[] { 1, 3, 8 })
        {
            var expected = BuiltInFormulaReferences.KaufmanRegressionValues(bars, length);
            foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                using var correlation = new KaufmanAdaptiveCorrelationOscillatorState(length: length);
                using var fit = new KaufmanAdaptiveLeastSquaresMovingAverageState(length: length);
                for (var index = 0; index < bars.Length; index++)
                {
                    if (index == 2)
                    {
                        var fields = new[] { bars[index].Open, bars[index].High, bars[index].Low, bars[index].Close, bars[index].Volume };
                        fields[field] = invalid;
                        var bad = new Bar(DateTime.UnixEpoch, fields[0], fields[1], fields[2], fields[3], fields[4]);
                        Assert.ThrowsAny<ArgumentException>(() => correlation.Update(Native(bad), final, true));
                        Assert.ThrowsAny<ArgumentException>(() => fit.Update(Native(bad), final, true));
                    }
                    foreach (var commit in new[] { false, true })
                    {
                        var point = correlation.Update(Native(bars[index]), commit, true);
                        foreach (var key in new[] { "IndexSt", "SrcSt", "Kaco" }) Equal(expected[key][index], point.Outputs![key]);
                        Equal(expected["Kaco"][index], point.Value);
                        Equal(expected["Kalsma"][index], fit.Update(Native(bars[index]), commit, true).Value);
                    }
                }
            }
        }
    }
}
