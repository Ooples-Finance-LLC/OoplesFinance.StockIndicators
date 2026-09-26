using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class FisherNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("FISHER", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static void Equal(double expected, double actual) => Assert.True(BuiltInFormulaReferences.FisherBudget.Accepts(expected, actual), $"Expected {expected:R}, got {actual:R}");

    [Fact]
    public void TinySignalsAndClampEndpointsMatchIndependentLogarithms()
    {
        var budget = new IndicatorErrorBudget(0, 4e-15, requireSameSign: true);
        foreach (var magnitude in new[] { double.Epsilon, 1e-300, 1e-20, 1e-9, 1e-4, .124999, .125, .5, .999 })
        foreach (var sign in new[] { -1d, 1d })
        {
            var x = magnitude * sign;
            Assert.True(budget.Accepts(BuiltInFormulaReferences.FisherReferenceTransform(x), FisherArithmetic.Transform(x)), $"atanh({x:R})");
        }
        Assert.Equal(.5, FisherArithmetic.Position(3, 3, 3));
        Assert.Equal(.5, FisherArithmetic.Position(0, -double.MaxValue, double.MaxValue));
        Assert.Equal(1, FisherArithmetic.Position(double.Epsilon, 0, double.Epsilon));
    }

    [Fact]
    public void DirectRoutesPreserveRangePeriodsSelectedPricesAndRecursiveState()
    {
        var prices = new[] { -double.MaxValue, double.MaxValue, 0d, double.Epsilon, -double.Epsilon, 7, 3, 8, 2, 1, 9, 4, 6, 5, 0, 3, 3, 3, 3 }
            .Concat(Enumerable.Range(10, 80).Select(i => (double)i)).ToArray();
        foreach (var length in new[] { 1, 3, 7 })
        {
            var expected = BuiltInFormulaReferences.FisherValues(prices, length);
            var core = new double[prices.Length]; OscillatorCore.EhlersFisherTransform(prices, core, length);
            var batch = Data(BarsOf(prices)).CalculateEhlersFisherTransform(length).CustomValuesList;
            using var context = new ComputeContext();
            var selected = Data(BarsOf(Enumerable.Repeat(42d, prices.Length).ToArray())); selected.CustomValuesList = prices.ToList();
            using var raw = IndicatorCompute.ComputeEhlersFisherTransformFast(selected, context, length);
            for (var i = 0; i < prices.Length; i++) { Equal(expected[i], core[i]); Equal(expected[i], batch[i]); Equal(expected[i], raw.Span[i]); }
            foreach (var indicator in new IBuiltInIndicator[] { new FisherTransform(length), new EhlersFisherTransform(length) })
            {
                var spec = new IndicatorSpec(indicator.BatchName, indicator.CreateOptions());
                var native = StatefulIndicatorFactory.Create(spec); var live = StreamingIndicatorFactory.CreateState(spec);
                Assert.NotNull(live); Assert.IsType<EhlersFisherTransformState>(native); Assert.IsType<EhlersFisherTransformState>(live);
                using var nativeLifetime = native as IDisposable; using var liveLifetime = live as IDisposable;
                foreach (var state in new[] { native, live! })
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < prices.Length; i++)
                    {
                        state.Update(Native(BarsOf(new[] { 100d })[0]), false, true);
                        foreach (var final in new[] { false, false, true })
                        {
                            var actual = state.Update(Native(BarsOf(prices)[i]), final, true);
                            Equal(expected[i], actual.Value); Equal(expected[i], actual.Outputs!["Eft"]);
                        }
                    }
                }
            }
        }
        OscillatorCore.EhlersFisherTransform(Array.Empty<double>(), Array.Empty<double>(), 3);
        Assert.Throws<ArgumentException>(() => OscillatorCore.EhlersFisherTransform(prices, Array.Empty<double>(), 3));
    }

    [Fact]
    public void DecayingSignalsRemainNonzeroAcrossBatchRawCoreAndNative()
    {
        var prices = new[] { 0d, 1 }.Concat(Enumerable.Repeat(1d, 1000)).ToArray();
        var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.FisherValues(prices, 3);
        var budget = new IndicatorErrorBudget(0, 8e-15, requireSameSign: true);
        Assert.True(expected[^1] > 0 && expected[^1] < 1e-150);
        var core = new double[prices.Length]; OscillatorCore.EhlersFisherTransform(prices, core, 3);
        var batch = Data(bars).CalculateEhlersFisherTransform(3).CustomValuesList;
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeEhlersFisherTransformFast(Data(bars), context, 3);
        using var native = new EhlersFisherTransformState(3);
        for (var i = 0; i < prices.Length; i++)
        foreach (var actual in new[] { core[i], batch[i], raw.Span[i], native.Update(Native(bars[i]), true, false).Value })
            Assert.True(budget.Accepts(expected[i], actual), $"Bar {i}: expected {expected[i]:R}, got {actual:R}");
    }

    [Fact]
    public void NativeRejectsEveryInvalidFieldBeforeAdvancingState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new EhlersFisherTransformState(3); using var control = new EhlersFisherTransformState(3);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("FISHER", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]);
            Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "FisherTransform", "EhlersFisherTransform" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route);

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
