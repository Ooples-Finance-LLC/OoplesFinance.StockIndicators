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

public sealed class RSquaredNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(RSquared))
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
    public void EveryRouteMatchesIndependentCorrelation(IndicatorValidationCase testCase)
    {
        var indicator = testCase.Factory();
        var builtIn = (IBuiltInIndicator)indicator;
        var options = builtIn.CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var key = "RSquared";
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedRSquared(bars, length);
            var prices = bars.Select(b => b.Close).ToArray();
            var actual = new double[bars.Count];
            TrendCore.RSquared(prices, actual, length);
            Assert.Equal(expected, actual);
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                prices, bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var legacy = Data().CalculateRSquared(length);
            Assert.Equal(expected, legacy.OutputValues[key]);
            using (var context = new ComputeContext())
            {
                using var buffer = IndicatorCompute.ComputeRSquaredFast(Data(), context, length);
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
    public void CorrelationIsBoundedOffsetInvariantAndRecoversAfterExpiry()
    {
        using var shifted = new ExactRSquaredWindow(3);
        using var plain = new ExactRSquaredWindow(3);
        for (var i = 0; i < 80; i++)
        {
            var delta = 2 * (i % 7);
            var value = shifted.Next(1e16 + delta, true);
            Assert.Equal(plain.Next(delta, true), value);
            Assert.InRange(value, 0, 1);
        }
        shifted.Next(double.MaxValue, true);
        shifted.Next(-double.MaxValue, true);
        for (var i = 0; i < 3; i++) shifted.Next(7, true);
        Assert.Equal(0, shifted.Next(7, false));
        shifted.Reset();
        shifted.Next(double.Epsilon, true);
        shifted.Next(2 * double.Epsilon, true);
        Assert.Equal(1, shifted.Next(3 * double.Epsilon, true));
    }

    [Fact]
    public async Task SelectedInputReachesBuilderAndLegacyRoutes()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var indicator = new RSquared(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedRSquared(projected, 3);
        Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.ComputeRSquaredFast(data, context, 3);
            Assert.Equal(expected, buffer.ToArray());
            Assert.Equal(expected, data.CalculateRSquared(3).OutputValues["RSquared"]);
        }
    }
}
