using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RootMeanSquareNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName == IndicatorName.QuadraticMovingAverage)
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
    public async Task RootMeanSquareMatchesAcrossRoutes(int period)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 250))
        {
            var bars = fixture.Bars.ToArray();
            var close = bars.Select(b => b.Close).ToArray();
            var expected = BuiltInFormulaReferences.RoundedRootMeanSquare(bars, period);
            var actual = new double[bars.Length];
            MovingAverageCore.QuadraticMovingAverage(close, actual, period);
            Assert.Equal(expected, actual);
            var indicator = new QuadraticMovingAverage(period);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                close.ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            Assert.Equal(expected, data.CalculateQuadraticMovingAverage(period).CustomValuesList);
            using var state = new QuadraticMovingAverageState(period);
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
                        Assert.Equal(expected[i], result.Outputs!["Qma"]);
                    }
                }
            }
        }
    }

    [Fact]
    public void ExactRootRoundingPreservesSubnormalHalfwayCasesAndFiniteExtremes()
    {
        foreach (var sign in new[] { -1d, 1d })
        foreach (var pair in new[] { (1, 0), (3, 2), (5, 2), (7, 4) })
        {
            var value = sign * pair.Item1 * double.Epsilon;
            var squares = new ExactMeanAccumulator();
            squares.AddProduct(value, value);
            Assert.Equal(pair.Item2 * double.Epsilon, squares.SqrtMean(4));
        }
        var input = new[] { double.MaxValue, -double.MaxValue, double.Epsilon };
        var output = new double[3];
        MovingAverageCore.QuadraticMovingAverage(input, output, 1);
        Assert.Equal(input.Select(Math.Abs).ToArray(), output);
        MovingAverageCore.QuadraticMovingAverage(input, output, int.MaxValue);
        Assert.Equal(double.MaxValue, output[0]);
        Assert.Equal(double.MaxValue, output[1]);
        MovingAverageCore.QuadraticMovingAverage(Array.Empty<double>(), Span<double>.Empty, 14);
    }

    [Fact]
    public void ExactRootMatchesRationalBisectionAcrossRandomExponentGrids()
    {
        var random = new Random(253);
        for (var sample = 0; sample < 32; sample++)
        {
            var squares = new ExactMeanAccumulator();
            var reference = new ReferenceFraction(0);
            for (var count = 1; count <= 8; count++)
            {
                var value = Math.Pow(2, random.Next(-1074, 1024)) * (1 + random.NextDouble());
                if (count % 2 == 0) value = -value;
                squares.AddProduct(value, value);
                var fraction = ReferenceFraction.FromDouble(value);
                reference += fraction * fraction;
                Assert.Equal((reference / new ReferenceFraction(count)).SqrtToDouble(), squares.SqrtMean(count));
            }
        }
    }

    [Fact]
    public async Task RootMeanSquarePreservesSelectedInput()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 250).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var root = new QuadraticMovingAverage(3);
        root.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, root).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedRootMeanSquare(projected, 3);
        Assert.Equal(expected, run[root].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            if (chained) data.CustomValuesList = selected.ToList();
            else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var result = IndicatorCompute.ComputeQuadraticMovingAverageFast(data, context, 3);
            Assert.Equal(expected, result.ToArray());
        }
    }
}
