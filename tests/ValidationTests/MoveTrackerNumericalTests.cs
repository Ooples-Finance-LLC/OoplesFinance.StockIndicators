using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MoveTrackerNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(MoveTracker) || c.IndicatorType == typeof(PriceChange))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task BothAliasesReceiveAllNumericalAndOutputOverflowCases(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
        foreach (var output in new[] { "primary", "signal" })
        foreach (var sign in new[] { "positive", "negative" })
            Assert.Equal(2, Assert.Single(report.FixtureEvidence,
                f => f.Name == "move-tracker-" + output + "-" + sign + "-output-overflow").OutputOverflowRejectionsChecked);
    }

    [Fact]
    public async Task BothRoundedStagesMatchAcrossRoutesPreviewAndReset()
    {
        var fixtures = IndicatorAdversarialCases.Generate(32, 244).Concat(new[] {
            Fixture("primary-overflow", new[] { -double.MaxValue, double.MaxValue }),
            Fixture("signal-overflow", new[] { 0d, -double.MaxValue, 0d }),
            Fixture("stage-rounding", new[] { 1e16, 1d, -1e16, 1d, 2d }) });
        foreach (var fixture in fixtures)
        {
            var bars = fixture.Bars;
            var primary = BuiltInFormulaReferences.RoundedMoveTracker(bars, false);
            var signal = BuiltInFormulaReferences.RoundedMoveTracker(bars, true);
            var data = Data(bars);
            data.CalculateMoveTracker();
            Assert.Equal(primary, data.OutputValues["Mt"]);
            for (var i = 0; i < signal.Length && !double.IsNaN(signal[i]); i++)
                Assert.Equal(signal[i], data.OutputValues["Signal"][i]);
            var core = new double[bars.Count];
            OscillatorCore.PriceChange(bars.Select(b => b.Close).ToArray(), core);
            Assert.Equal(primary, core);
            foreach (IIndicator indicator in new IIndicator[] { new MoveTracker(14), new PriceChange() })
            {
                if (primary.Concat(signal).Any(double.IsInfinity))
                    await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder()
                        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync());
                else
                {
                    using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
                    Assert.Equal(primary, run[indicator].ToArray());
                    Assert.Equal(signal, run[indicator.Outputs[1]].ToArray());
                }
                var builtIn = (IBuiltInIndicator)indicator;
                var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
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
                            var b = bars[i];
                            var native = new OhlcvBar("MT", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                            var actual = state.Update(native, commit, true);
                            Assert.Equal(primary[i], actual.Value);
                            if (!double.IsNaN(signal[i])) Assert.Equal(signal[i], actual.Outputs!["Signal"]);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public async Task SelectedPricesAndBothOutputsArePreserved()
    {
        var bars = Fixture("selection", new[] { 100d, 120, 110, 150, 90 }).Bars;
        var source = new Sma(2);
        var first = new MoveTracker(14).Of(source);
        var second = new PriceChange().Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, first, second).BuildAsync();
        var selected = Fixture("selected", run[source].ToArray()).Bars;
        foreach (IIndicator indicator in new IIndicator[] { first, second })
        {
            Assert.Equal(BuiltInFormulaReferences.RoundedMoveTracker(selected, false), run[indicator].ToArray());
            Assert.Equal(BuiltInFormulaReferences.RoundedMoveTracker(selected, true), run[indicator.Outputs[1]].ToArray());
        }
        var data = Data(bars);
        data.InputValues = new() { 1, 3, 2, 5, 4 };
        using var context = new ComputeContext();
        using var primary = IndicatorCompute.ComputePriceChangeFast(data, context);
        using var signal = IndicatorCompute.ComputePriceChangeFast(data, context, "Signal");
        Assert.Equal(new[] { 0d, 2, -1, 3, -1 }, primary.ToArray());
        Assert.Equal(new[] { 0d, 2, -3, 4, -4 }, signal.ToArray());
    }

    private static IndicatorValidationFixture Fixture(string name, double[] values) => new(name,
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)));
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
}
