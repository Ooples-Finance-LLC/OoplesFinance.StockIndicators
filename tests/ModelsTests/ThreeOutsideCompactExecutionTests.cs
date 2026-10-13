using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class ThreeOutsideCompactExecutionTests
{
    [Fact]
    public async Task AllExtremeAndSignedZeroComparisonCombinationsMatchOriginalState()
    {
        double[] values = [-double.MaxValue, -double.Epsilon, -0d, 0d, double.Epsilon, double.MaxValue];
        var list = new List<Bar>();
        void Add(double open, double close) => list.Add(new(DateTime.UnixEpoch.AddMinutes(list.Count),
            open, double.MaxValue, -double.MaxValue, close, 1));
        foreach (double middleOpen in values)
        foreach (double middleClose in values)
        foreach (double lastOpen in values)
        foreach (double lastClose in values)
        foreach (double currentClose in values)
        {
            Add(0, 0); Add(middleOpen, middleClose); Add(lastOpen, lastClose); Add(-currentClose, currentClose);
        }
        var bars = list.ToArray();
        var indicator = new ThreeOutsideCandle();
        var original = (IIndicatorState)indicator.CreateState();
        var expected = bars.Select(b => original.Update(b)).ToArray();
        Assert.Contains(100d, expected); Assert.Contains(-100d, expected); Assert.Contains(0d, expected);
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 8 })
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var input = bars.ToArray();
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                    .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
                Array.Clear(input); run.Dispose();
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), run[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(bars[^1], run.Latest.Bar);
                if (mode == IndicatorHistoryMode.Full)
                {
                    int row = 0;
                    await foreach (var snapshot in run)
                    {
                        Assert.Equal(BitConverter.DoubleToInt64Bits(bars[row].Open), BitConverter.DoubleToInt64Bits(snapshot.Bar.Open));
                        Assert.Equal(BitConverter.DoubleToInt64Bits(bars[row].Close), BitConverter.DoubleToInt64Bits(snapshot.Bar.Close));
                        row++;
                    }
                    Assert.Equal(bars.Length, row);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}
