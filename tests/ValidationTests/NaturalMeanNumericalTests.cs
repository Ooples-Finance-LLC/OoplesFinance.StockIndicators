using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class NaturalMeanNumericalTests
{
    [Fact]
    public void NativeFactoriesHonorPeriodPreviewAndReset()
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        foreach (var length in new[] { 1, 3, 14 })
        {
            var expected = BuiltInFormulaReferences.RoundedNaturalMean(fixture.Bars, length);
            var spec = new IndicatorSpec(IndicatorName.NaturalMovingAverage, new NaturalMaSpecOptions(length));
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < fixture.Bars.Count; i++)
                    {
                        var b = fixture.Bars[i];
                        var bar = new OhlcvBar("NMA", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        Assert.Equal(expected[i], state.Update(bar, false, true).Outputs!["Nma"]);
                        Assert.Equal(expected[i], state.Update(bar, true, true).Value);
                    }
                }
            }
        }
    }

    [Fact]
    public void ZeroLogTravelAndConvexBlendHaveExplicitContracts()
    {
        var output = new double[4];
        MovingAverageCore.NaturalMovingAverage(new[] { -2d, -4d, 0d, 1d }, output, 3);
        Assert.Equal(new[] { 0d, -2d, -4d, 0d }, output);
        MovingAverageCore.NaturalMovingAverage(new[] { 2d, 4d, 8d, 16d }, output, 1);
        Assert.Equal(new[] { 2d, 4d, 8d, 16d }, output);
        MovingAverageCore.NaturalMovingAverage(Array.Empty<double>(), Span<double>.Empty, int.MaxValue);
        var one = new double[1];
        MovingAverageCore.NaturalMovingAverage(new[] { double.MaxValue }, one, int.MaxValue);
        Assert.Equal(double.MaxValue, one[0]);
        using var state = new OoplesFinance.StockIndicators.Helpers.NaturalWindowMean(3);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            Assert.Equal(double.MaxValue, state.Next(double.MaxValue, true));
            state.Next(double.Epsilon, false);
            foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            foreach (var commit in new[] { false, true })
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Next(invalid, commit));
            Assert.Equal(double.MaxValue, state.Next(double.MaxValue, false));
            Assert.Equal(double.MaxValue, state.Next(double.MaxValue, true));
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && b.BatchName == IndicatorName.NaturalMovingAverage)
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
    [InlineData(5)]
    [InlineData(65)]
    public async Task NaturalMeanMatchesAcrossRoutes(int period)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(period == 14 ? 160 : 40, 250))
        {
            var bars = fixture.Bars.ToArray();
            var close = bars.Select(b => b.Close).ToArray();
            var expected = BuiltInFormulaReferences.RoundedNaturalMean(bars, period);
            var actual = new double[bars.Length];
            MovingAverageCore.NaturalMovingAverage(close, actual, period);
            Assert.Equal(expected, actual);
            var indicator = new NaturalMa(period);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                close.ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            Assert.Equal(expected, data.CalculateNaturalMovingAverage(period).CustomValuesList);
            using var state = new NaturalMovingAverageState(period);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("FIB", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    foreach (var commit in new[] { false, true })
                    {
                        var result = state.Update(input, commit, true);
                        Assert.Equal(expected[i], result.Value);
                        Assert.Equal(expected[i], result.Outputs!["Nma"]);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task NaturalMeanPreservesSelectedInput()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 250).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var distance = new NaturalMa(3);
        distance.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, distance).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedNaturalMean(projected, 3);
        Assert.Equal(expected, run[distance].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(), bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(), bars.Select(b => b.Volume).ToList(), bars.Select(b => b.Time).ToList());
            if (chained) data.CustomValuesList = selected.ToList();
            else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var result = IndicatorCompute.ComputeNaturalMaFast(data, context, 3);
            Assert.Equal(expected, result.ToArray());
        }
    }
}
