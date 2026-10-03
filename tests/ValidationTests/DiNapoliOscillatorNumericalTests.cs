using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DiNapoliOscillatorNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(DiNapoliMovingAverageConvergenceDivergence) || c.IndicatorType == typeof(DiNapoliPercentagePriceOscillator))
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
    public void EveryRouteMatchesIndependentFractionalFilters(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var percentage = builtIn.BatchName == IndicatorName.DiNapoliPercentagePriceOscillator;
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedDiNapoliOscillator(bars, options, percentage);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedInputControlsEveryOutput(bool percentage)
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        IIndicator indicator = percentage ? new DiNapoliPercentagePriceOscillator(2).Of(source)
            : new DiNapoliMovingAverageConvergenceDivergence(2, 3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, Math.Max(b.High, selected[i]),
            Math.Min(b.Low, selected[i]), selected[i], b.Volume)).ToArray();
        var builtIn = (IBuiltInIndicator)indicator;
        var expected = BuiltInFormulaReferences.RoundedDiNapoliOscillator(projected, builtIn.CreateOptions(), percentage);
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
    public void CoreUsesFractionalZeroSeededFiltersAndPreservesFiniteExtremeResults()
    {
        var values = new[] { 4d, 0, 2, double.MaxValue, -double.MaxValue };
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedDiNapoliOscillator(bars, new DiNapoliMovingAverageConvergenceDivergenceSpecOptions(3, 1, 1), true);
        var actual = new double[values.Length];
        Core.OscillatorCore.DiNapoliPercentagePriceOscillator(values, actual, sc: 1, lc: 3);
        Assert.Equal(expected["Ppo"], actual);
        Assert.Equal(new[] { 100d, -100, 100d / 3 }, actual.Take(3));
        Assert.All(actual, v => Assert.True(double.IsFinite(v)));
        Core.OscillatorCore.DiNapoliPercentagePriceOscillator(Array.Empty<double>(), Array.Empty<double>(), sc: double.MaxValue, lc: double.MaxValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OverflowPreviewDoesNotCommitSignalState(bool percentage)
    {
        Bar B(double value) => new(DateTime.UnixEpoch, value, value, value, value, 1);
        IStreamingIndicatorState state = percentage ? new DiNapoliPercentagePriceOscillatorState(lc: double.MaxValue, sc: 1, sp: 1)
            : new DiNapoliMovingAverageConvergenceDivergenceState(lc: 5, sc: 1, sp: 1);
        var stable = Native(B(percentage ? 0 : double.MaxValue));
        var overflow = Native(B(percentage ? double.MaxValue : -double.MaxValue));
        for (var i = 0; i < 12; i++) state.Update(stable, true, true);
        var preview = state.Update(overflow, false, true);
        Assert.True(double.IsInfinity(preview.Value));
        Assert.True(double.IsFinite(state.Update(stable, false, true).Outputs!["Signal"]));
        state.Update(overflow, true, true);
        Assert.True(double.IsNaN(state.Update(stable, true, true).Outputs!["Signal"]));
        state.Reset();
        Assert.True(double.IsFinite(state.Update(stable, true, true).Outputs!["Signal"]));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidFractionalPeriodsAreRejectedEvenForEmptyInput(double period)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiNapoliMovingAverageConvergenceDivergenceSpecOptions(lc: period));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiNapoliMovingAverageConvergenceDivergenceState(sc: period));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiNapoliPercentagePriceOscillatorState(sp: period));
        Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateDiNapoliMovingAverageConvergenceDivergence(lc: period));
        Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateDiNapoliPercentagePriceOscillator(sc: period));
    }
}
