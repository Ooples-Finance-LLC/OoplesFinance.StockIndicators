using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class CandleSinglePassGridTests
{
    private static IndicatorBase Owner(int kind, int period = 5) => kind switch
    {
        0 => new MatchingLowCandle(period),
        1 => new SeparatingLinesCandle(period, period, period),
        _ => new TasukiGapCandle(period),
    };

    private static double[] Reference(IndicatorBase owner, Bar[] bars) => (owner switch
    {
        MatchingLowCandle x => SequenceCandleReference.Evaluate(bars, x.Period, x.WarmupBars, SequenceCandleKind.MatchingLow),
        SeparatingLinesCandle x => MatchedLinesReference.Evaluate(bars, x.BodyPeriod, x.ShadowPeriod, x.EqualPeriod, MatchedLinesKind.SeparatingLines),
        TasukiGapCandle x => GapContinuationReference.Evaluate(bars, x.Period, GapContinuationKind.TasukiGap),
        _ => throw new InvalidOperationException(),
    }).ToArray();

    private static double[] Original(IndicatorBase owner, Bar[] bars)
    {
        var state = (IIndicatorState)owner.CreateState()!;
        return bars.Select(b => state.Update(in b)).ToArray();
    }

    private static Bar[] Data(int count, int kind, int exponent = 0) => Enumerable.Range(0, count).Select(i =>
    {
        int phase = i % 16;
        double open = 4, high = 10, low = 0, close = 6;
        if (kind == 0 && phase >= 14) { open = 8; close = 4; }
        if (kind == 1)
        {
            if (phase == 14) { open = 6; close = 4; }
            if (phase == 15) { open = 6; close = 9; low = 6; }
        }
        if (kind == 2)
        {
            if (phase == 13) { open = 2; close = 4; }
            if (phase == 14) { open = 6; close = 8; }
            if (phase == 15) { open = 7; close = 5; }
        }
        if ((i / 16) % 2 != 0) (open, high, low, close) = (-open, -low, -high, -close);
        double Scale(double value) => Math.ScaleB(1000 + Math.ScaleB(value, -20), exponent);
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), Scale(open), Scale(high), Scale(low), Scale(close), i);
    }).ToArray();

    [Theory]
    [InlineData(0, 0)] [InlineData(1, 0)] [InlineData(2, 0)]
    [InlineData(0, -1000)] [InlineData(1, -1000)] [InlineData(2, -1000)]
    [InlineData(0, -1074)] [InlineData(1, -1074)] [InlineData(2, -1074)]
    [InlineData(0, 900)] [InlineData(1, 900)] [InlineData(2, 900)]
    public void CertifiedBlocksMatchIndependentReferencesWithPositiveSignals(int kind, int exponent)
    {
        foreach (int period in new[] { 1, 2, 5, 64 })
        {
            var owner = Owner(kind, period);
            var bars = Data(323, kind, exponent);
            var actual = new double[bars.Length];
            Assert.True(ValuesBarExecution.TryCandleSinglePassGridBlock(bars, 0, owner,
                new long[5 * (bars.Length + 1)], actual, out bool single));
            Assert.True(single);
            Assert.Equal(Reference(owner, bars), actual);
            Assert.Equal(Original(owner, bars), actual);
            if (exponent != -1074)
            {
                Assert.Contains(100d, actual);
                if (kind != 0) Assert.Contains(-100d, actual);
            }
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task WorkerBlockAndSizeBoundariesRetainOwnedHistoryAndStateFallback(int kind)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int count in new[] { 1000, 8191, 8192, 8193, 10000 })
            foreach (bool wide in new[] { false, true })
            {
                var bars = Data(count, kind);
                if (wide)
                    foreach (int i in new[] { 0, 255, 256, 511, 1023, count - 1 }.Where(index => index >= 0 && index < count))
                        bars[i] = new(bars[i].Time, double.MaxValue, double.MaxValue, -double.MaxValue, double.Epsilon, i);
                var owner = Owner(kind);
                var expected = Original(owner, bars);
                Assert.Equal(count >= 8192, ValuesBarExecution.CandleSinglePassGridEligible(owner, count));
                foreach (int workers in new[] { 1, 2, 8 })
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    var input = bars.ToArray();
                    using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                        .ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
                    Array.Clear(input);
                    run.Dispose();
                    Assert.Equal(expected, run[owner].ToArray());
                    Assert.Equal(bars[^1], run.Latest.Bar);
                    if (mode == IndicatorHistoryMode.Full)
                    {
                        int i = 0;
                        await foreach (var snapshot in run) Assert.Equal(bars[i++], snapshot.Bar);
                        Assert.Equal(count, i);
                    }
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public void TinyPricesCannotDisappearDuringGridScaling()
    {
        Bar[] bars = [new(default, double.MaxValue, double.MaxValue, 0, double.MaxValue, 0),
            new(default, double.Epsilon, double.Epsilon, 0, double.Epsilon, 0)];
        double[] output = [99, 99];
        Assert.False(ValuesBarExecution.TryCandleSinglePassGridBlock(bars, 0, Owner(0, 1), new long[15], output, out bool single));
        Assert.False(single);
        Assert.Equal(new[] { 99d, 99d }, output);
    }

    [Fact]
    public void OriginalGridResolverRetainsWiderExponentCoverage()
    {
        Bar[] bars = [new(default, .125, .25, 0, .125, 0),
            new(default, 500, 600, 400, 450, 0), new(default, 500, 600, 400, 450, 0)];
        var output = new double[bars.Length];
        var owner = Owner(0, 1);
        Assert.True(ValuesBarExecution.TryCandleSinglePassGridBlock(bars, 0, owner, new long[20], output, out bool single));
        Assert.False(single);
        Assert.Equal(Reference(owner, bars), output);
    }

    [Fact]
    public void RangeOnlyKernelDoesNotRequireAnUnusedBodyPrefixToFit()
    {
        var bars = Enumerable.Range(0, 323).Select(i => new Bar(default, 1e200, 0, 0, -1e200, i)).ToArray();
        var owner = Owner(0, 1);
        var output = new double[bars.Length];
        Assert.True(ValuesBarExecution.TryCandleSinglePassGridBlock(bars, 0, owner,
            new long[5 * (bars.Length + 1)], output, out bool single));
        Assert.True(single);
        Assert.Contains(100d, output);
        Assert.Equal(Reference(owner, bars), output);
    }

    [Fact]
    public void ExactAndAdjacentThresholdsKeepTheirOriginalStrictness()
    {
        foreach (int step in new[] { -1, 0, 1 })
        {
            double Adjacent(double x) => step < 0 ? Math.BitDecrement(x) : step > 0 ? Math.BitIncrement(x) : x;
            Bar[] matching = [new(default, 4, 10, 0, 6, 0), new(default, 8, 10, 0, 4, 0), new(default, 8, 10, 0, Adjacent(4.5), 0)];
            Bar[] separating = [new(default, 4, 10, 0, 6, 0), new(default, 8, 10, 0, 4, 0), new(default, 8, 14, Adjacent(7), 13, 0)];
            Bar[] tasuki = [new(default, 4, 10, 0, 6, 0), new(default, 0, 10, 0, 2, 0),
                new(default, 4, 10, 0, 8, 0), new(default, 5, 10, 0, Adjacent(3), 0)];
            foreach (var (kind, bars, last) in new[] { (0, matching, step <= 0 ? 100d : 0d),
                (1, separating, step > 0 ? 100d : 0d), (2, tasuki, step < 0 ? 100d : 0d) })
            {
                var owner = Owner(kind, 1);
                var output = new double[bars.Length];
                Assert.True(ValuesBarExecution.TryCandleSinglePassGridBlock(bars, 0, owner,
                    new long[5 * (bars.Length + 1)], output, out _));
                Assert.Equal(last, output[^1]);
                Assert.Equal(Reference(owner, bars), output);
            }
        }
    }

    [Fact]
    public async Task SignedZeroHistoryAndCanonicalSignalsRemainDistinctContracts()
    {
        var bars = Enumerable.Range(0, 8193).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
            i % 2 == 0 ? -0d : 0d, 0, -0d, 0, -0d)).ToArray();
        foreach (int kind in new[] { 0, 1, 2 })
        {
            var owner = Owner(kind);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).BuildAsync();
            Assert.All(run[owner].ToArray(), value => Assert.Equal(0L, BitConverter.DoubleToInt64Bits(value)));
            int i = 0;
            await foreach (var snapshot in run)
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(bars[i].Open), BitConverter.DoubleToInt64Bits(snapshot.Bar.Open));
                Assert.Equal(BitConverter.DoubleToInt64Bits(bars[i].Low), BitConverter.DoubleToInt64Bits(snapshot.Bar.Low));
                i++;
            }
            Assert.Equal(bars.Length, i);
        }
    }

    [Fact]
    public async Task OtherPeriodsValidationAndCancellationKeepTheOriginalRoute()
    {
        foreach (var owner in new IndicatorBase[] { Owner(0, 65), new SeparatingLinesCandle(1, 64, 2),
            new SeparatingLinesCandle(65, 1, 2), Owner(2, 999) })
        {
            var bars = Data(8193, 1);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).BuildAsync();
            Assert.Equal(Original(owner, bars), run[owner].ToArray());
        }
        var invalid = Data(8193, 0);
        invalid[255] = new(default, 1, 2, 0, 1, double.NaN);
        var expectedError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new StockIndicatorBuilder().ConfigureSource(Bars.From(invalid, b => b)).ConfigureIndicators(Owner(0)).BuildAsync());
        var actualError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new StockIndicatorBuilder().ConfigureSource(Bars.From(invalid)).ConfigureIndicators(Owner(0)).BuildAsync());
        Assert.Equal(expectedError.Message, actualError.Message);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(Data(8193, 0)))
                .ConfigureIndicators(Owner(0)).BuildAsync(cancelled.Token);
        });
    }
}
