using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedWavePairTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(ElliottWaveOscillator) || c.IndicatorType == typeof(EmaWaveIndicator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind == 0 ? new ElliottWaveOscillator(length)
        : new EmaWaveIndicator(length);
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 2)
        from length in new[] { int.MinValue, 1, 2, 14, 65, 1024 } select new object[] { kind, length };

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

    [Theory]
    [InlineData(1, 2, 3, 1)] [InlineData(2, 1, 3, 2)] [InlineData(2, 3, 1, 3)] [InlineData(5, 25, 50, 4)]
    [InlineData(65, 2, 3, 5)] [InlineData(2, 65, 3, 5)] [InlineData(2, 3, 65, 5)] [InlineData(2, 3, 5, 65)]
    public async Task IndependentWavePeriodsKeepAllSlots(int first, int second, int third, int smooth)
    {
        await Compare([new ElliottWaveOscillator(first, second), new EmaWaveIndicator(first, second, third, smooth)], ToBars(Enumerable.Range(0, 97).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task ElliottAveragePrefixesKeepFallback(int count)
    {
        var root = new ElliottWaveOscillator(2, 3, new Ema(2), count > 1 ? new Ema(3) : null, count > 2 ? new Ema(2) : null);
        Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 2, -3, 4, 0, 2, -2, 0, 1, 3, -1, 4]));
    }

    [Fact]
    public void ThreeOutputsKeepNamesAndOrder()
    {
        var elliott = new ElliottWaveOscillator(); var ema = new EmaWaveIndicator();
        Assert.Equal(3, elliott.Outputs.Count); Assert.Equal(3, ema.Outputs.Count);
        Assert.Same(elliott.Outputs[0], elliott.Value); Assert.Same(elliott.Outputs[1], elliott.Signal); Assert.Same(elliott.Outputs[2], elliott.Histogram);
        Assert.Same(ema.Outputs[0], ema.Value); Assert.Same(ema.Outputs[1], ema.Wb); Assert.Same(ema.Outputs[2], ema.Wc);
    }

    [Fact]
    public async Task HandValuesKeepFullWindowSmaAndRoundedResiduals()
    {
        var elliott = new ElliottWaveOscillator(1, 2); var ema = new EmaWaveIndicator(2, 2, 2, 1);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars([1, 2, 3]))).ConfigureIndicators(elliott, ema).BuildAsync();
        Bits([1, .5, .5], run[elliott.Outputs[0]].ToArray()); Bits([1, .5, .5], run[elliott.Outputs[1]].ToArray()); Bits([0, 0, 0], run[elliott.Outputs[2]].ToArray());
        foreach (var output in ema.Outputs) Bits([0, .5, .5], run[output].ToArray());
    }

    [Fact]
    public async Task ElliottOverflowInvalidatesSignalUntilReset()
    {
        var root = new ElliottWaveOscillator(1, 3);
        var bars = ToBars([-double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, 0, -1]);
        await Compare([root], bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
        Assert.Equal(0, error.OutputSlot); Assert.Equal(2, error.BarIndex);
        Assert.True(double.IsPositiveInfinity(error.Value)); Assert.Null(builder.LastExecution);

        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedElliottWaveState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IMultiOutputState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { 1, 3, bars.Length }, null)!;
        try
        {
            var outputs = new double[3];
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], outputs);
                if (i == 2) Assert.True(double.IsPositiveInfinity(outputs[0]));
                if (i < 2) Assert.True(double.IsFinite(outputs[1]));
                else { Assert.True(double.IsNaN(outputs[1])); Assert.True(double.IsNaN(outputs[2])); }
            }
            state.Reset();
            var first = ToBars([1])[0]; state.Update(in first, outputs);
            Bits([1, 1, 0], outputs);
        }
        finally { ((IDisposable)state).Dispose(); }
    }

    [Fact]
    public async Task HugeSmoothingPeriodsKeepZeroStartupWithoutPeriodSizedBuffers()
    {
        IIndicator[] roots = [new ElliottWaveOscillator(int.MaxValue, int.MaxValue), new EmaWaveIndicator(2, 3, 5, int.MaxValue)];
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars([1, 2, -3, 4]))).ConfigureIndicators(roots).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime);
        foreach (var root in roots) foreach (var output in root.Outputs) Bits([0, 0, 0, 0], run[output].ToArray());
    }

    [Fact]
    public async Task LongHistoryCrossesAllDefaultPeriods()
    {
        await Compare([new ElliottWaveOscillator(), new EmaWaveIndicator()], ToBars(Enumerable.Range(0, 129).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new ElliottWaveOscillator(2, 3).Of(new Sma(2)), new EmaWaveIndicator().Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(0, 3); IIndicator[] roots = [repeated, Create(1, 14), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void StatesReplayAfterReset(bool ema)
    {
        var bars = ToBars([-double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, 0, -1]);
        var name = ema ? "GeneratedEmaWaveState" : "GeneratedElliottWaveState";
        var type = typeof(ValuesBarExecution).GetNestedType(name, System.Reflection.BindingFlags.NonPublic)!;
        object[] args = ema ? [2, 3, 5, 2, bars.Length] : [1, 3, bars.Length];
        var state = (IMultiOutputState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, args, null)!;
        double[] Run() { var values = new List<double>(); var outputs = new double[3]; foreach (var bar in bars) { state.Update(in bar, outputs); values.AddRange(outputs); } return values.ToArray(); }
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
