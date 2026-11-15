using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CandleDifferenceUpdateTests
{
    [Fact]
    public void EveryAcceptedAndFallbackUpdateRetainsTheExactAccumulator()
    {
        var random = new Random(17921);
        var actual = new ExactMeanAccumulator();
        var expected = new ExactMeanAccumulator();
        double[] edges = [0, -0d, double.Epsilon, -double.Epsilon,
            double.MaxValue, -double.MaxValue, Math.ScaleB(1, -1022), 1, -1];
        int[] weights = [1, -1, 5, -20, int.MaxValue - 3, -(int.MaxValue - 3)];
        for (int i = 0; i < 10000; i++)
        {
            double upper = i < edges.Length * edges.Length ? edges[i % edges.Length]
                : Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1020));
            double lower = i < edges.Length * edges.Length ? edges[i / edges.Length]
                : Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1020));
            int weight = weights[i % weights.Length];
            CandleDifferenceUpdate.Add(ref actual, upper, lower, weight);
            expected.Add(upper, weight);
            expected.Add(lower, -weight);
            var error = actual;
            error.Subtract(expected);
            Assert.Equal(0, error.Sign);
        }
    }

    [Fact]
    public void ExpiringTheSameRangeCancelsItExactly()
    {
        foreach (var prices in new[] { (10d, 9d), (.1, .01), (double.MaxValue, -double.MaxValue),
            (double.Epsilon, -double.Epsilon), (1d, double.Epsilon) })
        {
            var sum = new ExactMeanAccumulator();
            CandleDifferenceUpdate.Add(ref sum, prices.Item1, prices.Item2, 1);
            var margin = sum;
            CandleDifferenceUpdate.Add(ref sum, prices.Item1, prices.Item2, -1);
            Assert.Equal(0, sum.Sign);
            Assert.Equal(1, margin.Sign);
        }
    }
}
