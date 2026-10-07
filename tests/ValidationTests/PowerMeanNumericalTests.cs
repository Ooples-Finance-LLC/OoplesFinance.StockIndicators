using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PowerMeanNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName is IndicatorName.ParabolicWeightedMovingAverage or IndicatorName.CubedWeightedMovingAverage)
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
    [InlineData(50000)]
    public async Task IntegerPowerWeightsMatchAcrossRoutes(int period)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 245))
        {
            var bars = fixture.Bars.ToArray();
            var close = bars.Select(b => b.Close).ToArray();
            var expected = BuiltInFormulaReferences.RoundedPowerMean(bars, period, 2);
            var paired = BuiltInFormulaReferences.RoundedPowerMean(bars, period, 3);
            var actual = new double[bars.Length];
            MovingAverageCore.ParabolicWeightedMovingAverage(close, actual, period);
            Assert.Equal(expected, actual);
            MovingAverageCore.QuadraticWeightedMovingAverage(close, actual, period);
            Assert.Equal(expected, actual);
            MovingAverageCore.CubedWeightedMovingAverage(close, actual, period);
            Assert.Equal(paired, actual);
            MovingAverageCore.CubicWeightedMovingAverage(close, actual, period);
            Assert.Equal(paired, actual);
            var square = new ParabolicWma(period);
            var cube = new CubedWeightedMovingAverage(period);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(square, cube).BuildAsync();
            Assert.Equal(expected, run[square].ToArray());
            Assert.Equal(paired, run[cube].ToArray());
            StockData Data() => new(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                close.ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            Assert.Equal(expected, Data().CalculateParabolicWeightedMovingAverage(period).CustomValuesList);
            Assert.Equal(paired, Data().CalculateCubedWeightedMovingAverage(period).CustomValuesList);
            using var state = new ParabolicWeightedMovingAverageState(period);
            using var pairState = new CubedWeightedMovingAverageState(period);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset(); pairState.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("POWER", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    foreach (var commit in new[] { false, true })
                    {
                        Assert.Equal(expected[i], state.Update(input, commit, true).Value);
                        Assert.Equal(paired[i], pairState.Update(input, commit, true).Value);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(134217729)]
    [InlineData(int.MaxValue)]
    public void VeryLargePeriodsKeepExactIntegerWeights(int period)
    {
        var n = new ReferenceFraction(period);
        var input = new[] { double.MaxValue, -double.MaxValue, double.Epsilon };
        var actual = new double[3];
        foreach (var power in new[] { 2, 3 })
        {
            var total = power == 2 ? n * (n + new ReferenceFraction(1)) * (n * new ReferenceFraction(2) + new ReferenceFraction(1)) / new ReferenceFraction(6)
                : n * n * (n + new ReferenceFraction(1)) * (n + new ReferenceFraction(1)) / new ReferenceFraction(4);
            if (power == 2) MovingAverageCore.ParabolicWeightedMovingAverage(input, actual, period);
            else MovingAverageCore.CubedWeightedMovingAverage(input, actual, period);
            for (var i = 0; i < input.Length; i++)
            {
                var numerator = new ReferenceFraction(0);
                for (var j = 0; j <= i; j++)
                {
                    var weight = new ReferenceFraction(1);
                    for (var p = 0; p < power; p++) weight *= new ReferenceFraction(period - j);
                    numerator += ReferenceFraction.FromDouble(input[i - j]) * weight;
                }
                Assert.Equal((numerator / total).ToDouble(), actual[i]);
            }
        }
    }

    [Fact]
    public async Task PowerMeansPreserveSelectedInput()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 249).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var square = new ParabolicWma(3);
        var quadratic = new QuadraticWma(3);
        var cubic = new CubicWma(3);
        var cube = new CubedWeightedMovingAverage(3);
        square.Of(source); cube.Of(source); quadratic.Of(source); cubic.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, square, cube, quadratic, cubic).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expectedSquare = BuiltInFormulaReferences.RoundedPowerMean(projected, 3, 2);
        var expectedCube = BuiltInFormulaReferences.RoundedPowerMean(projected, 3, 3);
        Assert.Equal(expectedSquare, run[square].ToArray());
        Assert.Equal(expectedSquare, run[quadratic].ToArray());
        Assert.Equal(expectedCube, run[cube].ToArray());
        Assert.Equal(expectedCube, run[cubic].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            if (chained) data.CustomValuesList = selected.ToList();
            else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var first = IndicatorCompute.ComputeParabolicWmaFast(data, context, 3);
            using var second = IndicatorCompute.ComputeCubedWeightedMovingAverageFast(data, context, 3);
            using var third = IndicatorCompute.ComputeQuadraticWmaFast(data, context, 3);
            Assert.Equal(expectedSquare, first.ToArray());
            Assert.Equal(expectedCube, second.ToArray());
            Assert.Equal(expectedSquare, third.ToArray());
            using var fourth = IndicatorCompute.ComputeCubicWmaFast(data, context, 3);
            Assert.Equal(expectedCube, fourth.ToArray());
        }
    }
}
