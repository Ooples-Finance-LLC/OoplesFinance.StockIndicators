using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MidrangeNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName is IndicatorName.Midpoint or IndicatorName.Midprice)
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryAliasReceivesAllNumericalClasses(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    private static double[] Reference(IReadOnlyList<Bar> bars, int length, bool useHighLow) => bars.Select((_, i) =>
    {
        var window = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(length, i + 1)).ToArray();
        var upper = window.Max(b => useHighLow ? b.High : b.Close);
        var lower = window.Min(b => useHighLow ? b.Low : b.Close);
        return ((ReferenceFraction.FromDouble(upper) + ReferenceFraction.FromDouble(lower)) / new ReferenceFraction(2)).ToDouble();
    }).ToArray();

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    [InlineData(65)]
    public async Task MidrangesMatchExactExtremaAcrossRoutes(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(48, 248))
        {
            var bars = fixture.Bars.ToArray();
            var expected = Reference(bars, length, false);
            var highLow = Reference(bars, length, true);
            var actual = new double[bars.Length];
            TrendCore.Midpoint(bars.Select(b => b.Close).ToArray(), actual, length);
            Assert.Equal(expected, actual);
            TrendCore.Midprice(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), actual, length);
            Assert.Equal(highLow, actual);
            var midpoint = new Midpoint(length);
            var midprice = new Midprice(length);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(midpoint, midprice).BuildAsync();
            Assert.Equal(expected, run[midpoint].ToArray());
            Assert.Equal(highLow, run[midprice].ToArray());
            StockData Data() => new(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            Assert.Equal(expected, Data().CalculateMidpoint(length).CustomValuesList);
            Assert.Equal(highLow, Data().CalculateMidprice(length).CustomValuesList);
            using var pointState = new MidpointState(length);
            using var priceState = new MidpriceState(length);
            var pointInput = InputSeries.Midpoint(length);
            var priceInput = InputSeries.Midprice(length);
            using var pointInputLifetime = (IDisposable)pointInput;
            using var priceInputLifetime = (IDisposable)priceInput;
            for (var replay = 0; replay < 2; replay++)
            {
                pointState.Reset(); priceState.Reset(); pointInput.Reset(); priceInput.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("MID", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    foreach (var commit in new[] { false, true })
                    {
                        Assert.Equal(expected[i], pointInput.Next(input, commit));
                        Assert.Equal(highLow[i], priceInput.Next(input, commit));
                        var point = pointState.Update(input, commit, true);
                        var price = priceState.Update(input, commit, true);
                        Assert.Equal(expected[i], point.Value);
                        Assert.Equal(expected[i], point.Outputs!["HCLC2"]);
                        Assert.Equal(highLow[i], price.Value);
                        Assert.Equal(highLow[i], price.Outputs!["HHLL2"]);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task MidpointUsesSelectedInputThroughPublicChainAndFastArm()
    {
        var bars = IndicatorAdversarialCases.Generate(48, 248).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var midpoint = new Midpoint(3);
        midpoint.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, midpoint).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = Reference(projected, 3, false);
        Assert.Equal(expected, run[midpoint].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            if (chained) data.CustomValuesList = selected.ToList();
            else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var result = IndicatorCompute.ComputeMidpointFast(data, context, 3);
            Assert.Equal(expected, result.ToArray());
        }
    }
}
