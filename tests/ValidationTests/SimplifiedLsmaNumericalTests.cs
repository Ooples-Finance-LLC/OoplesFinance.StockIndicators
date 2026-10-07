using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core.Registry;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SimplifiedLsmaNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(SimplifiedLeastSquaresMovingAverage)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
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
    public void FullWindowReproducesAffinePricesAndForgetsExpiredExtremes(int length)
    {
        using var window = new SimplifiedLeastSquaresWindow(length);
        for (var i = 0; i < 100; i++)
        {
            var value = 7d + 3 * i;
            var actual = window.Next(value, true);
            if (i + 1 >= length) Assert.Equal(value, actual);
        }
        foreach (var extreme in new[] { double.MaxValue, -double.MaxValue })
        foreach (var retained in new[] { 1d, double.Epsilon, -double.Epsilon })
        {
            window.Reset();
            window.Next(extreme, true);
            for (var i = 0; i < length; i++) window.Next(retained, true);
            Assert.Equal(retained, window.Next(retained, false));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task RoutesMatchIndependentWindowReference(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        {
            var bars = fixture.Bars.ToArray();
            var expected = BuiltInFormulaReferences.RoundedSimplifiedLeastSquares(bars, length);
            var actual = new double[bars.Length];
            MovingAverageCore.SimplifiedLeastSquaresMovingAverage(bars.Select(b => b.Close).ToArray(), actual, length);
            Assert.Equal(expected, actual);
            new SlsmaCore().Compute(bars.Select(b => b.Close).ToArray(), actual, length);
            Assert.Equal(expected, actual);
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            using (var context = new ComputeContext())
            {
                using var result = IndicatorCompute.ComputeSimplifiedLeastSquaresMovingAverageFast(data, context, length);
                Assert.Equal(expected, result.ToArray());
            }
            data.CalculateSimplifiedLeastSquaresMovingAverage(length);
            Assert.Equal(expected, data.OutputValues["Slsma"]);
            var indicator = new SimplifiedLeastSquaresMovingAverage(length);
            if (expected.Any(double.IsInfinity))
                await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).BuildAsync());
            else
            {
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
                Assert.Equal(expected, run[indicator].ToArray());
            }
            var spec = new IndicatorSpec(IndicatorName.SimplifiedLeastSquaresMovingAverage, new SimplifiedLeastSquaresMovingAverageSpecOptions(length));
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Length; i++)
                    {
                        var b = bars[i];
                        var native = new OhlcvBar("LSMA", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        foreach (var commit in new[] { false, true })
                            Assert.Equal(expected[i], state.Update(native, commit, true).Outputs!["Slsma"]);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task UnrepresentableExtrapolationIsRejectedAtTheOutputBoundary()
    {
        // Zero-padded OLS at period three has weights [5/6, 1/3, -1/6].
        // The second startup value is therefore 7/6 * MaxValue.
        var values = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedSimplifiedLeastSquares(bars, 3);
        Assert.Contains(expected, double.IsInfinity);
        await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(new SimplifiedLeastSquaresMovingAverage(3)).BuildAsync());
    }
    [Fact]
    public async Task SelectedInputReachesBothPublicAndLegacyBuilderRoutes()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var indicator = new SimplifiedLeastSquaresMovingAverage(3);
        indicator.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedSimplifiedLeastSquares(projected, 3);
        Assert.Equal(expected, run[indicator].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var result = IndicatorCompute.ComputeSimplifiedLeastSquaresMovingAverageFast(data, context, 3);
            Assert.Equal(expected, result.ToArray());
        }
    }

}
