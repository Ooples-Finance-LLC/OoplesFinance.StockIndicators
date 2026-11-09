using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class CachedWordAccumulationMeanTests
{
    [Fact]
    public void CachedDivisionMatchesIndependentFullWordIntegerOracle()
    {
        var random = new Random(91109);
        int[] counts = [.. Enumerable.Range(1, 2047), 65535, 1 << 20, int.MaxValue,
            .. Enumerable.Range(0, 512).Select(_ => random.Next(1, int.MaxValue))];
        foreach (int count in counts)
        {
            var divisor = new ExactMeanAccumulator.WordMeanDivisor(count);
            ulong[] values = [0, 1, ulong.MaxValue, ulong.MaxValue - 1,
                (ulong)count - 1, (ulong)count, (ulong)count + 1,
                ((ulong)random.NextInt64() << 1) | (uint)random.Next(2)];
            foreach (ulong value in values)
                Assert.Equal((ulong)(new BigInteger(value) / count), divisor.Divide(value));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExactMeanAccumulator.WordMeanDivisor(0));
        Assert.Throws<DivideByZeroException>(() => default(ExactMeanAccumulator.WordMeanDivisor).Divide(1));
    }

    [Fact]
    public void MeansMatchIndependentFractionsAtRoundingAndFallbackBoundaries()
    {
        foreach (int count in new[] { 1, 2, 3, 20, 2047, 65535, int.MaxValue })
        foreach (int sign in new[] { -1, 1 })
        {
            for (int bit = 53; bit <= 62; bit++)
            foreach (int power in new[] { -1074, -1022, -70, 0, 900 })
            foreach (int tail in new[] { 1, 3, 17 })
                Check([sign * Math.ScaleB(1, bit + power), sign * Math.ScaleB(tail, power)], count);
            Check([sign * double.Epsilon], count);
            Check([sign * 1d, sign * double.Epsilon], count);
            Check([sign * double.MaxValue, sign * double.MaxValue], count);
            Check([sign * Math.ScaleB(1, 54), sign * 1d], count, -1200);
            Check([sign * Math.ScaleB(1, 54), sign * 1d], count, 1200);
            Check([sign * 1d, -sign * 1d], count);
        }
        var empty = new ExactMeanAccumulator();
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.MeanCachedWord(default));

        static void Check(double[] values, int count, int scale = 0)
        {
            var mean = new ExactMeanAccumulator();
            var reference = new ReferenceFraction(0);
            foreach (double value in values) { mean.Add(value); reference += ReferenceFraction.FromDouble(value); }
            if (scale != 0)
            {
                mean.ScaleByPowerOfTwo(scale);
                var factor = new ReferenceFraction(BigInteger.One << Math.Abs(scale));
                reference = scale < 0 ? reference / factor : reference * factor;
            }
            var divisor = new ExactMeanAccumulator.WordMeanDivisor(count);
            double expected = (reference / new ReferenceFraction(count)).ToDouble();
            Equal(expected, mean.MeanCachedWord(in divisor));
            Equal(mean.Mean(count), mean.MeanCachedWord(in divisor));
        }
    }

    [Fact]
    public async Task CapturedAdlMeansMatchIndependentPublishedPrefixWindows()
    {
        Bar[] bars = Enumerable.Range(0, 257).Select(i =>
        {
            double close = 10 + (i * 13 % 101) / 8d;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), close,
                close + i % 3 + 1.5, close - i % 5 - .25, close,
                (i % 11 == 0 ? -1 : 1) * (1000 + i * 47 % 10001));
        }).ToArray();
        var multiplier = new double[bars.Length];
        var flow = new double[bars.Length];
        var prefix = new double[bars.Length];
        var total = new ReferenceFraction(0);
        for (int i = 0; i < bars.Length; i++)
        {
            var bar = bars[i];
            var high = ReferenceFraction.FromDouble(bar.High);
            var low = ReferenceFraction.FromDouble(bar.Low);
            var balance = ReferenceFraction.FromDouble(bar.Close) * new ReferenceFraction(2) - high - low;
            multiplier[i] = (balance / (high - low)).ToDouble();
            flow[i] = (balance * ReferenceFraction.FromDouble(bar.Volume) / (high - low)).ToDouble();
            total += ReferenceFraction.FromDouble(flow[i]);
            prefix[i] = total.ToDouble();
        }
        foreach (int? period in new int?[] { null, 1, 2, 20, bars.Length, bars.Length + 1, int.MaxValue })
        {
            var average = new double[bars.Length];
            var presence = new double[bars.Length];
            if (period is int count && count <= bars.Length)
                for (int i = count - 1; i < bars.Length; i++)
                {
                    var sum = new ReferenceFraction(0);
                    for (int j = i - count + 1; j <= i; j++) sum += ReferenceFraction.FromDouble(prefix[j]);
                    average[i] = (sum / new ReferenceFraction(count)).ToDouble();
                    presence[i] = 1;
                }
            foreach (var history in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
            {
                var input = bars.ToArray();
                var indicator = new AccumulationDistributionWithAverage(period);
                using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                    .ConfigureIndicators(indicator).ConfigureHistory(history).BuildAsync();
                Array.Clear(input);
                double[][] expected = [multiplier, flow, prefix, average, presence];
                for (int slot = 0; slot < expected.Length; slot++)
                    Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),
                        actual[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(bars[^1], actual.Latest.Bar);
            }
        }
    }

    private static void Equal(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
