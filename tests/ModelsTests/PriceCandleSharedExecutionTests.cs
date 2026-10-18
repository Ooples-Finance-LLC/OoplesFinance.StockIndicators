using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class PriceCandleSharedExecutionTests
{
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 100, 110 + i % 17, 90 - i % 13, 100 + i % 7, i % 13)).ToArray();
    [Theory]
    [InlineData(PriceCandleKind.RelativeDoji, 0)] [InlineData(PriceCandleKind.RelativeDoji, .1)] [InlineData(PriceCandleKind.RelativeDoji, .5)]
    [InlineData(PriceCandleKind.Marubozu, 80)] [InlineData(PriceCandleKind.Marubozu, 95)] [InlineData(PriceCandleKind.Marubozu, 100)]
    public async Task ClassificationThresholdsAndAllDetailsStayExact(PriceCandleKind kind, double percent)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int dop in new[] { 1, 8 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = dop;
                foreach (int count in new[] { 0, 1, 511, 513, 1001, 10000 }) await Check(Data(count), new PriceCandlePattern(kind, percent), count < 1002);
                var special = new[] {
                    new Bar(default, -0d, 0d, -0d, -0d, -0d), new Bar(default, -1, -1, -1, -1, 1),
                    new Bar(default, 100, 101, 99, Math.BitDecrement(100.1), 1),
                    new Bar(default, 100, 101, 99, Math.BitIncrement(100.1), 1),
                    new Bar(default, 0, 100, 0, Math.BitDecrement(95d), 1),
                    new Bar(default, 0, 100, 0, Math.BitIncrement(95d), 1),
                    new Bar(default, 0, double.Epsilon, 0, double.Epsilon, 1)
                };
                await Check(special, new PriceCandlePattern(kind, percent));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SecondaryDetailOverflowRetainsLaterInputPriority(bool marubozu)
    {
        var bars = Data(1001); bars[510] = new(default, 0, double.MaxValue, -double.MaxValue, 0, 1);
        var owner = new PriceCandlePattern(marubozu ? PriceCandleKind.Marubozu : PriceCandleKind.RelativeDoji);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);
            var ordinary = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode).ConfigureBehavior(_ => { });
            var expected = await Assert.ThrowsAsync<IndicatorOutputException>(() => ordinary.BuildAsync());
            var actual = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Equal(expected.Message, actual.Message);
            var valid = bars[900]; bars[900] = new(valid.Time, valid.Open, valid.High, valid.Low, valid.Close, double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync()); bars[900] = valid;
        }
    }

    [Fact]
    public async Task HeikinAshiPreservesRecursiveOpenExtremeMeansAndRawVolume()
    {
        foreach (int count in new[] { 0, 1, 2, 65, 513, 1001 }) await Check(Data(count), new HeikinAshiCandles());
        var extremes = Enumerable.Range(0, 65).Select(i => new Bar(default, i % 2 == 0 ? double.MaxValue : -double.MaxValue, double.MaxValue, -double.MaxValue, i % 3 == 0 ? double.Epsilon : -double.Epsilon, i % 2 == 0 ? -0d : double.MaxValue)).ToArray();
        await Check(extremes, new HeikinAshiCandles());
        foreach (double scale in new[] { double.Epsilon, 1e-200, -1d, Math.ScaleB(1d, 900) })
            await Check(Data(65).Select(b => new Bar(b.Time, b.Open * scale, Math.Max(b.High * scale, b.Low * scale), Math.Min(b.High * scale, b.Low * scale), b.Close * scale, b.Volume)).ToArray(), new HeikinAshiCandles());
    }

    [Fact]
    public async Task MixedChainedRootsPreserveCancellationAndBackendContracts()
    {
        var bars = Data(513); MultiOutputIndicatorBase[] roots = [new PriceCandlePattern(), new PriceCandlePattern(PriceCandleKind.Marubozu), new HeikinAshiCandles()];
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots.Concat(new[] { roots[0] }).ToArray()).ConfigureHistory(mode);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
            foreach (var owner in roots)
            {
                using var ordinary = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureBehavior(_ => { }).BuildAsync();
                Columns(run, owner, owner.Outputs.Select(o => ordinary[o].ToArray()).ToArray());
            }
            await Assert.ThrowsAnyAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        }
        foreach (var root in roots) { root.Of(new FirstValueEma(5)); await Check(bars, root, false); }
    }

    private static async Task Check(Bar[] bars, IIndicator owner, bool reference = true)
    {
        using var ordinary = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureBehavior(_ => { }).BuildAsync();
        var expected = owner.Outputs.Select(o => ordinary[o].ToArray()).ToArray();
        if (reference)
            foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
                rule.Check(new IndicatorValidationContext("Price candle shared execution", bars, expected, 0));
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
            Array.Clear(input); run.Dispose(); Columns(run, owner, expected);
            if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            if (mode == IndicatorHistoryMode.Full)
            {
                int row = 0; await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }
    private static void Columns(IIndicatorRun run, IIndicator owner, double[][] expected)
    {
        for (int slot = 0; slot < expected.Length; slot++)
            Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits), run[owner.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }
}
