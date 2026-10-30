using OoplesFinance.StockIndicators.CompetitorBenchmarks;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class RsmkBatchTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, 2, 14, 79, 80, int.MaxValue })
        foreach (var shape in new[] { "empty", "singleton", "wave", "flat", "extreme", "subnormal", "selected", "rounded", "hidden" })
        foreach (var average in new[] { MovingAvgType.ExponentialMovingAverage, MovingAvgType.SimpleMovingAverage })
            yield return new object[] { length, shape, average };
    }

    private static StockData Data(string shape, bool market = false)
    {
        var count = shape == "empty" ? 0 : shape == "singleton" ? 1 : 80;
        var values = Enumerable.Range(0, count).Select(i => shape switch
        {
            "flat" => 2d,
            "extreme" => i % (market ? 3 : 2) == 0 ? double.MaxValue : double.Epsilon,
            "subnormal" => double.Epsilon * (1 + i % (market ? 7 : 5)),
            _ => 100 + Math.Sin(i * (market ? .19 : .37)) * 12
        }).ToArray();
        var data = new StockData(values, values, values, values, values.Select(_ => 1d),
            values.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
        if (shape == "selected") data.CustomValuesList = values.Select(v => v * 2).ToList();
        if (shape == "rounded") data.Options.RoundingDigits = 3;
        if (shape == "hidden") { data.Options.IncludeSignals = false; data.Options.IncludeOutputValues = false; data.Options.IncludeCustomValues = false; }
        return data;
    }

    private static void Equal(StockData expected, StockData actual)
    {
        Assert.Equal(expected.IndicatorName, actual.IndicatorName);
        Assert.Equal(expected.SignalsList, actual.SignalsList);
        Assert.Equal(expected.CustomValuesList.Select(BitConverter.DoubleToInt64Bits), actual.CustomValuesList.Select(BitConverter.DoubleToInt64Bits));
        Assert.Equal(expected.OutputValues.Keys.OrderBy(x => x), actual.OutputValues.Keys.OrderBy(x => x));
        foreach (var key in expected.OutputValues.Keys)
            Assert.Equal(expected.OutputValues[key].Select(BitConverter.DoubleToInt64Bits), actual.OutputValues[key].Select(BitConverter.DoubleToInt64Bits));
    }

    [Theory, MemberData(nameof(Cases))]
    public void ExactLegacyBitsSignalsAndOwnership(int length, string shape, MovingAvgType average)
    {
        foreach (var smooth in new[] { 0, 1, 3, int.MaxValue })
        {
            var actual = Data(shape); var expected = Data(shape); var market = Data(shape, true);
            var close = actual.ClosePrices.ToArray(); var selected = market.CustomValuesList.ToArray();
            PreservedRsmkBatch.Run(expected, Data(shape, true), average, length, smooth);
            actual.CalculateRSMKIndicator(market, average, length, smooth);
            Equal(expected, actual);
            Assert.Equal(close, actual.ClosePrices); Assert.Equal(selected, market.CustomValuesList);
        }
    }

    [Theory]
    [InlineData("count")]
    [InlineData("unaligned")]
    [InlineData("duplicate")]
    [InlineData("kind")]
    [InlineData("zero")]
    [InlineData("negative")]
    [InlineData("nan")]
    [InlineData("infinity")]
    public void InvalidLaterInputPreservesLegacyErrorAndCallerState(string fault)
    {
        (StockData Primary, StockData Market) Pair()
        {
            var p = Data("wave"); var m = Data(fault == "count" ? "singleton" : "wave", true);
            if (fault == "unaligned") m.Dates[79] = m.Dates[79].AddSeconds(1);
            if (fault == "duplicate") p.Dates[79] = m.Dates[79] = p.Dates[78];
            if (fault == "kind") m.Dates[79] = DateTime.SpecifyKind(m.Dates[79], DateTimeKind.Unspecified);
            if (fault is "zero" or "negative" or "nan" or "infinity")
                p.ClosePrices[79] = fault switch { "zero" => 0, "negative" => -1, "nan" => double.NaN, _ => double.PositiveInfinity };
            return (p, m);
        }
        var a = Pair(); var e = Pair();
        var published = a.Primary.CustomValuesList; var outputs = a.Primary.OutputValues;
        var oldError = Record.Exception(() => PreservedRsmkBatch.Run(e.Primary, e.Market));
        var error = Record.Exception(() => a.Primary.CalculateRSMKIndicator(a.Market));
        Assert.NotNull(oldError); Assert.NotNull(error);
        Assert.Equal(oldError.GetType(), error.GetType());
        Assert.Equal(((ArgumentException)oldError).ParamName, ((ArgumentException)error).ParamName);
        Assert.Same(published, a.Primary.CustomValuesList); Assert.Same(outputs, a.Primary.OutputValues);
        Equal(e.Primary, a.Primary);
    }

    [Fact]
    public void IdenticalLegsAndBeyondBatchLagHaveZeroStress()
    {
        var same = Data("wave"); same.CalculateRSMKIndicator(Data("wave"), length: 2);
        Assert.Equal(Enumerable.Repeat(0d, 80), same.CustomValuesList);
        var beyond = Data("wave"); beyond.CalculateRSMKIndicator(Data("wave", true), length: int.MaxValue);
        Assert.Equal(Enumerable.Repeat(0d, 80), beyond.CustomValuesList);
    }
}
