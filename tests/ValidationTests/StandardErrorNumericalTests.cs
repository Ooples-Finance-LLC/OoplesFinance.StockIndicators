using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class StandardErrorNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(StandardError) || c.IndicatorType == typeof(StandardErrorCore))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentCenteredResiduals(IndicatorValidationCase testCase)
    {
        var indicator = testCase.Factory();
        var builtIn = (IBuiltInIndicator)indicator;
        var options = builtIn.CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var regression = indicator is StandardError;
        var key = regression ? "StandardError" : "Sem";
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedStandardError(bars, length, regression);
            var prices = bars.Select(b => b.Close).ToArray();
            var actual = new double[bars.Count];
            if (regression) TrendCore.StandardError(prices, actual, length);
            else VolatilityCore.StandardError(prices, actual, length);
            Assert.Equal(expected, actual);
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                prices, bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var legacy = regression ? Data().CalculateStandardError(length) : Data().CalculateStandardErrorOfTheMean(length);
            Assert.Equal(expected, legacy.OutputValues[key]);
            using (var context = new ComputeContext())
            {
                using var buffer = regression ? IndicatorCompute.ComputeStandardErrorFast(Data(), context, length)
                    : IndicatorCompute.ComputeStandardErrorCoreFast(Data(), context, length);
                Assert.Equal(expected, buffer.ToArray());
            }
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var b = bars[i];
                        var native = new OhlcvBar("ERROR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        foreach (var commit in new[] { false, true })
                            Assert.Equal(expected[i], state.Update(native, commit, true).Outputs![key]);
                    }
                }
            }
        }
    }

    [Fact]
    public void AffinePricesHaveNoRegressionScatterAndExpiredExtremesLeaveNoResidue()
    {
        foreach (var length in new[] { 1, 2, 3, 14 })
        {
            using var regression = new ExactStandardErrorWindow(length, true);
            for (var i = 0; i < 100; i++) Assert.Equal(0, regression.Next(1e16 + 2 * i, true));
            foreach (var fit in new[] { false, true })
            {
                using var window = new ExactStandardErrorWindow(length, fit);
                window.Next(double.MaxValue, true);
                for (var i = 0; i < length; i++) window.Next(double.Epsilon, true);
                Assert.Equal(0, window.Next(double.Epsilon, false));
            }
        }
    }

    [Fact]
    public void SquareRootIsRoundedOnlyAfterTheExactMeanOrFit()
    {
        var mean = new double[3];
        VolatilityCore.StandardError(new[] { 0d, 2d }, mean, 2);
        Assert.Equal(Math.Sqrt(.5), mean[1]);
        VolatilityCore.StandardError(new[] { -double.Epsilon, 0, double.Epsilon }, mean, 3);
        Assert.Equal(0, mean[2]);
        TrendCore.StandardError(new[] { 0d, 3, 0 }, mean, 3);
        Assert.Equal(Math.Sqrt(2), mean[2]);
        TrendCore.StandardError(new[] { 0d, 2 * double.Epsilon, 0 }, mean, 3);
        Assert.Equal(double.Epsilon, mean[2]);
        Assert.Equal(0, ExactPopulationDeviation.RootRatio(BigInteger.One, new BigInteger(4)));
        Assert.Equal(2 * double.Epsilon, ExactPopulationDeviation.RootRatio(new BigInteger(9), new BigInteger(4)));
        Assert.Equal(2 * double.Epsilon, ExactPopulationDeviation.RootRatio(new BigInteger(25), new BigInteger(4)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedInputReachesPublicAndLegacyBuilderPaths(bool regression)
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        IndicatorBase indicator = regression ? new StandardError(3) : new StandardErrorCore(3);
        indicator.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedStandardError(projected, 3, regression);
        Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var buffer = regression ? IndicatorCompute.ComputeStandardErrorFast(data, context, 3)
                : IndicatorCompute.ComputeStandardErrorCoreFast(data, context, 3);
            Assert.Equal(expected, buffer.ToArray());
        }
    }
}
