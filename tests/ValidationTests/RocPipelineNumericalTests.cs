using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RocPipelineNumericalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1019)]
    [InlineData(-1074)]
    public void FixedKernelsAndSentinelsMatchHandCalculations(int exponent)
    {
        var prices = new[] { -20d, 20, -20, 0, 10, 20 }.Select(v => Math.ScaleB(v, exponent)).ToArray();
        var momentum = new[] { 0d, -2000, -2000, -1000, 0, 1000 };
        var coppock = new[] { 0d, -200, -200, -200, -150, 100 };
        var smoothed = new[] { 100d, -200, -200, -100, 100, 100 };
        var times = prices.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)).ToArray();
        StockData Data(bool selected = false)
        {
            var raw = selected ? prices.Select(_ => 100d).ToArray() : prices;
            var data = new StockData(raw, raw, raw, raw, prices.Select(_ => 1d), times);
            if (selected) data.CustomValuesList = prices.ToList();
            return data;
        }
        Assert.Equal(momentum, Data().CalculatePriceMomentumOscillator(length1: 2, length2: 2, signalLength: 1).CustomValuesList);
        var decision = Data().CalculateDecisionPointPriceMomentumOscillator(length1: 2, length2: 2, signalLength: 1);
        Assert.Equal(momentum, decision.CustomValuesList);
        Assert.All(decision.OutputValues["Histogram"], value => Assert.Equal(0d, value));
        Assert.Equal(coppock, Data().CalculateCoppockCurve(length: 1, fastLength: 1, slowLength: 2).CustomValuesList);
        Assert.Equal(smoothed, Data().CalculateSmoothedRateOfChange(length: 1, smoothingLength: 1).CustomValuesList);
        var core = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.OscillatorCore.PriceMomentumOscillator(prices, core, 2, 2);
        Assert.Equal(momentum, core);
        OoplesFinance.StockIndicators.Core.OscillatorCore.DecisionPointPriceMomentumOscillator(prices, core, 2, 2);
        Assert.Equal(momentum, core);
        OoplesFinance.StockIndicators.Core.OscillatorCore.CoppockCurve(prices, core, 2, 1, 1);
        Assert.Equal(coppock, core);
        OoplesFinance.StockIndicators.Core.OscillatorCore.SmoothedRateOfChange(prices, core, 1, 1);
        Assert.Equal(smoothed, core);
        foreach (var selected in new[] { false, true })
        {
            using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
            using var pmo = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputePriceMomentumOscillatorFast(Data(selected), context, 2, 2, signalLength: 1);
            using var dppmo = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeDecisionPointPriceMomentumOscillatorFast(Data(selected), context, 2, 2);
            using var cc = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeCoppockCurveFast(Data(selected), context, 1, fastLength: 1, slowLength: 2);
            using var sroc = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeSmoothedRateOfChangeFast(Data(selected), context, 1, 1);
            Assert.Equal(momentum, pmo.Span.ToArray()); Assert.Equal(momentum, dppmo.Span.ToArray());
            Assert.Equal(coppock, cc.Span.ToArray()); Assert.Equal(smoothed, sroc.Span.ToArray());
        }
        foreach (var (state, expected) in new (IStreamingIndicatorState, double[])[]
        {
            (new PriceMomentumOscillatorState(length1: 2, length2: 2, signalLength: 1), momentum),
            (new DecisionPointPriceMomentumOscillatorState(length1: 2, length2: 2, signalLength: 1), momentum),
            (new CoppockCurveState(length: 1, fastLength: 1, slowLength: 2), coppock),
            (new SmoothedRateOfChangeState(length: 1, smoothingLength: 1), smoothed)
        })
        {
            using var lifetime = state as IDisposable;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < prices.Length; i++)
                {
                    var v = prices[i];
                    var bar = new OhlcvBar("PIPELINE", BarTimeframe.Minutes(1), times[i], times[i], v, v, v, v, 1, true);
                    Assert.Equal(expected[i], state.Update(bar, false, true).Value);
                    Assert.Equal(expected[i], state.Update(bar, true, true).Value);
                }
            }
        }
    }

    [Fact]
    public async Task WideUnpublishedReturnBecomesFiniteAfterFixedSmoothing()
    {
        var prices = new[] { 32d, Math.ScaleB(1d, 1023) };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var indicator = new PriceMomentumOscillator(64, 2, 1);
        var expected = new[] { 0d, Math.ScaleB(.9765625, 1023) };
        Assert.Equal(expected, BuiltInFormulaReferences.RocPipelineOutputs(bars, indicator)["Pmo"]);
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, result[indicator.Value].ToArray());
        Assert.Equal(expected, result[indicator.Signal].ToArray());
    }

    [Fact]
    public async Task SmoothedMeanUsesTheRequestedLag()
    {
        var prices = new[] { 1d, 2, 4, 8 };
        var expected = new[] { 100d, 100, 300, 300 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var indicator = new SmoothedRateOfChange(2, 1);
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, result[indicator].ToArray());
        using var state = new SmoothedRateOfChangeState(length: 2, smoothingLength: 1);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var b = bars[i];
                var input = new OhlcvBar("LAG", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                Assert.Equal(expected[i], state.Update(input, false, true).Value);
                Assert.Equal(expected[i], state.Update(input, true, true).Value);
            }
        }
    }

    [Fact]
    public void CoreDefaultsMatchPublishedPeriodsAndFormulas()
    {
        var prices = Enumerable.Range(0, 64).Select(i => 5d + i % 13).ToArray();
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var output = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.OscillatorCore.PriceMomentumOscillator(prices, output);
        Assert.Equal(BuiltInFormulaReferences.RocPipelineOutputs(bars, new PriceMomentumOscillator())["Pmo"], output);
        OoplesFinance.StockIndicators.Core.OscillatorCore.DecisionPointPriceMomentumOscillator(prices, output);
        Assert.Equal(BuiltInFormulaReferences.RocPipelineOutputs(bars, new DecisionPointPriceMomentumOscillator(14))["Dppmo"], output);
        OoplesFinance.StockIndicators.Core.OscillatorCore.CoppockCurve(prices, output);
        Assert.Equal(BuiltInFormulaReferences.RocPipelineOutputs(bars, new CoppockCurve(10))["Cc"], output);
        OoplesFinance.StockIndicators.Core.OscillatorCore.SmoothedRateOfChange(prices, output);
        Assert.Equal(BuiltInFormulaReferences.RocPipelineOutputs(bars, new SmoothedRateOfChange())["Sroc"], output);
    }

    private sealed class CustomerTransform(double factor, double offset = 0) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(factor, offset);
        private sealed class State(double factor, double offset) : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => bar.Close * factor + offset;
        }
    }

    [Fact]
    public async Task CustomerAveragesDoNotReplaceTheFixedMomentumKernels()
    {
        var indicator = new PriceMomentumOscillator(2, 2, 1, new CustomerTransform(.5));
        var coppock = new CoppockCurve(1, new CustomerTransform(0, 7));
        var smoothed = new SmoothedRateOfChange(1, 1, new CustomerTransform(0));
        var prices = new[] { -Math.ScaleB(1d, 1023), Math.ScaleB(1d, 1023) };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator, coppock, smoothed).BuildAsync();
        Assert.Equal(new[] { 0d, -2000 }, result[indicator.Value].ToArray());
        Assert.Equal(new[] { 0d, -1000 }, result[indicator.Signal].ToArray());
        Assert.Equal(new[] { 7d, 7 }, result[coppock].ToArray());
        Assert.Equal(new[] { 100d, 100 }, result[smoothed].ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void NativeRoutesRejectNonfiniteFieldsBeforeChangingState(int family)
    {
        IStreamingIndicatorState Create() => family switch
        {
            0 => new PriceMomentumOscillatorState(length1: 2, length2: 2, signalLength: 2),
            1 => new DecisionPointPriceMomentumOscillatorState(length1: 2, length2: 2, signalLength: 2),
            2 => new CoppockCurveState(length: 2, fastLength: 1, slowLength: 2),
            3 => new SmoothedRateOfChangeState(length: 1, smoothingLength: 2),
            4 => new KnowSureThingState(length1: 1, length2: 1, length3: 1, length4: 1,
                rocLength1: 1, rocLength2: 1, rocLength3: 1, rocLength4: 1, signalLength: 2),
            _ => new PringSpecialKState()
        };
        OhlcvBar Bar(double[] values) => new("INPUT", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch,
            values[0], values[1], values[2], values[3], values[4], true);
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            var state = Create(); var control = Create();
            using var lifetime = state as IDisposable;
            using var controlLifetime = control as IDisposable;
            foreach (var price in new[] { 2d, 4, 3 })
            {
                var seed = Bar(new[] { price, price, price, price, 1d });
                state.Update(seed, true, true); control.Update(seed, true, true);
            }
            var fields = new[] { 5d, 5, 5, 5, 1 }; fields[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Bar(fields), final, true));
            var good = Bar(new[] { 5d, 5, 5, 5, 1 });
            var actual = state.Update(good, true, true);
            var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value);
            Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "PriceMomentumOscillator", "Pmo", "DecisionPointPriceMomentumOscillator", "CoppockCurve", "SmoothedRateOfChange", "SmoothedRoc"
    };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.RocPipelineOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

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
