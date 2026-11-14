using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedMacdAliasesTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(MacdLine) || c.IndicatorType == typeof(MacdSignal)
            || c.IndicatorType == typeof(MacdHistogram)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch {
        0 => new MacdLine(length, 26), 1 => new MacdSignal(length, 26, 9),
        _ => new MacdHistogram(length, 26, 9) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 3)
        from length in new[] { int.MinValue, 1, 2, 20, 65, int.MaxValue } select new object[] { kind, length };

    [Theory, MemberData(nameof(Cases))]
    public async Task AliasPeriodsRetainBits(int kind, int length)
    {
        var root = Create(kind, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
            Enumerable.Repeat(0d, 31).Concat(Enumerable.Repeat(1d, 31)).ToArray(),
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], ToBars(prices));
    }

    [Fact]
    public async Task LineAliasRetainsThreeSlotsAndFixedSignalPeriod()
    {
        var alias = new MacdLine(12, 26); var full = new Macd(12, 26, 9);
        Assert.Equal(3, alias.Outputs.Count);
        Assert.Single(new MacdSignal(12, 26, 9).Outputs);
        Assert.Single(new MacdHistogram(12, 26, 9).Outputs);
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(alias, full).BuildAsync();
        for (var i = 0; i < 3; i++) Bits(run[full.Outputs[i]].ToArray(), run[alias.Outputs[i]].ToArray());
    }

    [Fact]
    public async Task ExplicitSourcesRetainFallback()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new MacdLine(10, 26).Of(new Sma(2)),
            new MacdSignal(20, 26, 9).Of(new Sma(2)), new MacdHistogram(20, 26, 9).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
    }

    [Fact]
    public async Task RepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var repeated = Create(2, 20);
        IIndicator[] roots = [repeated, Create(0, 10), Create(1, 9), Create(2, 20), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public async Task OverflowInvalidatesSignalUntilReset()
    {
        var bars = ToBars([double.MaxValue, double.MaxValue, -double.MaxValue, 0, 1, 0, -1]);
        var line = new MacdLine(1, int.MaxValue);
        var signal = new MacdSignal(1, int.MaxValue, 9);
        var histogram = new MacdHistogram(1, int.MaxValue, 9);
        await Compare([line, signal, histogram], bars);
        foreach (var root in new IIndicator[] { line, signal, histogram })
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
            var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Equal(root.GetType(), error.IndicatorType);
            Assert.Equal(0, error.OutputSlot); Assert.Equal(2, error.BarIndex);
            Assert.Equal(IndicatorStartupPolicy.Finite, error.Policy); Assert.Null(builder.LastExecution);
            if (root == line) Assert.True(double.IsNegativeInfinity(error.Value));
            else Assert.True(double.IsNaN(error.Value));
        }
        // Keep raw propagation and reset checks separate from public rejection.
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedEmaGapSignalState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IMultiOutputState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { 1, int.MaxValue, 9, false }, null)!;
        for (var replay = 0; replay < 2; replay++)
        {
            for (var i = 0; i < bars.Length; i++)
            {
                var output = new double[3]; state.Update(in bars[i], output);
                if (i == 2) Assert.True(double.IsNegativeInfinity(output[0]));
                if (i < 2) Assert.True(double.IsFinite(output[1]));
                else { Assert.True(double.IsNaN(output[1])); Assert.True(double.IsNaN(output[2])); }
            }
            state.Reset();
        }
    }

    [Theory]
    [InlineData(1, 26, 9)] [InlineData(12, 1, 9)] [InlineData(12, 26, 1)]
    [InlineData(int.MaxValue, 2, 3)] [InlineData(2, int.MaxValue, int.MaxValue)]
    public async Task IndependentPeriodsRetainBits(int fast, int slow, int signal)
    {
        var bars = ToBars(Enumerable.Range(0, 33).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        await Compare([new MacdLine(fast, slow), new MacdSignal(fast, slow, signal), new MacdHistogram(fast, slow, signal)], bars);
    }

    [Fact]
    public void ScalarStatesClearInvalidSignalOnReset()
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedMacdAliasState", System.Reflection.BindingFlags.NonPublic)!;
        foreach (var slot in new[] { 1, 2 })
        {
            var state = (IIndicatorState)Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { 1, int.MaxValue, 9, slot }, null)!;
            var bars = ToBars([double.MaxValue, double.MaxValue, -double.MaxValue, 0, 1, 0, -1]);
            double[] Run() { var result = new List<double>(); foreach (var bar in bars) result.Add(state.Update(in bar)); return result.ToArray(); }
            var expected = Run(); state.Reset(); Bits(expected, Run());
        }
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
