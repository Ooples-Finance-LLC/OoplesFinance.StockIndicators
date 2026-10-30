using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TillsonCompactSnapshotTests
{
    public static IEnumerable<object[]> Cases =>
        from seed in Enum.GetValues<TillsonSeed>()
        from factor in new[] { 0d, .7, -1d, double.Epsilon, double.MaxValue }
        from dataset in new[] { 0, 1, 2 }
        select new object[] { seed, factor, dataset };

    [Theory, MemberData(nameof(Cases))]
    public void CompactStagesPreserveReferenceBitsPresenceAndReset(TillsonSeed seed, double factor, int dataset)
    {
        double[] edge = dataset switch
        {
            0 => [0, -0d, double.Epsilon, -double.Epsilon, 3 * double.Epsilon,
                -3 * double.Epsilon, Math.ScaleB(1, -1022), -Math.ScaleB(1, -1022)],
            1 => [double.MaxValue, -double.MaxValue, double.MaxValue / 2,
                -double.MaxValue / 2, 0, double.Epsilon, 1, -1],
            _ => [1, Math.BitIncrement(1), Math.BitDecrement(1), -1,
                Math.ScaleB(1, 52), Math.ScaleB(1, 52) + 1, -Math.ScaleB(1, 52), 0],
        };
        var bars = Enumerable.Range(0, 40).Select(i => new Bar(default,
            edge[i % edge.Length], edge[i % edge.Length], edge[i % edge.Length],
            edge[i % edge.Length], 1)).ToArray();
        foreach (int period in new[] { 1, 3, int.MaxValue })
        {
            var owner = new TillsonAverage(period, factor, seed, suppression: 2);
            var expected = Reference(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), period, factor, seed, 2);
            var state = (IMultiOutputState)owner.CreateState();
            var output = new double[2];
            for (int replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (int i = 0; i < bars.Length; i++)
                {
                    state.Update(in bars[i], output);
                    Assert.Equal(expected[i].HasValue ? 1d : 0d, output[1]);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i] ?? 0),
                        BitConverter.DoubleToInt64Bits(output[0]));
                }
            }
        }
    }
    [Theory]
    [InlineData(TillsonSeed.FirstPrice)]
    [InlineData(TillsonSeed.FollowingPrefix)]
    [InlineData(TillsonSeed.CascadedMeans)]
    public void UnpublishedWideInputsPreserveFallbackAndReset(TillsonSeed seed)
    {
        var inputs = Enumerable.Range(0, 32).Select(i =>
            (new BigInteger(i % 5 - 2) << 2400) + (i % 3 - 1)).ToArray();
        var prices = inputs.Select(n => new ReferenceFraction(n)
            / new ReferenceFraction(BigInteger.One << 1074)).ToArray();
        var expected = Reference(prices, 3, .7, seed, 0);
        var state = (IWideAverageState)new TillsonAverage(3, .7, seed).CreateState();
        var output = new double[2];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            Assert.Equal(BigInteger.Zero, state.RoundedUnits);
            for (int i = 0; i < inputs.Length; i++)
            {
                state.UpdateUnits(inputs[i], output);
                Assert.Equal(expected[i].HasValue ? 1d : 0d, output[1]);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i] ?? 0),
                    BitConverter.DoubleToInt64Bits(output[0]));
            }
        }
    }

    // Independent rational stage histories, evaluated stage-first. Keep the
    // final expression unrounded until ToDouble so negative underflow zero is
    // checked too; the validation library's extended rational zero has no sign.
    private static double?[] Reference(IReadOnlyList<ReferenceFraction> prices,
        int period, double factor, TillsonSeed seed, int suppression)
    {
        var levels = Enumerable.Range(0, 6).Select(_ => new ReferenceFraction?[prices.Count]).ToArray();
        var alpha = new ReferenceFraction(2) / new ReferenceFraction((long)period + 1);
        for (int stage = 0; stage < 6; stage++)
        for (int i = 0; i < prices.Count; i++)
        {
            ReferenceFraction? Input(int at) => stage == 0 ? prices[at] : levels[stage - 1][at];
            var current = Input(i);
            if (!current.HasValue) continue;
            long start = seed == TillsonSeed.CascadedMeans ? (long)stage * (period - 1) : 0;
            if (seed == TillsonSeed.CascadedMeans && i < start + period - 1) continue;
            ReferenceFraction sum = new(0);
            if (seed == TillsonSeed.CascadedMeans && i == start + period - 1)
            {
                for (int j = (int)start; j <= i; j++) sum += Input(j)!.Value;
                levels[stage][i] = (sum / new ReferenceFraction(period)).RoundExtendedBinary64();
            }
            else if (i == 0) levels[stage][i] = current;
            else if (seed == TillsonSeed.FollowingPrefix && i < period)
            {
                for (int j = 1; j <= i; j++) sum += Input(j)!.Value;
                levels[stage][i] = (sum / new ReferenceFraction(i)).RoundExtendedBinary64();
            }
            else levels[stage][i] = (levels[stage][i - 1]!.Value * (new ReferenceFraction(1) - alpha)
                + current.Value * alpha).RoundExtendedBinary64();
        }
        var a = ReferenceFraction.FromDouble(factor);
        var a2 = a * a;
        var a3 = a2 * a;
        var result = new double?[prices.Count];
        long first = (seed == TillsonSeed.CascadedMeans ? 6L * (period - 1) : 0) + suppression;
        for (int i = 0; i < result.Length; i++)
            if (i >= first)
                result[i] = ((new ReferenceFraction(1) + new ReferenceFraction(3) * a + new ReferenceFraction(3) * a2 + a3) * levels[2][i]!.Value
                    + (new ReferenceFraction(-3) * a - new ReferenceFraction(6) * a2 - new ReferenceFraction(3) * a3) * levels[3][i]!.Value
                    + (new ReferenceFraction(3) * a2 + new ReferenceFraction(3) * a3) * levels[4][i]!.Value
                    - a3 * levels[5][i]!.Value).ToDouble();
        return result;
    }

}
