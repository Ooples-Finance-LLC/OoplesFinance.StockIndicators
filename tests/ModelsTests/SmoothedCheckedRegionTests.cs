using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SmoothedCheckedRegionTests
{
    [Fact]
    public void KeysRequireCompleteSeedsAndBothConfigurationAndValue()
    {
        var mean = new SmoothedAccumulationOscillator.CompactMean(3, false);
        var one = JurikDyadic.FromDouble(1);
        Assert.False(mean.CaptureContinuation().Ready);
        mean.Add(one);
        mean.Add(one);
        Assert.False(mean.CaptureContinuation().Ready);
        mean.Add(one);
        var key = mean.CaptureContinuation();
        Assert.True(key.Ready);
        var other = new SmoothedAccumulationOscillator.CompactMean(4, false);
        for (int i = 0; i < 4; i++) other.Add(one);
        Assert.NotEqual(key, other.CaptureContinuation());
        var first = new SmoothedAccumulationOscillator.CompactMean(3, true);
        first.Add(one);
        Assert.NotEqual(key, first.CaptureContinuation());
        mean.Add(JurikDyadic.FromDouble(2));
        Assert.NotEqual(key, mean.CaptureContinuation());
        mean.Reset();
        Assert.False(mean.CaptureContinuation().Ready);
    }

    [Fact]
    public async Task ThresholdsDetailsSeedsAndOwnershipPreserveEveryColumn()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int count in new[] { 8191, 8192, 10003 })
            foreach (bool first in new[] { false, true })
            foreach (bool details in new[] { false, true })
            {
                var bars = Rich(count);
                var owner = new SmoothedAccumulationOscillator(3, 10, first, first ? 4000 : 0, details);
                var expected = Reference(bars, owner);
                foreach (int workers in new[] { 1, 8 })
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    await Check(bars, owner, expected);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task NonconvergentPlateauAndUnsupportedFlowsRepairOrFallback()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var plateau = Enumerable.Range(0, 10003).Select(i =>
                new Bar(default, 0, 2, 0, i < 1000 || i == 8500 ? 2 : 1, 1)).ToArray();
            foreach (bool first in new[] { false, true })
            foreach (bool details in new[] { false, true })
            {
                var owner = new SmoothedAccumulationOscillator(3, 10, first, includeDetails: details);
                await Check(plateau, owner, Reference(plateau, owner));
            }
            double[] extremes = [-0d, double.Epsilon, -double.Epsilon,
                Math.ScaleB(1d, -1022), Math.ScaleB(1d, 900), Math.ScaleB(-1d, 900)];
            var wide = Enumerable.Range(0, 8192).Select(i =>
                new Bar(default, 0, 1, -1, extremes[i % extremes.Length], i + 1)).ToArray();
            var hidden = new SmoothedAccumulationOscillator(3, 3);
            await Check(wide, hidden, Reference(wide, hidden));
            // Negative ranges produce -0 through ordinary division. The
            // original dyadic detail publisher converts both zero signs to +0.
            var reversed = Enumerable.Range(0, 10003).Select(i =>
                new Bar(default, 0, 0, 4, i % 2 == 0 ? 2 : 1, 1)).ToArray();
            var reversedOwner = new SmoothedAccumulationOscillator(3, 10, includeDetails: true);
            await Check(reversed, reversedOwner, Reference(reversed, reversedOwner));
            foreach (var periods in new[] { (1, 2), (10, 3), (int.MaxValue, int.MaxValue) })
            {
                var owner = new SmoothedAccumulationOscillator(periods.Item1, periods.Item2, includeDetails: true);
                await Check(plateau, owner, Reference(plateau, owner));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task RawErrorsCancellationAndMixedRootsRetainGenericContracts()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Rich(8192);
            var owner = new SmoothedAccumulationOscillator(3, 10, includeDetails: true);
            var invalid = bars.ToArray();
            invalid[0] = new(default, 0, double.Epsilon, 0, double.MaxValue, double.MaxValue);
            invalid[^1] = new(default, double.NaN, 1, 0, 1, 1);
            StockIndicatorBuilder Build(Bar[] input) => new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(owner).ConfigureExecution(IndicatorExecutionBackend.Cpu);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Build(invalid).BuildAsync());
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(bars).BuildAsync(new CancellationToken(true)));
            var second = new SmoothedAccumulationOscillator(10, 3, true);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(owner, second).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            foreach (var indicator in new[] { owner, second })
            {
                var expected = Reference(bars, indicator);
                for (int slot = 0; slot < 8; slot++)
                    Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),
                        run[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    private static Bar[] Rich(int count) => Enumerable.Range(0, count).Select(i =>
    {
        double close = 100 + (i * 13 % 101) / 8d;
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), close - 1, close + 2, close - 2,
            i % 13 == 0 ? close + 1 : close, i + 1);
    }).ToArray();

    private static double[][] Reference(Bar[] bars, SmoothedAccumulationOscillator owner)
    {
        var state = (IMultiOutputState)owner.CreateState();
        var result = Enumerable.Range(0, 8).Select(_ => new double[bars.Length]).ToArray();
        var scratch = new double[8];
        for (int i = 0; i < bars.Length; i++)
        {
            state.Update(in bars[i], scratch);
            for (int slot = 0; slot < 8; slot++) result[slot][i] = scratch[slot];
        }
        return result;
    }

    private static async Task Check(Bar[] bars, SmoothedAccumulationOscillator owner, double[][] expected)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner)
                .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            Array.Clear(input);
            run.Dispose();
            for (int slot = 0; slot < 8; slot++)
                Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),
                    run[owner.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(bars[^1], run.Latest.Bar);
        }
    }
}
