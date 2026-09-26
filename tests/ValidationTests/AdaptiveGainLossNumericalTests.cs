using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder.Compute;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AdaptiveGainLossNumericalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(-1074)]
    public async Task RapidWindowHasTheSameBoundedShareAtEveryScale(int exponent)
    {
        var v = Math.ScaleB(1d, exponent);
        var prices = new[] { -v, v, -v, 0d, v, v };
        var bars = BarsOf(prices); var expected = new[] { 100d, 100, 50, 100d / 3, 100, 100 };
        var indicator = new RapidRelativeStrengthIndex(2);
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, result[indicator.Outputs[0]].ToArray());
        Assert.Equal(expected, BuiltInFormulaReferences.AdaptiveGainLossOutputs(bars, indicator)["Rrsi"]);
        Assert.Equal(expected, Data(prices).CalculateRapidRelativeStrengthIndex(length: 2).CustomValuesList);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeRapidRsiFast(Data(prices), context, 2);
        Assert.Equal(expected, raw.Span.ToArray());
        using var state = new RapidRelativeStrengthIndexState(length: 2); CheckState(state, bars, expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1022)]
    [InlineData(-1074)]
    public async Task AsymmetricCountsRetainQuietLegsAndCountZeroReturns(int exponent)
    {
        var v = Math.ScaleB(1d, exponent);
        await CheckAsymmetric(new[] { v, 2 * v, v, 0d, v, 2 * v }, 1, new[] { 100d, 100, 200d / 3, 50, 0, 50 });
    }

    [Fact]
    public Task WideOpposingReturnsRecoverAFiniteRatio() => CheckAsymmetric(
        new[] { double.Epsilon, -double.MaxValue, double.Epsilon, double.MaxValue }, 3, new[] { 100d, 0, 0, 800d / 9 });

    private static async Task CheckAsymmetric(double[] prices, int length, double[] expected)
    {
        var bars = BarsOf(prices);
        IIndicator[] indicators = [new AsymmetricalRelativeStrengthIndex(length), new AsymmetricalRsi(length, 7)];
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).BuildAsync();
        foreach (var indicator in indicators)
        {
            Assert.Equal(expected, result[indicator.Outputs[0]].ToArray());
            Assert.Equal(expected, BuiltInFormulaReferences.AdaptiveGainLossOutputs(bars, (IBuiltInIndicator)indicator)["Arsi"]);
        }
        Assert.Equal(expected, Data(prices).CalculateAsymmetricalRelativeStrengthIndex(length).CustomValuesList);
        var core = new double[prices.Length]; OscillatorCore.AsymmetricalRelativeStrengthIndex(prices, core, length); Assert.Equal(expected, core);
        OscillatorCore.AsymmetricalRsi(prices, core, length, 99); Assert.Equal(expected, core);
        using var context = new ComputeContext();
        using var first = IndicatorCompute.ComputeAsymmetricalRsiFast(Data(prices), context, length);
        using var second = IndicatorCompute.ComputeAsymmetricalRelativeStrengthIndexFast(Data(prices), context, length);
        Assert.Equal(expected, first.Span.ToArray()); Assert.Equal(expected, second.Span.ToArray());
        using var state = new AsymmetricalRelativeStrengthIndexState(length); CheckState(state, bars, expected);
    }

    [Fact]
    public void AsymmetricCoresAcceptEmptyInputsAndRejectShortOutputs()
    {
        OscillatorCore.AsymmetricalRelativeStrengthIndex(Array.Empty<double>(), Array.Empty<double>());
        OscillatorCore.AsymmetricalRsi(Array.Empty<double>(), Array.Empty<double>());
        Assert.Throws<ArgumentException>(() => OscillatorCore.AsymmetricalRelativeStrengthIndex(new[] { 1d }, Array.Empty<double>()));
        Assert.Throws<ArgumentException>(() => OscillatorCore.AsymmetricalRsi(new[] { 1d }, Array.Empty<double>()));
    }

    [Fact]
    public void RawArmsReadASelectedSeriesDistinctFromStoredCloses()
    {
        var prices = new[] { 1d, 2, 1, 0, 1, 2 };
        var data = Data(Enumerable.Repeat(10d, 6).ToArray()); data.CustomValuesList = prices.ToList();
        using var context = new ComputeContext();
        using var first = IndicatorCompute.ComputeAsymmetricalRsiFast(data, context, 1);
        using var second = IndicatorCompute.ComputeAsymmetricalRelativeStrengthIndexFast(data, context, 1);
        using var rapid = IndicatorCompute.ComputeRapidRsiFast(data, context, 1);
        Assert.Equal(new[] { 100d, 100, 200d / 3, 50, 0, 50 }, first.Span.ToArray());
        Assert.Equal(first.Span.ToArray(), second.Span.ToArray());
        Assert.Equal(new[] { 100d, 100, 0, 0, 100, 100 }, rapid.Span.ToArray());
    }

    private static Bar[] BarsOf(double[] prices) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static StockData Data(double[] prices) => new(prices, prices, prices, prices, Enumerable.Repeat(1d, prices.Length), prices.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
    private static OhlcvBar Native(Bar b) => new("ADAPTIVE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static void CheckState(IStreamingIndicatorState state, Bar[] bars, double[] expected)
    {
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                Assert.Equal(expected[i], state.Update(Native(bars[i]), false, true).Value);
                Assert.Equal(expected[i], state.Update(Native(bars[i]), true, true).Value);
            }
        }
    }

    private sealed class HalfAverage : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State();
        private sealed class State : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => bar.Close / 2;
        }
    }
    [Fact]
    public async Task RapidPublicAndRawSignalHonorTheCustomerAverage()
    {
        var indicator = new RapidRelativeStrengthIndex(1, new HalfAverage());
        var prices = new[] { 1d, 2, 1 };
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsOf(prices))).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(new[] { 100d, 100, 0 }, result[indicator.Outputs[0]].ToArray());
        Assert.Equal(new[] { 50d, 50, 0 }, result[indicator.Outputs[1]].ToArray());
        using var scope = ComponentAverage.Arm((values, _) => values.Select(v => v / 2).ToArray());
        using var context = new ComputeContext();
        var builtIn = (IBuiltInIndicator)indicator;
        var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), "Signal");
        using var raw = IndicatorCompute.ComputeArm(Data(prices), spec, context);
        Assert.NotNull(raw); Assert.Equal(new[] { 50d, 50, 0 }, raw.Value.ToArray()); Assert.Equal(1, ComponentAverage.Substitutions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeAdaptiveGainLossRejectsInvalidFieldsWithoutAdvancing(bool relative)
    {
        IStreamingIndicatorState Create() => relative ? new AsymmetricalRelativeStrengthIndexState(2) : new RapidRelativeStrengthIndexState(length: 2);
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            var state = Create(); var control = Create();
            using var stateLifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var v in new[] { 3d, 1, 4 })
            {
                var seed = Native(new Bar(DateTime.UnixEpoch, 2, 4, 1, v, 1));
                state.Update(seed, true, true); control.Update(seed, true, true);
            }
            var fields = new[] { 2d, 4, 1, 2, 1 }; fields[field] = invalid;
            var bad = new OhlcvBar("GAINLOSS", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, fields[0], fields[1], fields[2], fields[3], fields[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(new Bar(DateTime.UnixEpoch, 2, 4, 1, 2, 1));
            var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value);
            Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "RapidRelativeStrengthIndex", "AsymmetricalRsi", "AsymmetricalRelativeStrengthIndex"
    };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.AdaptiveGainLossOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

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
