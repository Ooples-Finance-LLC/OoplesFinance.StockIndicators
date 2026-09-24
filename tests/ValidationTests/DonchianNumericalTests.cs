using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DonchianNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName == IndicatorName.DonchianChannels)
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryChannelAliasReceivesAllNumericalClasses(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public void AllOutputsMatchExactWindowExtremesAndMidpoints(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(48, 245))
        {
            var bars = fixture.Bars.ToArray();
            var high = bars.Select(b => b.High).ToArray(); var low = bars.Select(b => b.Low).ToArray();
            var upper = bars.Select((_, i) => high.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(length, i + 1)).Max()).ToArray();
            var lower = bars.Select((_, i) => low.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(length, i + 1)).Min()).ToArray();
            var middle = upper.Select((u, i) => ((ReferenceFraction.FromDouble(u) + ReferenceFraction.FromDouble(lower[i])) / new ReferenceFraction(2)).ToDouble()).ToArray();
            var actual = new double[bars.Length];
            TrendCore.DonchianChannelMiddle(high, low, actual, length);
            Assert.Equal(middle, actual);
            var data = new StockData(bars.Select(b => b.Open).ToList(), high.ToList(), low.ToList(), bars.Select(b => b.Close).ToList(),
                bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList()).CalculateDonchianChannels(length);
            Assert.Equal(upper, data.OutputValues["UpperChannel"]);
            Assert.Equal(lower, data.OutputValues["LowerChannel"]);
            Assert.Equal(middle, data.OutputValues["MiddleChannel"]);
            using var state = new DonchianChannelsState(length);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("DC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    foreach (var commit in new[] { false, true })
                    {
                        var result = state.Update(input, commit, true);
                        Assert.Equal(middle[i], result.Value);
                        Assert.Equal(upper[i], result.Outputs!["UpperChannel"]);
                        Assert.Equal(lower[i], result.Outputs!["LowerChannel"]);
                    }
                }
            }
        }
    }
}
