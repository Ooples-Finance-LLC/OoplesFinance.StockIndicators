using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TrueStrengthNumericalTests
{
    public static IEnumerable<object[]> ExtremeHandExamples => new[]
    {
        new object[] { new[] { double.MaxValue, -double.MaxValue, double.MaxValue }, new[] { 0d, -100, 100 } },
        new object[] { new[] { double.Epsilon, -double.Epsilon, double.Epsilon }, new[] { 0d, -100, 100 } },
        new object[] { new[] { 0d, double.MaxValue, double.MaxValue / 2 }, new[] { 0d, 100, -100 } },
        new object[] { new[] { 3d, 3, 3 }, new[] { 0d, 0, 0 } }
    };

    [Theory, MemberData(nameof(ExtremeHandExamples))]
    public void BoundedRatioSurvivesUnboundedDifferencesAndTinyChanges(double[] prices, double[] expected)
    {
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var coreOutput = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.OscillatorCore.TrueStrengthIndex(prices, coreOutput, 1, 1);
        Assert.Equal(expected, coreOutput);
        OoplesFinance.StockIndicators.Core.OscillatorCore.TrueStrengthIndex(Array.Empty<double>(), Array.Empty<double>(), int.MaxValue, int.MaxValue);
        foreach (var indicator in new IBuiltInIndicator[] { new TrueStrengthIndex(1, 1, 1), new ErgodicTrueStrengthIndexV1(1, 1, 1, 1) })
        {
            var key = indicator.BatchName == IndicatorName.TrueStrengthIndex ? "Tsi" : "Etsi";
            var reference = BuiltInFormulaReferences.StrengthOutputs(bars, indicator);
            Assert.Equal(expected, reference[key]);
            Assert.Equal(expected, reference["Signal"]);
            foreach (var selected in new[] { false, true })
            {
                var raw = selected ? prices.Select(_ => 100d).ToArray() : prices;
                var data = new StockData(raw, raw, raw, raw, prices.Select(_ => 1d), bars.Select(b => b.Time));
                if (selected) data.CustomValuesList = prices.ToList();
                foreach (var output in new[] { key, "Signal" })
                {
                    var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), output);
                    using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
                    using var arm = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeArm(data, spec, context);
                    Assert.NotNull(arm);
                    Assert.Equal(expected, arm.Value.ToArray());
                }
            }
        }
    }

    [Fact]
    public void WideStagesPreserveCancellationAfterExtremeInputsLeaveTheWindow()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage,
                     MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            using var stage = new OoplesFinance.StockIndicators.Helpers.StrengthAverage(kind, 1);
            var wide = new OoplesFinance.StockIndicators.Helpers.StrengthValue(double.MaxValue, true);
            Assert.True(stage.Next(wide, true).Doubled);
            Assert.Equal(double.Epsilon, stage.Next(new(double.Epsilon), false).Mantissa);
            Assert.Equal(double.Epsilon, stage.Next(new(double.Epsilon), true).Mantissa);
            stage.Reset();
            Assert.Equal(1d, stage.Next(new(1d), true).Mantissa);
        }
    }

    [Fact]
    public void SmoothedDenominatorCanExceedBinary64WhileRatioRemainsFinite()
    {
        var large = Math.ScaleB(1.5, 1023);
        var prices = new[] { large, -large, 0d };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var indicator = new TrueStrengthIndex(2, 1, 1, new Sma(2));
        var expected = new[] { 0d, -100, -100d / 3 };
        Assert.Equal(expected, BuiltInFormulaReferences.StrengthOutputs(bars, indicator)["Tsi"]);
        var data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), bars.Select(b => b.Time));
        Assert.Equal(expected, data.CalculateTrueStrengthIndex(MovingAvgType.SimpleMovingAverage, 2, 1, 1).CustomValuesList);
    }

    [Fact]
    public void FastSignalRouteHonorsEveryCustomerAverageStage()
    {
        foreach (var indicator in new IBuiltInIndicator[] { new TrueStrengthIndex(2, 3, 4), new ErgodicTrueStrengthIndexV1(2, 3, 4, 5),
                     new ErgodicTrueStrengthIndexV2(2, 3, 4, 5, 6, 7, 8) })
        {
            var averages = indicator.BatchName == IndicatorName.TrueStrengthIndex ? 4 : 6;
            var requests = Enumerable.Range(0, averages + 1)
                .Select(i => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((values, _) =>
                    values.Select(v => i == averages ? v / 2 : v).ToArray())).ToArray();
            var prices = new[] { 1d, 2, 1 };
            var data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), prices.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
            using var armed = OoplesFinance.StockIndicators.Builder.Compute.ComponentAverage.Arm(requests);
            using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
            var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), "Signal");
            using var result = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeArm(data, spec, context);
            Assert.NotNull(result);
            Assert.Equal(new[] { 0d, 50, -50 }, result.Value.ToArray());
            Assert.Equal(averages + 1, OoplesFinance.StockIndicators.Builder.Compute.ComponentAverage.Substitutions);
        }
    }

    [Fact]
    public void NativePreviewAndReplayMatchRationalStages()
    {
        var large = Math.ScaleB(1.5, 1023);
        var prices = new[] { large, -large, 0d, large, 1d, -1d, double.Epsilon, -double.Epsilon, 3d, 8d, 2d, 6d };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        foreach (var indicator in new IBuiltInIndicator[] { new TrueStrengthIndex(2, 3, 4), new ErgodicTrueStrengthIndexV1(2, 3, 4, 5),
                     new ErgodicTrueStrengthIndexV2(2, 3, 4, 5, 6, 7, 8) })
        {
            var expected = BuiltInFormulaReferences.StrengthOutputs(bars, indicator);
            var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(indicator.BatchName, indicator.CreateOptions());
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Length; i++)
                    {
                        var bar = bars[i];
                        var input = new OhlcvBar("STRENGTH", BarTimeframe.Minutes(1), bar.Time, bar.Time, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true);
                        for (var update = 0; update < 3; update++)
                        {
                            var actual = state.Update(input, update == 2, true);
                            foreach (var pair in expected) Assert.Equal(pair.Value[i], actual.Outputs![pair.Key]);
                        }
                    }
                }
            }
        }
    }

    private sealed class CustomerScale(double factor) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(factor);
        private sealed class State(double factor) : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => factor * bar.Close;
        }
    }

    [Fact]
    public async Task PublicTrueStrengthUsesAllFiveCustomerComponents()
    {
        var indicator = new TrueStrengthIndex(2, 3, 4, new CustomerScale(1), new CustomerScale(1),
            new CustomerScale(1), new CustomerScale(1), new CustomerScale(.5));
        var bars = new[] { 1d, 2, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(new[] { 0d, 100, -100 }, result[indicator.Value].ToArray());
        Assert.Equal(new[] { 0d, 50, -50 }, result[indicator.Signal].ToArray());
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "Tsi", "TrueStrengthIndex", "ErgodicTrueStrengthIndexV1", "ErgodicTrueStrengthIndexV2"
    };
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
