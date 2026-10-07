using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SimpleReturnsNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(SimpleReturns))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryPeriodReceivesNumericalAndSignedOverflowCases(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
        var prefix = "simple-returns-";
        foreach (var sign in new[] { "positive", "negative" })
            Assert.Equal(2, Assert.Single(report.FixtureEvidence, f => f.Name == prefix + sign + "-output-overflow").OutputOverflowRejectionsChecked);
    }

    [Fact]
    public void FinalRatioAvoidsIntermediateOverflowAndPreservesTheZeroDenominatorPolicy()
    {
        Assert.Equal(-2, ReturnRatio(double.MaxValue, -double.MaxValue));
        Assert.Equal(-2, ReturnRatio(-double.MaxValue, double.MaxValue));
        Assert.Equal(-.5, ReturnRatio(double.MaxValue / 2, double.MaxValue));
        Assert.Equal(1, ReturnRatio(2 * double.Epsilon, double.Epsilon));
        Assert.Equal(0, ReturnRatio(double.MaxValue, 0));
        Assert.Equal(double.PositiveInfinity, ReturnRatio(double.MaxValue, double.Epsilon));
        Assert.Equal(double.NegativeInfinity, ReturnRatio(-double.MaxValue, double.Epsilon));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(12)]
    public async Task EveryRouteMatchesTheIndependentExactRatio(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244).Concat(new[] {
            new IndicatorValidationFixture("opposite-extreme-prices", Enumerable.Range(0, length * 3 + 1).Select(i => {
                var value = (i / length) % 2 == 0 ? double.MaxValue : -double.MaxValue;
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), value, value, value, value, 1);
            })) }))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedSimpleReturns(bars, length);
            var data = Data(bars);
            data.CalculateSimpleReturns(length);
            Assert.Equal(expected, data.OutputValues["Returns"]);
            var core = new double[bars.Count];
            OscillatorCore.SimpleReturns(bars.Select(b => b.Close).ToArray(), core, length);
            Assert.Equal(expected, core);
            foreach (IIndicator indicator in new IIndicator[] { new SimpleReturns(length) })
            {
                if (expected.Any(double.IsInfinity))
                    await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder()
                        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync());
                else
                {
                    using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
                    Assert.Equal(expected, run[indicator].ToArray());
                }
                var builtIn = (IBuiltInIndicator)indicator;
                var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
                foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
                {
                    Assert.NotNull(state);
                    using var lifetime = state as IDisposable;
                    for (var replay = 0; replay < 2; replay++)
                    {
                        state.Reset();
                        for (var i = 0; i < bars.Count; i++)
                        foreach (var commit in new[] { false, true })
                        {
                            var b = bars[i];
                            var native = new OhlcvBar("ROC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                            Assert.Equal(expected[i], state.Update(native, commit, true).Value);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public async Task TypedSelectionUsesTheSelectedPrices()
    {
        var bars = new[] { 100d, 120, 110, 150 }.Select((v, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var source = new Sma(2);
        var compact = new SimpleReturns(1).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(source, compact).BuildAsync();
        var projected = run[source].ToArray().Select((v, i) => new Bar(bars[i].Time, v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedSimpleReturns(projected, 1);
        Assert.Equal(expected, run[compact].ToArray());
        var data = Data(bars);
        data.InputValues = new() { 1, 2, 4, 8 };
        using var context = new ComputeContext();
        using var result = IndicatorCompute.ComputeSimpleReturnsFast(data, context, 1);
        Assert.Equal(new[] { 0d, 1, 1, 1 }, result.ToArray());
    }

    private static double ReturnRatio(double current, double previous) => RoundedRangeRatio.Of(current, previous, previous);

    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
}
