using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class FixedGridReturnBetaExecutionTests
{
    [Theory]
    [InlineData(1, CandlePriceField.Close, CandlePriceField.Open)]
    [InlineData(2, CandlePriceField.Close, CandlePriceField.Open)]
    [InlineData(20, CandlePriceField.Close, CandlePriceField.Open)]
    [InlineData(20, CandlePriceField.Open, CandlePriceField.Close)]
    [InlineData(20, CandlePriceField.High, CandlePriceField.Low)]
    [InlineData(512, CandlePriceField.Close, CandlePriceField.Open)]
    public async Task CertifiedGridsMatchIndependentHistoryForPeriodsAndFields(
        int period, CandlePriceField market, CandlePriceField evaluation)
    {
        double[] x = [1, 2, 1, 4, 2, 8, 4, 2];
        double[] y = [4, 2, 1, 2, 4, 8, 4, 1];
        var bars = Enumerable.Range(0, Math.Max(96, period + 37)).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), y[i % 8], x[(i + 2) % 8],
                y[(i + 3) % 8], x[i % 8], 1)).ToArray();
        await Check(bars, period, market, evaluation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExtendedReturnsAndUncertifiedGridsKeepOriginalFallback(bool finiteWideGrid)
    {
        double[] prices = finiteWideGrid
            ? [1, Math.BitIncrement(1), 1, Math.ScaleB(1, -100), 1, 2, 4, 1]
            : [1, double.Epsilon, double.MaxValue, -double.MaxValue, 0, -0d, 2, 4];
        var bars = Enumerable.Range(0, 64).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), prices[i % 8], 1, 0, prices[i % 8], 1)).ToArray();
        await Check(bars, 3, CandlePriceField.Close, CandlePriceField.Open);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(31)]
    public async Task RandomSignedPowerGridsMatchIndependentRollingMoments(int period)
    {
        var random = new Random(1739);
        double Price() => Math.ScaleB(random.Next(2) == 0 ? -1d : 1d, random.Next(-8, 9));
        var bars = Enumerable.Range(0, 257).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), Price(), 1, 0, Price(), 1)).ToArray();
        await Check(bars, period, CandlePriceField.Close, CandlePriceField.Open);
    }

    [Fact]
    public async Task EmptyAndUnderfilledWindowsPreserveStartupWithoutPeriodSizedStorage()
    {
        foreach (var (count, period) in new[] { (0, 20), (1, 1), (17, 4096), (17, int.MaxValue) })
        {
            var bars = Enumerable.Range(0, count).Select(i =>
                new Bar(default, i + 1, i + 2, 0, i + 1, 1)).ToArray();
            await Check(bars, period, CandlePriceField.Close, CandlePriceField.Open);
        }
    }

    [Fact]
    public async Task NarrowGridsPreserveSignedUnderflowThroughScaledRatioFallback()
    {
        double next = Math.BitIncrement(1);
        Bar[] bars = [new(default, 1, 1, 0, double.Epsilon, 1),
            new(default, next, 1, 0, Math.ScaleB(1, -52), 1),
            new(default, Math.BitIncrement(next), 1, 0, Math.ScaleB(1, 971), 1)];
        var reference = ReturnBetaReference.Calculate(bars, 2, CandlePriceField.Close,
            CandlePriceField.Open, ReturnBetaSelection.Standard, false)[0];
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(reference[2]!.Value));
        await Check(bars, 2, CandlePriceField.Close, CandlePriceField.Open);
    }

    [Fact]
    public async Task InvalidRawFieldsPrecedeDeferredOverflowAndCancellationStillThrows()
    {
        Bar[] overflow = [new(default, double.Epsilon, 1, 0, 1, 1),
            new(default, double.MaxValue, 1, 0, 2, 1), new(default, double.Epsilon, 1, 0, 1, 1)];
        double next = Math.BitIncrement(1);
        Bar[] fixedGridOverflow = [new(default, double.Epsilon, 1, 0, 1, 1),
            new(default, Math.ScaleB(1, -52), 1, 0, next, 1),
            new(default, Math.ScaleB(1, 971), 1, 0, Math.BitIncrement(next), 1)];
        foreach (var bars in new[] { overflow, fixedGridOverflow,
            overflow.Append(new Bar(default, 1, 1, 0, 1, double.NaN)).ToArray(),
            fixedGridOverflow.Append(new Bar(default, 1, 1, 0, 1, double.NaN)).ToArray() })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            async Task Consume(bool original)
            {
                var indicator = new WindowReturnBeta(2);
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).ConfigureHistory(history);
                if (original) builder.ConfigureBehavior(_ => { });
                using var result = await builder.BuildAsync();
                _ = result[indicator.Value].ToArray();
            }
            var expected = await Record.ExceptionAsync(() => Consume(true));
            var actual = await Record.ExceptionAsync(() => Consume(false));
            Assert.NotNull(expected); Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(expected.Message, actual.Message);
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(overflow)).ConfigureIndicators(new WindowReturnBeta(2))
            .BuildAsync(new CancellationToken(true)));
    }

    private static async Task Check(Bar[] bars, int period, CandlePriceField market, CandlePriceField evaluation)
    {
        var expected = ReturnBetaReference.Calculate(bars, period, market, evaluation,
            ReturnBetaSelection.Standard, false)[0];
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            var indicator = new WindowReturnBeta(period, market, evaluation);
            using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicator).ConfigureHistory(history).BuildAsync();
            Array.Clear(input); actual.Dispose();
            Assert.Equal(expected.Select(v => BitConverter.DoubleToInt64Bits(v ?? 0)),
                actual[indicator.Value].ToArray().Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(expected.Select(v => v.HasValue ? 1d : 0d), actual[indicator.IsDefined].ToArray());
            if (bars.Length != 0) Assert.Equal(bars[^1], actual.Latest.Bar);
            if (history != IndicatorHistoryMode.Full) continue;
            int row = 0;
            await foreach (var snapshot in actual) Assert.Equal(bars[row++], snapshot.Bar);
            Assert.Equal(bars.Length, row);
        }
    }
}
