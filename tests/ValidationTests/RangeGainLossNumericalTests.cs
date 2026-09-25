using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder.Compute;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RangeGainLossNumericalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(-1074)]
    public async Task WideAndTinyRangesPreserveTheHandCalculatedShare(int exponent)
    {
        var v = Math.ScaleB(1d, exponent);
        var prices = new[] { -v, v, -v, 0d };
        var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
        var expected = new[] { 100d, 100, 0, 100 };
        var indicator = new MomentaRelativeStrengthIndex(2, 1);
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, result[indicator.Outputs[0]].ToArray());
        Assert.Equal(expected, result[indicator.Outputs[1]].ToArray());
        Assert.Equal(expected, BuiltInFormulaReferences.RangeGainLossOutputs(bars, indicator)["Mrsi"]);
        var core = new double[prices.Length]; OscillatorCore.DoubleSmoothedRelativeStrengthIndex(prices, core, 2, 1, 1); Assert.Equal(expected, core);
        OscillatorCore.DoubleSmoothedRelativeStrengthIndex(prices, core, 1, 1, 1); Assert.Equal(expected, core);
        foreach (var state in new IStreamingIndicatorState[] { new MomentaRelativeStrengthIndexState(length1: 2, length2: 1), new DoubleSmoothedRelativeStrengthIndexState(length1: 2, length2: 1, length3: 1), new DoubleSmoothedRelativeStrengthIndexState(length1: 1, length2: 1, length3: 1) })
        {
            using var lifetime = state as IDisposable;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i]; var bar = new OhlcvBar("RANGE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    Assert.Equal(expected[i], state.Update(bar, false, true).Value);
                    Assert.Equal(expected[i], state.Update(bar, true, true).Value);
                }
            }
        }
        var data = new StockData(prices, prices, prices, prices, Enumerable.Repeat(1d, prices.Length), bars.Select(b => b.Time));
        using var context = new ComputeContext();
        using var first = IndicatorCompute.ComputeMomentaRelativeStrengthIndexFast(data, context, 2, 1);
        using var second = IndicatorCompute.ComputeDoubleSmoothedRelativeStrengthIndexFast(data, context, 2, 1, 1);
        Assert.Equal(expected, first.Span.ToArray()); Assert.Equal(expected, second.Span.ToArray());
        using var minimum = IndicatorCompute.ComputeDoubleSmoothedRelativeStrengthIndexFast(data, context, 1, 1, 1);
        Assert.Equal(expected, minimum.Span.ToArray());
    }

    [Fact]
    public void DoubleSmoothedCoreDefaultsMatchTheIndependentFormula()
    {
        var prices = Enumerable.Range(0, 80).Select(i => (double)(i % 13 - 5)).ToArray();
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RangeGainLossOutputs(bars, new DoubleSmoothedRelativeStrengthIndex(14))["Dsrsi"];
        var actual = new double[prices.Length]; OscillatorCore.DoubleSmoothedRelativeStrengthIndex(prices, actual); Assert.Equal(expected, actual);
        OscillatorCore.DoubleSmoothedRelativeStrengthIndex(Array.Empty<double>(), Array.Empty<double>());
    }

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
    public async Task PublicRangeMomentumPreservesThreeCustomerAverageStages()
    {
        var constructor = typeof(MomentaRelativeStrengthIndex).GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
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
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeRangeGainLossRejectsInvalidFieldsWithoutAdvancing(bool relative)
    {
        IStreamingIndicatorState Create() => relative ? new DoubleSmoothedRelativeStrengthIndexState(length1: 2, length2: 2, length3: 2) : new MomentaRelativeStrengthIndexState(length1: 2, length2: 2);
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

    private static OhlcvBar Native(Bar b) => new("RANGE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RawRangePreservesEveryCustomerStage(bool twice)
    {
        var prices = new[] { 1d, 3, 2, 4 };
        var data = new StockData(prices, prices, prices, prices, Enumerable.Repeat(1d, 4), Enumerable.Range(0, 4).Select(i => DateTime.UnixEpoch.AddMinutes(i)));
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> up = (v, _) => v.Select(_ => 2d).ToArray();
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> down = (v, _) => v.Select(_ => 6d).ToArray();
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> identity = (v, _) => v;
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> signal = (v, _) => v.Select(p => p / 2).ToArray();
        using var scope = ComponentAverage.Arm(twice ? new[] { up, identity, down, identity, signal } : new[] { up, down, signal });
        using var context = new ComputeContext();
        using var result = twice ? IndicatorCompute.ComputeDoubleSmoothedRelativeStrengthIndexFast(data, context, outputKey: "Signal")
            : IndicatorCompute.ComputeMomentaRelativeStrengthIndexFast(data, context, outputKey: "Signal");
        Assert.Equal(Enumerable.Repeat(12.5, 4), result.Span.ToArray());
        Assert.Equal(twice ? 5 : 3, ComponentAverage.Substitutions);
    }

    [Fact]
    public void ResetDropsExtremaFromADifferentPriceRegime()
    {
        foreach (var state in new IStreamingIndicatorState[] { new MomentaRelativeStrengthIndexState(length1: 3, length2: 1), new DoubleSmoothedRelativeStrengthIndexState(length1: 3, length2: 1, length3: 1) })
        {
            using var lifetime = state as IDisposable;
            foreach (var p in new[] { -100d, -50, -200 }) state.Update(Native(new Bar(DateTime.UnixEpoch, p, p, p, p, 1)), true, true);
            state.Reset();
            var prices = new[] { 10d, 5, 20 }; var expected = new[] { 100d, 0, 100 };
            for (var i = 0; i < prices.Length; i++)
            {
                var p = prices[i];
                Assert.Equal(expected[i], state.Update(Native(new Bar(DateTime.UnixEpoch, p, p, p, p, 1)), true, true).Value);
            }
        }
    }

    [Fact]
    public void RawRangeArmsReadSelectedPricesInsteadOfStoredCloses()
    {
        var prices = new[] { -2d, 2, -2, 0 };
        var data = new StockData(Enumerable.Repeat(100d, 4), Enumerable.Repeat(100d, 4), Enumerable.Repeat(100d, 4), Enumerable.Repeat(100d, 4),
            Enumerable.Repeat(1d, 4), Enumerable.Range(0, 4).Select(i => DateTime.UnixEpoch.AddMinutes(i)));
        data.CustomValuesList = prices.ToList();
        using var context = new ComputeContext();
        using var first = IndicatorCompute.ComputeMomentaRelativeStrengthIndexFast(data, context, 2, 1);
        using var second = IndicatorCompute.ComputeDoubleSmoothedRelativeStrengthIndexFast(data, context, 2, 1, 1);
        Assert.Equal(new[] { 100d, 100, 0, 100 }, first.Span.ToArray());
        Assert.Equal(first.Span.ToArray(), second.Span.ToArray());
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "MomentaRelativeStrengthIndex", "DoubleSmoothedRelativeStrengthIndex"
    };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.RangeGainLossOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

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
