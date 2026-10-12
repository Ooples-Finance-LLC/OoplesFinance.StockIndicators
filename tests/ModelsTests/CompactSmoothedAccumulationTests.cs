using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactSmoothedAccumulationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CertifiedFlowsAndPowerOfTwoMeansPreserveEveryRoundingStage(bool first)
    {
        var random = new Random(24736);
        foreach (int scale in new[] { -1074, -1022, -1000, 0, 1000, 1020 })
        {
            var bars = Enumerable.Range(0, 80).Select(i =>
            {
                double close = i % 11 == 0 ? 2 : 4 * random.NextDouble();
                double volume = i % 13 == 0 ? 0 : Math.ScaleB(1 + random.NextDouble(), scale);
                return new Bar(default, 0, i % 7 == 0 ? 0 : 4, i % 7 == 0 ? 4 : 0, close, volume);
            }).ToArray();
            foreach (var periods in new[] { (1, 3), (3, 7), (7, 15), (15, 3), (2, 4), (3, 10) })
            foreach (bool details in new[] { false, true })
                CheckState(bars, new(periods.Item1, periods.Item2, first, includeDetails: details));
        }
        // A rounded FMA numerator can overflow even though division by its
        // power-of-two denominator would bring the exact mean back into range.
        Bar[] highLine = Enumerable.Range(0, 20).Select(i =>
            new Bar(default, 0, 2, 0, 2, i == 0 ? double.MaxValue * .75 : 0)).ToArray();
        CheckState(highLine, new(3, 7, first, includeDetails: true));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MixedDetailModesKeepEveryOwnedColumnIndependentAcrossRuns(bool full)
    {
        foreach (int count in new[] { 0, 1, 9, 10, 513 })
        {
            var bars = OrdinaryBars()[..count];
            SmoothedAccumulationOscillator[] indicators = [new(3, 10, false, includeDetails: false),
                new(3, 10, false, includeDetails: true), new(3, 10, true, includeDetails: false),
                new(3, 10, true, includeDetails: true)];
            var expected = indicators.Select(i => SmoothedAccumulationReference.Values(bars, i)).ToArray();
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicators)
                .ConfigureHistory(full ? IndicatorHistoryMode.Full : IndicatorHistoryMode.LatestOnly).BuildAsync();
            Array.Clear(input);
            run.Dispose();
            // A subsequent run must not reuse the prior run's shared constant
            // arrays. Both detail conventions coexist in the same values graph.
            using var next = await new StockIndicatorBuilder().ConfigureSource(Bars.From(OrdinaryBars()[..17]))
                .ConfigureIndicators(indicators).ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
            for (int owner = 0; owner < indicators.Length; owner++)
            for (int slot = 0; slot < 8; slot++)
                Assert.Equal(expected[owner][slot % 4].Select(v => Bits(slot < 4 ? v ?? 0 : v.HasValue ? 1 : 0)),
                    run[indicators[owner].Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
            if (count > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            if (full)
            {
                int row = 0;
                await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(count, row);
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExactStagesMatchIndependentFractionsAcrossScalesAndReset(bool first)
    {
        var random = new Random(71421);
        foreach (int scale in new[] { -1070, -500, 0, 500, 1000 })
        {
            var bars = Enumerable.Range(0, 80).Select(i =>
            {
                double high = Math.ScaleB(1 + random.NextDouble(), scale);
                double low = Math.ScaleB(-random.NextDouble(), scale);
                double close = Math.ScaleB(random.NextDouble(), scale);
                double volume = Math.ScaleB((i % 3 == 0 ? -1 : 1) * (1 + random.NextDouble()), -scale / 2);
                if (i % 7 == 0) (high, low) = (low, high);
                if (i % 11 == 0) high = low;
                return new Bar(default, 0, high, low, close, volume);
            }).ToArray();
            foreach (var periods in new[] { (1, 2), (3, 10), (10, 3), (2, 2), (int.MaxValue, int.MaxValue) })
            foreach (bool details in new[] { false, true })
                CheckState(bars, new(periods.Item1, periods.Item2, first, first ? 2 : 0, details));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WideFallbackAndHiddenOverflowPreserveCancellation(bool first)
    {
        double m = double.MaxValue, e = double.Epsilon;
        Bar[] bars = [new(default, 0, e, 0, m, m), new(default, 0, e, 0, m, -m),
            new(default, 0, 1, e, e, e), new(default, 0, 1, e, 1, 1),
            new(default, 0, m, -m, e, 1), new(default, 0, 1, 0, .5, 1)];
        // Equal periods cancel even when the hidden flow and EMA exceed binary64.
        foreach (var periods in new[] { (1, 1), (2, 2), (3, 10) })
        foreach (bool details in new[] { false, true })
            CheckState(bars, new(periods.Item1, periods.Item2, first, includeDetails: details));
    }

    [Fact]
    public void UnderflowedStagesAndExactCancellationPublishPositiveZero()
    {
        Bar[] bars = [new(default, 0, 1, 0, 0, double.Epsilon),
            new(default, 0, 4, 0, 1, double.Epsilon), new(default, 0, 4, 0, 1, -double.Epsilon),
            new(default, 0, 1, 0, 1, double.Epsilon)];
        CheckState(bars, new(1, 2, includeDetails: true));
        var state = (IMultiOutputState)new SmoothedAccumulationOscillator(1, 1, includeDetails: true).CreateState();
        Span<double> output = stackalloc double[8];
        state.Update(in bars[1], output);
        for (int slot = 0; slot < 4; slot++)
            if (slot != 1) Assert.Equal(0L, BitConverter.DoubleToInt64Bits(output[slot]));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task BuilderOwnsAllColumnsAndPreservesInputPrecedence(bool full, bool first)
    {
        var bars = OrdinaryBars();
        var indicator = new SmoothedAccumulationOscillator(3, 10, first, includeDetails: true);
        var expected = SmoothedAccumulationReference.Values(bars, indicator);
        var input = bars.ToArray();
        using var run = await Build(input, indicator)
            .ConfigureHistory(full ? IndicatorHistoryMode.Full : IndicatorHistoryMode.LatestOnly).BuildAsync();
        Array.Clear(input);
        run.Dispose();
        for (int slot = 0; slot < 8; slot++)
            Assert.Equal(expected[slot % 4].Select(v => Bits(slot < 4 ? v ?? 0 : v.HasValue ? 1 : 0)),
                run[indicator.Outputs[slot]].ToArray().Select(Bits));
        Assert.Equal(bars[^1], run.Latest.Bar);
        if (full)
        {
            int row = 0;
            await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
            Assert.Equal(bars.Length, row);
        }
        Bar[] invalid = [new(default, 0, double.Epsilon, 0, double.MaxValue, double.MaxValue),
            new(default, double.NaN, 1, 0, 1, 1)];
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Build(invalid, indicator).BuildAsync());
        await Assert.ThrowsAsync<IndicatorOutputException>(() => Build(invalid[..1], indicator).BuildAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(bars, indicator).BuildAsync(new CancellationToken(true)));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OrdinaryFullMantissaStateAllocatesNoPerBarObjects(bool first)
    {
        var bars = OrdinaryBars();
        var state = (IMultiOutputState)new SmoothedAccumulationOscillator(3, 10, first, includeDetails: true).CreateState();
        Span<double> output = stackalloc double[8];
        foreach (var bar in bars) state.Update(in bar, output);
        state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        foreach (var bar in bars) state.Update(in bar, output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(double.IsFinite(output[0]));
        Assert.Equal(1, output[4]);
    }

    private static Bar[] OrdinaryBars()
    {
        var random = new Random(61528);
        return Enumerable.Range(0, 513).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i,
            12 + random.NextDouble(), 9 + random.NextDouble(), 10 + random.NextDouble(), 1 + random.NextDouble())).ToArray();
    }

    private static void CheckState(Bar[] bars, SmoothedAccumulationOscillator indicator)
    {
        var expected = SmoothedAccumulationReference.Values(bars, indicator);
        var state = (IMultiOutputState)indicator.CreateState();
        Span<double> actual = stackalloc double[8];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], actual);
                for (int slot = 0; slot < 8; slot++)
                {
                    var value = expected[slot % 4][i];
                    Assert.Equal(Bits(slot < 4 ? value ?? 0 : value.HasValue ? 1 : 0),
                        BitConverter.DoubleToInt64Bits(actual[slot]));
                }
            }
        }
    }

    // The integer-unit contract discards zero signs at every rounded stage.
    private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value == 0 ? 0 : value);

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
}
