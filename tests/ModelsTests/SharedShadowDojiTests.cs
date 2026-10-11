using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedShadowDojiTests : IDisposable
{
    private readonly int _priorDop = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
    public SharedShadowDojiTests() => AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
    public void Dispose() => AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;

    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task FusedShadowGraphsPreserveExactDecimalThresholdsAndParallelBoundaries(bool upper)
    {
        decimal[] fractions = [0, .1m, .1000000000000000000000000001m, .3333333333333333333333333333m, 1];
        double[] prices = [-double.MaxValue, -1, -double.Epsilon, -0d, 0d, double.Epsilon, 1, 9, Math.BitIncrement(9), Math.BitDecrement(9), double.MaxValue];
        var cases = new List<Bar>();
        foreach (double price in prices)
        {
            cases.Add(new Bar(default, price, 10, 0, price, 1));
            cases.Add(new Bar(default, price, double.MaxValue, -double.MaxValue, -price, 1));
            cases.Add(new Bar(default, 0, double.Epsilon, 0, price, 1));
            cases.Add(new Bar(default, price, -1, 1, price, 1));
        }
        cases.Add(new Bar(default, 9, 10, 0, 10, 1));
        foreach (decimal body in fractions) foreach (decimal shadow in fractions)
        foreach (int count in new[] { 0, 1, cases.Count })
        {
            var indicator = Create(upper, body, shadow);
            var bars = cases.Take(count).ToArray();
            Assert.True(ValuesBarExecution.SupportsOwned([indicator]));
            await Compare(bars, indicator, ShadowDojiReference.Evaluate(bars, body, shadow, upper).ToArray());
        }
        foreach (int count in new[] { 8191, 8192, 8193, 10001 })
        {
            var bars = Enumerable.Range(0, count).Select(i => cases[i % cases.Count]).ToArray();
            await Compare(bars, Create(upper, .1m, .1m), ShadowDojiReference.Evaluate(bars, .1m, .1m, upper).ToArray());
        }
    }

    [Fact]
    public async Task ChangedComponentsRetainGraphEvaluationAndInvalidInputsRetainPrecedence()
    {
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 9, 10, 0, 9 + (i % 5) / 8d, 1)).ToArray();
        foreach (bool upper in new[] { true, false })
        {
            var changed = Create(upper, .1m, .1m);
            ((IndicatorBase)changed.Components[0]).Of(new Sma(3));
            Assert.False(ValuesBarExecution.SupportsOwned([changed])); await Compare(bars, changed);
            changed = Create(upper, .1m, .1m);
            ((IIndicator[])changed.Components)[0] = new DojiCandle(.5m);
            Assert.False(ValuesBarExecution.SupportsOwned([changed])); await Compare(bars, changed);
            changed = Create(upper, .1m, .1m);
            ((IndicatorBase)changed).Of(new Sma(3));
            Assert.False(ValuesBarExecution.SupportsOwned([changed])); await Compare(bars, changed);
            var indicator = Create(upper, .1m, .1m);
            using var saved = await Builder(bars, indicator, IndicatorHistoryMode.Full).BuildAsync();
            var original = bars.ToArray(); Array.Clear(bars);
            int index = 0; await foreach (var snapshot in saved) Assert.Equal(original[index++], snapshot.Bar);
            original.CopyTo(bars, 0);
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Builder(bars, indicator, IndicatorHistoryMode.Full).BuildAsync(cancellation.Token));
            await Assert.ThrowsAsync<NotSupportedException>(() => Builder(bars, indicator, IndicatorHistoryMode.Full).ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            foreach (int field in Enumerable.Range(0, 5))
            {
                var invalid = bars.ToArray();
                invalid[^1] = new Bar(default, field == 0 ? double.NaN : 0, field == 1 ? double.NaN : 1,
                    field == 2 ? double.NaN : 0, field == 3 ? double.NaN : 1, field == 4 ? double.NaN : 1);
                await Compare(invalid, indicator);
            }
        }
    }
    private static IIndicator Create(bool upper, decimal body, decimal shadow) => upper ? new DragonflyDojiCandle(body, shadow) : new GravestoneDojiCandle(body, shadow);
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
                    if (bars.Length > 0) Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                }
            }
        }
    }
}
