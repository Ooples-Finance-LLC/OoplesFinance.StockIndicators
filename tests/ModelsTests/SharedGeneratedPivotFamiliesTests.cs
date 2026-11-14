using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedPivotFamiliesTests
{
    private static MultiOutputIndicatorBase Create(int kind) => kind switch
    {
        0 => new CamarillaPivotPoint(),
        1 => new FibonacciPivotPoint(),
        2 => new DemarkPivotPoint(),
        3 => new FloorPivotPoint(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(CamarillaPivotPoint) || c.IndicatorType == typeof(FibonacciPivotPoint) || c.IndicatorType == typeof(DemarkPivotPoint) || c.IndicatorType == typeof(FloorPivotPoint)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    [Theory, InlineData(0), InlineData(1), InlineData(2), InlineData(3)]
    public async Task DailyTransitionsRetainEveryOutputBit(int kind)
    {
        var root = Create(kind); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        var day = new DateTime(2026, 1, 1);
        Bar[][] cases = [[], [new(day, 1, 2, 0, 1, 1)],
            Enumerable.Range(0, 65).Select(i => new Bar(day.AddHours(i * 6), i, i + 2, i - 2, i + 1, 1)).ToArray(),
            [new(day, 1, 2, 0, 1, 1), new(day.AddDays(3), 2, 4, -1, 3, 1), new(day.AddDays(1), 5, 7, 2, 4, 1)],
            [new(day, -double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, 1), new(day.AddDays(1), 0, 1, -1, 0, 1)],
            [new(day, -0d, 0, -0d, -0d, 1), new(day.AddDays(1), 0, double.Epsilon, -double.Epsilon, 0, 1)]];
        foreach (var bars in cases) await Compare([root], bars);
    }

    [Theory, InlineData(0), InlineData(1), InlineData(2), InlineData(3)]
    public void ExactStateReplaysAfterReset(int kind)
    {
        var day = new DateTime(2026, 1, 1);
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(day.AddHours(i * 6), i, i + 2, i - 2, i + 1, 1)).ToArray();
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedPivotFamiliesState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IMultiOutputState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { kind }, null)!;
        double[] Run() { var result = new List<double>(); var outputs = new double[Create(kind).Outputs.Count]; foreach (var bar in bars) { state.Update(in bar, outputs); result.AddRange(outputs); } return result.ToArray(); }
        try { var expected = Run(); state.Reset(); Bits(expected, Run()); } finally { ((IDisposable)state).Dispose(); }
    }

    [Theory, InlineData(0, 17), InlineData(1, 13), InlineData(2, 3), InlineData(3, 13)]
    public void OutputSlotsKeepPivotPrimary(int kind, int expectedCount)
    {
        var root = Create(kind);
        Assert.Equal(expectedCount, root.Outputs.Count);
        Assert.Same(root.Outputs[0], root.PrimaryOutput);
    }

    [Fact]
    public async Task CustomSourcesErrorsAndCancellationKeepContracts()
    {
        var day = new DateTime(2026, 1, 1);
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(day.AddHours(i * 6), i, i + 2, i - 2, i + 1, 1)).ToArray();
        foreach (var kind in new[] { 0, 1, 2, 3 })
        {
            var selected = Create(kind).Of(new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        }
        var repeated = new CamarillaPivotPoint(); IIndicator[] roots = [repeated, new FibonacciPivotPoint(), new DemarkPivotPoint(), new FloorPivotPoint(), repeated];
        await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
        bars[^1] = new(day, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
    }

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
