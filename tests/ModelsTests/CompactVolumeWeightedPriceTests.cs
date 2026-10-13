using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class CompactVolumeWeightedPriceTests
{
    [Theory]
    [InlineData(0, null)] [InlineData(1, null)] [InlineData(511, null)]
    [InlineData(512, null)] [InlineData(1025, null)] [InlineData(513, 1)]
    [InlineData(511, 20)] [InlineData(512, 20)] [InlineData(513, 20)]
    [InlineData(1025, 127)] [InlineData(513, 4097)] [InlineData(513, int.MaxValue)]
    public async Task WorkersPreserveExactColumnsAndArrivalAnchors(int count, int? period)
    {
        var random = new Random(7114);
        var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i % 19),
            i, 12 + random.NextDouble(), 10 + random.NextDouble(), 11 + random.NextDouble(), i % 17 - 8)).ToArray();
        foreach (bool typical in new[] { false, true })
        foreach (DateTime? anchor in new DateTime?[] { null, DateTime.UnixEpoch.AddMinutes(7), DateTime.UnixEpoch.AddDays(1) })
            await CheckWorkers(bars, new(period, typical, anchor));
    }

    [Theory]
    [InlineData(null)] [InlineData(20)]
    public async Task WorkerBoundariesPreserveWideCancellationAndNegativeUnderflow(int? period)
    {
        foreach (bool typical in new[] { false, true })
        {
            var bars = Enumerable.Range(0, 1025).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                0, 0, 0, 0, 1)).ToArray();
            foreach (int index in new[] { 0, 126, 127, 128, 511, 512, 895 })
                bars[index] = new(bars[index].Time, 0, -double.Epsilon, -double.Epsilon, -double.Epsilon, 1);
            await CheckWorkers(bars, new(period, typical));
            double m = double.MaxValue;
            foreach (int index in new[] { 126, 127, 511, 512 })
            {
                double price = index % 2 == 0 ? m : -m;
                bars[index] = new(bars[index].Time, 0, price, price, price, m);
            }
            await CheckWorkers(bars, new(period, typical));
            await CheckWorkers(bars, new(period, typical, bars[1].Time));
        }
    }

    [Theory]
    [InlineData(null, false)] [InlineData(null, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public async Task WorkersRejectInputBeforeOverflowAndRecover(int? period, bool anchored)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Repeat(new Bar(DateTime.UnixEpoch, 0, 0, 0, 0, 0), 1025).ToArray();
            var indicator = new VolumeWeightedPrice(period, true, anchored ? DateTime.UnixEpoch : null);
            var builder = Build(bars, indicator);
            double m = double.MaxValue;
            bars[127] = new(DateTime.UnixEpoch, 0, m, m, m, 1);
            bars[128] = new(DateTime.UnixEpoch, 0, -m, -m, -m, -.5);
            bars[^1] = new(DateTime.UnixEpoch.AddDays(-1), double.NaN, 0, 0, 0, 0);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            bars[^1] = bars[0];
            var failure = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Equal(0, failure.OutputSlot); Assert.Equal(128, failure.BarIndex);
            bars[127] = bars[128] = bars[0];
            using var recovered = await builder.BuildAsync();
            Assert.All(recovered[indicator.Value].ToArray(), value => Assert.Equal(0, value));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    private static async Task CheckWorkers(Bar[] bars, VolumeWeightedPrice indicator)
    {
        var expected = Reference(bars, indicator);
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 2, 8 })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                Assert.True(ValuesBarExecution.SupportsOwned([indicator]));
                var input = bars.ToArray();
                using var run = await Build(input, indicator).ConfigureHistory(history).BuildAsync();
                Array.Clear(input); run.Dispose();
                for (int slot = 0; slot < 2; slot++)
                    Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),
                        run[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
                if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
                if (history == IndicatorHistoryMode.Full)
                {
                    int row = 0;
                    await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                    Assert.Equal(bars.Length, row);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(null, true)] [InlineData(null, false)]
    [InlineData(1, true)] [InlineData(1, false)]
    [InlineData(2, true)] [InlineData(2, false)]
    [InlineData(20, true)] [InlineData(20, false)]
    [InlineData(int.MaxValue, true)] [InlineData(int.MaxValue, false)]
    public void ExactProductsWindowsAndWideCancellationSurviveReset(int? period, bool typical)
    {
        var random = new Random(8146);
        foreach (int scale in new[] { -1070, -500, 0, 500, 1000 })
        {
            var bars = Enumerable.Range(0, 161).Select(i =>
            {
                double close = Math.ScaleB(1 + random.NextDouble(), scale);
                double high = Math.ScaleB(2 + random.NextDouble(), scale);
                double low = Math.ScaleB(random.NextDouble(), scale);
                double volume = Math.ScaleB((i % 3 == 0 ? -1 : 1) * (1 + random.NextDouble()), -scale / 2);
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, high, low, close, volume);
            }).ToArray();
            CheckState(bars, new(period, typical));
        }
        double m = double.MaxValue, e = double.Epsilon;
        CheckState([new(default, 0, m, m, m, m), new(default, 0, -m, -m, -m, m),
            new(default, 0, e, e, e, e), new(default, 0, 1, 0, 0, 1),
            new(default, 0, 2, 0, 0, 2)], new(period, typical));
    }

    [Theory]
    [InlineData(null)] [InlineData(2)]
    public void InclusiveAnchorOnlyCountsParticipatingBarsInArrivalOrder(int? period)
    {
        int[] offsets = [0, 4, 1, 3, 2, 5, 1, 6];
        var bars = offsets.Select((day, i) => new Bar(DateTime.UnixEpoch.AddDays(day), 0,
            i + 2, i, i + 1, i % 3 + 1)).ToArray();
        foreach (var anchor in new[] { DateTime.UnixEpoch.AddDays(-1), DateTime.UnixEpoch.AddDays(2),
            DateTime.UnixEpoch.AddDays(2).AddTicks(1), DateTime.UnixEpoch.AddDays(10) })
            CheckState(bars, new(period, true, anchor));
    }

    [Fact]
    public void ExactZeroIsCanonicalAndNegativeUnderflowRetainsSign()
    {
        var state = (IMultiOutputState)new VolumeWeightedPrice(typicalPrice: false).CreateState();
        Span<double> output = stackalloc double[2];
        state.Update(new Bar(default, 0, 0, 0, -double.Epsilon, 1), output);
        state.Update(new Bar(default, 0, 0, 0, 0, 4), output);
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(output[0]));
        state.Reset();
        state.Update(new Bar(default, 0, 0, 0, 0, -1), output);
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(output[0]));
        Assert.Equal(1, output[1]);
        CheckState([new(default, 0, 0, 0, -double.Epsilon, 1)], new());
    }

    [Theory]
    [InlineData(null, true)] [InlineData(null, false)] [InlineData(20, true)] [InlineData(20, false)]
    public void OrdinaryFullMantissaStateAllocatesNoPerBarObjects(int? period, bool typical)
    {
        var random = new Random(981);
        var bars = Enumerable.Range(0, 1024).Select(_ => new Bar(default, 0,
            12 + random.NextDouble(), 10 + random.NextDouble(), 11 + random.NextDouble(), 1 + random.NextDouble())).ToArray();
        var state = (IMultiOutputState)new VolumeWeightedPrice(period, typical).CreateState();
        Span<double> output = stackalloc double[2];
        foreach (var bar in bars) state.Update(in bar, output);
        state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        foreach (var bar in bars) state.Update(in bar, output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(double.IsFinite(output[0]));
        Assert.Equal(1, output[1]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OwnedColumnsAnchorAndValidationOrderSurviveSourceMutation(bool full)
    {
        var bars = Enumerable.Range(0, 65).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), i, i + 3, i, i + 1, i % 7 + 1)).ToArray();
        var indicator = new VolumeWeightedPrice(7, true, bars[3].Time);
        var expected = Reference(bars, indicator);
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
        double m = double.MaxValue;
        Bar[] invalid = [new(default, 0, m, m, m, 1), new(default, 0, -m, -m, -m, -.5),
            new(default, double.NaN, 1, 0, 1, 1)];
        var cumulative = new VolumeWeightedPrice();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Build(invalid, cumulative).BuildAsync());
        await Assert.ThrowsAsync<IndicatorOutputException>(() => Build(invalid[..2], cumulative).BuildAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(bars, indicator).BuildAsync(new CancellationToken(true)));
    }

    private static void CheckState(Bar[] bars, VolumeWeightedPrice indicator)
    {
        var expected = Reference(bars, indicator);
        var state = (IMultiOutputState)indicator.CreateState();
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

    private static double[][] Reference(Bar[] bars, VolumeWeightedPrice indicator)
    {
        var output = new[] { new double[bars.Length], new double[bars.Length] };
        var weighted = new List<ExactMeanAccumulator> { default };
        var volumes = new List<ExactMeanAccumulator> { default };
        for (int i = 0; i < bars.Length; i++)
        {
            var bar = bars[i];
            if (indicator.StartDate is DateTime anchor && bar.Time < anchor) continue;
            var total = weighted[^1]; var mass = volumes[^1];
            // Independent distributed products and exact prefix differences.
            total.AddProduct(bar.Close, bar.Volume);
            if (indicator.TypicalPrice)
            { total.AddProduct(bar.High, bar.Volume); total.AddProduct(bar.Low, bar.Volume); }
            mass.Add(bar.Volume, indicator.TypicalPrice ? 3 : 1);
            weighted.Add(total); volumes.Add(mass);
            int count = weighted.Count - 1;
            if (indicator.Period is int required && count < required) continue;
            int first = indicator.Period is int period ? count - period : 0;
            total.Subtract(weighted[first]); mass.Subtract(volumes[first]);
            if (mass.IsExactlyZero) continue;
            output[0][i] = total.Ratio(mass); output[1][i] = 1;
        }
        return output;
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
            .ConfigureExecution(IndicatorExecutionBackend.Cpu);
}
