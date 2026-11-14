using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class PriorDirectionalTypedExecutionTests
{
    [Fact]
    public async Task EveryMeasurePreservesOriginalStartupSuppressionAndOwnedColumns()
    {
        foreach (var measure in Enum.GetValues<PriorDirectionalMeasure>())
        foreach (int count in new[] { 1, 17, 1003, 10003 })
        foreach (int period in new[] { measure >= PriorDirectionalMeasure.Index ? 2 : 1, 20, int.MaxValue })
        {
            var bars = Rich(count);
            var owner = new PriorSeededDirectionalMeasure(measure, period, 7);
            await Check(bars, owner, Reference(bars, owner));
        }
    }

    [Fact]
    public async Task WideInvertedAndConcurrentRunsMatchOriginalAndMixedFallback()
    {
        double[] levels = [-0d, double.Epsilon, -double.Epsilon, Math.ScaleB(1d, -1022),
            Math.ScaleB(1d, 900), Math.ScaleB(-1d, 900)];
        var bars = Enumerable.Range(0, 10003).Select(i =>
        {
            double close = levels[i % levels.Length];
            bool inverted = i % 29 == 0;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), close,
                close + (inverted ? -2 : 2), close + (inverted ? 2 : -2), close, i + 1);
        }).ToArray();
        foreach (var measure in Enum.GetValues<PriorDirectionalMeasure>())
        {
            var owner = new PriorSeededDirectionalMeasure(measure, 20, 5000);
            var expected = Reference(bars, owner);
            await Check(bars, owner, expected);
            await Check(bars, owner, expected, mixed: true);
        }
        var sharedOwner = new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.Average, 20);
        var rich = Rich(10003);
        var sharedExpected = Reference(rich, sharedOwner);
        await Task.WhenAll(Enumerable.Range(0, 3)
            .Select(_ => Task.Run(() => Check(rich, sharedOwner, sharedExpected))));
    }

    [Fact]
    public async Task LateRawErrorPrecedesOutputOverflowAndSameBuilderRecovers()
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Enumerable.Range(0, 10003).Select(i =>
            {
                double price = i % 2 == 0 ? -double.MaxValue : double.MaxValue;
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, price, price, 0, 1);
            }).ToArray();
            var owner = new PriorSeededDirectionalMeasure(PriorDirectionalMeasure.PositiveMovement, 1);
            int firstFailure = Array.FindIndex(Reference(bars, owner)[0], value => !double.IsFinite(value));
            Assert.True(firstFailure >= 0);
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner)
                .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            var last = bars[^1];
            bars[^1] = new(last.Time, last.Open, last.High, last.Low, last.Close, double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            bars[^1] = last;
            var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Equal(0, error.OutputSlot);
            Assert.Equal(firstFailure, error.BarIndex);
            for (int i = 0; i < bars.Length; i++) bars[i] = new(bars[i].Time, 1, 1, 1, 1, 1);
            var expected = Reference(bars, owner);
            using var recovered = await builder.BuildAsync();
            AssertColumns(recovered, owner, expected);
        }
    }

    private static Bar[] Rich(int count) => Enumerable.Range(0, count).Select(i =>
    {
        double close = 100 + (i * 13 % 101) / 8d;
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), close - 1, close + 2, close - 2, close, i + 1);
    }).ToArray();

    private static double[][] Reference(Bar[] bars, PriorSeededDirectionalMeasure owner)
    {
        var state = (IMultiOutputState)owner.CreateState();
        var result = new[] { new double[bars.Length], new double[bars.Length] };
        var scratch = new double[2];
        for (int i = 0; i < bars.Length; i++)
        {
            state.Update(in bars[i], scratch);
            for (int slot = 0; slot < 2; slot++) result[slot][i] = scratch[slot];
        }
        return result;
    }

    private static async Task Check(Bar[] bars, PriorSeededDirectionalMeasure owner,
        double[][] expected, bool mixed = false)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            IIndicator[] indicators = mixed ? [owner, new Sma(3)] : [owner];
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicators).ConfigureHistory(history)
                .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            Array.Clear(input);
            run.Dispose();
            AssertColumns(run, owner, expected);
            Assert.Equal(bars[^1], run.Latest.Bar);
            if (history == IndicatorHistoryMode.Full && bars.Length == 10003
                && owner.Period == 20 && owner.Measure == PriorDirectionalMeasure.Rating)
            {
                int row = 0;
                await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }

    private static void AssertColumns(IIndicatorRun run, PriorSeededDirectionalMeasure owner, double[][] expected)
    {
        for (int slot = 0; slot < 2; slot++)
            Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),
                run[owner.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }
}
