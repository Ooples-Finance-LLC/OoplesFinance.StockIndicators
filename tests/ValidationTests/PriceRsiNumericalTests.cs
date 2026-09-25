using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder.Compute;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PriceRsiNumericalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(-1074)]
    public async Task WideAndTinyChangesPreserveBoundedRsi(int exponent)
    {
        var v = Math.ScaleB(1d, exponent); var prices = new[] { -v, v, -v, 0d, v, v };
        var bars = BarsOf(prices); var expected = new[] { 100d, 100, 0, 100, 100, 100 };
        var indicator = new Rsi(1);
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, result[indicator.Outputs[0]].ToArray());
        Assert.Equal(expected, BuiltInFormulaReferences.PriceRsiOutputs(bars, indicator)["Rsi"]);
        Assert.Equal(expected, Data(prices).CalculateRelativeStrengthIndex(length: 1).CustomValuesList);
        var core = new double[prices.Length]; OscillatorCore.RelativeStrengthIndex(prices, core, 1); Assert.Equal(expected, core);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeRsiFast(Data(prices), context, 1);
        Assert.Equal(expected, raw.Span.ToArray());
        using var state = new RelativeStrengthIndexState(length: 1);
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

    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public async Task FlatPriceRunsRetainTheirRatioAfterSmoothingUnderflows(MovingAvgType kind)
    {
        var prices = new[] { -1d, 1, 0 }.Concat(Enumerable.Repeat(0d, 1200)).Append(1d).ToArray();
        var plateau = kind == MovingAvgType.ExponentialMovingAverage ? 100d / 3 : 50;
        var expected = new[] { 100d, 100 }.Concat(Enumerable.Repeat(plateau, 1201)).Append(100d).ToArray();
        var indicator = new Rsi(2, kind == MovingAvgType.ExponentialMovingAverage ? new Ema(2) : new Wwma(2));
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsOf(prices))).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, result[indicator.Outputs[0]].ToArray());
        Assert.Equal(expected, Data(prices).CalculateRelativeStrengthIndex(kind, 2).CustomValuesList);
        using var internalState = new RsiState(kind, 2);
        Assert.Equal(expected, prices.Select(p => internalState.Next(p, true)).ToArray());
    }

    [Fact]
    public void RawRsiUsesSelectedPricesAndCoreHandlesEmptyInput()
    {
        var data = Data(new[] { 10d, 10, 10 }); data.CustomValuesList = new List<double> { 1, 2, 1 };
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeRsiFast(data, context, 1);
        Assert.Equal(new[] { 100d, 100, 0 }, raw.Span.ToArray());
        OscillatorCore.RelativeStrengthIndex(Array.Empty<double>(), Array.Empty<double>(), 2);
        Assert.Throws<ArgumentException>(() => OscillatorCore.RelativeStrengthIndex(new[] { 1d }, Array.Empty<double>(), 2));
    }

    private static Bar[] BarsOf(double[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static StockData Data(double[] values) => new(values, values, values, values, Enumerable.Repeat(1d, values.Length), values.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
    private static OhlcvBar Native(Bar b) => new("RSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    private sealed class CustomerAverage(double factor, double offset) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(factor, offset);
        private sealed class State(double factor, double offset) : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => factor * bar.Close + offset;
        }
    }

    [Fact]
    public async Task PublicRsiPreservesThreeCustomerAverageStages()
    {
        var constructor = typeof(Rsi).GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var stages = new IMovingAverage[] { new CustomerAverage(0, 2), new CustomerAverage(0, 6), new CustomerAverage(.5, 0) };
        var stage = 0;
        var args = constructor.GetParameters().Select(p => typeof(IMovingAverage).IsAssignableFrom(p.ParameterType)
            ? (object)stages[stage++] : p.ParameterType == typeof(int) ? 2 : p.DefaultValue).ToArray();
        Assert.Equal(3, stage);
        var indicator = (IIndicator)constructor.Invoke(args);
        var bars = new[] { 1d, 3, 2, 4 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(Enumerable.Repeat(25d, 4), result[indicator.Outputs[0]].ToArray());
        Assert.Equal(Enumerable.Repeat(12.5, 4), result[indicator.Outputs[1]].ToArray());
        Assert.Equal(Enumerable.Repeat(12.5, 4), result[indicator.Outputs[2]].ToArray());
    }

    [Fact]
    public void NativeRsiRejectsInvalidFieldsWithoutAdvancing()
    {
        IStreamingIndicatorState Create() => new RelativeStrengthIndexState(length: 2);
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

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "Rsi" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.PriceRsiOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

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
