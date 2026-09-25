using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ReverseMacdNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(ReverseMovingAverageConvergenceDivergence))
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
    public void EveryRouteMatchesIndependentEquilibrium(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedReverseMacdReference(bars, options);
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
        IIndicator indicator = new ReverseMovingAverageConvergenceDivergence(2, 3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var builtIn = (IBuiltInIndicator)indicator;
        var expected = BuiltInFormulaReferences.RoundedReverseMacdReference(projected, builtIn.CreateOptions());
        var slot = 0;
        foreach (var (key, values) in expected)
        {
            Assert.Equal(values, run[indicator.Outputs[slot++]].ToArray());
            var data = Data(bars);
            data.CustomValuesList = selected.ToList();
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), key), context);
            Assert.NotNull(buffer);
            Assert.Equal(values, buffer.Value.ToArray());
        }
    }

    [Fact]
    public void CoreSolvesUnchangedDifferenceAndEqualPeriodBoundary()
    {
        var actual = new double[3];
        Core.MovingAverageCore.ReverseMovingAverageConvergenceDivergence(new[] { 4d, 0, 2 }, actual, 1, 3);
        Assert.Equal(new[] { 0d, 4, -2 }, actual);
        Core.MovingAverageCore.ReverseMovingAverageConvergenceDivergence(new[] { 4d, 0, 2 }, actual, 1, 1);
        Assert.Equal(new[] { 0d, 4, 0 }, actual);
        Core.MovingAverageCore.ReverseMovingAverageConvergenceDivergence(Array.Empty<double>(), Array.Empty<double>(), int.MaxValue, int.MaxValue);
        Assert.Equal(double.MaxValue, RoundedReverseMacd.Equilibrium(double.MaxValue, double.MaxValue, 1, .5));
        Assert.Equal(double.Epsilon, RoundedReverseMacd.Equilibrium(double.Epsilon, double.Epsilon, .5, .25));
    }

    [Fact]
    public void PreviewDoesNotChangeFutureEquilibriumAndOverflowInvalidatesSignalUntilReset()
    {
        OhlcvBar B(double v) => Native(new Bar(DateTime.UnixEpoch, v, v, v, v, 1));
        using var state = new ReverseMovingAverageConvergenceDivergenceState(fastLength: 1, slowLength: 3);
        state.Update(B(double.MaxValue), true, true);
        state.Update(B(-double.MaxValue), false, true);
        Assert.Equal(double.MaxValue, state.Update(B(double.MaxValue), true, true).Value);
        Assert.Equal(double.MaxValue, state.Update(B(-double.MaxValue), true, true).Value);
        Assert.True(double.IsNegativeInfinity(state.Update(B(0), false, true).Value));
        Assert.True(double.IsNaN(state.Update(B(0), true, true).Outputs!["Signal"]));
        Assert.True(double.IsNaN(state.Update(B(0), true, true).Outputs!["Signal"]));
        state.Reset();
        Assert.Equal(0, state.Update(B(1), true, true).Outputs!["Signal"]);
    }
}
