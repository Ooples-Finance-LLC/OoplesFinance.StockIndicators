using OoplesFinance.StockIndicators.CompetitorBenchmarks;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class KaufmanStressBatchTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, 2, 14, 32, 33, int.MaxValue })
        foreach (var shape in new[] { "empty", "singleton", "wave", "flat", "negative", "extreme", "subnormal", "inverted", "selected", "short-high", "long-range", "unequal" })
            yield return new object[] { length, shape };
    }

    internal static StockData Data(int count, bool market = false)
    {
        var close = Enumerable.Range(0, count).Select(i => 100 + Math.Sin(i * (market ? .19 : .37)) * 12 + i * .01).ToArray();
        return new StockData(close, close.Select(v => v + 2), close.Select(v => v - 3), close,
            close.Select(_ => 1d), close.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
    }

    private static StockData Shaped(string shape, bool market)
    {
        var count = shape == "empty" ? 0 : shape == "singleton" ? 1 : shape == "unequal" && market ? 79 : 80;
        var data = Data(count, market);
        if (shape is "flat" or "negative" or "extreme" or "subnormal")
        {
            var values = Enumerable.Range(0, count).Select(i => shape switch
            {
                "flat" => 2d,
                "negative" => -10d - i % (market ? 7 : 5),
                "extreme" => i % 2 == 0 ? double.MaxValue : -double.MaxValue,
                _ => double.Epsilon * (1 + i % (market ? 7 : 5))
            }).ToArray();
            data = new StockData(values, values, values, values, values.Select(_ => 1d),
                values.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
        }
        if (shape == "inverted") (data.HighPrices, data.LowPrices) = (data.LowPrices, data.HighPrices);
        if (shape == "selected") data.CustomValuesList = Enumerable.Range(0, count).Select(i => 1000d + i).ToList();
        if (shape == "short-high") data.HighPrices.RemoveAt(count - 1);
        if (shape == "long-range") { data.HighPrices.Add(10000); data.LowPrices.Add(-10000); }
        return data;
    }

    [Theory, MemberData(nameof(Cases))]
    public void ExactLegacyValuesSignalsErrorsAndOwnership(int length, string shape)
    {
        var actual = Shaped(shape, false); var expected = Shaped(shape, false);
        var market = Shaped(shape, true); var oldMarket = Shaped(shape, true);
        var close = actual.ClosePrices.ToArray(); var high = actual.HighPrices.ToArray(); var low = actual.LowPrices.ToArray();
        var marketClose = market.ClosePrices.ToArray(); var marketCustom = market.CustomValuesList.ToArray();
        var oldError = Record.Exception(() => PreservedKaufmanStressBatch.Run(expected, oldMarket, length));
        var error = Record.Exception(() => actual.CalculateKaufmanStressIndicator(market, length));
        Assert.Equal(oldError?.GetType(), error?.GetType());
        if (error is null)
        {
            Assert.Equal(expected.IndicatorName, actual.IndicatorName);
            Assert.Equal(expected.SignalsList, actual.SignalsList);
            Assert.Equal(expected.CustomValuesList.Select(BitConverter.DoubleToInt64Bits), actual.CustomValuesList.Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(expected.OutputValues.Keys.OrderBy(x => x), actual.OutputValues.Keys.OrderBy(x => x));
            foreach (var key in expected.OutputValues.Keys)
                Assert.Equal(expected.OutputValues[key].Select(BitConverter.DoubleToInt64Bits), actual.OutputValues[key].Select(BitConverter.DoubleToInt64Bits));
        }
        Assert.Equal(close, actual.ClosePrices); Assert.Equal(high, actual.HighPrices); Assert.Equal(low, actual.LowPrices);
        Assert.Equal(marketClose, market.ClosePrices); Assert.Equal(marketCustom, market.CustomValuesList);
    }

    [Fact]
    public void IdenticalLegsHaveNeutralStress()
    {
        var data = Data(80); data.CalculateKaufmanStressIndicator(Data(80), 14);
        Assert.Equal(Enumerable.Repeat(50d, 80), data.CustomValuesList);
    }
}
