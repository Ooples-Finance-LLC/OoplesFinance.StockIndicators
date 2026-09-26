using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class StochasticMacdNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(StochasticMacdOscillator))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));

    private static OhlcvBar Native(Bar b) => new("DINAPOLI", BarTimeframe.Minutes(1), b.Time, b.Time,
        b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentRangeFormula(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedStochasticMacdReference(bars, options);
            foreach (var key in expected.Keys)
            {
                var outputSpec = new IndicatorSpec(builtIn.BatchName, options, key);
                Assert.Equal(expected[key], BuilderArmBinding.Compute(Data(bars), outputSpec, target));
                using var context = new ComputeContext();
                using var buffer = IndicatorCompute.TryComputeFast(Data(bars), outputSpec, context);
                Assert.NotNull(buffer);
                Assert.Equal(expected[key], buffer.Value.ToArray());
            }
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    foreach (var commit in new[] { false, true })
                    {
                        var result = state.Update(Native(bars[i]), commit, true);
                        foreach (var key in expected.Keys) Assert.Equal(expected[key][i], result.Outputs![key]);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task SelectedInputControlsEveryOutput()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        IIndicator indicator = new StochasticMacdOscillator(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var legacyProjected = bars.Select((b, i) =>
        {
            var value = selected[i];
            var within = value >= b.Low && value <= b.High;
            var previous = i == 0 ? value : selected[i - 1];
            return new Bar(b.Time, b.Open, within ? b.High : Math.Max(value, previous),
                within ? b.Low : Math.Min(value, previous), value, b.Volume);
        }).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var builtIn = (IBuiltInIndicator)indicator;
        var expected = BuiltInFormulaReferences.RoundedStochasticMacdReference(projected, builtIn.CreateOptions());
        var legacyExpected = BuiltInFormulaReferences.RoundedStochasticMacdReference(legacyProjected, builtIn.CreateOptions());
        var slot = 0;
        foreach (var (key, values) in expected)
        {
            Assert.Equal(values, run[indicator.Outputs[slot++]].ToArray());
            var data = Data(bars);
            data.CustomValuesList = selected.ToList();
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), key), context);
            Assert.NotNull(buffer);
            Assert.Equal(legacyExpected[key], buffer.Value.ToArray());
        }
    }

    [Fact]
    public void RangeRatioPreservesCancellationSubnormalsAndFiniteExtremeResults()
    {
        Assert.Equal(10, RoundedStochasticMacd.Of(double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue));
        Assert.Equal(5, RoundedStochasticMacd.Of(double.Epsilon, 0, double.Epsilon, -double.Epsilon));
        Assert.Equal(0, RoundedStochasticMacd.Of(10, -10, 2, 2));
        var next = Math.BitIncrement(1d);
        Assert.Equal((next - 1) * 5, RoundedStochasticMacd.Of(next, 1, 2, 0));
    }

    [Fact]
    public void CloseOnlyCoreMatchesDegenerateOhlcAndDoesNotStochasticizeTheMacdHistory()
    {
        Core.OscillatorCore.StochasticMacdOscillator(Array.Empty<double>(), Array.Empty<double>(), int.MaxValue, int.MaxValue);
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        {
            var bars = fixture.Bars.Select(b => new Bar(b.Time, b.Close, b.Close, b.Close, b.Close, b.Volume)).ToArray();
            var expected = BuiltInFormulaReferences.RoundedStochasticMacdReference(bars, new StochasticMacdOscillatorSpecOptions(3));
            var actual = new double[bars.Length];
            Core.OscillatorCore.StochasticMacdOscillator(bars.Select(b => b.Close).ToArray(), actual, stochLength: 3);
            Assert.Equal(expected["Macd"], actual);
        }
    }

    [Fact]
    public void NarrowRangeOverflowPreviewDoesNotCommitSignalState()
    {
        OhlcvBar B(double value, double high, double low) => Native(new Bar(DateTime.UnixEpoch, value, high, low, value, 1));
        using var state = new StochasticMovingAverageConvergenceDivergenceOscillatorState(length: 1, fastLength: 1, slowLength: 3);
        for (var i = 0; i < 4; i++) state.Update(B(1, 1, 1), true, true);
        var overflow = B(0, double.Epsilon, 0);
        Assert.True(double.IsInfinity(state.Update(overflow, false, true).Value));
        Assert.True(double.IsFinite(state.Update(B(1, 1, 1), false, true).Outputs!["Signal"]));
        state.Update(overflow, true, true);
        Assert.True(double.IsNaN(state.Update(B(1, 1, 1), true, true).Outputs!["Signal"]));
        state.Reset();
        Assert.Equal(0, state.Update(B(1, 1, 1), true, true).Outputs!["Signal"]);
    }
}
