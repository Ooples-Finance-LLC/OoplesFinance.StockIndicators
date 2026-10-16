using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class RickshawSharedExecutionTests
{
    [Theory]
    [InlineData(1, 3)] [InlineData(10, 5)] [InlineData(int.MaxValue, int.MaxValue)]
    public async Task OwnedOutputsMatchIndependentContractAndSurviveDisposal(int doji, int near)
    {
        foreach (int count in new[] { 0, 1, 9, 10, 11, 65 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                i % 3 == 0 ? 5 : 1, 10, 0, i % 3 == 0 ? 5 : 2, i + 1)).ToArray();
            if (count > 35) bars[35] = new(bars[35].Time, double.Epsilon, double.MaxValue, -double.MaxValue, 0, 1);
            var indicator = new RickshawManCandle(doji, near);
            Assert.True(ValuesBarExecution.SupportsOwned([indicator]));
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var input = bars.ToArray();
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                    .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
                Array.Clear(input); run.Dispose();
                var values = run[indicator].ToArray();
                foreach (var rule in indicator.ValidationRules)
                    rule.Check(new IndicatorValidationContext("Rickshaw shared ownership", bars, [values], 0));
                if (count > 0) Assert.Equal(bars[^1], run.Latest.Bar);
                if (mode == IndicatorHistoryMode.Full)
                {
                    int row = 0; await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                    Assert.Equal(count, row);
                }
            }
        }
    }
    [Fact]
    public async Task CancellationAndRawValidationRecover()
    {
        var bars = Enumerable.Repeat(new Bar(default, 5, 10, 0, 5, 1), 33).ToArray();
        var indicator = new RickshawManCandle(10, 5);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(mode);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            bars[^1] = new(default, 5, 10, 0, 5, double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            bars[^1] = new(default, 5, 10, 0, 5, 1);
            using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
            Assert.All(run[indicator].ToArray().Skip(10), v => Assert.Equal(100d, v));
        }
    }
}
