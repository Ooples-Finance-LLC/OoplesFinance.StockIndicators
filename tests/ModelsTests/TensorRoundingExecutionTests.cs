using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorRoundingExecutionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DirectedRoundingMatchesMathForRandomBitsTailsAndOwnedHistory(bool ceiling)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            var random = new Random(170113);
            double[] boundary = [-0d, 0d, double.Epsilon, -double.Epsilon, .25, -.25,
                double.MaxValue, -double.MaxValue, Math.BitDecrement(1), Math.BitIncrement(-1)];
            foreach (int workers in new[] { 1, 8 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                foreach (int count in new[] { 0, 1, 3, 4, 5, 10000, 65537 })
                {
                    var bars = new Bar[count];
                    var expected = new double[count];
                    for (int i = 0; i < count; i++)
                    {
                        long bits = random.NextInt64();
                        if ((i & 2) != 0) bits |= long.MinValue;
                        double value = i % 2 == 0 ? boundary[i / 2 % boundary.Length]
                            : BitConverter.Int64BitsToDouble(bits);
                        if (!double.IsFinite(value)) value = 1;
                        bars[i] = new Bar(DateTime.UnixEpoch.AddMinutes(i), 3, 4, 2, value, i);
                        expected[i] = ceiling ? Math.Ceiling(value) : Math.Floor(value);
                    }
                    var original = bars.ToArray();
                    var indicator = new PriceRoundingTransform(ceiling ? PriceRoundingOperation.Ceiling : PriceRoundingOperation.Floor);
                    using var full = await Build(bars, indicator, IndicatorHistoryMode.Full);
                    using var latest = await Build(bars, indicator, IndicatorHistoryMode.LatestOnly);
                    Array.Clear(bars);
                    Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), full[indicator.Value].ToArray().Select(BitConverter.DoubleToInt64Bits));
                    Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), latest[indicator.Value].ToArray().Select(BitConverter.DoubleToInt64Bits));
                    Assert.All(full[indicator.IsDefined].ToArray(), value => Assert.Equal(1d, value));
                    int index = 0;
                    await foreach (var snapshot in full) Assert.Equal(original[index++], snapshot.Bar);
                    Assert.Equal(count, index);
                    if (count > 0) Assert.Equal(original[^1], latest.Latest.Bar);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InvalidNonCloseInputAndCancellationPreserveFailureAndRecovery(bool ceiling)
    {
        var indicator = new PriceRoundingTransform(ceiling ? PriceRoundingOperation.Ceiling : PriceRoundingOperation.Floor);
        foreach (var history in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        {
            var bars = Enumerable.Repeat(new Bar(default, 1, 2, 0, -.25, 1), 65537).ToArray();
            bars[^1] = new Bar(default, 1, 2, 0, -.25, double.NaN);
            var expected = Record.Exception(() => IndicatorInputDomain.Finite.Validate(in bars[^1]));
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
            var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
            Assert.Equal(expected!.GetType(), actual!.GetType());
            Assert.Equal(expected.Message, actual.Message);
            Assert.Null(builder.LastExecution);
            bars[^1] = bars[0];
            using var recovered = await builder.BuildAsync();
            Assert.Equal(ceiling ? -0d : -1d, recovered[indicator.Value].ToArray()[^1]);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancelled.Token));
        }
    }

    private static Task<IIndicatorRun> Build(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history).BuildAsync();
}
