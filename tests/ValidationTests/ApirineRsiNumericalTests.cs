using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder.Compute;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ApirineRsiNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(double[] prices) => new(prices, prices, prices, prices, Enumerable.Repeat(1d, prices.Length), BarsOf(prices).Select(b => b.Time));

    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(-1074)]
    public async Task ResidualStagesPreserveWideDifferencesAndSubnormalRounding(int exponent)
    {
        var v = Math.ScaleB(1d, exponent); var prices = new[] { -v, v, -v, 0d, v, v };
        var expected = exponent == -1074 ? Enumerable.Repeat(100d, 6).ToArray() : new[] { 0d, 100, 0, 100, 100, 100 };
        var bars = BarsOf(prices); var indicator = new ApirineSlowRelativeStrengthIndex(1, 2);
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, result[indicator.Outputs[0]].ToArray());
        Assert.Equal(expected, Data(prices).CalculateApirineSlowRelativeStrengthIndex(length: 1, smoothLength: 2).CustomValuesList);
        using var state = new ApirineSlowRelativeStrengthIndexState(length: 1, smoothLength: 2);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                Assert.Equal(expected[i], state.Update(Native(bars[i]), false, true).Value);
                Assert.Equal(expected[i], state.Update(Native(bars[i]), true, true).Value);
            }
        }
    }

    [Fact]
    public void RawArmPreservesSelectedPrices()
    {
        var prices = new[] { 1d, 3, 1, 2 };
        var expected = BuiltInFormulaReferences.ApirineRsiOutputs(BarsOf(prices), new ApirineSlowRelativeStrengthIndex(2, 3))["Asrsi"];
        var data = Data(new[] { 9d, 9, 9, 9 }); data.CustomValuesList = prices.ToList();
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeApirineSlowRsiFast(data, context, 2, 3); Assert.Equal(expected, raw.Span.ToArray());
    }

    private sealed class ConstantAverage(double value) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(value);
        private sealed class State(double value) : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => value;
        }
    }

    [Fact]
    public async Task CustomerStagesPreserveDistinctPriceGainAndLossAverages()
    {
        var ctor = typeof(ApirineSlowRelativeStrengthIndex).GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var stages = new IMovingAverage[] { new ConstantAverage(1), new ConstantAverage(2), new ConstantAverage(6) }; var stage = 0;
        var args = ctor.GetParameters().Select(p => typeof(IMovingAverage).IsAssignableFrom(p.ParameterType) ? (object)stages[stage++] : p.ParameterType == typeof(int) ? 2 : p.DefaultValue).ToArray();
        Assert.Equal(3, stage); var indicator = (IIndicator)ctor.Invoke(args);
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsOf(new[] { 1d, 3, 2, 4 }))).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(Enumerable.Repeat(25d, 4), result[indicator.Outputs[0]].ToArray());
    }

    [Fact]
    public void NativeApirineRsiRejectsInvalidFieldsWithoutAdvancing()
    {
        IStreamingIndicatorState Create() => new ApirineSlowRelativeStrengthIndexState(length: 2);
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

    private static OhlcvBar Native(Bar b) => new("FOLDED", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "ApirineSlowRelativeStrengthIndex" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.ApirineRsiOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

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
