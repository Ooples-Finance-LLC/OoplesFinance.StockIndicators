using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Helpers;
using static OoplesFinance.StockIndicators.Helpers.SignalHelper;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SectorRotationBatchTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, 2, 14, 79, 80, int.MaxValue })
        foreach (var average in new[] { MovingAvgType.ExponentialMovingAverage, MovingAvgType.SimpleMovingAverage,
            MovingAvgType.WeightedMovingAverage, MovingAvgType.WildersSmoothingMethod })
            yield return new object[] { period, average };
    }

    private static StockData Data(double[] values) => new(values, values, values, values,
        values.Select(_ => 1d), values.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));

    [Theory, MemberData(nameof(Cases))]
    public void DirectBatchPreservesBothOutputsSignalsAndInputOwnership(int period, MovingAvgType average)
    {
        var fixtures = new[]
        {
            new[] { 2d }, Enumerable.Repeat(2d, 80).ToArray(),
            Enumerable.Range(0, 80).Select(i => 100 + Math.Sin(i * .37) * 12).ToArray(),
            new[] { double.Epsilon, double.MaxValue, -double.MaxValue, 0d, 1d },
            new[] { 1d, double.NaN }, new[] { 1d, double.PositiveInfinity }
        };
        foreach (var prices in fixtures)
        {
            var market = prices.Select((v, i) => i % 2 == 0 ? v : 1d).ToArray();
            var actual = Data(prices); var benchmark = Data(market);
            Dictionary<string, List<double>>? expected = null;
            var oldError = Record.Exception(() => expected = PreservedSectorRotationBatch.Run(prices, market,
                (p, b) => new SectorRotationModelState(p, b, average, period, 2)));
            var error = Record.Exception(() => actual.CalculateSectorRotationModel(benchmark, average, period, 2));
            Assert.Equal(oldError?.GetType(), error?.GetType());
            if (error is null)
            {
                Assert.NotNull(expected);
                Assert.Equal(expected.Keys, actual.OutputValues.Keys);
                foreach (var key in expected.Keys)
                    Assert.Equal(expected[key].Select(BitConverter.DoubleToInt64Bits), actual.OutputValues[key].Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(expected["Srm"].Select(BitConverter.DoubleToInt64Bits), actual.CustomValuesList.Select(BitConverter.DoubleToInt64Bits));
                var line = expected["Signal"];
                var signals = Enumerable.Range(0, line.Count).Select(i => GetCompareSignal(line[i] - (i == 0 ? 0 : line[i - 1]),
                    (i == 0 ? 0 : line[i - 1]) - (i < 2 ? 0 : line[i - 2])));
                Assert.Equal(signals, actual.SignalsList);
            }
            Assert.Equal(prices, actual.ClosePrices); Assert.Equal(market, benchmark.ClosePrices);
            Assert.Empty(benchmark.OutputValues); Assert.Empty(benchmark.CustomValuesList);
        }
    }

    [Fact]
    public void IdenticalLegsRetainZeroLineAndSignal()
    {
        var prices = new[] { double.Epsilon, double.MaxValue, -double.MaxValue, 0d, 1d };
        var data = Data(prices); data.CalculateSectorRotationModel(Data(prices), length1: 1, length2: 2);
        Assert.Equal(Enumerable.Repeat(0d, prices.Length), data.OutputValues["Srm"]);
        Assert.Equal(Enumerable.Repeat(0d, prices.Length), data.OutputValues["Signal"]);
    }
}
