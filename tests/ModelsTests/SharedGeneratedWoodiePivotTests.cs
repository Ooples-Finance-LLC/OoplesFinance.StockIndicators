using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedWoodiePivotTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(WoodiePivotPoint)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => new WoodiePivotPoint();
    [Fact]
    public async Task WholeHistoryRetainsAllOutputBits()
    {
        var root = new WoodiePivotPoint(); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
            Enumerable.Repeat(0d, 31).Concat(Enumerable.Repeat(1d, 31)).ToArray(),
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], ToBars(prices).Select((bar, i) => new Bar(new DateTime(2026, 1, 1).AddDays(i), bar.Open, bar.High, bar.Low, bar.Close, bar.Volume)).ToArray());
    }

    [Fact]
    public async Task CompletedDayKeepsAllThirteenHandLevels()
    {
        var day = new DateTime(2026, 1, 1);
        Bar[] bars = [new(day.AddHours(9), 6, 10, 4, 6, 1), new(day.AddHours(16), 8, 12, 2, 8, 1),
            new(day.AddDays(1), 0, 100, -100, 0, 1), new(day.AddDays(1).AddHours(1), 0, 200, -200, 0, 1)];
        var root = new WoodiePivotPoint(); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        double[] levels = [7.5, 3, -2.5, -7, -17, 13, 17.5, 23, 33, .25, 5.25, 10.25, 15.25];
        for (var slot = 0; slot < levels.Length; slot++) Bits([0, 0, levels[slot], levels[slot]], run[root.Outputs[slot]].ToArray());
    }
    [Fact]
    public async Task FirstDayKeepsZeroLevels()
    {
        var root = new WoodiePivotPoint(); var bars = ToBars([1, 3, 2]); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        foreach (var output in root.Outputs) Bits([0, 0, 0], run[output].ToArray());
    }
    [Fact]
    public async Task DateChangesAndGapsKeepLegacySequence()
    {
        var day = new DateTime(2026, 1, 5);
        Bar[] bars = [new(day, 1, 2, 0, 1, 1), new(day.AddDays(3), 2, 4, -1, 3, 1),
            new(day.AddDays(1), 5, 7, 2, 4, 1), new(day.AddDays(1).AddHours(1), 4, 6, 1, 2, 1)];
        await Compare([new WoodiePivotPoint()], bars);
    }
    [Fact]
    public void OutputOrderKeepsPivotPrimary()
    {
        var root = new WoodiePivotPoint(); Assert.Equal(13, root.Outputs.Count);
        Assert.Equal(new[] { root.Value, root.S1, root.S2, root.S3, root.S4, root.R1, root.R2, root.R3, root.R4, root.M1, root.M2, root.M3, root.M4 }, root.Outputs);
        Assert.Same(root.Value, root.PrimaryOutput);
    }
    [Theory] [InlineData(0)]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts(int kind)
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = Create(kind, 3) switch { IndicatorBase scalar => (IIndicator)scalar.Of(new Sma(2)), MultiOutputIndicatorBase multi => multi.Of(new Sma(2)), _ => throw new InvalidOperationException() }; Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        var repeated = Create(kind, 3); IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }
    [Theory] [InlineData(0)]
    public void BothKernelsReplayAfterReset(int kind)
    {
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(new DateTime(2026, 1, 1).AddHours(i * 6), i, i + 2, i - 2, i + 1, 1)).ToArray();
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedWoodiePivotState", System.Reflection.BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, Array.Empty<object>(), null)!;
        double[] Run() { var values = new List<double>(); foreach (var bar in bars) { if (state is IIndicatorState scalar) values.Add(scalar.Update(in bar)); else { var output = new double[13]; ((IMultiOutputState)state).Update(in bar, output); values.AddRange(output); } } return values.ToArray(); }
        try { var expected = Run(); if (state is IIndicatorState scalar) scalar.Reset(); else ((IMultiOutputState)state).Reset(); Bits(expected, Run()); } finally { ((IDisposable)state).Dispose(); }
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
