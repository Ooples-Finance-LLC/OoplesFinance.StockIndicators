using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class WindowAroonExactPercentageTests
{
    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(7)] [InlineData(20)] [InlineData(101)] [InlineData(int.MaxValue)]
    public void AgePercentagesRoundOnceAcrossIntegerBounds(int period)
    {
        var ages = new HashSet<long> { 0, 1, -1, period, -period, period - 1L, 1L - period };
        var random = new Random(701);
        for (int i = 0; i < 1000; i++) ages.Add(random.NextInt64(-period, (long)period + 1));
        foreach (long age in ages)
        {
            double expected = (new ReferenceFraction(100L * age) / new ReferenceFraction(period)).ToDouble();
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(WindowAroon.Percentage(age, period)));
        }
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task AllSixOutputsKeepWindowTieStartupAndOwnedHistoryContracts(bool extra, bool oldest)
    {
        double[] values = [-0d, 0d, -1, -1, 3, double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon];
        foreach (int period in new[] { 1, 3, 20, int.MaxValue })
        {
            var bars = Enumerable.Range(0, 257).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0,
                values[i % values.Length], values[(i + 3) % values.Length], 0, 1)).ToArray();
            var indicator = new WindowAroon(period, extra, oldest);
            var expected = Reference(bars, period, extra, oldest);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
                using var run = await builder.BuildAsync();
                for (int slot = 0; slot < 6; slot++)
                    Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits), run[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(bars[^1], run.Latest.Bar);
                if (history == IndicatorHistoryMode.Full)
                {
                    int i = 0;
                    await foreach (var snapshot in run) Assert.Equal(bars[i++], snapshot.Bar);
                    Assert.Equal(bars.Length, i);
                }
            }
        }
    }

    [Fact]
    public async Task SharedPresenceKeepsDifferentIndicatorsAndCompletedRunsIndependent()
    {
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 0, i % 7, -(i % 5), 0, 1)).ToArray();
        var first = new WindowAroon(3);
        var second = new WindowAroon(20);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(first, second).ConfigureHistory(history);
            using var original = await builder.BuildAsync();
            var firstPresence = original[first.UpIsDefined].ToArray();
            var secondPresence = original[second.DownIsDefined].ToArray();
            Assert.Equal(1, firstPresence[3]); Assert.Equal(0, secondPresence[3]);
            // A writable caller copy cannot mutate any of the shared read-only outputs.
            var copy = original[first.UpIsDefined].ToArray(); Array.Fill(copy, -1);
            using var later = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars.Take(2).ToArray()))
                .ConfigureIndicators(first, second).ConfigureHistory(history).BuildAsync();
            original.Dispose();
            foreach (var output in new[] { first.UpIsDefined, first.DownIsDefined, first.OscillatorIsDefined })
                Assert.Equal(firstPresence, original[output].ToArray());
            foreach (var output in new[] { second.UpIsDefined, second.DownIsDefined, second.OscillatorIsDefined })
                Assert.Equal(secondPresence, original[output].ToArray());
            Assert.All(later[first.OscillatorIsDefined].ToArray(), value => Assert.Equal(0, value));
        }
    }

    private static double[][] Reference(Bar[] bars, int period, bool extra, bool oldest)
    {
        var result = Enumerable.Range(0, 6).Select(_ => new double[bars.Length]).ToArray();
        long size = (long)period + (extra ? 1 : 0);
        for (int i = 0; i < bars.Length; i++)
        {
            if (i + 1 < size) continue;
            int high = i, low = i;
            for (int j = i - 1; j >= i - size + 1; j--)
            {
                if (oldest ? bars[j].High >= bars[high].High : bars[j].High > bars[high].High) high = j;
                if (oldest ? bars[j].Low <= bars[low].Low : bars[j].Low < bars[low].Low) low = j;
            }
            long[] ages = [period - (i - high), period - (i - low), high - low];
            for (int slot = 0; slot < 3; slot++)
            {
                result[slot][i] = (new ReferenceFraction(100 * ages[slot]) / new ReferenceFraction(period)).ToDouble();
                result[slot + 3][i] = 1;
            }
        }
        return result;
    }
}
