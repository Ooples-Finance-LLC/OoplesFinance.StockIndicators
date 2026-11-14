using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedGOscillatorsTests
{
    private static IndicatorBase Create(int kind, int length) => kind == 0 ? new GOscillator(length) : new GannSwingOscillator(length);
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1 } from length in new[] { int.MinValue, 0, 1, 2, 14, 32 } select new object[] { kind, length };
    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsKeepBitsErrorsAndOwnership(int kind, int length)
    {
        var root = Create(kind, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, 2, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 2, 4, 1],
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], BarsOf(prices));
    }
    [Theory, InlineData(0), InlineData(1)]
    public async Task SourcesDuplicatesErrorsAndCancellationKeepContracts(int kind)
    {
        var root = Create(kind, 14); var selected = Create(kind, 14).Of(new Sma(2));
        Assert.False(ValuesBarExecution.SupportsOwned([selected])); var bars = BarsOf([1, 3, 3, 1, 2]);
        await Compare([selected], bars);
        IIndicator[] roots = [root, Create(kind, 3), root]; Assert.True(ValuesBarExecution.SupportsOwned(roots));
        await Compare(roots, bars);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare([root], [new(default, 1, 2, 0, 1, invalid)]);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }
    [Theory, InlineData(0), InlineData(1)]
    public void ResetReplaysTheNumericalWindow(int kind)
    {
        var type = typeof(ValuesBarExecution).GetNestedType(kind == 0 ? "GeneratedGOscillatorState" : "GeneratedGannTrendState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, kind == 0 ? new object[] { 14, 5 } : new object[] { 14 }, null)!;
        var state = (IIndicatorState)disposable;
        double[] Run() => BarsOf([1, 3, 3, 1, 2]).Select(bar => state.Update(in bar)).ToArray();
        var expected = Run(); state.Reset(); Bits(expected, Run());
    }
    [Theory, InlineData(1), InlineData(14), InlineData(32)]
    public async Task GOscillatorKeepsPrefixBitsAcrossRepeatedWraps(int length)
    {
        var prices = Enumerable.Range(0, 1025).Select(i => Math.Sin(i * .7)).ToArray();
        await Compare([new GOscillator(length)], BarsOf(prices));
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedGOscillatorState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { length, prices.Length }, null)!;
        var state = (IIndicatorState)disposable;
        double[] Run() => BarsOf(prices).Select(bar => state.Update(in bar)).ToArray();
        var expected = Run(); state.Reset(); Bits(expected, Run());
    }
    [Theory, InlineData(0), InlineData(1)]
    public async Task MaximumPeriodUsesOnlyObservedStorage(int kind)
    {
        var type = typeof(ValuesBarExecution).GetNestedType(kind == 0 ? "GeneratedGOscillatorState" : "GeneratedGannTrendState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null,
            kind == 0 ? new object[] { int.MaxValue, 5 } : new object[] { 5 }, null)!;
        var state = (IIndicatorState)disposable;
        var w = 100.0 / int.MaxValue;
        var expected = kind == 0 ? new[] { w, w + w, w + w, w + w, w + w + w } : new double[5];
        double[] Run() => BarsOf([1, 3, 3, 1, 2]).Select(bar => state.Update(in bar)).ToArray();
        Bits(expected, Run()); state.Reset(); Bits(expected, Run());
        var root = Create(kind, int.MaxValue);
        using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsOf([1, 3, 3, 1, 2])))
            .ConfigureIndicators(root).ConfigureHistory(IndicatorHistoryMode.Full).BuildAsync();
        Assert.False(((IndicatorRun)actual).HasLegacyRuntime);
        Bits(expected, actual[root.Value].ToArray());
    }
    private static Bar[] BarsOf(double[] prices) => prices.Select(v => new Bar(default, v / 2, Math.Abs(v), -Math.Abs(v), v, 1)).ToArray();
    private static async Task Compare(IIndicator[] roots, Bar[] bars)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(roots).ConfigureHistory(history);
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
                        if (bars.Length > 0)
                        {
                            Assert.Equal(expected.Latest.Bar, actual.Latest.Bar);
                            Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                        }
                        actual.Dispose();
                        foreach (var root in roots) foreach (var output in root.Outputs)
                            Assert.Equal(bars.Length, expected[output].ToArray().Length);
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

public sealed class SharedGOscillatorNumericalTests
{
    public static IEnumerable<object[]> Cases => OoplesFinance.StockIndicators.Validation.IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(GOscillator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(Cases))]
    public Task IndependentNumericalContracts(OoplesFinance.StockIndicators.Validation.IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
