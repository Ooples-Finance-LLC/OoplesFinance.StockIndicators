using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class FareyMeanNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName == IndicatorName.FareySequenceWeightedMovingAverage)
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryAliasReceivesAllNumericalClasses(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(14)]
    [InlineData(5)]
    [InlineData(65)]
    public async Task FareyWeightsMatchAcrossRoutes(int period)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(period == 14 ? 160 : 40, 250))
        {
            var bars = fixture.Bars.ToArray();
            var close = bars.Select(b => b.Close).ToArray();
            var expected = BuiltInFormulaReferences.RoundedFareyMean(bars, period);
            var actual = new double[bars.Length];
            MovingAverageCore.FareySequenceWeightedMovingAverage(close, actual, period);
            Assert.Equal(expected, actual);
            var indicator = new FareySequenceWeightedMovingAverage(period);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                close.ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            Assert.Equal(expected, data.CalculateFareySequenceWeightedMovingAverage(period).CustomValuesList);
            using var state = new FareySequenceWeightedMovingAverageState(period);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("FIB", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    foreach (var commit in new[] { false, true })
                    {
                        var result = state.Update(input, commit, true);
                        Assert.Equal(expected[i], result.Value);
                        Assert.Equal(expected[i], result.Outputs!["Fswma"]);
                    }
                }
            }
        }
    }

    [Fact]
    public void FareyOrderIsNotTheKernelLengthAndWarmupCoversIt()
    {
        var output = new double[3];
        MovingAverageCore.FareySequenceWeightedMovingAverage(new[] { 3d, 6d, 9d }, output, 2);
        Assert.Equal(new[] { 2d, 5d, 8d }, output);
        var prices = Enumerable.Repeat(double.MaxValue, 160).ToArray();
        var actual = new double[prices.Length];
        MovingAverageCore.FareySequenceWeightedMovingAverage(prices, actual, 14);
        var bars = prices.Select(p => new Bar(default, p, p, p, p, 0)).ToArray();
        Assert.Equal(BuiltInFormulaReferences.RoundedFareyMean(bars, 14), actual);
        Assert.Equal(double.MaxValue, actual[63]); // Sum phi(1..14) = 64 taps.
        Assert.True(new FareySequenceWeightedMovingAverage(14).WarmupBars >= 63);
        Assert.Equal(int.MaxValue, new FareySequenceWeightedMovingAverage(int.MaxValue).WarmupBars);
        MovingAverageCore.FareySequenceWeightedMovingAverage(Array.Empty<double>(), Span<double>.Empty, int.MaxValue);
    }

    [Fact]
    public async Task FareyMeanPreservesSelectedInput()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 250).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var farey = new FareySequenceWeightedMovingAverage(3);
        farey.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, farey).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedFareyMean(projected, 3);
        Assert.Equal(expected, run[farey].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            if (chained) data.CustomValuesList = selected.ToList();
            else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var result = IndicatorCompute.ComputeFareySequenceWeightedMovingAverageFast(data, context, 3);
            Assert.Equal(expected, result.ToArray());
        }
    }
}
