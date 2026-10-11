using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedRollingPercentileTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CachedRanksAndWarmupMatchOriginalArithmeticAndTieOrdering(bool average)
    {
        double[] edges = [-0d, 0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1, -1, Math.BitIncrement(1)];
        var random = new Random(719);
        var input = edges.Concat(Enumerable.Range(0, 150).Select(_ => Math.ScaleB(random.Next(-32, 33), random.Next(-1074, 1018)))).ToArray();
        foreach (int period in new[] { 1, 2, 3, 20, int.MaxValue })
        foreach (double percentile in new[] { -0d, 0d, double.Epsilon, .1, .25, .5, .9, Math.BitDecrement(1), 1 })
        {
            var rank = ExactVarianceWindow.Units(percentile) * (period - 1);
            Assert.Equal(!(rank % (BigInteger.One << 1074)).IsZero,
                new RollingPercentile(period, percentile, average).HasFractionalFullRank);
            var actual = new RollingPercentile.State(period, percentile, average);
            foreach (var prices in new[] { input, input.Reverse().ToArray() })
            {
                actual.Reset();
                var expected = new Original(period, percentile, average);
                foreach (double price in prices)
                {
                    var bar = new Bar(default, 0, 1, -1, price, 1);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Next(price)), BitConverter.DoubleToInt64Bits(actual.Update(in bar)));
                }
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SharedBuilderRetainsOutputsSnapshotsAndFailureRecovery(bool average)
    {
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, 0, 1, -1, (i % 31 - 15) / 8d, 1)).ToArray();
        var indicator = new RollingPercentile(20, .25, average);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
            using var run = await builder.BuildAsync();
            var original = new Original(20, .25, average);
            Assert.Equal(bars.Select(b => original.Next(b.Close)), run[indicator].ToArray());
            if (history == IndicatorHistoryMode.Full)
            {
                int i = 0;
                await foreach (var snapshot in run) Assert.Equal(bars[i++], snapshot.Bar);
                Assert.Equal(bars.Length, i);
            }
            var saved = bars[20]; bars[20] = new Bar(default, 0, 1, -1, double.NaN, 1);
            var error = await Record.ExceptionAsync(() => builder.BuildAsync()); Assert.NotNull(error); Assert.Null(builder.LastExecution);
            bars[20] = saved;
            using var recovered = await builder.BuildAsync(); Assert.Equal(run[indicator].ToArray(), recovered[indicator].ToArray());
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
        }
    }

    [Theory]
    [InlineData(2, .5)] [InlineData(3, .5)] [InlineData(20, .5)] [InlineData(20, .25)]
    [InlineData(20, .1)] [InlineData(20, double.Epsilon)] [InlineData(4096, .5)] [InlineData(4097, .5)]
    public async Task FractionalWorkerReplayMatchesOriginalBitsIncludingEqualZeroHistory(int period, double percentile)
    {
        int previous = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
            double[] prices = [-0d, 0d, 0d, -0d, double.Epsilon, -double.Epsilon, 2, -2, double.MaxValue, -double.MaxValue];
            foreach (int count in new[] { 511, 512, 513, 8193 })
            foreach (bool average in new[] { false, true })
            {
                var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1, prices[i % prices.Length], 1)).ToArray();
                var oracle = new Original(period, percentile, average);
                var expected = bars.Select(b => BitConverter.DoubleToInt64Bits(oracle.Next(b.Close))).ToArray();
                var indicator = new RollingPercentile(period, percentile, average);
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
                    using var run = await builder.BuildAsync();
                    Assert.Equal(expected, run[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
                    using var mixed = await builder.ConfigureIndicators(indicator, new LowestLow(7)).BuildAsync();
                    Assert.Equal(expected, mixed[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
                }
            }
        }
        finally { AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    // Prior algorithm is retained here to detect changes to observable equal-zero ordering,
    // warmup, exact-rank boundaries and once-rounded interpolation, including Reset reuse.
    private sealed class Original
    {
        private readonly int _period;
        private readonly bool _average;
        private readonly BigInteger _top, _bottom;
        private readonly Queue<double> _history = new();
        private readonly List<double> _sorted = new();
        private ExactMeanAccumulator _sum;
        internal Original(int period, double percentile, bool average)
        {
            _period = period; _average = average;
            var top = ExactVarianceWindow.Units(percentile); var bottom = BigInteger.One << 1074;
            var gcd = BigInteger.GreatestCommonDivisor(top, bottom); _top = top / gcd; _bottom = bottom / gcd;
        }
        internal double Next(double value)
        {
            if (_history.Count == _period)
            {
                double oldest = _history.Dequeue(); _sorted.RemoveAt(_sorted.BinarySearch(oldest)); _sum.Add(oldest, -1);
            }
            int insertion = _sorted.BinarySearch(value); _sorted.Insert(insertion < 0 ? ~insertion : insertion, value);
            _history.Enqueue(value); _sum.Add(value);
            if (_average && _history.Count < _period) return _sum.Mean(_history.Count);
            int index = (int)BigInteger.DivRem(_top * (_history.Count - 1), _bottom, out var remainder);
            if (remainder.IsZero) return _sorted[index];
            var numerator = new ExactMeanAccumulator(); numerator.Add(_sorted[index], _bottom - remainder); numerator.Add(_sorted[index + 1], remainder);
            var denominator = new ExactMeanAccumulator(); denominator.Add(1, _bottom); return numerator.Ratio(denominator);
        }
    }
}
