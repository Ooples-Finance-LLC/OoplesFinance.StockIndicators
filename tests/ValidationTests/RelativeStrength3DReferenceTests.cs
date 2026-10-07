using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RelativeStrength3DReferenceTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery
        .DiscoverMultiSeries(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(RelativeStrength3DIndicatorState))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public void AllPeriodConfigurationsMatchPairedFormula(MultiSeriesIndicatorValidationCase testCase)
        => MultiSeriesIndicatorValidation.ValidateAndThrow(testCase);

    [Fact]
    public void DecayingSpikeKeepsDistinctAveragesAndDoesNotAwardATieVote()
    {
        var bars = Enumerable.Range(0, 256).Select(i =>
        {
            var price = i == 128 ? 200d : 100d;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1000);
        }).ToArray();
        var market = bars.Select(b => new Bar(b.Time, 50, 50, 50, 50, 1000)).ToArray();
        var ratio = bars.Select(b => b.Close / 50 * 100).ToArray();
        var fast = BuiltInFormulaReferences.Average(ratio, 5, 3);
        var medium = BuiltInFormulaReferences.Average(fast, 4, 3);
        var slow = BuiltInFormulaReferences.Average(fast, 8, 3);
        // Rounded exact EMA stages after the spike: medium < slow, so the score is zero.
        Assert.Equal(200.00000000000006, medium[236]);
        Assert.Equal(200.00000000016914, slow[236]);
        var testCase = Assert.Single(Cases.Select(row => (MultiSeriesIndicatorValidationCase)row[0]),
            candidate => candidate.Name == "shorter-periods");
        Assert.Equal(0, testCase.Reference!(bars, market)["Rs3d"][236]);
        StockData Data(Bar[] values) => new(values.Select(b => b.Open), values.Select(b => b.High),
            values.Select(b => b.Low), values.Select(b => b.Close), values.Select(b => b.Volume), values.Select(b => b.Time));
        Assert.Equal(0, Data(bars).CalculateRelativeStrength3DIndicator(Data(market),
            length1: 2, length2: 4, length3: 5, length4: 8, length5: 15).OutputValues["Rs3d"][236]);
    }
}
