using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedNaturalMarketTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(NaturalMarketMirror) || c.IndicatorType == typeof(NaturalMarketRiver) || c.IndicatorType == typeof(NaturalMarketCombo)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IndicatorBase Create(int kind, int length) => kind switch { 0 => new NaturalMarketMirror(length), 1 => new NaturalMarketRiver(length), _ => new NaturalMarketCombo(length) };
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2 } from length in new[] { int.MinValue, 1, 2, 3, 14, 65, 257 } select new object[] { kind, length };

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

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task CustomAverageKeepsFallback(int kind)
    { IIndicator root = kind switch { 0 => new NaturalMarketMirror(3, new Sma(2)), 1 => new NaturalMarketRiver(3, new Sma(2)), _ => new NaturalMarketCombo(3, 2, new Sma(2)) }; Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 3, -2, 4, 0, 2, -1])); }
    [Fact]
    public async Task UnitWindowKeepsMirrorDivisionAndRiverDifferences()
    {
        var mirror = new NaturalMarketMirror(1); var river = new NaturalMarketRiver(1); var bars = ToBars([2, 1, 2]); await Compare([mirror, river], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(mirror, river).BuildAsync();
        var logarithm = Math.Log(2) * 1000; Bits([logarithm * 100, -logarithm * 100, logarithm * 100], run[mirror.Value].ToArray()); Bits([logarithm, -logarithm, logarithm], run[river.Value].ToArray());
    }
    [Theory] [InlineData(int.MinValue)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(257)]
    public async Task ComboPublishesUnsmoothedValues(int smooth)
    {
        var root = new NaturalMarketCombo(3, smooth); var reference = new NaturalMarketCombo(3, 1); var bars = ToBars([1, 3, 2, 4, 0, 2, -1]); await Compare([root, reference], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root, reference).BuildAsync(); Bits(run[reference.Value].ToArray(), run[root.Value].ToArray());
    }
    [Fact]
    public async Task MaximumSignalPeriodKeepsUnsmoothedOneBarValue()
    {
        var root = new NaturalMarketCombo(1, int.MaxValue); var reference = new NaturalMarketCombo(1, 1);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars([2]))).ConfigureIndicators(root, reference).BuildAsync(); Bits(run[reference.Value].ToArray(), run[root.Value].ToArray());
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task CachedCoefficientBoundaryKeepsLongHistory(int kind)
    { foreach(var length in new[] { 11, 256, 257 }) await Compare([Create(kind, length)], ToBars(Enumerable.Range(0, 270).Select(i => 2 + Math.Sin(i * .7)).ToArray())); }
    [Fact]
    public void OutputsRemainScalar() { Assert.Single(new NaturalMarketMirror().Outputs); Assert.Single(new NaturalMarketRiver().Outputs); Assert.Single(new NaturalMarketCombo().Outputs); }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts(int kind)
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = Create(kind, 3).Of(new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        var repeated = Create(kind, 3); IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void BothKernelsReplayAfterReset(int kind)
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        var type = typeof(ValuesBarExecution).GetNestedType(kind == 2 ? "GeneratedNaturalMarketComboState" : "GeneratedNaturalMarketState", System.Reflection.BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, kind == 2 ? new object[] { 3, 2, 65 } : new object[] { 3, 65, kind == 0, false }, null)!;
        double[] Run() { var values = new List<double>(); foreach (var bar in bars) { if (state is IIndicatorState scalar) values.Add(scalar.Update(in bar)); else { var output = new double[2]; ((IMultiOutputState)state).Update(in bar, output); values.AddRange(output); } } return values.ToArray(); }
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
