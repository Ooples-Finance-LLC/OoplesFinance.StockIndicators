using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class IchimokuNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName is IndicatorName.IchimokuCloud or IndicatorName.IchimokuChikouSpan)
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryCloudAndLaggingSpanAliasReceivesAllNumericalClasses(IndicatorValidationCase testCase)
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
    public void CloudOutputsUseRoundedMidpointsAtTheComputingBar(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(48, 245))
        {
            var bars = fixture.Bars.ToArray();
            var high = bars.Select(b => b.High).ToArray(); var low = bars.Select(b => b.Low).ToArray();
            static double Mid(double a, double b) => ((ReferenceFraction.FromDouble(a) + ReferenceFraction.FromDouble(b)) / new ReferenceFraction(2)).ToDouble();
            double[] Line(int period) => bars.Select((_, i) =>
            {
                var window = bars.Skip(Math.Max(0, i - period + 1)).Take(Math.Min(period, i + 1)).ToArray();
                return Mid(window.Max(b => b.High), window.Min(b => b.Low));
            }).ToArray();
            var tenkan = Line(length); var kijun = Line(length + 2); var senkouB = Line(length + 5);
            var expected = new Dictionary<string, double[]>
            {
                ["TenkanSen"] = tenkan, ["KijunSen"] = kijun,
                ["SenkouSpanA"] = tenkan.Select((v, i) => Mid(v, kijun[i])).ToArray(), ["SenkouSpanB"] = senkouB
            };
            var actual = new double[bars.Length];
            TrendCore.IchimokuTenkanSen(high, low, actual, length);
            Assert.Equal(tenkan, actual);
            TrendCore.IchimokuKijunSen(high, low, actual, length + 2);
            Assert.Equal(kijun, actual);
            TrendCore.IchimokuSenkouSpanA(high, low, actual, length, length + 2);
            Assert.Equal(expected["SenkouSpanA"], actual);
            TrendCore.IchimokuSenkouSpanB(high, low, actual, length + 5);
            Assert.Equal(senkouB, actual);
            var data = new StockData(bars.Select(b => b.Open).ToList(), high.ToList(), low.ToList(), bars.Select(b => b.Close).ToList(),
                bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList()).CalculateIchimokuCloud(length, length + 2, length + 5);
            foreach (var output in expected) Assert.Equal(output.Value, data.OutputValues[output.Key]);
            using var state = new IchimokuCloudState(length, length + 2, length + 5);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("IC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    foreach (var commit in new[] { false, true })
                    {
                        var result = state.Update(input, commit, true);
                        foreach (var output in expected) Assert.Equal(output.Value[i], result.Outputs![output.Key]);
                    }
                }
            }
        }
    }
}
