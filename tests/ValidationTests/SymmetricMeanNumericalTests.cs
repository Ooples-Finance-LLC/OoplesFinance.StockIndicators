using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SymmetricMeanNumericalTests
{
    [Fact]
    public async Task SelectedInputFlowsThroughBothTriangularWindowArms()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 247).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var symmetric = new SymmetricallyWeightedMovingAverage(3);
        var triangle = new EhlersTriangleMovingAverage(3);
        var jsa = new JsaMovingAverage(3);
        symmetric.Of(source); triangle.Of(source); jsa.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, symmetric, triangle, jsa).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedSymmetricMean(projected, 3);
        Assert.Equal(expected, run[symmetric].ToArray());
        Assert.Equal(expected, run[triangle].ToArray());
        Assert.Equal(BuiltInFormulaReferences.RoundedJsaMean(projected, 3), run[jsa].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            if (chained) data.CustomValuesList = selected.ToList();
            else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var first = IndicatorCompute.ComputeSymmetricallyWeightedMovingAverageFast(data, context, 3);
            using var second = IndicatorCompute.ComputeEhlersTriangleMovingAverageFast(data, context, 3);
            Assert.Equal(expected, first.ToArray());
            Assert.Equal(expected, second.ToArray());
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName is IndicatorName.SymmetricallyWeightedMovingAverage
            or IndicatorName.EhlersTriangleMovingAverage or IndicatorName.JsaMovingAverage)
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
    [InlineData(100000)]
    public async Task SymmetricWeightsAndLaggedMidpointsMatchAcrossRoutes(int period)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 245))
        {
            var bars = fixture.Bars.ToArray();
            var close = bars.Select(b => b.Close).ToArray();
            var expected = BuiltInFormulaReferences.RoundedSymmetricMean(bars, period);
            var paired = BuiltInFormulaReferences.RoundedJsaMean(bars, period);
            var actual = new double[bars.Length];
            MovingAverageCore.SymmetricallyWeightedMovingAverage(close, actual, period);
            Assert.Equal(expected, actual);
            MovingAverageCore.EhlersTriangleMovingAverage(close, actual, period);
            Assert.Equal(expected, actual);
            MovingAverageCore.JsaMovingAverage(close, actual, period);
            Assert.Equal(paired, actual);
            var symmetric = new SymmetricallyWeightedMovingAverage(period);
            var triangle = new EhlersTriangleMovingAverage(period);
            var jsa = new JsaMovingAverage(period);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(symmetric, triangle, jsa).BuildAsync();
            Assert.Equal(expected, run[symmetric].ToArray());
            Assert.Equal(expected, run[triangle].ToArray());
            Assert.Equal(paired, run[jsa].ToArray());
            StockData Data() => new(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                close.ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            Assert.Equal(expected, Data().CalculateSymmetricallyWeightedMovingAverage(period).CustomValuesList);
            Assert.Equal(expected, Data().CalculateEhlersTriangleMovingAverage(period).CustomValuesList);
            Assert.Equal(paired, Data().CalculateJsaMovingAverage(period).CustomValuesList);
            using var state = new SymmetricallyWeightedMovingAverageState(period);
            using var triangleState = new EhlersTriangleMovingAverageState(period);
            using var pairState = new JsaMovingAverageState(period);
            using var smoother = new SymmetricallyWeightedMovingAverageSmoother(period);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset(); triangleState.Reset(); pairState.Reset(); smoother.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("SYM", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    foreach (var commit in new[] { false, true })
                    {
                        Assert.Equal(expected[i], state.Update(input, commit, true).Value);
                        Assert.Equal(expected[i], triangleState.Update(input, commit, true).Value);
                        Assert.Equal(expected[i], smoother.Next(b.Close, commit));
                        Assert.Equal(paired[i], pairState.Update(input, commit, true).Value);
                    }
                }
            }
        }
    }

    [Fact]
    public void CoreWeightTotalsDoNotOverflowAtTheLargestPeriod()
    {
        var bars = IndicatorAdversarialCases.Generate(16, 245).Single(f => f.Name.EndsWith("/overflow-adjacent")).Bars;
        var expected = BuiltInFormulaReferences.RoundedSymmetricMean(bars, int.MaxValue);
        var actual = new double[bars.Count];
        MovingAverageCore.SymmetricallyWeightedMovingAverage(bars.Select(b => b.Close).ToArray(), actual, int.MaxValue);
        Assert.Equal(expected, actual);
    }
}
