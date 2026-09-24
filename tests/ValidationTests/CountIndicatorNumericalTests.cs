using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CountIndicatorNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator builtIn && builtIn.BatchName is
            IndicatorName.AroonUp or IndicatorName.AroonDown or IndicatorName.AroonOscillator
            or IndicatorName.PsychologicalLine or IndicatorName.ChandeTrendScore)
        .Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EveryDiscoveredCountConfigurationReceivesAllNumericalClasses(IndicatorValidationCase testCase)
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
    [InlineData(37)]
    public async Task PsychologicalLineRoundsTheExactCountRatioOnceAcrossRoutes(int period)
    {
        var levels = new[] { double.MaxValue, -double.MaxValue, 0d, double.Epsilon, double.Epsilon, 1d };
        var values = Enumerable.Range(0, 100).Select(i => levels[i % levels.Length]).ToArray();
        var expected = values.Select((value, i) =>
        {
            var count = Enumerable.Range(Math.Max(0, i - period + 1), Math.Min(i + 1, period))
                .Count(j => j > 0 && values[j] > values[j - 1]);
            return (new ReferenceFraction(100L * count) / new ReferenceFraction(period)).ToDouble();
        }).ToArray();
        var core = new double[values.Length];
        OscillatorCore.PsychologicalLine(values, core, period);
        Assert.Equal(expected, core);
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var indicator = new PsychologicalLine(period);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, run[indicator].ToArray());
        var stock = new StockData(values.ToList(), values.ToList(), values.ToList(), values.ToList(),
            bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
        Assert.Equal(expected, stock.CalculatePsychologicalLine(period).CustomValuesList);
        using var state = new PsychologicalLineState(period);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var b = bars[i];
                var input = new OhlcvBar("COUNT", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                Assert.Equal(expected[i], state.Update(input, false, true).Value);
                Assert.Equal(expected[i], state.Update(input, true, true).Value);
            }
        }
    }

    [Fact]
    public void LongPsychologicalWindowsDoNotAllocateTheirHistoryOnTheStack()
    {
        const int period = 300000;
        var input = Enumerable.Range(0, period + 2).Select(i => (double)i + 1).ToArray();
        var actual = new double[input.Length];
        OscillatorCore.PsychologicalLine(input, actual, period);
        Assert.Equal(0, actual[0]);
        Assert.Equal(100d * (period - 1) / period, actual[period - 1]);
        Assert.Equal(100, actual[period]);
        Assert.Equal(100, actual[period + 1]);
    }
}
