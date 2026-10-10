using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedCatalogExecutionTests
{
    private static readonly IReadOnlyList<IndicatorValidationCase> ReviewedCases =
        IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
            .Where(c => SharedCpuStates.ReviewedTypes.Contains(c.IndicatorType)).ToArray();

    public static IEnumerable<object[]> Configurations => ReviewedCases.Select(c => new object[] { c });
    public static IEnumerable<object[]> Families => ReviewedCases.GroupBy(c => c.IndicatorType)
        .Select(g => new object[] { g.First() });

    [Fact]
    public void EveryExplicitFamilyHasExecutableFactoriesAndOnlyPlainFiniteStates()
    {
        foreach (var type in SharedCpuStates.ReviewedTypes)
        {
            Assert.True(type.IsSealed, type.FullName);
            var cases = ReviewedCases.Where(c => c.IndicatorType == type).ToArray();
            Assert.NotEmpty(cases);
            foreach (var testCase in cases)
            {
                var indicator = testCase.Factory();
                Assert.Null(indicator.Source);
                Assert.Empty(indicator.Components);
                Assert.False(indicator is IBuiltInIndicator or IHistoricalIndicator);
                Assert.Same(IndicatorInputDomain.Finite, IndicatorInputDomain.StableFor(indicator));
                var state = indicator switch
                {
                    IndicatorBase single => single.CreateState(),
                    MultiOutputIndicatorBase multi => multi.CreateState(),
                    _ => null
                };
                using var lifetime = state as IDisposable;
                Assert.True(state is IIndicatorState or IMultiOutputState, testCase.ToString());
                Assert.True(ValuesBarExecution.SupportsOwned(new[] { indicator }), testCase.ToString());
            }
        }
    }

    [Theory, MemberData(nameof(Configurations))]
    public async Task EveryConfigurationPreservesOrdinaryBuilderResults(IndicatorValidationCase testCase)
    {
        var indicator = testCase.Factory();
        var count = (int)Math.Min(512L, Math.Max(33L, (long)indicator.WarmupBars + 3));
        var bars = Input(count);
        await Compare(bars, indicator, testCase.ToString(), requireFused: true);
    }

    [Theory, MemberData(nameof(Families))]
    public async Task EveryFamilyPreservesEmptySingleAndExceptionalInputs(IndicatorValidationCase testCase)
    {
        foreach (var count in new[] { 0, 1 })
            await Compare(Input(count), testCase.Factory(), testCase.ToString(), requireFused: true);
        var values = new[] { -0d, 0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, .125, 1d, -1d };
        var bars = values.Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
        await Compare(bars, testCase.Factory(), testCase.ToString(), requireFused: true);
        bars[^1] = new Bar(default, 1, 2, 0, 1, double.NaN);
        await Compare(bars, testCase.Factory(), testCase.ToString(), requireFused: true);
    }

    [Theory, MemberData(nameof(Families))]
    public async Task ReviewedDependencyGraphsPreserveResultsWithoutLegacyRuntime(IndicatorValidationCase testCase)
    {
        var indicator = testCase.Factory();
        switch (indicator)
        {
            case IndicatorBase single: single.Of(new FirstValueEma(3)); break;
            case MultiOutputIndicatorBase multi: multi.Of(new FirstValueEma(3)); break;
        }
        await Compare(Input(41), indicator, testCase.ToString(), requireFused: false);
    }

    private static Bar[] Input(int count) => Enumerable.Range(0, count).Select(i =>
    {
        var close = 10 + (i * 13 % 37) / 8d;
        var open = close + (i % 3 - 1) / 4d;
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), open, Math.Max(open, close) + .5,
            Math.Min(open, close) - .5, close, 10 + i % 17);
    }).ToArray();

    private static async Task Compare(Bar[] bars, IIndicator indicator, string name, bool requireFused)
    {
        StockIndicatorBuilder Builder(IndicatorHistoryMode history) => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
        IIndicatorRun? reference = null;
        var expectedError = await Record.ExceptionAsync(async () =>
            reference = await Builder(IndicatorHistoryMode.Full).ConfigureBehavior(_ => { }).BuildAsync());
        using (reference)
        foreach (var history in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        {
            var builder = Builder(history);
            IIndicatorRun? actual = null;
            var actualError = await Record.ExceptionAsync(async () => actual = await builder.BuildAsync());
            using (actual)
            {
                if (expectedError is not null)
                {
                    Assert.NotNull(actualError);
                    Assert.Equal(expectedError.GetType(), actualError.GetType());
                    Assert.Equal(expectedError.Message, actualError.Message);
                    Assert.Null(builder.LastExecution);
                    continue;
                }
                Assert.True(actualError is null, name + ": " + actualError);
                Assert.NotNull(actual);
                Assert.Equal(reference!.BarCount, actual.BarCount);
                if (history == IndicatorHistoryMode.Full)
                {
                    Assert.False(((IndicatorRun)actual).HasLegacyRuntime);
                    if (requireFused) Assert.Contains("Fused CPU values", builder.LastExecution!.Reason);
                }
                foreach (var output in indicator.Outputs)
                    Assert.Equal(reference[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                        actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                if (bars.Length != 0)
                {
                    Assert.Equal(reference.Latest.Bar, actual.Latest.Bar);
                    Assert.Equal(reference.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                }
            }
        }
    }
}
