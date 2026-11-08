using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class PriorDirectionalSingleLoopTests
{
    [Fact]
    public async Task SingleDirectionalModesMatchOrdinaryExecutionAcrossStartupAndHistory()
    {
        var data = Enumerable.Range(0, 257)
            .Select(i => new Bar(default, 10 + i % 11, 12 + i % 11, 8 + i % 11, 10 + i % 11, 1)).ToArray();
        foreach (var measure in Enum.GetValues<PriorDirectionalMeasure>())
        foreach (int period in new[] { 1, 3, 20 })
        {
            if (period == 1 && measure >= PriorDirectionalMeasure.Index) continue;
            foreach (int count in new[] { 0, 1, 19, 64, 257 })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                await Compare(data.Take(count).ToArray(), history,
                    [new PriorSeededDirectionalMeasure(measure, period, 3)]);
        }
    }

    [Fact]
    public async Task OverflowAndInvalidTailRetainOrdinaryExceptionPrecedence()
    {
        double m = double.MaxValue;
        Bar[] overflow = [new(default, 0, -m, -m, 0, 1), new(default, 0, m, m, 0, 1),
            new(default, 0, -m, -m, 0, 1), new(default, 0, m, m, 0, 1)];
        var invalid = new Bar(default, double.NaN, 1, 0, .5, 1);
        foreach (var measure in new[] { PriorDirectionalMeasure.PositiveMovement, PriorDirectionalMeasure.NegativeMovement })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (var bars in new Bar[][] { overflow, [.. overflow, invalid], [invalid, .. overflow] })
            await Compare(bars, history, [new PriorSeededDirectionalMeasure(measure, 1)],
                allowError: true, mustFail: bars.Any(bar => !double.IsFinite(bar.Open)));
    }

    [Fact]
    public async Task MixedPlansAndOtherSingleStatesRetainOrdinaryResults()
    {
        var bars = Enumerable.Range(0, 87)
            .Select(i => new Bar(default, 10 + i % 7, 12 + i % 7, 8 + i % 7, 10 + i % 7, 1)).ToArray();
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            await Compare(bars, history, [new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Average, 3),
                new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.NegativeMovement, 5), new Sma(3)]);
            await Compare(bars, history, [new FirstValueEma(5)]);
            await Compare(bars, history, [new DelayedHilbertSine()]);
        }
    }

    private static async Task Compare(Bar[] bars, IndicatorHistoryMode history, IIndicator[] indicators,
        bool allowError = false, bool mustFail = false)
    {
        var input = bars.ToArray();
        StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
            .ConfigureIndicators(indicators).ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu);
        IIndicatorRun? expected = null, actual = null;
        try
        {
            var expectedError = await Record.ExceptionAsync(async () => { expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync(); });
            var actualError = await Record.ExceptionAsync(async () => { actual = await Builder().BuildAsync(); });
            if (!allowError) Assert.Null(expectedError);
            if (mustFail) Assert.NotNull(expectedError);
            Assert.Equal(expectedError?.GetType(), actualError?.GetType());
            Assert.Equal(expectedError?.Message, actualError?.Message);
            if (expectedError is not null) return;
            Assert.Equal(bars.Length, actual!.BarCount);
            Assert.True(actual.IsComplete);
            if (bars.Length > 0) Assert.Equal(bars[^1], actual.Latest.Bar);
            foreach (var indicator in indicators)
            foreach (var output in indicator.Outputs)
                Assert.Equal(expected![output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                    actual![output].ToArray().Select(BitConverter.DoubleToInt64Bits));
            if (history == IndicatorHistoryMode.Full)
            {
                Array.Clear(input);
                var replay = new List<Bar>();
                await foreach (var snapshot in actual) replay.Add(snapshot.Bar);
                Assert.Equal(bars, replay);
            }
        }
        finally { expected?.Dispose(); actual?.Dispose(); }
    }
}
