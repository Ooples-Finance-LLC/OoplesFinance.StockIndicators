using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SquareRootMeanNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName == IndicatorName.SquareRootWeightedMovingAverage)
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
    [InlineData(79)]
    [InlineData(1024)]
    public async Task SquareRootWeightsMatchAcrossRoutes(int period)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 250))
        {
            var bars = fixture.Bars.ToArray();
            var close = bars.Select(b => b.Close).ToArray();
            var expected = BuiltInFormulaReferences.RoundedSquareRootMean(bars, period);
            var actual = new double[bars.Length];
            MovingAverageCore.SquareRootWeightedMovingAverage(close, actual, period);
            Assert.Equal(expected, actual);
            var indicator = new SquareRootWeightedMovingAverage(period);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                close.ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            Assert.Equal(expected, data.CalculateSquareRootWeightedMovingAverage(period).CustomValuesList);
            using var state = new SquareRootWeightedMovingAverageState(period);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("ROOT", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    foreach (var commit in new[] { false, true })
                    {
                        var result = state.Update(input, commit, true);
                        Assert.Equal(expected[i], result.Value);
                        Assert.Equal(expected[i], result.Outputs!["Srwma"]);
                    }
                }
            }
        }
    }

    [Fact]
    public void SquareRootCoefficientsMatchIntegerDerivedRounding()
    {
        foreach (var value in Enumerable.Range(1, 4096).Concat(new[] { 65535, 65536, 65537, int.MaxValue - 1, int.MaxValue }))
            Assert.Equal(BuiltInFormulaReferences.RoundedIntegerSquareRoot(value), Math.Sqrt(value));
        uint seed = 251;
        for (var i = 0; i < 4096; i++)
        {
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
            var value = (int)(seed & int.MaxValue);
            if (value != 0) Assert.Equal(BuiltInFormulaReferences.RoundedIntegerSquareRoot(value), Math.Sqrt(value));
        }
        var input = new[] { double.MaxValue, -double.MaxValue, double.Epsilon };
        var output = new double[3];
        MovingAverageCore.SquareRootWeightedMovingAverage(input, output, 1);
        Assert.Equal(input, output);
        MovingAverageCore.SquareRootWeightedMovingAverage(Array.Empty<double>(), Span<double>.Empty, 14);
    }

    [Fact]
    public async Task SquareRootMeanPreservesSelectedInput()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 250).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var root = new SquareRootWeightedMovingAverage(3);
        root.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, root).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedSquareRootMean(projected, 3);
        Assert.Equal(expected, run[root].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            if (chained) data.CustomValuesList = selected.ToList();
            else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var result = IndicatorCompute.ComputeSquareRootWeightedMovingAverageFast(data, context, 3);
            Assert.Equal(expected, result.ToArray());
        }
    }
}
