using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class MatchingLowGridTests
{
    private static double[] Reference(MatchingLowCandle owner, Bar[] bars) =>
        SequenceCandleReference.Evaluate(bars, owner.Period, owner.WarmupBars, SequenceCandleKind.MatchingLow).ToArray();

    private static double[] Original(MatchingLowCandle owner, Bar[] bars)
    {
        var state = (IIndicatorState)owner.CreateState()!;
        return bars.Select(b => state.Update(in b)).ToArray();
    }

    private static Bar[] Data(int count, int exponent = 0) => Enumerable.Range(0, count).Select(i =>
    {
        double open = i % 16 >= 14 ? 8 : 4;
        double close = i % 16 >= 14 ? 4 : 6;
        double high = 10, low = 0;
        if (i / 16 % 2 != 0) (open, high, low, close) = (-open, -low, -high, -close);
        double Scale(double value) => Math.ScaleB(1000 + Math.ScaleB(value, -20), exponent);
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), Scale(open), Scale(high), Scale(low), Scale(close), i);
    }).ToArray();

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    [InlineData(-1074)]
    [InlineData(900)]
    public void CertifiedBlocksMatchIndependentReferencesWithPositiveSignals(int exponent)
    {
        foreach (int period in new[] { 1, 2, 5, 64 })
        {
            var owner = new MatchingLowCandle(period);
            var bars = Data(323, exponent);
            var actual = new double[bars.Length];
            Assert.True(ValuesBarExecution.TryMatchingLowGridBlock(bars, 0, owner,
                new long[3 * bars.Length + 1], actual, out bool single));
            Assert.True(single);
            Assert.Equal(Reference(owner, bars), actual);
            Assert.Equal(Original(owner, bars), actual);
            if (exponent != -1074) Assert.Contains(100d, actual);
        }
    }

    [Theory]
    [InlineData(8191, 1)] [InlineData(8191, 64)] [InlineData(8191, 65)]
    [InlineData(8192, 1)] [InlineData(8192, 64)] [InlineData(8192, 65)]
    [InlineData(8193, 1)] [InlineData(8193, 64)] [InlineData(8193, 65)]
    public async Task SizePeriodAndWorkerBoundariesRetainValuesAndOwnedHistory(int count, int period)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (bool wide in new[] { false, true })
            {
                var bars = Data(count);
                if (wide)
                    foreach (int i in new[] { 0, 255, 256, 511, 1023, count - 1 })
                        bars[i] = new(bars[i].Time, double.MaxValue, double.MaxValue, -double.MaxValue, double.Epsilon, i);
                var owner = new MatchingLowCandle(period);
                var expected = Original(owner, bars);
                Assert.Equal(count >= 8192 && period <= 64,
                    ValuesBarExecution.MatchingLowGridEligible(owner, count));
                foreach (int workers in new[] { 1, 2, 8 })
                foreach (var mode in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
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
        Assert.False(ValuesBarExecution.TryMatchingLowGridBlock(bars, 0, new MatchingLowCandle(1),
            new long[7], output, out bool single));
        Assert.False(single);
        Assert.Equal(new[] { 99d, 99d }, output);
    }

    [Fact]
    public void OriginalGridResolverRetainsWiderExponentCoverage()
    {
        Bar[] bars = [new(default, .125, .25, 0, .125, 0),
            new(default, 500, 600, 400, 450, 0), new(default, 500, 600, 400, 450, 0)];
        var actual = new double[bars.Length];
        var owner = new MatchingLowCandle(1);
        Assert.True(ValuesBarExecution.TryMatchingLowGridBlock(bars, 0, owner,
            new long[10], actual, out bool single));
        Assert.False(single);
        Assert.Equal(Reference(owner, bars), actual);
    }

    [Fact]
    public void RangeOnlyKernelDoesNotRequireAnUnusedBodyPrefixToFit()
    {
        var bars = Enumerable.Range(0, 323).Select(i => new Bar(default, 1e200, 0, 0, -1e200, i)).ToArray();
        var owner = new MatchingLowCandle(1);
        var actual = new double[bars.Length];
        Assert.True(ValuesBarExecution.TryMatchingLowGridBlock(bars, 0, owner,
            new long[3 * bars.Length + 1], actual, out bool single));
        Assert.True(single);
        Assert.Contains(100d, actual);
        Assert.Equal(Reference(owner, bars), actual);
    }

    [Theory]
    [InlineData(-1, 100d)]
    [InlineData(0, 100d)]
    [InlineData(1, 0d)]
    public void ExactAndAdjacentEqualityThresholdsRetainOriginalStrictness(int step, double expected)
    {
        double close = step < 0 ? Math.BitDecrement(4.5) : step > 0 ? Math.BitIncrement(4.5) : 4.5;
        Bar[] bars = [new(default, 4, 10, 0, 6, 0), new(default, 8, 10, 0, 4, 0),
            new(default, 8, 10, 0, close, 0)];
        var owner = new MatchingLowCandle(1);
        var actual = new double[bars.Length];
        Assert.True(ValuesBarExecution.TryMatchingLowGridBlock(bars, 0, owner,
            new long[3 * bars.Length + 1], actual, out _));
        Assert.Equal(expected, actual[^1]);
        Assert.Equal(Reference(owner, bars), actual);
    }

    [Fact]
    public async Task SignedZeroHistoryAndCanonicalSignalsRemainDistinctContracts()
    {
        var bars = Enumerable.Range(0, 8193).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
            i % 2 == 0 ? -0d : 0d, 0, -0d, 0, -0d)).ToArray();
        var owner = new MatchingLowCandle(64);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).BuildAsync();
        Assert.All(run[owner].ToArray(), value => Assert.Equal(0L, BitConverter.DoubleToInt64Bits(value)));
        int i = 0;
        await foreach (var snapshot in run)
        {
            var expected = bars[i++];
            Assert.Equal(expected.Time, snapshot.Bar.Time);
            foreach (var (before, after) in new[] { (expected.Open, snapshot.Bar.Open), (expected.High, snapshot.Bar.High),
                (expected.Low, snapshot.Bar.Low), (expected.Close, snapshot.Bar.Close), (expected.Volume, snapshot.Bar.Volume) })
                Assert.Equal(BitConverter.DoubleToInt64Bits(before), BitConverter.DoubleToInt64Bits(after));
        }
        Assert.Equal(bars.Length, i);
    }

    [Fact]
    public async Task InvalidInputAndCancellationPreserveErrorsAndBuilderRecovery()
    {
        var bars = Data(8193);
        var owner = new MatchingLowCandle(64);
        var expected = Original(owner, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancelled.Token));
        foreach (int index in new[] { 0, 255, 8192 })
        {
            var valid = bars[index];
            bars[index] = new(valid.Time, valid.Open, valid.High, valid.Low, valid.Close, double.NaN);
            var originalError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                new StockIndicatorBuilder().ConfigureSource(Bars.From(bars, b => b)).ConfigureIndicators(owner).BuildAsync());
            var actualError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            Assert.Equal(originalError.Message, actualError.Message);
            bars[index] = valid;
        }
        using var run = await builder.BuildAsync();
        Assert.Equal(expected, run[owner].ToArray());
    }
}
