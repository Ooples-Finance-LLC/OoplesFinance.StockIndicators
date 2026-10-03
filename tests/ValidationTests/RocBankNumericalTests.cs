using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RocBankNumericalTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1019, false)]
    [InlineData(-1074, false)]
    [InlineData(0, true)]
    [InlineData(1019, true)]
    [InlineData(-1074, true)]
    public void WeightedBanksMatchHandReturnsAtEveryScale(int exponent, bool special)
    {
        var prices = new[] { -20d, 20, -20, 0, 20, 20 }.Select(v => Math.ScaleB(v, exponent)).ToArray();
        var expected = special ? new[] { 0d, -6000, -6000, -3000, 0, 0 } : new[] { 0d, -2000, -2000, -1000, 0, 0 };
        IIndicator indicator = special ? new PringSpecialK(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1)
            : new KnowSureThing(1, 1, 1, 1, 1, 1, 1, 1, 1);
        CheckHand(indicator, prices, expected);
        if (!special)
        {
            var core = new double[prices.Length];
            OoplesFinance.StockIndicators.Core.OscillatorCore.KnowSureThing(prices, core, 1, 1, 1, 1, 1, 1, 1, 1);
            Assert.Equal(expected, core);
        }
    }

    [Fact]
    public void OverflowingUnpublishedReturnsCancelToFinitePublishedValues()
    {
        var indicator = new KnowSureThing(1, 1, 1, 1, 2, 1, 2, 2, 1);
        CheckHand(indicator, new[] { 4 * double.Epsilon, -double.Epsilon, double.MaxValue }, new[] { 0d, -250, 0 });
    }

    [Fact]
    public void UnpublishedOverflowBecomesAFiniteSmoothedValue()
    {
        var prices = Enumerable.Repeat(Math.ScaleB(1d, 1023), 16).ToArray();
        prices[0] = 32;
        var expected = new double[16];
        expected[15] = Math.ScaleB(1.953125, 1023);
        CheckHand(new KnowSureThing(16, 16, 16, 16, 1, 1, 1, 1, 1), prices, expected);
    }

    [Fact]
    public void DefaultSpecialKCoreUsesEveryPublishedHorizon()
    {
        var prices = Enumerable.Range(0, 600).Select(i => 10d + i % 37).ToArray();
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RocBankOutputs(bars, new SpecialK(14))["PringSpecialK"];
        var actual = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.OscillatorCore.SpecialK(prices, actual);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task GenuinePublishedOverflowHasIndependentRejectionEvidence()
    {
        var prices = new[] { double.Epsilon, double.MaxValue };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        KnowSureThing Create() => new(1, 1, 1, 1, 1, 1, 1, 1, 1);
        Assert.Equal(double.PositiveInfinity, BuiltInFormulaReferences.RocBankOutputs(bars, Create())["Kst"][1]);
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(typeof(KnowSureThing), "bank-overflow", Create), new()
        {
            AdditionalFixtures = new[] { new IndicatorValidationFixture("proven-bank-overflow", bars) }
        });
        report.ThrowIfInvalid();
        Assert.Equal(2, Assert.Single(report.FixtureEvidence, f => f.Name == "proven-bank-overflow").OutputOverflowRejectionsChecked);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(6, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(6, true)]
    public void DistinctPeriodsAndEverySmoothingKindMatchRationalStages(int kind, bool special)
    {
        IMovingAverage average = kind switch { 1 => new Sma(), 2 => new Wma(), 3 => new Ema(), _ => new Wwma() };
        IBuiltInIndicator indicator = special
            ? new PringSpecialK(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 3, average)
            : new KnowSureThing(3, 4, 5, 6, 1, 2, 3, 4, 2, average);
        var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(indicator.BatchName, indicator.CreateOptions());
        foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
        {
            Assert.NotNull(state);
            using var lifetime = state as IDisposable;
            for (var replay = 0; replay < 2; replay++)
            {
                var bars = Enumerable.Range(0, 64).Select(i =>
                {
                    var price = 5d + (i * (replay + 3)) % 23;
                    return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
                }).ToArray();
                var expected = BuiltInFormulaReferences.RocBankOutputs(bars, indicator);
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("BANK", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    var preview = state.Update(input, false, true);
                    var final = state.Update(input, true, true);
                    foreach (var pair in expected)
                    {
                        Assert.Equal(pair.Value[i], preview.Outputs![pair.Key]);
                        Assert.Equal(pair.Value[i], final.Outputs![pair.Key]);
                    }
                }
            }
        }
    }

    private static void CheckHand(IIndicator indicator, double[] prices, double[] expected)
    {
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var builtIn = (IBuiltInIndicator)indicator;
        var reference = BuiltInFormulaReferences.RocBankOutputs(bars, builtIn);
        foreach (var values in reference.Values) Assert.Equal(expected, values);
        var options = builtIn.CreateOptions();
        foreach (var key in reference.Keys)
        foreach (var selected in new[] { false, true })
        {
            var raw = selected ? prices.Select(_ => 100d).ToArray() : prices;
            var data = new StockData(raw, raw, raw, raw, prices.Select(_ => 1d), bars.Select(b => b.Time));
            if (selected) data.CustomValuesList = prices.ToList();
            using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
            using var arm = options is OoplesFinance.StockIndicators.Builder.Specs.KnowSureThingSpecOptions kst
                ? OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeKnowSureThingFast(data, context,
                    kst.RocLength1, kst.RocLength2, kst.RocLength3, kst.RocLength4, kst.Length1, kst.Length2, kst.Length3, kst.Length4,
                    signalLength: kst.SignalLength, outputKey: key)
                : OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputePringSpecialKFast(data, context,
                    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                    series: key == "Signal" ? OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.MacdSeries.Signal : OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.MacdSeries.Line);
            Assert.Equal(expected, arm.Span.ToArray());
        }
        var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(builtIn.BatchName, options);
        foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
        {
            Assert.NotNull(state);
            using var lifetime = state as IDisposable;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("BANK", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    Assert.Equal(expected[i], state.Update(input, false, true).Value);
                    Assert.Equal(expected[i], state.Update(input, true, true).Outputs!["Signal"]);
                }
            }
        }
    }

    private sealed class CustomerScale(double factor, double offset = 0) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(factor, offset);
        private sealed class State(double factor, double offset) : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => bar.Close * factor + offset;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicBanksHonorEveryCustomerStage(bool special)
    {
        IMovingAverage Stage(int index) => new CustomerScale(index / 16d, index);
        IIndicator indicator = special
            ? new PringSpecialK(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, Stage(1), Stage(2), Stage(3), Stage(4), Stage(5),
                Stage(6), Stage(7), Stage(8), Stage(9), Stage(10), Stage(11), Stage(12), new CustomerScale(.5))
            : new KnowSureThing(1, 1, 1, 1, 1, 1, 1, 1, 1, Stage(1), Stage(2), Stage(3), Stage(4), new CustomerScale(.5));
        var bars = new[] { 1d, 2, 4 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        var total = special ? 210d : 30d;
        Assert.Equal(new[] { total, 7.25 * total, 7.25 * total }, result[indicator.Outputs[0]].ToArray());
        Assert.Equal(new[] { total / 2, 3.625 * total, 3.625 * total }, result[indicator.Outputs[1]].ToArray());
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "KnowSureThing", "Kst", "PringSpecialK", "SpecialK"
    };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.RocBankOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

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
