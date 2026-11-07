using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedWilliamsFractalsTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(WilliamsFractalUp) || c.IndicatorType == typeof(WilliamsFractalDown) || c.IndicatorType == typeof(WilliamsFractalsUp) || c.IndicatorType == typeof(WilliamsFractalsDown) || c.IndicatorType == typeof(WilliamsFractals)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch { 0 => new WilliamsFractalUp(length), 1 => new WilliamsFractalDown(length), 2 => new WilliamsFractalsUp(length), 3 => new WilliamsFractalsDown(length), 4 => new WilliamsFractals(length), _ => throw new ArgumentOutOfRangeException(nameof(kind)) };
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2, 3, 4 } from length in new[] { int.MinValue, 1, 2, 3, 14, 65 } select new object[] { kind, length };

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

    private static Bar[] Mirrored(double[] highs) => highs.Select(v => new Bar(default, 0, v, -v, 0, 1)).ToArray();
    [Fact]
    public async Task StrictPeakAndTroughWaitForTwoConfirmations()
    {
        var bars = Mirrored([1, 2, 5, 2, 1]); var roots = Enumerable.Range(0, 5).Select(k => Create(k, 2)).ToArray(); await Compare(roots, bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).BuildAsync();
        foreach (var root in roots) Bits([0, 0, 0, 0, 1], run[root.Outputs[0]].ToArray());
    }
    [Fact]
    public async Task ExactPlateauWaitsForItsLastEqualPeak()
    {
        var bars = Mirrored([1, 2, 5, 5, 2, 1]); var roots = Enumerable.Range(0, 5).Select(k => Create(k, 2)).ToArray(); await Compare(roots, bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).BuildAsync();
        foreach (var root in roots) Bits([0, 0, 0, 0, 0, 1], run[root.Outputs[0]].ToArray());
    }
    [Fact]
    public async Task LongerConfiguredDelayKeepsConfirmationBar()
    {
        var bars = Mirrored([1, 2, 5, 2, 1, 0]); var roots = Enumerable.Range(0, 5).Select(k => Create(k, 3)).ToArray(); await Compare(roots, bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).BuildAsync();
        foreach (var root in roots) Bits([0, 0, 0, 0, 0, 1], run[root.Outputs[0]].ToArray());
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task MaximumDelayKeepsBoundedOwnedStorage(int kind)
    {
        var root = Create(kind, int.MaxValue); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(Mirrored([1, 2, 5, 2, 1]))).ConfigureIndicators(root).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime); Bits([0, 0, 0, 0, 0], run[root.Outputs[0]].ToArray());
    }
    [Fact]
    public async Task DownAliasesUseLowsInsteadOfHighs()
    {
        var bars = new double[] { 1, 2, 5, 2, 1 }.Select(v => new Bar(default, 0, v, 0, 0, 1)).ToArray();
        var roots = Enumerable.Range(0, 5).Select(k => Create(k, 2)).ToArray(); await Compare(roots, bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).BuildAsync();
        for (var kind = 0; kind < 5; kind++) Bits([0, 0, 0, 0, kind is 1 or 3 ? 0 : 1], run[roots[kind].Outputs[0]].ToArray());
    }
    [Fact]
    public void AllAliasesRemainScalar()
    { foreach (var kind in Enumerable.Range(0, 5)) Assert.Single(Create(kind, 2).Outputs); }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts(int kind)
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = Create(kind, 3) switch { IndicatorBase scalar => (IIndicator)scalar.Of(new Sma(2)), MultiOutputIndicatorBase multi => multi.Of(new Sma(2)), _ => throw new InvalidOperationException() }; Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        var repeated = Create(kind, 3); IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void BothKernelsReplayAfterReset(int kind)
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedWilliamsFractalState", System.Reflection.BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new object[] { 3, bars.Length, kind is not (1 or 3) }, null)!;
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
