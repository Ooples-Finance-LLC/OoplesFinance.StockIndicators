using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedSignalAliasesTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(KlingerSignal) || c.IndicatorType == typeof(ChandeMomentumOscillatorSignal)
            || c.IndicatorType == typeof(MassIndexCore)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch {
        0 => new KlingerSignal(length), 1 => new ChandeMomentumOscillatorSignal(length),
        _ => new MassIndexCore(length) };
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
    public async Task OutputCountsAndFixedMassSmoothingRemainUnchanged()
    {
        Assert.Single(new KlingerSignal(20).Outputs);
        Assert.Single(new ChandeMomentumOscillatorSignal(20).Outputs);
        var alias = new MassIndexCore(25); var full = new MassIndex(21, 25);
        Assert.Equal(2, alias.Outputs.Count);
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(alias, full).BuildAsync();
        for (var i = 0; i < 2; i++) Bits(run[full.Outputs[i]].ToArray(), run[alias.Outputs[i]].ToArray());
    }

    [Fact]
    public async Task ExplicitSourcesRetainFallback()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new KlingerSignal(10).Of(new Sma(2)),
            new ChandeMomentumOscillatorSignal(20).Of(new Sma(2)), new MassIndexCore(20).Of(new Sma(2)) })
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
    public async Task OriginalHighLowAndVolumeRetainBits()
    {
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 100 + i,
            15 + i % 7, 1 + i % 3, 4 + i % 5, i % 9 == 0 ? 0 : 100 + i * 3)).ToArray();
        foreach (var root in new IIndicator[] { Create(0, 13), Create(1, 14), Create(2, 20) })
            await Compare([root], bars);
    }

    [Theory]
    [InlineData(1, 55, 13)] [InlineData(34, 1, 13)] [InlineData(34, 55, 1)]
    [InlineData(int.MaxValue, 2, 3)] [InlineData(2, int.MaxValue, int.MaxValue)]
    public async Task IndependentPeriodsRetainSignals(int fast, int slow, int signal)
    {
        var bars = ToBars(Enumerable.Range(0, 33).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        await Compare([new KlingerSignal(fast, slow, signal), new ChandeMomentumOscillatorSignal(fast, signal)], bars);
    }

    [Fact]
    public async Task ExplicitChandeAverageRetainsFallback()
    {
        var root = new ChandeMomentumOscillatorSignal(14, 3, new Sma(3));
        Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]));
    }

    [Fact]
    public void SignalStatesReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 33).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        foreach (var config in new[] {
            (Name: "GeneratedKlingerSignalState", Arguments: new object[] { 34, 55, 13 }),
            (Name: "GeneratedChandeSignalState", Arguments: new object[] { 14, bars.Length, 3 }) })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(config.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = (IIndicatorState)Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, config.Arguments, null)!;
            double[] Run() { var result = new List<double>(); foreach (var bar in bars) result.Add(state.Update(in bar)); return result.ToArray(); }
            try { var expected = Run(); state.Reset(); Bits(expected, Run()); }
            finally { ((IDisposable)state).Dispose(); }
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
