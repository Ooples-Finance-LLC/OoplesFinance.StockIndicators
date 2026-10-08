using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DiNapoliNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(DiNapoliPreferredStochasticOscillator))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(1, 1, 7)]
    [InlineData(3, 7, 1)]
    [InlineData(8, 3, 3)]
    public async Task IndependentRoundedRecurrenceMatchesAllRoutes(int length, int first, int second)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedDiNapoli(bars, length, first, second);
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            data.CalculateDiNapoliPreferredStochasticOscillator(length, first, second);
            foreach (var (key, values) in expected) Assert.Equal(values, data.OutputValues[key]);
            using var state = new DiNapoliPreferredStochasticOscillatorState(length, first, second);
            Check(state, expected);

            // V2 exposes the range period; its two smoothing periods are fixed at three.
            var indicator = new DiNapoliPreferredStochasticOscillator(length);
            var fixedExpected = BuiltInFormulaReferences.RoundedDiNapoli(bars, length);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(fixedExpected["Dpso"], run[indicator].ToArray());
            Assert.Equal(fixedExpected["Signal"], run[indicator.Signal].ToArray());
            var builtIn = (IBuiltInIndicator)indicator;
            var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
            foreach (var factoryState in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(factoryState);
                using var lifetime = factoryState as IDisposable;
                Check(factoryState, fixedExpected);
            }

            void Check(IStreamingIndicatorState engine, IReadOnlyDictionary<string, double[]> reference)
            {
                for (var replay = 0; replay < 2; replay++)
                {
                    engine.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    foreach (var commit in new[] { false, true })
                    {
                        var b = bars[i];
                        var bar = new OhlcvBar("DPSO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        var result = engine.Update(bar, commit, true);
                        foreach (var (key, values) in reference)
                        {
                            Assert.Equal(values[i], result.Outputs![key]);
                            Assert.InRange(values[i], 0, 100);
                        }
                    }
                }
            }
        }
    }
}
