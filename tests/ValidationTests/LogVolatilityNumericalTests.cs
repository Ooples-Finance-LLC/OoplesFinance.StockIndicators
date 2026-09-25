using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class LogVolatilityNumericalTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ExtremeReturnHasFiniteDeviationAndDependency(bool kase, bool negative)
    {
        var values = new[] { double.Epsilon, double.MaxValue, double.MaxValue, double.MaxValue }
            .Select(v => negative ? -v : v).ToArray();
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var log = Math.Log(double.MaxValue) - Math.Log(double.Epsilon);
        var expected = new[] { 0d, 0, kase ? 2 : log / 2 * 100 * Math.Sqrt(365), 0 };
        IIndicator indicator = kase ? new KaseSerialDependencyIndex(2) : new HistoricalVolatility(length: 2);
        StockData Data() => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        var builtIn = (IBuiltInIndicator)indicator;
        var keys = kase ? new[] { "KsdiUp", "KsdiDn" } : new[] { "Hv" };
        foreach (var rule in BuiltInFormulaReferences.For(indicator))
            rule.Check(new IndicatorValidationContext("extreme-return", bars, keys.Select(_ => expected).ToArray(), 0));
        var batch = kase ? Data().CalculateKaseSerialDependencyIndex(2) : Data().CalculateHistoricalVolatility(length: 2);
        void Check(double[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);
            for (var i = 0; i < expected.Length; i++)
                Assert.True(new IndicatorErrorBudget(1e-9, 1e-12).Accepts(expected[i], actual[i]), $"bar {i}: {actual[i]} expected {expected[i]}");
        }
        foreach (var key in keys)
        {
            Check(batch.OutputValues[key].ToArray());
            using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
            using var fast = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.TryComputeFast(Data(),
                new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), key), context);
            Assert.NotNull(fast);
            Check(fast.Value.ToArray());
            using var arm = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeArm(Data(),
                new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), key), context);
            Assert.NotNull(arm);
            Check(arm.Value.ToArray());
        }
        var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions());
        foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
        {
            Assert.NotNull(state);
            using var lifetime = state as IDisposable;
            foreach (var commit in new[] { false, true })
            {
                state.Reset();
                var output = keys.Select(_ => new List<double>()).ToArray();
                foreach (var bar in bars)
                {
                    var input = new OhlcvBar("LOG", BarTimeframe.Minutes(1), bar.Time, bar.Time, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true);
                    var result = state.Update(input, commit, true);
                    for (var slot = 0; slot < keys.Length; slot++) output[slot].Add(result.Outputs![keys[slot]]);
                    if (!commit) state.Update(input, true, true);
                }
                foreach (var series in output) Check(series.ToArray());
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContractsRejectErasingAdjacentPriceMovement(bool kase)
    {
        var next = Math.BitIncrement(1d);
        var prices = kase ? new[] { 1d, 2d, next } : new[] { 1d, next, next };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        IIndicator indicator = kase ? new KaseSerialDependencyIndex(2) : new HistoricalVolatility(length: 2);
        var rules = BuiltInFormulaReferences.For(indicator).ToArray();
        foreach (var rule in rules)
            Assert.Throws<InvalidOperationException>(() => rule.Check(new IndicatorValidationContext("erased-adjacent-return", bars,
                new[] { new double[3], new double[3] }, 0)));
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "HistoricalVolatility", "KaseSerialDependencyIndex"
    };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(testCase, route);

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
