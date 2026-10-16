using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedBasicCandleTests : IDisposable
{
    private readonly int _priorDop = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
    public SharedBasicCandleTests() => AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
    public void Dispose() => AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;

    [Fact]
    public async Task CandleKernelsPreserveExactBodyThresholdsEqualityAndParallelBoundaries()
    {
        double[] endpoints = [-double.MaxValue, -1, -double.Epsilon, -0d, 0d, double.Epsilon, 1, Math.BitIncrement(1), Math.BitDecrement(1), double.MaxValue];
        var cases = (from open in endpoints from close in endpoints select new Bar(default, open, 10, 0, close, 1))
            .Concat(endpoints.Select(v => new Bar(default, v, double.MaxValue, -double.MaxValue, -v, 1)))
            .Concat(endpoints.Select(v => new Bar(default, 0, double.Epsilon, 0, v, 1))).ToArray();
        IIndicator[] indicators = [new DojiCandle(0), new DojiCandle(.1m), new DojiCandle(.1000000000000000000000000001m),
            new DojiCandle(1), new BullishCandle(), new BearishCandle()];
        foreach (var indicator in indicators)
        foreach (int count in new[] { 0, 1, cases.Length, 8191, 8192, 8193 })
        {
            var bars = Enumerable.Range(0, count).Select(i => cases[i % cases.Length]).ToArray();
            Assert.True(ValuesBarExecution.IsPointwise(indicator));
            var oracle = bars.Select(bar => indicator switch
            {
                BullishCandle => bar.Close > bar.Open ? 1d : 0d,
                BearishCandle => bar.Close < bar.Open ? 1d : 0d,
                _ => double.NaN
            }).ToArray();
            await Compare(bars, indicator, indicator is DojiCandle ? null : oracle);
        }
    }
    [Fact]
    public async Task CandleKernelsPreserveValidationOwnershipCancellationAndSourceFallback()
    {
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, 1, 10, 0, (i % 5) / 2d, 1)).ToArray();
        foreach (var indicator in new IIndicator[] { new DojiCandle(), new BullishCandle(), new BearishCandle() })
        {
            using var saved = await Builder(bars, indicator, IndicatorHistoryMode.Full).BuildAsync();
            var original = bars.ToArray(); Array.Clear(bars);
            int index = 0; await foreach (var snapshot in saved) Assert.Equal(original[index++], snapshot.Bar);
            original.CopyTo(bars, 0);
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Builder(bars, indicator, IndicatorHistoryMode.Full).BuildAsync(cancellation.Token));
            foreach (int field in Enumerable.Range(0, 5))
            {
                var invalid = bars.ToArray(); invalid[^1] = new Bar(default, field == 0 ? double.NaN : 0, field == 1 ? double.NaN : 1,
                    field == 2 ? double.NaN : 0, field == 3 ? double.NaN : 1, field == 4 ? double.NaN : 1);
                await Compare(invalid, indicator);
            }
            ((IndicatorBase)indicator).Of(new Sma(3));
            Assert.False(ValuesBarExecution.IsPointwise(indicator)); await Compare(bars, indicator);
        }
    }
    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
    private static async Task Compare(Bar[] bars, IIndicator indicator, double[]? oracle = null)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            IIndicatorRun? expected = null, actual = null;
            var expectedError = await Record.ExceptionAsync(async () => expected = await Builder(bars, indicator, history).ConfigureBehavior(_ => { }).BuildAsync());
            var actualError = await Record.ExceptionAsync(async () => actual = await Builder(bars, indicator, history).BuildAsync());
            using (expected) using (actual)
            {
                if (expectedError is not null)
                {
                    Assert.NotNull(actualError); Assert.Equal(expectedError.GetType(), actualError.GetType()); Assert.Equal(expectedError.Message, actualError.Message);
                }
                else
                {
                    Assert.Null(actualError); Assert.Equal(expected![indicator].ToArray(), actual![indicator].ToArray());
                    if (oracle is not null) Assert.Equal(oracle, actual[indicator].ToArray());
                    if (indicator is DojiCandle { Source: null } doji)
                        foreach (var rule in doji.ValidationRules)
                            rule.Check(new IndicatorValidationContext("fused-doji", bars, [actual[indicator].ToArray()], 0));
                }
            }
        }
    }
}
