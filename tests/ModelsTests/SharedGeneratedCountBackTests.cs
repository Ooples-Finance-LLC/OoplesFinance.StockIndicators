using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedCountBackTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(GuppyCountBackLine)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => new GuppyCountBackLine(length);
    public static IEnumerable<object[]> Cases => new[] { int.MinValue, 1, 2, 3, 14, 65, 1_073_741_824 }.Select(length => new object[] { 0, length });

    [Theory, MemberData(nameof(Cases))]
    public async Task AliasPeriodsRetainAllOutputBits(int kind, int length)
    {
        var root = Create(kind, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
            Enumerable.Repeat(0d, 31).Concat(Enumerable.Repeat(1d, 31)).ToArray(),
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], ToBars(prices));
    }

    [Theory] [InlineData(1_073_741_825)] [InlineData(int.MaxValue)]
    public async Task OverflowSensitiveLegacyOffsetsKeepFallback(int length)
    { var root = new GuppyCountBackLine(length); Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 7, 2, -3, 4, 0, 2])); }

    [Fact]
    public async Task OutsidePivotTieCountsBackBeyondCurrentLookback()
    {
        var root = new GuppyCountBackLine(3);
        Bar[] bars = [new(default, 0, 4, 0, 2, 1), new(default, 0, 5, 3, 4, 1), new(default, 0, 6, 4, 5, 1), new(default, 0, 10, 5, 7, 1), new(default, 0, 9, 6, 8, 1), new(default, 0, 8, 7, 7.5, 1)];
        await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([2, 4, 0, 3, 3, 3], run[root.Outputs[0]].ToArray());
    }

    private static IIndicatorState State(int length, int count)
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedCountBackState", System.Reflection.BindingFlags.NonPublic)!;
        return (IIndicatorState)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { length, count }, null)!;
    }
    private static double[] Reference(Bar[] bars, int length) => bars.Select((bar, i) =>
    {
        var indices = Enumerable.Range(Math.Max(0, i - length + 1), Math.Min(i + 1, length)).Reverse().ToArray();
        var top = indices.OrderByDescending(j => bars[j].High).First(); var bottom = indices.OrderBy(j => bars[j].Low).First();
        var rising = top >= bottom; var pivot = Math.Max(top, bottom); double Level(int j) => rising ? bars[j].Low : -bars[j].High;
        var records = Enumerable.Range(Math.Max(0, pivot - length), Math.Min(pivot, length)).Reverse()
            .Where(j => Enumerable.Range(j + 1, pivot - j).All(k => Level(j) < Level(k))).Take(2).ToArray();
        return records.Length < 2 ? bar.Close : rising ? bars[records[1]].Low : bars[records[1]].High;
    }).ToArray();

    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(5)] [InlineData(9)]
    public void ExhaustiveShortHistoriesKeepStrictRecordsLatestTiesAndSignedZero(int length)
    {
        Bar[] choices = [new(default, 0, 0, -0d, -0d, 1), new(default, 0, 1, 0, .5, 1), new(default, 0, 0, -1, -.5, 1), new(default, 0, 1, -1, 0, 1), new(default, 0, 2, 1, 1.5, 1)];
        var state = State(length, 5);
        try
        {
            for (var pattern = 0; pattern < 3125; pattern++)
            {
                var value = pattern; var bars = new Bar[5];
                for (var i = 0; i < bars.Length; i++) { bars[i] = choices[value % choices.Length]; value /= choices.Length; }
                state.Reset(); var actual = new double[bars.Length]; for (var i = 0; i < bars.Length; i++) actual[i] = state.Update(in bars[i]);
                Bits(Reference(bars, length), actual);
            }
        }
        finally { ((IDisposable)state).Dispose(); }
    }

    [Fact]
    public void OutputRemainsScalar() => Assert.Single(new GuppyCountBackLine().Outputs);

    [Fact]
    public async Task LongHistoryCrossesBothRetainedWindows()
    { await Compare([new GuppyCountBackLine(3), new GuppyCountBackLine(11), new GuppyCountBackLine(65)], ToBars(Enumerable.Range(0, 257).Select(i => Math.Sin(i * .7)).ToArray())); }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = new GuppyCountBackLine(3).Of(new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        var repeated = new GuppyCountBackLine(3); IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void EveryLinkAndDequeReplaysAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray()); var state = State(3, bars.Length);
        double[] Run() { var values = new List<double>(); foreach (var bar in bars) values.Add(state.Update(in bar)); return values.ToArray(); }
        try { var expected = Run(); state.Reset(); Bits(expected, Run()); } finally { ((IDisposable)state).Dispose(); }
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
