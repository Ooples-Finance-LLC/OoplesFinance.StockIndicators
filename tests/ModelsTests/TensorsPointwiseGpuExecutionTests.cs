using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsPointwiseGpuExecutionTests
{
    public static IEnumerable<object[]> Configurations => SharedArithmeticExecutionTests.Configurations
        .Select(row => new object[] { (int)(CandleArithmeticOperation)row[0], row[1], row[2] })
        .Concat(Enumerable.Range(4, 3).Select(operation => new object[] { operation, CandlePriceField.Close, CandlePriceField.Open }));

    private static void RequireGpu() => Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
    private static MultiOutputIndicatorBase Create(int operation, CandlePriceField left = CandlePriceField.Close,
        CandlePriceField right = CandlePriceField.Open) => operation < 4
        ? new CandleArithmetic((CandleArithmeticOperation)operation, left, right)
        : new PriceRoundingTransform((PriceRoundingOperation)(operation - 4));

    [SkippableTheory, MemberData(nameof(Configurations))]
    public async Task DeviceExecutionPreservesAllFieldsBitsPresenceAndOwnership(int operation, CandlePriceField left, CandlePriceField right)
    {
        RequireGpu();
        double[] inputs = [-0d, 0d, -.25, .25, 1, -1, 123.4567, -123.4567];
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Enumerable.Range(0, 1025).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                inputs[i % 8], inputs[(i + 1) % 8], inputs[(i + 3) % 8], inputs[(i + 5) % 8], i)).ToArray();
            var indicator = Create(operation, left, right);
            var builder = Builder(bars, indicator, history);
            using var cpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            using var gpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            Assert.False(string.IsNullOrWhiteSpace(builder.LastExecution.DeviceName));
            Assert.Equal(cpu.Latest.Bar, gpu.Latest.Bar);
            var original = bars.ToArray();
            Array.Clear(bars);
            for (int slot = 0; slot < 2; slot++) Bits(cpu[indicator.Outputs[slot]].ToArray(), gpu[indicator.Outputs[slot]].ToArray());
            if (history == IndicatorHistoryMode.Full)
            {
                int index = 0;
                await foreach (var snapshot in gpu) Assert.Equal(original[index++], snapshot.Bar);
                Assert.Equal(original.Length, index);
            }
        }
    }

    [SkippableTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public async Task ExtremeAndSubnormalValuesKeepExactRounding(int operation)
    {
        RequireGpu();
        double[] inputs = [double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon,
            Math.ScaleB(1, -1022), -Math.ScaleB(1, -1022), -0d, 0d, Math.BitIncrement(1d), Math.BitDecrement(1d)];
        var bars = inputs.Select(v => new Bar(default, operation is 2 or 3 ? 1 : 0, 1, 0, v, 1)).ToArray();
        var indicator = Create(operation);
        var builder = Builder(bars, indicator, IndicatorHistoryMode.LatestOnly);
        using var cpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
        using var gpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync();
        for (int slot = 0; slot < 2; slot++) Bits(cpu[indicator.Outputs[slot]].ToArray(), gpu[indicator.Outputs[slot]].ToArray());
    }

    [SkippableTheory]
    [InlineData(CandleArithmeticOperation.Add)] [InlineData(CandleArithmeticOperation.Subtract)]
    [InlineData(CandleArithmeticOperation.Multiply)] [InlineData(CandleArithmeticOperation.Divide)]
    public async Task InputBeforeOverflowPrecedenceAndRecoveryMatchCpu(CandleArithmeticOperation operation)
    {
        RequireGpu();
        var bars = Enumerable.Repeat(new Bar(default, 1, 2, 0, .5, 1), 1025).ToArray();
        var right = operation == CandleArithmeticOperation.Subtract ? -double.MaxValue
            : operation == CandleArithmeticOperation.Divide ? double.Epsilon : double.MaxValue;
        bars[0] = new Bar(default, right, 1, 0, double.MaxValue, 1);
        bars[^1] = new Bar(default, 1, 2, 0, .5, double.NaN);
        var indicator = new CandleArithmetic(operation);
        var builder = Builder(bars, indicator, IndicatorHistoryMode.Full);
        for (int step = 0; step < 2; step++)
        {
            var expected = await Record.ExceptionAsync(() => builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
            var actual = await Record.ExceptionAsync(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            Assert.NotNull(expected);
            Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(expected.Message, actual.Message);
            Assert.Null(builder.LastExecution);
            bars[^1] = bars[1];
        }
        bars[0] = bars[1];
        using var recovered = await builder.BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
        Assert.Equal(bars.Length, recovered.BarCount);
    }

    [SkippableFact]
    public async Task WorkspaceReuseConcurrencyAndPresenceDoNotMutateCompletedRuns()
    {
        RequireGpu();
        var bars = Enumerable.Repeat(new Bar(default, 1, 2, 0, .5, 1), 16385).ToArray();
        var indicator = new CandleArithmetic(CandleArithmeticOperation.Divide);
        var builder = Builder(bars, indicator, IndicatorHistoryMode.LatestOnly);
        using var first = await builder.BuildAsync();
        int[] missing = [0, 63, 64, 8191, 8192, 16384];
        foreach (int i in missing) bars[i] = new Bar(default, -0d, 1, 0, 1, 1);
        using var mixed = await builder.BuildAsync();
        await Task.WhenAll(Enumerable.Range(0, 7).Select(operation => Task.Run(async () =>
        {
            var other = Create(operation);
            using var result = await Builder(bars.Take(1025).ToArray(), other, IndicatorHistoryMode.Full).BuildAsync();
            Assert.Equal(1025, result.BarCount);
        })));
        Assert.All(first[indicator.IsDefined].ToArray(), value => Assert.Equal(1d, value));
        var flags = mixed[indicator.IsDefined].ToArray();
        for (int i = 0; i < flags.Length; i++) Assert.Equal(missing.Contains(i) ? 0d : 1d, flags[i]);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public async Task EmptyOrUnsupportedRequiredGpuRequestsAreNeverReportedAsCpuSuccess()
    {
        var indicator = new CandleArithmetic(CandleArithmeticOperation.Add);
        var builder = Builder([], indicator, IndicatorHistoryMode.LatestOnly);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        indicator.Of(new Sma(3));
        builder.ConfigureSource(Bars.From(new[] { new Bar(default, 1, 2, 0, .5, 1) }));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
    }

    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Gpu);
    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
