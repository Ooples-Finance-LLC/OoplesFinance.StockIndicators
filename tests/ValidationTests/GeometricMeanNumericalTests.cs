using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class GeometricMeanNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName == IndicatorName.GeometricMovingAverage)
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
    [InlineData(32)]
    public async Task GeometricMeanMatchesAcrossRoutes(int period)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 250))
        {
            var bars = fixture.Bars.ToArray();
            var close = bars.Select(b => b.Close).ToArray();
            var expected = BuiltInFormulaReferences.RoundedGeometricMean(bars, period);
            var actual = new double[bars.Length];
            MovingAverageCore.GeometricMovingAverage(close, actual, period);
            Assert.Equal(expected, actual);
            var indicator = new GeoMa(period);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                close.ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            Assert.Equal(expected, data.CalculateGeometricMovingAverage(period).CustomValuesList);
            using var state = new GeometricMovingAverageState(period);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("GEO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    foreach (var commit in new[] { false, true })
                    {
                        var result = state.Update(input, commit, true);
                        Assert.Equal(expected[i], result.Value);
                        Assert.Equal(expected[i], result.Outputs!["Gma"]);
                    }
                }
            }
        }
    }

    [Fact]
    public void PerfectRootsFloorAndStartupAreExplicit()
    {
        var output = new double[4];
        MovingAverageCore.GeometricMovingAverage(new[] { 1d, 8d, 27d, 64d }, output, 3);
        Assert.Equal(new[] { 0d, 0d, 6d, 24d }, output);
        MovingAverageCore.GeometricMovingAverage(new[] { -1d, 0d, double.Epsilon, 0.000001 }, output, 1);
        Assert.All(output, value => Assert.Equal(0.000001, value));
        MovingAverageCore.GeometricMovingAverage(Enumerable.Repeat(double.MaxValue, 4).ToArray(), output, 3);
        Assert.Equal(new[] { 0d, 0d, double.MaxValue, double.MaxValue }, output);
        MovingAverageCore.GeometricMovingAverage(new[] { 1d, 8d, 27d, 64d }, output, int.MaxValue);
        Assert.All(output, value => Assert.Equal(0d, value));
        MovingAverageCore.GeometricMovingAverage(Array.Empty<double>(), Span<double>.Empty, 14);
    }

    [Fact]
    public async Task GeometricMeanPreservesSelectedInput()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 250).Single(f => f.Name.EndsWith("/large")).Bars;
        var source = new Sma(2);
        var geometric = new GeoMa(3);
        geometric.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, geometric).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedGeometricMean(projected, 3);
        Assert.Equal(expected, run[geometric].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            if (chained) data.CustomValuesList = selected.ToList();
            else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var result = IndicatorCompute.ComputeGeoMaFast(data, context, 3);
            Assert.Equal(expected, result.ToArray());
        }
    }
}
