using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedEnhancedIndexTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EnhancedIndex)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int length) => new EnhancedIndex(length);
    public static IEnumerable<object[]> Cases => from length in new[] { int.MinValue, 1, 2, 14, 65, 1024 } select new object[] { length };

    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsRetainAllOutputBits(int length)
    {
        var root = Create(length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
            Enumerable.Repeat(0d, 31).Concat(Enumerable.Repeat(1d, 31)).ToArray(),
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(1, 1)] [InlineData(2, 65)] [InlineData(65, 2)] [InlineData(14, 8)] [InlineData(int.MaxValue, int.MaxValue)]
    public async Task IndependentIndexSignalAndMeanClampKeepBits(int length, int signal)
    {
        await Compare([new EnhancedIndex(length, signal)], ToBars(Enumerable.Range(0, 97).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Fact]
    public async Task ExplicitAverageKeepsFallback()
    {
        IIndicator root = new EnhancedIndex(3, 2, new Ema(2));
        Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 2, -3, 4, 0, 2, -2, 0, 1, 3, -1, 4]));
    }

    [Fact]
    public void PairedIndexKeepsShape()
    {
        var root = new EnhancedIndex(); Assert.Equal(2, root.Outputs.Count);
        Assert.Same(root.Outputs[0], root.Value); Assert.Same(root.Outputs[1], root.Signal);
    }

    [Fact]
    public async Task OriginalRangeKeepsHandValues()
    {
        var index = new EnhancedIndex(2, 1);
        Bar[] bars = [new(default, 99, 2, 0, 1, 1), new(default, 99, 2, 0, 1, 1)];
        await Compare([index], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(index).BuildAsync();
        Bits([1, 0], run[index.Outputs[0]].ToArray()); Bits([1, 0], run[index.Outputs[1]].ToArray());
    }

    [Fact]
    public async Task LongHistoryCrossesMeanAndRangeExpiration()
    {
        var bars = Enumerable.Range(0, 129).Select(i => new Bar(default, 100 + i, 15 + i % 7, 1 + i % 3, 4 + i % 5, i + 1)).ToArray();
        await Compare([new EnhancedIndex(14, 8)], bars);
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new EnhancedIndex(3).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(3); IIndicator[] roots = [repeated, Create(14), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void StatesReplayAfterReset()
    {
        var bars = ToBars([1, 2, 3, 4, -2, 0, 1]);
        var name = "GeneratedEnhancedIndexState";
        var type = typeof(ValuesBarExecution).GetNestedType(name, System.Reflection.BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { 3, 2 }, null)!;
        double[] Run() { var values = new List<double>(); var outputs = new double[2]; foreach (var bar in bars) {
            if (state is IMultiOutputState multi) { multi.Update(in bar, outputs); values.AddRange(outputs); }
            else values.Add(((IIndicatorState)state).Update(in bar)); } return values.ToArray(); }
        try { var expected = Run(); if (state is IMultiOutputState multi) multi.Reset(); else ((IIndicatorState)state).Reset(); Bits(expected, Run()); }
        finally { ((IDisposable)state).Dispose(); }
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
