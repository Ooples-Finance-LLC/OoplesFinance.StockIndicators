using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

internal static class CandleZeroBlockTestData
{
    internal static IndicatorBase Create(int kind, int longPeriod = 10, int shortPeriod = 10) => kind switch
    {
        0 => new MatHoldCandle(longPeriod, shortPeriod),
        1 => new BreakawayCandle(longPeriod),
        2 => new ConcealingBabySwallowCandle(longPeriod),
        _ => new LadderBottomCandle(longPeriod)
    };

    private static Bar[] Pattern(int kind) => kind switch
    {
        0 => [new(default, 10, 21, 9, 20, 1), new(default, 21.75, 24, 20, 21.5, 1), new(default, 19.7, 20, 19.4, 19.5, 1), new(default, 19.1, 19.2, 18.9, 19, 1), new(default, 19.2, 26, 19, 25, 1)],
        1 => [new(default, 20, 21, 9, 10, 1), new(default, 9, 10, 7, 8, 1), new(default, 8, 9, 6, 7.5, 1), new(default, 7, 8, 5, 6.5, 1), new(default, 8, 10, 7, 9.5, 1)],
        2 => [new(default, 10, 20, 0, 9, 1), new(default, 10, 10.1, 8.9, 9, 1), new(default, 8, 8.1, 6.9, 7, 1), new(default, 6, 9, 4, 5, 1), new(default, 8, 10, 3, 4.5, 1)],
        _ => [new(default, 20, 20.1, 18.9, 19, 1), new(default, 18, 18.1, 16.9, 17, 1), new(default, 16, 16.1, 14.9, 15, 1), new(default, 14, 16, 12.9, 13, 1), new(default, 15, 18, 14.9, 17, 1)]
    };

    internal static Bar[] Data(int count, int kind, bool dense = false)
    {
        var background = kind == 2 ? new Bar(default, 10, 20, 0, 9, 1) : new Bar(default, 10, 10.2, 9.8, 9.9, 1);
        var pattern = Pattern(kind);
        var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
            background.Open, background.High, background.Low, background.Close, i + 1)).ToArray();
        var targets = dense ? Enumerable.Range(0, count / 32).Select(i => i * 32 + 31)
            : new[] { 31, 64, 129, count / 8 + 1, count / 4 + 1, count / 2 + 1, count * 3 / 4 + 1, count - 1 };
        foreach (int end in targets)
            if (end >= 4 && end < count)
                for (int j = 0; j < pattern.Length; j++)
                {
                    int row = end - 4 + j;
                    var value = pattern[j];
                    bars[row] = new(bars[row].Time, value.Open, value.High, value.Low, value.Close, row + 1);
                }
        return bars;
    }

    internal static async Task Check(Bar[] bars, IndicatorBase owner, int workers)
    {
        var original = (IIndicatorState)owner.CreateState();
        var expected = bars.Select(b => original.Update(b)).ToArray();
        foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
            rule.Check(new IndicatorValidationContext("Certified candle block independent reference", bars, [expected], 0));
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = workers;
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var input = bars.ToArray();
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
                Array.Clear(input);
                run.Dispose();
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), run[owner].ToArray().Select(BitConverter.DoubleToInt64Bits));
                if (bars.Length != 0) Assert.Equal(bars[^1], run.Latest.Bar);
                if (mode == IndicatorHistoryMode.Full)
                {
                    int row = 0;
                    await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                    Assert.Equal(bars.Length, row);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}
