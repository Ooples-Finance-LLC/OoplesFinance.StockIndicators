using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ReciprocalDistanceMeanNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName == IndicatorName.DistanceWeightedMovingAverage)
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
    public async Task ReciprocalWeightsMatchAcrossRoutes(int period)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(period == 14 ? 160 : 40, 250))
        {
            var bars = fixture.Bars.ToArray();
            var close = bars.Select(b => b.Close).ToArray();
            var expected = BuiltInFormulaReferences.RoundedDistanceMassMean(bars, period, reciprocal: true);
            var actual = new double[bars.Length];
            MovingAverageCore.DistanceWeightedMovingAverage(close, actual, period);
            Assert.Equal(expected, actual);
            var indicator = new DistanceWeightedMovingAverage(period);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                close.ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            Assert.Equal(expected, data.CalculateDistanceWeightedMovingAverage(period).CustomValuesList);
            using var state = new DistanceWeightedMovingAverageState(period);
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
                        Assert.Equal(expected[i], result.Outputs!["Dwma"]);
                    }
                }
            }
        }
    }

    [Fact]
    public void ReciprocalHandVectorsAndConstantFallbackAreExplicit()
    {
        var actual = new double[3];
        MovingAverageCore.DistanceWeightedMovingAverage(new[] { 2d, 4d, 8d }, actual, 2);
        Assert.Equal(new[] { 1d, 3d, 6d }, actual);
        MovingAverageCore.DistanceWeightedMovingAverage(new[] { 5d, 5d, 5d }, actual, 3);
        Assert.Equal(new[] { 1d, 4d, 5d }, actual);
        MovingAverageCore.DistanceWeightedMovingAverage(new[] { double.Epsilon, double.Epsilon, double.Epsilon }, actual, 3);
        Assert.Equal(new[] { 0d, double.Epsilon, double.Epsilon }, actual);
        var one = new double[1];
        MovingAverageCore.DistanceWeightedMovingAverage(new[] { 2d }, one, int.MaxValue);
        var coefficient = ReferenceFraction.FromDouble(1d / (int.MaxValue - 1d));
        Assert.Equal((new ReferenceFraction(2) * coefficient / (new ReferenceFraction(int.MaxValue - 1) + coefficient)).ToDouble(), one[0]);
        MovingAverageCore.DistanceWeightedMovingAverage(Array.Empty<double>(), Span<double>.Empty, int.MaxValue);
        using var state = new OoplesFinance.StockIndicators.Helpers.DistanceMassWindowMean(2, reciprocal: true);
        state.Next(2, true);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var commit in new[] { false, true })
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Next(invalid, commit));
        Assert.Equal(3d, state.Next(4, true));
        Assert.Equal(6d, state.Next(8, true));
    }

    [Fact]
    public void RoundedCoefficientsRespectTheIdealMeanErrorBound()
    {
        var random = new Random(24451);
        for (var sample = 0; sample < 24; sample++)
        {
            var values = Enumerable.Range(0, 12).Select(_ =>
            {
                var value = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
                return double.IsFinite(value) ? value : double.Epsilon;
            }).ToArray();
            var actual = new double[values.Length];
            MovingAverageCore.DistanceWeightedMovingAverage(values, actual, 3);
            for (var i = 0; i < values.Length; i++)
            {
                var window = Enumerable.Range(0, 3).Select(lag => i >= lag ? values[i - lag] : 0).ToArray();
                var prices = window.Select(ReferenceFraction.FromDouble).ToArray();
                var distances = prices.Select(p => prices.Aggregate(new ReferenceFraction(0), (sum, q) => sum + (p - q).Abs())).ToArray();
                if (distances[0].Sign == 0) continue;
                var denominator = distances.Aggregate(new ReferenceFraction(0), (sum, d) => sum + new ReferenceFraction(1) / d);
                var numerator = prices.Select((p, j) => p / distances[j]).Aggregate(new ReferenceFraction(0), (sum, term) => sum + term);
                var ideal = numerator / denominator;
                var range = ReferenceFraction.FromDouble(window.Max()) - ReferenceFraction.FromDouble(window.Min());
                var magnitude = Math.Abs(actual[i]);
                var lowerGap = magnitude - Math.BitDecrement(magnitude);
                var upperGap = magnitude == double.MaxValue ? lowerGap : Math.BitIncrement(magnitude) - magnitude;
                var rounding = ReferenceFraction.FromDouble(Math.Max(lowerGap, upperGap)) / new ReferenceFraction(2);
                var bound = range / new ReferenceFraction((1L << 53) - 1) + rounding;
                Assert.True((ReferenceFraction.FromDouble(actual[i]) - ideal).Abs().CompareTo(bound) <= 0);
            }
        }
    }

    [Fact]
    public async Task ReciprocalDistanceMeanPreservesSelectedInput()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 250).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var distance = new DistanceWeightedMovingAverage(3);
        distance.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, distance).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedDistanceMassMean(projected, 3, reciprocal: true);
        Assert.Equal(expected, run[distance].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            if (chained) data.CustomValuesList = selected.ToList();
            else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var result = IndicatorCompute.ComputeDistanceWeightedMovingAverageFast(data, context, 3);
            Assert.Equal(expected, result.ToArray());
        }
    }
}
