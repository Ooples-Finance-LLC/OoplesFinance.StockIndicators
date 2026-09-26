using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AverageDayRangeNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(AverageDayRange) || c.IndicatorType == typeof(Adr)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesNumericalAndSignedOverflowCases(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
        foreach (var sign in new[] { "positive", "negative" })
            Assert.Equal(2, Assert.Single(report.FixtureEvidence,
                f => f.Name == "average-day-range-" + sign + "-output-overflow").OutputOverflowRejectionsChecked);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task EveryRouteMatchesTheIndependentMeanIncludingPreviewAndReset(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244).Concat(new[] {
            new IndicatorValidationFixture("overflowing-ranges-finite-mean-and-eviction", new[] {
                (double.MaxValue, -double.MaxValue), (0d, 0d), (-double.MaxValue, double.MaxValue),
                (double.Epsilon, -double.Epsilon), (2 * double.Epsilon, -2 * double.Epsilon) }
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, v.Item1, v.Item2, 0, 1))) }))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedAverageDayRange(bars, length);
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            data.CalculateAverageDayRange(length);
            Assert.Equal(expected, data.OutputValues["Adr"]);
            var core = new double[bars.Count];
            VolatilityCore.AverageDayRange(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), core, length);
            Assert.Equal(expected, core);
            TrendCore.AverageDayRange(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), core, length);
            Assert.Equal(expected, core);
            foreach (IIndicator indicator in new IIndicator[] { new AverageDayRange(length), new Adr(length) })
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
                            var native = new OhlcvBar("FACILITATION", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                            Assert.Equal(expected[i], state.Update(native, commit, true).Value);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public async Task TypedChainingPreservesRangeAndBuilderReadsCurrentColumns()
    {
        var bars = Enumerable.Range(0, 4).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 100 + i, 104 + i, 98 + i, 102 + i, 1)).ToArray();
        var source = new Sma(3);
        var indicator = new AverageDayRange(2).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(BuiltInFormulaReferences.RoundedAverageDayRange(bars, 2), run[indicator].ToArray());
        var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        _ = data.TickerDataList;
        data.HighPrices = new() { 2, 2, 2, 2 };
        data.LowPrices = new() { 0, 0, 0, 0 };
        using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
        using var result = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeAverageDayRangeFast(data, context, 2);
        Assert.Equal(new[] { 0d, 2, 2, 2 }, result.ToArray());
        using var alias = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeAdrFast(data, context, 2);
        Assert.Equal(new[] { 0d, 2, 2, 2 }, alias.ToArray());
    }
}
