using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PositiveGeometricMeanNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName == IndicatorName.GeometricMeanMovingAverage)
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
    public async Task PositiveOnlyGeometricMeanMatchesAcrossRoutes(int period)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 250))
        {
            var bars = fixture.Bars.ToArray();
            var close = bars.Select(b => b.Close).ToArray();
            var expected = BuiltInFormulaReferences.RoundedGeometricMean(bars, period, positiveOnly: true);
            var actual = new double[bars.Length];
            MovingAverageCore.GeometricMeanMovingAverage(close, actual, period);
            Assert.Equal(expected, actual);
            var indicator = new GeometricMeanMovingAverage(period);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                close.ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            Assert.Equal(expected, data.CalculateGeometricMeanMovingAverage(period).CustomValuesList);
            using var state = new GeometricMeanMovingAverageState(period);
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
                        Assert.Equal(expected[i], result.Outputs!["Gmma"]);
                    }
                }
            }
        }
    }

    [Fact]
    public void PositiveFactorsDetermineTheRootDegreeAndStartupCopiesPrices()
    {
        var output = new double[5];
        MovingAverageCore.GeometricMeanMovingAverage(new[] { 4d, -1d, 16d, 0d, 64d }, output, 3);
        Assert.Equal(new[] { 4d, -1d, 8d, 16d, 32d }, output);
        MovingAverageCore.GeometricMeanMovingAverage(new[] { -1d, -2d, -3d, -4d, -5d }, output, 3);
        Assert.Equal(new[] { -1d, -2d, 0d, 0d, 0d }, output);
        var extremes = new[] { double.Epsilon, double.MaxValue, 0d };
        var actual = new double[3];
        MovingAverageCore.GeometricMeanMovingAverage(extremes, actual, 3);
        var product = ReferenceFraction.FromDouble(double.Epsilon) * ReferenceFraction.FromDouble(double.MaxValue);
        Assert.Equal(new[] { double.Epsilon, double.MaxValue, product.SqrtToDouble() }, actual);
        MovingAverageCore.GeometricMeanMovingAverage(extremes, actual, int.MaxValue);
        Assert.Equal(extremes, actual);
        MovingAverageCore.GeometricMeanMovingAverage(Array.Empty<double>(), Span<double>.Empty, 14);
        using var state = new OoplesFinance.StockIndicators.Helpers.RollingGeometricMean(2, positiveOnly: true);
        Assert.Equal(double.Epsilon, state.Next(double.Epsilon, true));
        Assert.Equal(double.Epsilon, state.Next(-1, true));
        Assert.Equal(0d, state.Next(0, true));
        Assert.Equal(double.MaxValue, state.Next(double.MaxValue, false));
        Assert.Equal(double.MaxValue, state.Next(double.MaxValue, true));
    }

    [Fact]
    public async Task PositiveGeometricMeanPreservesSelectedInput()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 250).Single(f => f.Name.EndsWith("/large")).Bars;
        var source = new Sma(2);
        var geometric = new GeometricMeanMovingAverage(3);
        geometric.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, geometric).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedGeometricMean(projected, 3, positiveOnly: true);
        Assert.Equal(expected, run[geometric].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            if (chained) data.CustomValuesList = selected.ToList();
            else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var result = IndicatorCompute.ComputeGeometricMeanMovingAverageFast(data, context, 3);
            Assert.Equal(expected, result.ToArray());
        }
    }
}
