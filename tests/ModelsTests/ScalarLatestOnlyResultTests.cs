using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ScalarLatestOnlyResultTests
{
    public static IEnumerable<object[]> Cases => from operation in Enumerable.Range(0, 3)
        from count in new[] { 0, 1, 2, 9 }
        from duplicate in new[] { false, true }
        select new object[] { operation, count, duplicate };

    [Theory, MemberData(nameof(Cases))]
    public async Task ScalarResultsPreserveLookupWarmupAndOwnership(int operation, int count, bool duplicate)
    {
        IIndicator indicator = operation switch
        {
            0 => new Sma(3),
            1 => new RollingPriceSum(3),
            _ => new WindowLinearRegression(3)
        };
        var bars = Enumerable.Range(0, count)
            .Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 2, -2, (i % 5 - 2) / 8d, 1)).ToArray();
        IIndicator[] roots = duplicate ? [indicator, indicator] : [indicator];
        StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(roots);
        using var expected = await Builder().BuildAsync();
        using var actual = await Builder().ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
        Assert.Equal(count, actual.BarCount);
        Assert.True(actual.IsComplete);
        var expectedBits = expected[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits).ToArray();
        Assert.Equal(expectedBits, actual[indicator.Outputs[0]].ToArray().Select(BitConverter.DoubleToInt64Bits));
        Assert.Throws<ArgumentNullException>(() => actual[(IIndicator)null!].ToArray());
        Assert.Throws<ArgumentNullException>(() => actual[(IIndicatorOutput)null!].ToArray());
        Assert.Throws<KeyNotFoundException>(() => actual[new Sma(3)].ToArray());
        Assert.Throws<InvalidOperationException>(() => actual.GetAsyncEnumerator());
        Assert.ThrowsAny<OperationCanceledException>(() => actual.GetAsyncEnumerator(new CancellationToken(true)));
        if (count == 0)
        {
            Assert.Throws<InvalidOperationException>(() => actual.Latest);
            return;
        }
        var snapshot = actual.Latest;
        var lastBar = bars[^1];
        Assert.Equal(expected.Latest.IsWarmedUp, snapshot.IsWarmedUp);
        Assert.Equal(count - 1, snapshot.Index);
        Assert.Equal(expectedBits[^1], BitConverter.DoubleToInt64Bits(snapshot[indicator]));
        Assert.Throws<ArgumentNullException>(() => snapshot[(IIndicator)null!]);
        Assert.Throws<ArgumentNullException>(() => snapshot[(IIndicatorOutput)null!]);
        Assert.Throws<KeyNotFoundException>(() => snapshot[new Sma(3)]);
        Array.Clear(bars);
        actual.Dispose();
        Assert.Equal(lastBar, snapshot.Bar);
        Assert.Equal(expectedBits, actual[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
        Assert.Equal(expectedBits[^1], BitConverter.DoubleToInt64Bits(snapshot[indicator.Outputs[0]]));
    }
}
