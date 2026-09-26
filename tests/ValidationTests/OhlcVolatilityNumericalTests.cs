using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class OhlcVolatilityNumericalTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void OhlcEstimatorsHaveIndependentRangeExamples(bool extreme, bool atHigh)
    {
        var high = extreme ? double.MaxValue : 2d;
        var low = extreme ? double.Epsilon : .5;
        var price = atHigh ? high : 1d;
        var bars = Enumerable.Range(0, 3).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), price, high, low, price, 1)).ToArray();
        var upperLog = atHigh ? 0 : Math.Log(high);
        var lowerLog = Math.Log(low) - Math.Log(price);
        var rangeLog = upperLog - lowerLog;
        var parkinson = Math.Sqrt(252 / (4 * Math.Log(2))) * rangeLog;
        var garman = Math.Sqrt(126) * rangeLog;
        var rogers = Math.Sqrt(252 * (upperLog * upperLog + lowerLog * lowerLog));
        var weight = .34 / (1.34 + 3);
        CheckAllRoutes(new ParkinsonVolatility(2), bars, new[] { 0d, parkinson, parkinson });
        CheckAllRoutes(new GarmanKlassVolatility(2), bars, new[] { 0d, garman, garman },
            new[] { 0d, garman * 7 / 28, garman * 13 / 28 });
        CheckAllRoutes(new RogersSatchellVolatility(2), bars, new[] { 0d, rogers, rogers });
        CheckAllRoutes(new YangZhangVolatility(2), bars, new[] { 0d, 0, Math.Sqrt(1 - weight) * rogers });
    }

    [Fact]
    public void CloseReturnsSurviveExtremeQuotientsAndIncludeTheStartupZero()
    {
        var values = new[] { double.Epsilon, double.MaxValue, double.MaxValue };
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var expected = (Math.Log(double.MaxValue) - Math.Log(double.Epsilon)) * Math.Sqrt(252) / 2;
        CheckAllRoutes(new CloseToCloseVolatility(2), bars, new[] { 0d, expected, expected });
    }

    [Fact]
    public void YangZhangOmitsUndefinedResidualWithoutChangingDivisors()
    {
        var bars = new[]
        {
            new Bar(DateTime.UnixEpoch, 1, 1, 0, 0, 1),
            new Bar(DateTime.UnixEpoch.AddMinutes(1), 2, 2, 1, 1, 1),
            new Bar(DateTime.UnixEpoch.AddMinutes(2), 4, 4, 2, 2, 1)
        };
        CheckAllRoutes(new YangZhangVolatility(2), bars, new[] { 0d, 0, Math.Log(2) * Math.Sqrt(252) });
    }

    [Fact]
    public void GarmanKlassClampsNegativeVarianceForSignedCandles()
    {
        var bars = Enumerable.Range(0, 3).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 1, 2, -1, 2, 1)).ToArray();
        CheckAllRoutes(new GarmanKlassVolatility(2), bars, new double[3], new double[3]);
    }

    private static void CheckAllRoutes(IIndicator indicator, Bar[] bars, params double[][] expected)
    {
        StockData Data() => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        var builtIn = (IBuiltInIndicator)indicator;
        var keys = OoplesFinance.StockIndicators.Builder.GeneratedIndicatorOutputs.KeysFor(builtIn.BatchName).ToArray();
        var rules = BuiltInFormulaReferences.For(indicator).ToArray();
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("hand-example", bars, expected, 0));
        void Check(double[] actual, int slot)
        {
            Assert.Equal(expected[slot].Length, actual.Length);
            for (var i = 0; i < actual.Length; i++)
                Assert.True(new IndicatorErrorBudget(0, 1e-12, requireSameSign: true).Accepts(expected[slot][i], actual[i]),
                    $"{indicator.GetType().Name}/{keys[slot]} bar {i}: {actual[i]} expected {expected[slot][i]}");
        }
        Assert.True(OoplesFinance.StockIndicators.Builder.Compute.BuilderArmBinding.TryGetTarget(builtIn.CreateOptions().GetType(), out var target));
        for (var slot = 0; slot < keys.Length; slot++)
        {
            var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), keys[slot]);
            Check(OoplesFinance.StockIndicators.Builder.Compute.BuilderArmBinding.Compute(Data(), spec, target).ToArray(), slot);
            using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
            using var dispatched = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.TryComputeFast(Data(), spec, context);
            using var arm = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeArm(Data(), spec, context);
            Assert.NotNull(dispatched);
            Assert.NotNull(arm);
            Check(dispatched.Value.ToArray(), slot);
            Check(arm.Value.ToArray(), slot);
        }
        var stateSpec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions());
        foreach (var state in new[] { StatefulIndicatorFactory.Create(stateSpec), StreamingIndicatorFactory.CreateState(stateSpec) })
        {
            Assert.NotNull(state);
            using var lifetime = state as IDisposable;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                var output = keys.Select(_ => new List<double>()).ToArray();
                foreach (var bar in bars)
                {
                    var input = new OhlcvBar("OHLC", BarTimeframe.Minutes(1), bar.Time, bar.Time, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true);
                    var preview = state.Update(input, false, true);
                    var final = state.Update(input, true, true);
                    for (var slot = 0; slot < keys.Length; slot++)
                    {
                        Assert.Equal(preview.Outputs![keys[slot]], final.Outputs![keys[slot]]);
                        output[slot].Add(final.Outputs[keys[slot]]);
                    }
                }
                for (var slot = 0; slot < keys.Length; slot++) Check(output[slot].ToArray(), slot);
            }
        }
    }

    [Theory, MemberData(nameof(Cases))]
    public void RawArmPreservesSelectedCloseAndOriginalCandleFields(IndicatorValidationCase testCase)
    {
        var indicator = testCase.Factory();
        var builtIn = (IBuiltInIndicator)indicator;
        var count = Math.Max(64, indicator.WarmupBars + 8);
        var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 30, 100, 1, 10 + i % 7, 1)).ToArray();
        var keys = GeneratedIndicatorOutputs.KeysFor(builtIn.BatchName).ToArray();
        var output = new double[keys.Length][];
        for (var slot = 0; slot < keys.Length; slot++)
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(_ => 50d), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            data.CustomValuesList = bars.Select(b => b.Close).ToList();
            using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
            using var arm = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeArm(data,
                new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), keys[slot]), context);
            Assert.NotNull(arm);
            output[slot] = arm.Value.ToArray();
        }
        foreach (var rule in BuiltInFormulaReferences.For(indicator))
            rule.Check(new IndicatorValidationContext("raw-selected-price", bars, output, 0));
    }

    [Theory, MemberData(nameof(Cases))]
    public void ContractsDistinguishTinyVolatilityFromExactZero(IndicatorValidationCase testCase)
    {
        var indicator = testCase.Factory();
        var count = Math.Max(64, indicator.WarmupBars + 8);
        var next = Math.BitIncrement(1d);
        var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 1, next, 1, i % 2 == 0 ? 1 : next, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(indicator).ToArray();
        var erased = rules.Select(_ => new double[count]).ToArray();
        var oneReturn = ((IBuiltInIndicator)indicator).CreateOptions() is OoplesFinance.StockIndicators.Builder.Specs.CloseToCloseVolatilitySpecOptions { Length: 1 };
        foreach (var rule in rules)
        {
            var context = new IndicatorValidationContext("erased-tiny-volatility", bars, erased, 0);
            if (oneReturn) rule.Check(context); // A one-observation population variance is exactly zero.
            else Assert.Throws<InvalidOperationException>(() => rule.Check(context));
        }
    }

    [Fact]
    public void NearUnityLogsAreCorrectlyRoundedBeforeVariance()
    {
        foreach (var previous in new[] { 1e-200, 1d, 1e12, 1e200, double.MaxValue / 2 })
        foreach (var steps in new[] { 1, 3, 17, 1024 })
        {
            var current = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(previous) + steps);
            var expected = (ReferenceFraction.FromDouble(current) / ReferenceFraction.FromDouble(previous)).LogToDouble();
            Assert.Equal(expected, OoplesFinance.StockIndicators.Helpers.StableLogRatio.Of(current, previous));
            var reverse = (ReferenceFraction.FromDouble(previous) / ReferenceFraction.FromDouble(current)).LogToDouble();
            Assert.Equal(reverse, OoplesFinance.StockIndicators.Helpers.StableLogRatio.Of(previous, current));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "CloseToCloseVolatility", "ParkinsonVolatility", "GarmanKlassVolatility", "RogersSatchellVolatility", "YangZhangVolatility"
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
