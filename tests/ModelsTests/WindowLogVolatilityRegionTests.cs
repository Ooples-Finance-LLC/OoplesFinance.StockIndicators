using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class WindowLogVolatilityRegionTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task PositiveWindowsPreserveBothColumnsAndOwnedHistory(bool realized, bool annualized)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            foreach (int count in new[] { 0, 1, 20, 21, 8191, 8192, 8193, 10000 })
                await Check(BarsFor(count), new(20, realized, annualized));
            // Long periods retain the original lazy startup and avoid replay allocation.
            await Check(BarsFor(100), new(int.MaxValue, realized, annualized));
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SkippedReturnsAndPermanentPoisonReplayTheCapturedWholeSeries(bool realized)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, 10000).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 7, 9, -3, 1, i)).ToArray();
            foreach (var change in new[] { (1, 2d), (1022, 0d), (1023, -0d), (1250, -2d), (1251, -4d), (1252, -8d), (5000, 0d) })
            {
                var old = bars[change.Item1];
                bars[change.Item1] = new(old.Time, old.Open, old.High, old.Low, change.Item2, old.Volume);
            }
            await Check(bars, new(20, realized), wholeReference: true);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ExtremeAndAdjacentPricesKeepExactRootsAcrossWorkerBoundaries(bool realized)
    {
        var bars = BarsFor(10000);
        for (int i = 0; i < bars.Length; i++)
        {
            double close = i is 2000 or 2002 ? double.MaxValue
                : i == 2001 ? double.Epsilon : i % 2 == 0 ? 1 : Math.BitIncrement(1d);
            var old = bars[i];
            bars[i] = new(old.Time, old.Open, old.High, old.Low, close, old.Volume);
        }
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            await Check(bars, new(20, realized));
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task MixedRootsCancellationAndInvalidRawFieldsRecover()
    {
        var bars = BarsFor(8193);
        var owner = new WindowLogVolatility(20, realized: true);
        foreach (bool mixed in new[] { false, true })
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            IIndicator[] roots = mixed ? [owner, new Sma(3)] : [owner];
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(roots).ConfigureHistory(mode);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            var original = bars[^1];
            bars[^1] = new(original.Time, original.Open, original.High, original.Low, original.Close, double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            bars[^1] = original;
            using var run = await builder.BuildAsync();
            Assert.Equal(Bits(Expected(bars, owner)[0]), Bits(run[owner.Value].ToArray()));
        }
    }

    private static Bar[] BarsFor(int count) => Enumerable.Range(0, count).Select(i =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), 7 + i % 5, 20 + i % 7, -3,
            Math.ScaleB(1d + (i * 13 % 61) / 64d, i / 127 % 9 - 4), i + 1)).ToArray();

    private static long[] Bits(double[] values) => values.Select(BitConverter.DoubleToInt64Bits).ToArray();

    private static double[][] Expected(Bar[] bars, WindowLogVolatility owner)
    {
        var state = new WindowLogVolatility.State(owner.Period, owner.Realized, owner.Annualized);
        var expected = new[] { new double[bars.Length], new double[bars.Length] };
        Span<double> values = stackalloc double[2];
        for (int i = 0; i < bars.Length; i++)
        {
            state.Update(in bars[i], values);
            expected[0][i] = values[0]; expected[1][i] = values[1];
        }
        return expected;
    }

    private static async Task Check(Bar[] bars, WindowLogVolatility owner, bool wholeReference = false)
    {
        var expected = Expected(bars, owner);
        if (wholeReference)
        {
            var reference = LogVolatilityReference.Values(bars, owner.Period, owner.Realized, owner.Annualized);
            Assert.Equal(Bits(reference.Select(v => v ?? 0).ToArray()), Bits(expected[0]));
            Assert.Equal(reference.Select(v => v.HasValue ? 1d : 0).ToArray(), expected[1]);
        }
        else
        {
            // Independent fraction/root reference around every potential worker
            // boundary. Positive inputs make exactly P+1 prices sufficient.
            int[] points = [0, 19, 20, 1249, 1250, 1999, 2000, 2001, 2002, 2499, 2500, 4999, 5000, 7499, 7500, bars.Length - 1];
            foreach (int index in points.Distinct().Where(i => i >= 0 && i < bars.Length))
            {
                int start = Math.Max(0, index - owner.Period);
                var reference = LogVolatilityReference.Values(bars[start..(index + 1)], owner.Period, owner.Realized, owner.Annualized)[^1];
                Assert.Equal(BitConverter.DoubleToInt64Bits(reference ?? 0), BitConverter.DoubleToInt64Bits(expected[0][index]));
                Assert.Equal(reference.HasValue ? 1d : 0, expected[1][index]);
            }
        }
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
            Array.Clear(input); run.Dispose();
            Assert.Equal(Bits(expected[0]), Bits(run[owner.Value].ToArray()));
            Assert.Equal(expected[1], run[owner.IsDefined].ToArray());
            if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            if (mode != IndicatorHistoryMode.Full) continue;
            int row = 0;
            await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
            Assert.Equal(bars.Length, row);
        }
    }
}
