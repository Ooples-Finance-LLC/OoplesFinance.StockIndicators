using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedDailyPivotsTests
{
    private static MultiOutputIndicatorBase Create(bool standard) => standard ? new StandardPivotPoints() : new DynamicPivotPoints();
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(StandardPivotPoints) || c.IndicatorType == typeof(DynamicPivotPoints)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    [Theory, InlineData(false), InlineData(true)]
    public async Task DailyTransitionsRetainEveryOutputBit(bool standard)
    {
        var root = Create(standard); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        var day = new DateTime(2026, 1, 1);
        Bar[][] cases = [[], [new(day, 1, 2, 0, 1, 1)],
            Enumerable.Range(0, 65).Select(i => new Bar(day.AddHours(i * 6), i, i + 2, i - 2, i + 1, 1)).ToArray(),
            [new(day, 1, 2, 0, 1, 1), new(day.AddDays(3), 2, 4, -1, 3, 1), new(day.AddDays(1), 5, 7, 2, 4, 1)],
            [new(day, -double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, 1), new(day.AddDays(1), 0, 1, -1, 0, 1)],
            [new(day, -0d, 0, -0d, -0d, 1), new(day.AddDays(1), 0, double.Epsilon, -double.Epsilon, 0, 1)]];
        foreach (var bars in cases) await Compare([root], bars);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task CompletedDayKeepsDistinctHandCalculatedFormulas(bool standard)
    {
        var day = new DateTime(2026, 1, 1);
        Bar[] bars = [new(day.AddHours(9), 6, 10, 4, 6, 1), new(day.AddHours(16), 8, 12, 2, 8, 1),
            new(day.AddDays(1), 0, 100, -100, 0, 1), new(day.AddDays(1).AddHours(1), 0, 200, -200, 0, 1)];
        var root = Create(standard); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        // The published pivot is rounded before the support/resistance arithmetic.
        var dynamicPivot = 22d / 3;
        double[] levels = standard ? [7, 2, -3, -3, 12, 17, 17, -3, -.5, 4.5, 9.5, 14.5, 17] : [dynamicPivot, 2 * dynamicPivot - 12, 2 * dynamicPivot - 2];
        Assert.Equal(levels.Length, root.Outputs.Count);
        for (var slot = 0; slot < levels.Length; slot++) Bits([0, 0, levels[slot], levels[slot]], run[root.Outputs[slot]].ToArray());
    }

    [Theory, InlineData(false), InlineData(true)]
    public void ExactStateReplaysAfterReset(bool standard)
    {
        var day = new DateTime(2026, 1, 1);
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(day.AddHours(i * 6), i, i + 2, i - 2, i + 1, 1)).ToArray();
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedDailyPivotState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IMultiOutputState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { standard }, null)!;
        double[] Run() { var result = new List<double>(); var outputs = new double[standard ? 13 : 3]; foreach (var bar in bars) { state.Update(in bar, outputs); result.AddRange(outputs); } return result.ToArray(); }
        try { var expected = Run(); state.Reset(); Bits(expected, Run()); } finally { ((IDisposable)state).Dispose(); }
    }

    [Fact]
    public void OutputSlotsKeepPivotPrimary()
    {
        var standard = new StandardPivotPoints();
        Assert.Equal(new[] { standard.Value, standard.S1, standard.S2, standard.S3, standard.R1, standard.R2, standard.R3, standard.M1, standard.M2, standard.M3, standard.M4, standard.M5, standard.M6 }, standard.Outputs);
        Assert.Same(standard.Value, standard.PrimaryOutput);
        var dynamic = new DynamicPivotPoints(); Assert.Equal(new[] { dynamic.Value, dynamic.S1, dynamic.R1 }, dynamic.Outputs);
        Assert.Same(dynamic.Value, dynamic.PrimaryOutput);
    }

    [Fact]
    public async Task CustomSourcesErrorsAndCancellationKeepContracts()
    {
        var day = new DateTime(2026, 1, 1);
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(day.AddHours(i * 6), i, i + 2, i - 2, i + 1, 1)).ToArray();
        foreach (var standard in new[] { false, true })
        {
            var selected = Create(standard).Of(new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        }
        var repeated = new StandardPivotPoints(); IIndicator[] roots = [repeated, new DynamicPivotPoints(), repeated];
        await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
        bars[^1] = new(day, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
    }

    private static Bar[] ToBars(double[] prices) => prices.Select((v, i) => new Bar(default,
        i % 5 == 0 ? 0 : v / 4, Math.Abs(v), i % 7 == 0 ? 0 : Math.Abs(v) / 2, v, 1)).ToArray();

    private static async Task Compare(IIndicator[] roots, Bar[] bars)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).ConfigureHistory(history);
            IIndicatorRun? expected = null;
            try
            {
                var expectedError = await Record.ExceptionAsync(async () => expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync());
                var builder = Builder();
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    IIndicatorRun? actual = null;
                    try
                    {
                        var actualError = await Record.ExceptionAsync(async () => actual = await builder.BuildAsync());
                        Assert.Equal(expectedError?.GetType(), actualError?.GetType());
                        Assert.Equal(expectedError?.Message, actualError?.Message);
                        if (expectedError is not null) { Assert.Null(builder.LastExecution); continue; }
                        Assert.NotNull(expected); Assert.NotNull(actual);
                        if (history == IndicatorHistoryMode.Full && ValuesBarExecution.SupportsOwned(roots))
                            Assert.False(((IndicatorRun)actual).HasLegacyRuntime);
                        foreach (var root in roots) foreach (var output in root.Outputs)
                            Bits(expected[output].ToArray(), actual[output].ToArray());
                        if (bars.Length > 0) { Assert.Equal(expected.Latest.Bar, actual.Latest.Bar); Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp); }
                        actual.Dispose();
                        foreach (var root in roots) foreach (var output in root.Outputs)
                            Bits(expected[output].ToArray(), actual[output].ToArray());
                    }
                    finally { actual?.Dispose(); }
                }
            }
            finally { expected?.Dispose(); }
        }
    }

    private static void Bits(double[] expected, double[] actual) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
