using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class CertifiedBarCaptureOwnershipTests
{
    [Theory]
    [InlineData(1, false)] [InlineData(2, true)] [InlineData(3, true)]
    [InlineData(17, false)] [InlineData(513, true)] [InlineData(1025, false)]
    public async Task ConsumedCapturesCannotReplaceRetainedOutputsOrOriginalBars(int count, bool full)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, count).Select(i =>
            {
                double price = 10 + (i * 13 % 101) / 8d;
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), 123 + i, price + 1, price - 1, price, i % 13 + 1);
            }).ToArray();
            foreach (IIndicator indicator in new IIndicator[] { new WindowMoneyFlowIndex(2), new WindowUltimateOscillator(1, 2, 3) })
            {
                using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).ConfigureBehavior(_ => { }).BuildAsync();
                var input = bars.ToArray();
                using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                    .ConfigureIndicators(indicator).ConfigureHistory(full ? IndicatorHistoryMode.Full : IndicatorHistoryMode.LatestOnly).BuildAsync();
                Array.Clear(input);
                actual.Dispose();
                using var churn = await new StockIndicatorBuilder().ConfigureSource(Bars.From(
                    Enumerable.Repeat(new Bar(default, -10, 3, 1, 2, 5), count).ToArray()))
                    .ConfigureIndicators(indicator).BuildAsync();
                foreach (var output in indicator.Outputs)
                    Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                        actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(bars[^1], actual.Latest.Bar);
                if (full)
                {
                    int row = 0;
                    await foreach (var snapshot in actual) Assert.Equal(bars[row++], snapshot.Bar);
                    Assert.Equal(count, row);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}
