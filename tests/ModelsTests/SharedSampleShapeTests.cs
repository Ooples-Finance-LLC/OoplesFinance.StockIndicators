using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedSampleShapeTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompactMomentsMatchIndependentCenteredFractions(bool kurtosis)
    {
        double[][] fixtures = [
            [0, -0d, 1, 2, 3, 4, 4, 4, 4, 4, 0, -1, -2, -3],
            [1, 1.25, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1, 2, 3, 4, 5, 6],
            Enumerable.Range(0, 18).Select(i => Math.ScaleB((i % 7 - 3) * 1.23456789012345, 1000 - i * 110)).ToArray()
        ];
        foreach (int period in new[] { kurtosis ? 4 : 3, 7, int.MaxValue })
        foreach (var values in fixtures)
        {
            var bars = values.Select(v => new Bar(default, 0, 1, -1, v, 1)).ToArray();
            var expected = SampleShapeReference.Calculate(bars, period, kurtosis);
            var state = new WindowSampleShape.State(period, kurtosis);
            var output = new double[2];
            for (int pass = 0; pass < 2; pass++)
            {
                state.Reset();
                for (int i = 0; i < bars.Length; i++)
                {
                    state.Update(in bars[i], output);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i] ?? 0), BitConverter.DoubleToInt64Bits(output[0]));
                    Assert.Equal(expected[i].HasValue ? 1d : 0, output[1]);
                }
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WorkerReplayPreservesScaleChangesFlatPresenceAndLargePeriods(bool kurtosis)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        try
        {
            foreach (int period in new[] { 4, 20, 4096, 4097, int.MaxValue })
            foreach (int count in new[] { 0, 1, 511, 512, 513, period is 4096 or 4097 ? 8193 : 1025 })
            {
                var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1,
                    i == 0 ? double.Epsilon : i % 91 < 40 ? 1 : (i % 31 - 15) / 8d, 1)).ToArray();
                var shape = new WindowSampleShape(period, kurtosis);
                using var expected = await Builder(bars, shape).ConfigureBehavior(_ => { }).BuildAsync();
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    using var actual = await Builder(bars, shape).ConfigureHistory(history).BuildAsync();
                    using var mixed = await Builder(bars, shape).ConfigureHistory(history).ConfigureIndicators(shape, new LowestLow(7)).BuildAsync();
                    foreach (var output in shape.Outputs)
                    {
                        Bits(expected[output].ToArray(), actual[output].ToArray());
                        Bits(expected[output].ToArray(), mixed[output].ToArray());
                    }
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task InvalidInputCancellationRecoveryAndSnapshotOwnership()
    {
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, 0, 1, -1, i % 13, 1)).ToArray();
        var original = bars.ToArray(); var shape = new WindowSampleShape(20, true);
        var builder = Builder(bars, shape); using var saved = await builder.BuildAsync();
        bars[600] = new Bar(default, 0, 1, -1, 1, double.NaN);
        var expected = await Record.ExceptionAsync(() => Builder(bars, shape).ConfigureBehavior(_ => { }).BuildAsync());
        var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
        Assert.Null(builder.LastExecution);
        original.CopyTo(bars, 0); using var recovered = await builder.BuildAsync();
        Bits(saved[shape.Value].ToArray(), recovered[shape.Value].ToArray());
        Array.Clear(bars); int index = 0;
        await foreach (var snapshot in saved) Assert.Equal(original[index++], snapshot.Bar);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }
    private static void Bits(IEnumerable<double> expected, IEnumerable<double> actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
}
