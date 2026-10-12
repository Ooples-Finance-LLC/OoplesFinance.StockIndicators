using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactForceIndexTests
{
    [Theory]
    [InlineData(3)] [InlineData(13)]
    public void WideExponentGapsSurviveSeedCancellationAndRecurrence(int period)
    {
        double m = double.MaxValue, e = double.Epsilon;
        Bar[] initial = [new(default, 0, 0, 0, 0, 0), new(default, 0, m, m, m, m),
            new(default, 0, m, m, m, e), new(default, 0, 0, 0, 0, m),
            new(default, 0, e, e, e, e), new(default, 0, 1, 1, 1, 1)];
        // The enormous seed terms cancel before publication. Epsilon squared
        // and 1-epsilon then force a span far wider than the compact mantissa.
        var tail = Enumerable.Range(0, 30).Select(i =>
        {
            double close = i % 2 == 0 ? e : 1;
            return new Bar(default, 0, close, close, close, i % 3 == 0 ? e : 1);
        });
        CheckState(initial.Concat(tail).ToArray(), period);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(13)] [InlineData(int.MaxValue)]
    public void ProductsSeedsAndRecurrencesMatchIndependentAccumulatorAfterReset(int period)
    {
        var random = new Random(72138);
        foreach (int scale in new[] { -1070, -500, 0, 500, 1000 })
        {
            var bars = Enumerable.Range(0, 160).Select(i =>
            {
                double close = Math.ScaleB((i % 3 == 0 ? -1 : 1) * (1 + random.NextDouble()), scale);
                double volume = Math.ScaleB((i % 5 == 0 ? -1 : 1) * (1 + random.NextDouble()), -scale / 2);
                return new Bar(default, 0, close, close, close, volume);
            }).ToArray();
            CheckState(bars, period);
        }
        double m = double.MaxValue;
        CheckState([new(default, 0, 0, 0, 0, 0), new(default, 0, m, m, m, m),
            new(default, 0, 0, 0, 0, m), new(default, 0, 1, 1, 1, -0d)], period);
    }

    [Fact]
    public void ExactAndUnderflowedZeroRetainTheirDistinctSigns()
    {
        CheckState([new(default, 0, 0, 0, 0, 0), new(default, 0, -double.Epsilon, -double.Epsilon, -double.Epsilon, .25),
            new(default, 0, -double.Epsilon, -double.Epsilon, -double.Epsilon, -1)], 1);
        var state = (IMultiOutputState)new SeededForceIndex(1).CreateState();
        Span<double> values = stackalloc double[2];
        state.Update(new Bar(default, 0, 0, 0, 0, 0), values);
        state.Update(new Bar(default, 0, 0, 0, -double.Epsilon, .25), values);
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(values[0]));
        state.Update(new Bar(default, 0, 0, 0, -double.Epsilon, -1), values);
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(values[0]));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BuilderOwnsBothColumnsAndPreservesValidationOrder(bool full)
    {
        var random = new Random(5298);
        var bars = Enumerable.Range(0, 513).Select(i =>
        {
            double close = 10 + random.NextDouble();
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), i, close + 1, close - 1, close, 1 + random.NextDouble());
        }).ToArray();
        var indicator = new SeededForceIndex(13);
        var expected = Reference(bars, indicator.Period);
        var input = bars.ToArray();
        using var run = await Build(input, indicator).ConfigureHistory(full ? IndicatorHistoryMode.Full : IndicatorHistoryMode.LatestOnly).BuildAsync();
        Array.Clear(input); run.Dispose();
        for (int slot = 0; slot < 2; slot++)
            Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits), run[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
        Assert.Equal(bars[^1], run.Latest.Bar);
        if (full)
        {
            int row = 0;
            await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
            Assert.Equal(bars.Length, row);
        }
        Bar[] invalid = [new(default, 0, 0, 0, 0, 0), new(default, 0, 0, 0, double.MaxValue, double.MaxValue),
            new(default, 0, 0, 0, 1, 1), new(default, double.NaN, 0, 0, 1, 1)];
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Build(invalid, new SeededForceIndex(1)).BuildAsync());
        await Assert.ThrowsAsync<IndicatorOutputException>(() => Build(invalid[..2], new SeededForceIndex(1)).BuildAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(bars, indicator).BuildAsync(new CancellationToken(true)));
    }

    [Fact]
    public void FullMantissaOrdinaryRecurrencesAllocateNoPerBarObjects()
    {
        var random = new Random(521);
        var bars = Enumerable.Range(0, 1024).Select(_ =>
        {
            double close = 10 + random.NextDouble();
            return new Bar(default, 0, close, close, close, 1 + random.NextDouble());
        }).ToArray();
        var state = (IMultiOutputState)new SeededForceIndex(13).CreateState();
        Span<double> values = stackalloc double[2];
        foreach (var bar in bars) state.Update(in bar, values);
        state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        foreach (var bar in bars) state.Update(in bar, values);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(double.IsFinite(values[0]));
        Assert.Equal(1, values[1]);
    }

    private static void CheckState(Bar[] bars, int period)
    {
        var expected = Reference(bars, period);
        var state = (IMultiOutputState)new SeededForceIndex(period).CreateState();
        Span<double> actual = stackalloc double[2];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], actual);
                for (int slot = 0; slot < 2; slot++)
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i]), BitConverter.DoubleToInt64Bits(actual[slot]));
                if (!double.IsFinite(actual[0])) break;
            }
        }
    }

    // Independent accumulator formulation distributes both products instead of
    // forming a compact difference, and uses integer mean publication.
    private static double[][] Reference(Bar[] bars, int period)
    {
        double[][] result = [new double[bars.Length], new double[bars.Length]];
        var seed = new ExactMeanAccumulator();
        double previous = 0;
        for (int i = 1; i < bars.Length; i++)
        {
            if (i <= period)
            {
                seed.AddProduct(bars[i].Close, bars[i].Volume);
                seed.AddProduct(bars[i - 1].Close, bars[i].Volume, -1);
                if (i < period) continue;
                previous = seed.Mean(period);
            }
            else
            {
                var next = new ExactMeanAccumulator();
                next.Add(previous, period - 1);
                next.AddProduct(bars[i].Close, bars[i].Volume, 2);
                next.AddProduct(bars[i - 1].Close, bars[i].Volume, -2);
                previous = next.Mean((long)period + 1);
            }
            result[0][i] = previous; result[1][i] = 1;
            if (!double.IsFinite(previous)) break;
        }
        return result;
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
}
