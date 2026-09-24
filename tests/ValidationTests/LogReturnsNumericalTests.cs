using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class LogReturnsNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(LogReturns)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesAllNumericalCases(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Fact]
    public void IndependentLogarithmMatchesKnownValuesAndPreservesAdjacentPrices()
    {
        Assert.Equal(0, new ReferenceFraction(1).LogToDouble());
        Assert.Equal(0.6931471805599453, new ReferenceFraction(2).LogToDouble());
        Assert.Equal(-0.6931471805599453, (new ReferenceFraction(1) / new ReferenceFraction(2)).LogToDouble());
        Assert.Equal(2.302585092994046, new ReferenceFraction(10).LogToDouble());
        foreach (var previous in new[] { double.Epsilon, 1e-300, 1d, 1e100, double.MaxValue / 2 })
        {
            var current = Math.BitIncrement(previous);
            var expected = (ReferenceFraction.FromDouble(current) / ReferenceFraction.FromDouble(previous)).LogToDouble();
            Assert.True(expected > 0);
            Assert.True(BuiltInFormulaReferences.LogReturnsBudget.Accepts(expected, StableLogRatio.Of(current, previous)));
            Assert.True(BuiltInFormulaReferences.LogReturnsBudget.Accepts(-expected, StableLogRatio.Of(previous, current)));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task EveryRouteMatchesIndependentLogarithmsIncludingPreviewAndReset(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244).Concat(new[] {
            Fixture("extreme-ratios", new[] { double.Epsilon, double.MaxValue, double.Epsilon, 1d, Math.BitIncrement(1d), 0, -1, 2, 2 }),
            Fixture("near-branch-boundaries", new[] { 1d, Math.BitDecrement(0.5), 1d, 0.5, 1d, Math.BitIncrement(0.5),
                1d, Math.BitDecrement(2d), 1d, 2d, 1d, Math.BitIncrement(2d), double.Epsilon, 2 * double.Epsilon,
                Math.BitDecrement(2.2250738585072014e-308), 2.2250738585072014e-308 }),
            Fixture("adjacent-large-prices", new[] { 1e100, Math.BitIncrement(1e100), Math.BitDecrement(1e100), 1e100 }) }))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.ReferenceLogReturns(bars, length);
            var data = Data(bars);
            data.CalculateLogReturns(length);
            var actual = data.OutputValues["Returns"].ToArray();
            for (var i = 0; i < actual.Length; i++)
                Assert.True(BuiltInFormulaReferences.LogReturnsBudget.Accepts(expected[i], actual[i]), $"{fixture.Name}/{i}: {expected[i]:R} != {actual[i]:R}");
            var core = new double[bars.Count];
            OscillatorCore.LogReturns(bars.Select(b => b.Close).ToArray(), core, length);
            Assert.Equal(actual, core);
            var indicator = new LogReturns(length);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(actual, run[indicator].ToArray());
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
                        var native = new OhlcvBar("LOG", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        Assert.Equal(actual[i], state.Update(native, commit, true).Value);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task SelectedPricesAreConsumedByTypedAndDirectBuilders()
    {
        var bars = Fixture("selection", new[] { 100d, 120, 110, 150, 90 }).Bars;
        var source = new Sma(2);
        var indicator = new LogReturns(1).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var actual = run[indicator].ToArray();
        var expected = BuiltInFormulaReferences.ReferenceLogReturns(Fixture("selected", selected).Bars, 1);
        for (var i = 0; i < actual.Length; i++) Assert.True(BuiltInFormulaReferences.LogReturnsBudget.Accepts(expected[i], actual[i]));
        var data = Data(bars);
        data.InputValues = new(selected);
        using var context = new ComputeContext();
        using var result = IndicatorCompute.ComputeLogReturnsFast(data, context, 1);
        Assert.Equal(actual, result.ToArray());
    }

    private static IndicatorValidationFixture Fixture(string name, double[] values) => new(name,
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)));
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
}
