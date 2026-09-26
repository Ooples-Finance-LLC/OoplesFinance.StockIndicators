using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class KaufmanNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName == IndicatorName.KaufmanAdaptiveMovingAverage)
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task BothOutputsReceiveAllNumericalClasses(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(1, 2, 30)]
    [InlineData(3, 2, 30)]
    [InlineData(10, 2, 30)]
    [InlineData(3, 1, 1)]
    [InlineData(3, 30, 2)]
    [InlineData(3, int.MaxValue, int.MaxValue)]
    public async Task EfficiencyAndConvexUpdateMatchIndependentRationalTrajectory(int length, int fast, int slow)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 252))
        {
            var bars = fixture.Bars.ToArray();
            var close = bars.Select(b => b.Close).ToArray();
            var expected = BuiltInFormulaReferences.RoundedKaufmanTrajectory(bars, length, fast, slow);
            var actual = new double[bars.Length];
            MovingAverageCore.KaufmanAdaptiveMovingAverage(close, actual, length, fast, slow);
            Assert.Equal(expected["Kama"], actual);
            if (fast == 2 && slow == 30)
            {
                var indicator = new Kama(length);
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
                Assert.Equal(expected["Kama"], run[indicator.Value].ToArray());
                Assert.Equal(expected["Er"], run[indicator.Er].ToArray());
            }
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                close.ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList()).CalculateKaufmanAdaptiveMovingAverage(length, fast, slow);
            Assert.Equal(expected["Kama"], data.OutputValues["Kama"]);
            Assert.Equal(expected["Er"], data.OutputValues["Er"]);
            using var state = new KaufmanAdaptiveMovingAverageState(length, fast, slow);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("KAMA", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    foreach (var commit in new[] { false, true })
                    {
                        var result = state.Update(input, commit, true);
                        Assert.Equal(expected["Kama"][i], result.Value);
                        Assert.Equal(expected["Er"][i], result.Outputs!["Er"]);
                        Assert.InRange(result.Outputs["Er"], 0, 1);
                        if (i >= length) Assert.InRange(result.Value, Math.Min(close[i], actual[i - 1]), Math.Max(close[i], actual[i - 1]));
                    }
                }
            }
        }
    }

    [Fact]
    public async Task BothChainedOutputsUseSelectedPrices()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 252).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var kama = new Kama(3);
        kama.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, kama).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedKaufmanTrajectory(projected, 3);
        Assert.Equal(expected["Kama"], run[kama.Value].ToArray());
        Assert.Equal(expected["Er"], run[kama.Er].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            if (chained) data.CustomValuesList = selected.ToList();
            else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var average = IndicatorCompute.ComputeKamaFast(data, context, 3);
            using var efficiency = IndicatorCompute.ComputeKamaFast(data, context, 3, IndicatorCompute.KamaSeries.EfficiencyRatio);
            Assert.Equal(expected["Kama"], average.ToArray());
            Assert.Equal(expected["Er"], efficiency.ToArray());
        }
    }
}
