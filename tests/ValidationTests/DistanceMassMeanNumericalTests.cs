using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DistanceMassMeanNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName == IndicatorName.InverseDistanceWeightedMovingAverage)
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
    public async Task DistanceMassWeightsMatchAcrossRoutes(int period)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(period == 14 ? 160 : 40, 250))
        {
            var bars = fixture.Bars.ToArray();
            var close = bars.Select(b => b.Close).ToArray();
            var expected = BuiltInFormulaReferences.RoundedDistanceMassMean(bars, period);
            var actual = new double[bars.Length];
            MovingAverageCore.InverseDistanceWeightedMovingAverage(close, actual, period);
            Assert.Equal(expected, actual);
            var indicator = new InverseDistanceWeightedMovingAverage(period);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                close.ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            Assert.Equal(expected, data.CalculateInverseDistanceWeightedMovingAverage(period).CustomValuesList);
            using var state = new InverseDistanceWeightedMovingAverageState(period);
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
                        Assert.Equal(expected[i], result.Outputs!["Idwma"]);
                    }
                }
            }
        }
    }

    [Fact]
    public void DistanceMassHandVectorsAndConstantFallbackAreExplicit()
    {
        var actual = new double[3];
        MovingAverageCore.InverseDistanceWeightedMovingAverage(new[] { 2d, 4d, 8d }, actual, 3);
        Assert.Equal(new[] { 1d, 2d, 5d }, actual);
        MovingAverageCore.InverseDistanceWeightedMovingAverage(new[] { 5d, 5d, 5d }, actual, 3);
        Assert.Equal(new[] { 2.5, 2.5, 5d }, actual);
        MovingAverageCore.InverseDistanceWeightedMovingAverage(new[] { double.Epsilon, double.Epsilon, double.Epsilon }, actual, 3);
        Assert.Equal(new[] { 0d, 0d, double.Epsilon }, actual);
        var one = new double[1];
        MovingAverageCore.InverseDistanceWeightedMovingAverage(new[] { 2d }, one, int.MaxValue);
        Assert.Equal(1d, one[0]);
        MovingAverageCore.InverseDistanceWeightedMovingAverage(Array.Empty<double>(), Span<double>.Empty, int.MaxValue);
        using var state = new OoplesFinance.StockIndicators.Helpers.DistanceMassWindowMean(3);
        state.Next(2, true);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var commit in new[] { false, true })
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Next(invalid, commit));
        Assert.Equal(2d, state.Next(4, true));
        Assert.Equal(5d, state.Next(8, true));
    }

    [Fact]
    public async Task DistanceMassMeanPreservesSelectedInput()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 250).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var distance = new InverseDistanceWeightedMovingAverage(3);
        distance.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, distance).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedDistanceMassMean(projected, 3);
        Assert.Equal(expected, run[distance].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            if (chained) data.CustomValuesList = selected.ToList();
            else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var result = IndicatorCompute.ComputeInverseDistanceWeightedMovingAverageFast(data, context, 3);
            Assert.Equal(expected, result.ToArray());
        }
    }
}
