using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class IdentityAdlColumnExecutionTests
{
    [Fact]
    public async Task PrefixesMatchIndependentFractionsAcrossExactAndWidePaths()
    {
        foreach (int sign in new[] { -1, 1 })
        {
            double[] volumes = [1, Math.ScaleB(1, 64), .1, Math.ScaleB(1, -70),
                -Math.ScaleB(1, 64), -1, -.1, -Math.ScaleB(1, -70), double.Epsilon, -double.Epsilon];
            var bars = volumes.Select((volume, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                0, 3, 1, i % 3 == 0 ? 1 : 3, sign * volume)).ToArray();
            await Check(bars);
        }
        await Check([
            new(default, 0, double.MaxValue, -double.MaxValue, double.MaxValue, 1),
            new(default, 0, 3 * double.Epsilon, double.Epsilon, 2 * double.Epsilon, 1),
            new(default, 0, 3, 0, 2, .1),
            new(default, 0, -1, -3, -1.5, -.1),
            new(default, 0, 2, 2, 3, double.MaxValue)
        ]);
    }

    [Fact]
    public async Task InvalidInputPrecedesDeferredOutputFailures()
    {
        foreach (var bars in new Bar[][]
        {
            [new(default, 0, 1, 0, 1, double.MaxValue), new(default, 0, 1, 0, 1, double.MaxValue)],
            [new(default, 0, 1, 0, 1, double.MaxValue), new(default, 0, 1, 0, 1, double.MaxValue), new(default, double.NaN, 1, 0, 1, 1)],
            [new(default, 0, double.Epsilon, 0, double.MaxValue, double.MaxValue), new(default, 0, 1, 0, 1, double.NaN)]
        })
        foreach (var history in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        {
            var indicator = new Adl(1);
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(history);
            var expected = await Record.ExceptionAsync(() => Builder().ConfigureBehavior(_ => { }).BuildAsync());
            var actual = await Record.ExceptionAsync(() => Builder().BuildAsync());
            Assert.NotNull(expected); Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(expected.Message, actual.Message);
        }
    }

    [Fact]
    public async Task CapturedHistoryEmptyAndCancellationKeepTheirContracts()
    {
        Bar[] bars = Enumerable.Range(0, 65).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
            i, 3, 1, i % 2 == 0 ? 3 : 1, i + 1)).ToArray();
        await Check(bars);
        var indicator = new Adl(1);
        StockIndicatorBuilder Builder(Bar[] input) => new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(indicator);
        using var empty = await Builder([]).BuildAsync();
        await foreach (var _ in empty) Assert.Fail("Empty input published a bar.");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Builder(bars).BuildAsync(new CancellationToken(true)));
    }

    private static async Task Check(Bar[] bars)
    {
        var sum = new ReferenceFraction(0);
        var expected = new double[bars.Length];
        for (int i = 0; i < bars.Length; i++)
        {
            var bar = bars[i];
            if (bar.High != bar.Low)
            {
                var high = ReferenceFraction.FromDouble(bar.High);
                var low = ReferenceFraction.FromDouble(bar.Low);
                double flow = ((ReferenceFraction.FromDouble(bar.Close) * new ReferenceFraction(2) - high - low)
                    * ReferenceFraction.FromDouble(bar.Volume) / (high - low)).ToDouble();
                sum += ReferenceFraction.FromDouble(flow);
            }
            expected[i] = sum.ToDouble();
        }
        foreach (var history in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        {
            var input = bars.ToArray();
            var indicator = new Adl(1);
            using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicator).ConfigureHistory(history).BuildAsync();
            Array.Clear(input);
            foreach (var output in indicator.Outputs)
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits),
                    actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(bars[^1], actual.Latest.Bar);
            if (history != IndicatorHistoryMode.Full) continue;
            int row = 0;
            await foreach (var snapshot in actual) Assert.Equal(bars[row++], snapshot.Bar);
            Assert.Equal(bars.Length, row);
        }
    }
}
