using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactCmfTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(20)] [InlineData(int.MaxValue)]
    public void ExactWindowAndSeparatelyRoundedFlowSurviveResetAndExtremeScales(int period)
    {
        var random = new Random(93482);
        foreach (int scale in new[] { -1070, -500, 0, 500, 1000 })
        {
            var bars = Enumerable.Range(0, 161).Select(i =>
            {
                double low = Math.ScaleB(1 + random.NextDouble(), scale);
                double high = i % 11 == 0 ? low : Math.ScaleB(2 + random.NextDouble(), scale);
                double close = Math.ScaleB(1 + 2 * random.NextDouble(), scale);
                if (i % 7 == 0) (high, low) = (low, high);
                double volume = Math.ScaleB((i % 3 == 0 ? -1 : 1) * (1 + random.NextDouble()), -scale / 2);
                return new Bar(default, 0, high, low, close, volume);
            }).ToArray();
            CheckState(bars, period);
        }
        double m = double.MaxValue, e = double.Epsilon;
        CheckState([new(default, 0, 1, 0, 1, m), new(default, 0, 1, 0, 1, -m),
            new(default, 0, 1, 0, .25, e), new(default, 0, 1, 0, 1, 1),
            new(default, 0, m, -m, e, 1), new(default, 0, 3, 0, 1, 5)], period);
    }

    [Fact]
    public void PublishedFlowCanonicalizesUnderflowButMultiplierAndCmfKeepItsSign()
    {
        double e = double.Epsilon, m = double.MaxValue;
        CheckState([new(default, 0, m, -m, -e, 1),
            new(default, 0, 2, 0, .5, e), new(default, 0, 1, 0, 0, e),
            new(default, 0, 1, 0, .5, m)], 2);
        var state = (IMultiOutputState)new MoneyFlowWithDetails(2).CreateState();
        Span<double> actual = stackalloc double[4];
        state.Update(new Bar(default, 0, m, -m, -e, 1), actual);
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(actual[0]));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(actual[1]));
        state.Reset();
        state.Update(new Bar(default, 0, 1, 0, 0, e), actual);
        state.Update(new Bar(default, 0, 1, 0, .5, m), actual);
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(actual[2]));
        Assert.Equal(1, actual[3]);
    }

    [Fact]
    public void OrdinaryFullMantissaWindowAllocatesNoPerBarObjectsAfterWarmup()
    {
        var random = new Random(615);
        var bars = Enumerable.Range(0, 1024).Select(_ =>
        {
            double low = 10 + random.NextDouble(), high = low + 1 + random.NextDouble();
            return new Bar(default, low, high, low, low + random.NextDouble(), 1 + random.NextDouble());
        }).ToArray();
        var state = (IMultiOutputState)new MoneyFlowWithDetails(20).CreateState();
        Span<double> actual = stackalloc double[4];
        foreach (var bar in bars) state.Update(in bar, actual);
        state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        foreach (var bar in bars) state.Update(in bar, actual);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(1, actual[3]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BothHistoriesOwnEveryColumnAndValidateInputsBeforeOutputs(bool full)
    {
        var bars = Enumerable.Range(0, 65).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), i, 3, 0, i % 4, i % 7 - 3)).ToArray();
        var expected = Reference(bars, 7);
        var indicator = new MoneyFlowWithDetails(7);
        var input = bars.ToArray();
        using var run = await Build(input, indicator).ConfigureHistory(full ? IndicatorHistoryMode.Full : IndicatorHistoryMode.LatestOnly).BuildAsync();
        Array.Clear(input); run.Dispose();
        for (int slot = 0; slot < 4; slot++)
            Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits), run[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
        Assert.Equal(bars[^1], run.Latest.Bar);
        if (full)
        {
            int row = 0;
            await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
            Assert.Equal(bars.Length, row);
        }
        Bar[] invalid = [new(default, 0, double.Epsilon, 0, double.MaxValue, 1),
            new(default, double.NaN, 1, 0, 1, 1)];
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Build(invalid, indicator).BuildAsync());
        await Assert.ThrowsAsync<IndicatorOutputException>(() => Build(invalid[..1], indicator).BuildAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(bars, indicator).BuildAsync(new CancellationToken(true)));
    }

    private static void CheckState(Bar[] bars, int period)
    {
        var expected = Reference(bars, period);
        var state = (IMultiOutputState)new MoneyFlowWithDetails(period).CreateState();
        Span<double> actual = stackalloc double[4];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], actual);
                for (int slot = 0; slot < 4; slot++)
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i]), BitConverter.DoubleToInt64Bits(actual[slot]));
                if (actual.ToArray().Any(v => !double.IsFinite(v))) break;
            }
        }
    }

    private static double[][] Reference(Bar[] bars, int period)
    {
        var output = Enumerable.Range(0, 4).Select(_ => new double[bars.Length]).ToArray();
        for (int i = 0; i < bars.Length; i++)
        {
            var b = bars[i];
            var numerator = new ExactMeanAccumulator();
            numerator.Add(b.Close, 2); numerator.Add(b.High, -1); numerator.Add(b.Low, -1);
            var range = new ExactMeanAccumulator(); range.Add(b.High); range.Add(b.Low, -1);
            output[0][i] = numerator.Ratio(range);
            // Distributed exact products form the complete flow independently
            // of the compact state's difference followed by multiplication.
            var product = new ExactMeanAccumulator();
            product.AddProduct(b.Close, b.Volume, 2);
            product.AddProduct(b.High, b.Volume, -1); product.AddProduct(b.Low, b.Volume, -1);
            double flow = product.Ratio(range);
            output[1][i] = flow == 0 ? 0 : flow;
            if (!double.IsFinite(output[0][i]) || !double.IsFinite(flow)) break;
            if (i < period - 1) continue;
            var flows = new ExactMeanAccumulator(); var volumes = new ExactMeanAccumulator();
            for (int j = i - period + 1; j <= i; j++)
            { flows.Add(output[1][j]); volumes.Add(bars[j].Volume); }
            if (volumes.IsExactlyZero) continue;
            output[2][i] = flows.Ratio(volumes); output[3][i] = 1;
            if (!double.IsFinite(output[2][i])) break;
        }
        return output;
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
            .ConfigureExecution(IndicatorExecutionBackend.Cpu);
}
