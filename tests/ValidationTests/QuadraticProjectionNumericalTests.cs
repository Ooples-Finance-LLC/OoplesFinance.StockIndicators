using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class QuadraticProjectionNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("QR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    [Fact]
    public void IntegerScaledOracleMatchesDirectFractionOrthogonalization()
    {
        var prices = Enumerable.Range(0, 72).Select(i => ((i * 37 % 43) - 21d) * Math.Pow(2, i % 17 - 8)).ToArray();
        foreach (var length in new[] { 3, 4, 7, 50 })
        {
            var actual = BuiltInFormulaReferences.QuadraticProjectionOutputs(BarsOf(prices), length, 1)["QuadReg"];
            var zero = new ReferenceFraction(0); var n = new ReferenceFraction(length);
            ReferenceFraction Sum(IEnumerable<ReferenceFraction> values) => values.Aggregate(zero, (a, b) => a + b);
            for (var i = 2; i < prices.Length; i++)
            {
                var x = Enumerable.Range(i - length + 1, length).Select(j => new ReferenceFraction(Math.Max(0, j))).ToArray();
                var y = Enumerable.Range(i - length + 1, length).Select(j => j < 0 ? zero : ReferenceFraction.FromDouble(prices[j])).ToArray();
                var xm = Sum(x) / n; var qm = Sum(x.Select(t => t * t)) / n;
                var u = x.Select(t => t - xm).ToArray(); var norm = Sum(u.Select(t => t * t));
                var projection = Sum(x.Select((t, j) => u[j] * (t * t - qm))) / norm;
                var v = x.Select((t, j) => t * t - qm - projection * u[j]).ToArray();
                var curvature = Sum(v.Select((t, j) => t * y[j])) / Sum(v.Select(t => t * t));
                var slope = Sum(u.Select((t, j) => t * y[j])) / norm - projection * curvature;
                var xMean = i + 1 < length ? zero : ReferenceFraction.FromDouble(xm.ToDouble());
                var qMean = i + 1 < length ? zero : ReferenceFraction.FromDouble(qm.ToDouble());
                var yMean = i + 1 < length ? zero : ReferenceFraction.FromDouble((Sum(y) / n).ToDouble());
                var expected = (yMean + slope * (new ReferenceFraction(i) - xMean) + curvature * (new ReferenceFraction((long)i * i) - qMean)).ToDouble();
                Assert.Equal(expected, actual[i]);
            }
        }
    }
    [Fact]
    public void RollingSlopesAndSelectedCentersMatchIndependentOrthogonalProjection()
    {
        foreach (var length in new[] { 1, 2, 3, 4, 7 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 0d, 1, 4, 9, 16, 25, 36, 49, 64 }, new[] { double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue, 0, 0, 0, 0, 0 }, new[] { double.Epsilon, 3 * double.Epsilon, -double.Epsilon, 0, 0, 0, 0, 0 } })
        {
            var bars = BarsOf(prices); var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
            var expected = BuiltInFormulaReferences.QuadraticProjectionOutputs(bars, length, code)["QuadReg"];
            Assert.Equal(expected, Data(bars).CalculateQuadraticRegression(kind, length).CustomValuesList);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeQuadraticRegressionFast(Data(bars), context, length, kind); Assert.Equal(expected, raw.Span.ToArray());
            if (code == 1)
            {
                var core = new double[prices.Length]; OoplesFinance.StockIndicators.Core.Registry.MovingAverageRegistry.Get(MovingAvgType.QuadraticRegression)!.Compute(prices, core, length); Assert.Equal(expected, core);
            }
            using var state = new QuadraticRegressionState(kind, length);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
            }
        }
        Assert.Equal(new[] { 0d, 0, 4, 9, 16 }, Data(BarsOf(new[] { 0d, 1, 4, 9, 16 })).CalculateQuadraticRegression(length: 3).CustomValuesList);
    }
    [Fact]
    public void SelectedPricesAndThreeCustomerCentersKeepTheirOrder()
    {
        var prices = new[] { 2d, 4, 1 }; var data = Data(BarsOf(new[] { 9d, 9, 9 })); data.SetCustomValues(prices.ToList());
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeQuadraticRegressionFast(data, context, 1); Assert.Equal(prices, raw.Span.ToArray());
        var inputs = new[] { new[] { 0d, 1, 2 }, new[] { 0d, 1, 4 }, prices };
        foreach (var batch in new[] { false, true })
        {
            var callbacks = inputs.Select(expected => new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>((values, period) => { Assert.Equal(2, period); Assert.Equal(expected, values); return new[] { 3d, 6, 9 }; })).ToArray();
            using var armed = ComponentAverage.Arm(callbacks);
            if (batch) Assert.Equal(new[] { 3d, 6, 9 }, Data(BarsOf(prices)).CalculateQuadraticRegression(length: 2).CustomValuesList);
            else { using var value = IndicatorCompute.ComputeQuadraticRegressionFast(Data(BarsOf(prices)), context, 2); Assert.Equal(new[] { 3d, 6, 9 }, value.Span.ToArray()); }
            Assert.Equal(3, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void EmptyAndInvalidInputsDoNotAdvanceState()
    {
        Assert.Empty(Data(Array.Empty<Bar>()).CalculateQuadraticRegression().CustomValuesList);
        var empty = Array.Empty<double>(); OoplesFinance.StockIndicators.Core.MovingAverageCore.QuadraticRegression(empty, empty);
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new QuadraticRegressionState(length: 3); using var control = new QuadraticRegressionState(length: 3);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("AE", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(QuadraticRegression)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.QuadraticProjectionOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory, MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory, MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
