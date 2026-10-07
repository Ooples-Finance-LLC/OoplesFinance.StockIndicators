using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class QuickMeanNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName == IndicatorName.QuickMovingAverage)
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryAliasReceivesAllNumericalClasses(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(14)]
    [InlineData(1600)]
    public async Task PiecewiseTriangleMatchesAcrossRoutes(int period)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 250))
        {
            var bars = fixture.Bars.ToArray();
            var close = bars.Select(b => b.Close).ToArray();
            var expected = BuiltInFormulaReferences.RoundedQuickMean(bars, period);
            var actual = new double[bars.Length];
            MovingAverageCore.QuickMovingAverage(close, actual, period);
            Assert.Equal(expected, actual);
            var indicator = new QuickMovingAverage(period);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                close.ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            Assert.Equal(expected, data.CalculateQuickMovingAverage(period).CustomValuesList);
            using var state = new QuickMovingAverageState(period);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("QUICK", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    foreach (var commit in new[] { false, true })
                    {
                        var result = state.Update(input, commit, true);
                        Assert.Equal(expected[i], result.Value);
                        Assert.Equal(expected[i], result.Outputs!["Qma"]);
                    }
                }
            }
        }
    }

    [Fact]
    public void PeriodOneKeepsBothTapsAndHugePeriodsDoNotOverflow()
    {
        var input = new[] { 3d, 6d, 9d };
        var output = new double[3];
        MovingAverageCore.QuickMovingAverage(input, output, 1);
        Assert.Equal(new[] { 1d, 4d, 7d }, output);
        // The original fractional taps sum to (length+1)/2. The peak cap is 530.
        var hugeInput = new[] { double.MaxValue, -double.MaxValue, double.Epsilon };
        var denominator = new ReferenceFraction(1L + int.MaxValue) / new ReferenceFraction(2);
        MovingAverageCore.QuickMovingAverage(hugeInput, output, int.MaxValue);
        for (var i = 0; i < hugeInput.Length; i++)
        {
            var numerator = new ReferenceFraction(0);
            for (var lag = 0; lag <= i; lag++)
                numerator += ReferenceFraction.FromDouble(hugeInput[i - lag]) * new ReferenceFraction(lag + 1) / new ReferenceFraction(530);
            Assert.Equal((numerator / denominator).ToDouble(), output[i]);
        }
        MovingAverageCore.QuickMovingAverage(Array.Empty<double>(), Span<double>.Empty, 14);
    }

    [Fact]
    public async Task QuickMeanPreservesSelectedInput()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 250).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var quick = new QuickMovingAverage(3);
        quick.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, quick).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedQuickMean(projected, 3);
        Assert.Equal(expected, run[quick].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            if (chained) data.CustomValuesList = selected.ToList();
            else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var result = IndicatorCompute.ComputeQuickMovingAverageFast(data, context, 3);
            Assert.Equal(expected, result.ToArray());
        }
    }
}
