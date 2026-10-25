using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class TillsonWordBinadeTests
{
    [Fact]
    public void CachedBinadeRetainsOriginalBitsAcrossSeedingRangeChangesAndReset()
    {
        double[] prices = new[] { 100d, 200d, 60d, 150d }
            .SelectMany(value => Enumerable.Repeat(value, 160))
            .Concat(Enumerable.Range(0, 160).Select(i => i % 2 == 0 ? 128d : Math.BitDecrement(128d))).ToArray();
        foreach (TillsonSeed seed in Enum.GetValues<TillsonSeed>())
        foreach (int period in new[] { 1, 3, 20, 2047 })
        foreach (int suppression in new[] { 0, 7 })
            Check(prices, period, seed, suppression);
    }

    [Fact]
    public void UnsupportedPeriodsAndExtremeInputsRetainOriginalFallbackBits()
    {
        double[] prices = Enumerable.Repeat(100d, 160).Concat(new[] {
            0d, -0d, -100d, double.Epsilon, -double.Epsilon,
            Math.ScaleB(1, -1022), double.MaxValue, -double.MaxValue })
            .Concat(Enumerable.Repeat(100d, 160)).ToArray();
        foreach (TillsonSeed seed in Enum.GetValues<TillsonSeed>())
        foreach (int period in new[] { 1, 20, 2047, 2048, int.MaxValue })
            Check(prices, period, seed, 0);
    }

    private static void Check(double[] prices, int period, TillsonSeed seed, int suppression)
    {
        var original = (IMultiOutputState)new TillsonAverage(period, .7, seed, suppression).CreateState();
        var candidate = new ValuesBarExecution.TillsonValueState(period, .7, seed, suppression);
        Span<double> expected = stackalloc double[2]; Span<double> actual = stackalloc double[2];
        for (int replay = 0; replay < 2; replay++)
        {
            original.Reset(); candidate.Reset();
            for (int i = 0; i < prices.Length; i++)
            {
                double price = prices[i];
                var bar = new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
                original.Update(bar, expected); candidate.Update(bar, actual);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[0]), BitConverter.DoubleToInt64Bits(actual[0]));
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[1]), BitConverter.DoubleToInt64Bits(actual[1]));
            }
        }
    }
}
