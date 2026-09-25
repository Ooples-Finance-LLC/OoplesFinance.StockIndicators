using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder.Compute;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class GainLossNumericalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(-1074)]
    public async Task WideAndTinyCandleMovementHasTheSameBoundedShare(int exponent)
    {
        var v = Math.ScaleB(1d, exponent);
        var opens = new[] { -v, v, -v, 0d };
        var closes = new[] { v, -v, v, 0d };
        var bars = closes.Select((c, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), opens[i], Math.Max(c, opens[i]), Math.Min(c, opens[i]), c, 1)).ToArray();
        var expected = new[] { 100d, 50, 50, 100 };
        IIndicator[] indicators = [new IntradayMomentumIndex(2), new ChandeIntradayMomentumIndex(2)];
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).BuildAsync();
        foreach (var indicator in indicators)
        {
            Assert.Equal(expected, result[indicator].ToArray());
            Assert.Equal(expected, BuiltInFormulaReferences.GainLossOutputs(bars, (IBuiltInIndicator)indicator)["Cimi"]);
        }
        var core = new double[4]; OscillatorCore.IntradayMomentumIndex(opens, closes, core, 2); Assert.Equal(expected, core);
        using var state = new ChandeIntradayMomentumIndexState(2);
        CheckState(state, bars, expected);
        using var context = new ComputeContext();
        using var arm = IndicatorCompute.ComputeIntradayMomentumIndexFast(Data(bars), context, 2);
        Assert.Equal(expected, arm.Span.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(-1074)]
    public async Task RelativeMomentumPreservesItsLagAndZeroLossConvention(int exponent)
    {
        var v = Math.ScaleB(1d, exponent);
        var prices = new[] { -v, v, v, -v, -v, v };
        var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
        var expected = new[] { 100d, 100, 100, 0, 0, 100 };
        var indicator = new RelativeMomentumIndex(1, 2);
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, result[indicator.Outputs[0]].ToArray());
        Assert.Equal(expected, result[indicator.Outputs[1]].ToArray());
        Assert.Equal(new double[prices.Length], result[indicator.Outputs[2]].ToArray());
        Assert.Equal(expected, BuiltInFormulaReferences.GainLossOutputs(bars, indicator)["Rmi"]);
        var core = new double[prices.Length]; OscillatorCore.RelativeMomentumIndex(prices, core, 1, 2); Assert.Equal(expected, core);
        using var state = new RelativeMomentumIndexState(length1: 1, length2: 2);
        CheckState(state, bars, expected);
        using var context = new ComputeContext();
        using var arm = IndicatorCompute.ComputeRelativeMomentumIndexFast(Data(bars), context, 1, 2);
        Assert.Equal(expected, arm.Span.ToArray());
    }

    [Fact]
    public void EmptyCoresAndMismatchedInputsHaveExplicitContracts()
    {
        OscillatorCore.RelativeMomentumIndex(Array.Empty<double>(), Array.Empty<double>());
        OscillatorCore.IntradayMomentumIndex(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());
        Assert.Throws<ArgumentException>(() => OscillatorCore.IntradayMomentumIndex(Array.Empty<double>(), new[] { 1d }, new double[1]));
        Assert.Throws<ArgumentException>(() => OscillatorCore.RelativeMomentumIndex(new[] { 1d }, Array.Empty<double>()));
    }

    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GAINLOSS", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
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
    public async Task PublicRelativeMomentumPreservesThreeCustomerAverageStages()
    {
        var constructor = typeof(RelativeMomentumIndex).GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeGainLossRejectsInvalidFieldsWithoutAdvancing(bool relative)
    {
        IStreamingIndicatorState Create() => relative ? new RelativeMomentumIndexState(length1: 2, length2: 1) : new ChandeIntradayMomentumIndexState(2);
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

    [Fact]
    public void RawArmsHonorASelectedSeriesDistinctFromStoredClose()
    {
        var prices = new[] { -2d, 2, 2, -2, -2, 2 };
        var data = new StockData(new double[6], Enumerable.Repeat(100d, 6), new double[6], Enumerable.Repeat(100d, 6),
            Enumerable.Repeat(1d, 6), Enumerable.Range(0, 6).Select(i => DateTime.UnixEpoch.AddMinutes(i)));
        data.CustomValuesList = prices.ToList();
        using var context = new ComputeContext();
        using var relative = IndicatorCompute.ComputeRelativeMomentumIndexFast(data, context, 1, 2);
        Assert.Equal(new[] { 100d, 100, 100, 0, 0, 100 }, relative.Span.ToArray());
        using var intraday = IndicatorCompute.ComputeIntradayMomentumIndexFast(data, context, 2);
        using var chande = IndicatorCompute.ComputeChandeIntradayMomentumIndexFast(data, context, 2);
        Assert.Equal(new[] { 0d, 50, 100, 50, 0, 50 }, intraday.Span.ToArray());
        Assert.Equal(intraday.Span.ToArray(), chande.Span.ToArray());
    }

    [Fact]
    public void RawHistogramHonorsEveryCustomerStage()
    {
        var prices = new[] { 1d, 3, 2, 4 };
        var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] stages =
        [ (v, _) => v.Select(_ => 2d).ToArray(), (v, _) => v.Select(_ => 6d).ToArray(), (v, _) => v.Select(p => p / 2).ToArray() ];
        using var scope = ComponentAverage.Arm(stages);
        using var context = new ComputeContext();
        using var result = IndicatorCompute.ComputeRelativeMomentumIndexFast(Data(bars), context, 2, 1, outputKey: "Histogram");
        Assert.Equal(Enumerable.Repeat(12.5, 4), result.Span.ToArray());
        Assert.Equal(3, ComponentAverage.Substitutions);
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "RelativeMomentumIndex", "IntradayMomentumIndex", "ChandeIntradayMomentumIndex"
    };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.GainLossOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

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
