using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ValueChartSpanPublicationTests
{
    [Fact]
    public void ShortDestinationLeavesStateUntouchedAndPreviewResetMatchIndependentCoordinates()
    {
        var bars = Enumerable.Range(0, 9).Select(i => new Bar(default, i % 3, i % 5 + 4, i % 3 - 4, i % 7 - 2, i + 1)).ToArray();
        var expected = BuiltInFormulaReferences.ValueChartValues(bars, 3, MovingAvgType.SimpleMovingAverage).Outputs;
        var keys = new[] { "vClose", "vOpen", "vHigh", "vLow" };
        using var window = new ValueChartWindow(MovingAvgType.SimpleMovingAverage, 3);
        for (var replay = 0; replay < 2; replay++)
        {
            window.Reset();
            Assert.Throws<ArgumentException>(() => window.Next(100, 200, -100, 50, new double[3], false, true));
            for (var i = 0; i < bars.Length; i++)
            foreach (var final in new[] { false, false, true })
            {
                var bar = bars[i]; var output = new[] { 0d, 0, 0, 0, 91, 92 };
                window.Next(bar.Open, bar.High, bar.Low, bar.Close, output, false, final);
                for (var slot = 0; slot < 4; slot++) Assert.Equal(BitConverter.DoubleToInt64Bits(expected[keys[slot]][i]), BitConverter.DoubleToInt64Bits(output[slot]));
                Assert.Equal(91, output[4]); Assert.Equal(92, output[5]);
            }
        }
    }
}
