using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedDoubleSmoothedTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(DoubleSmoothedMomenta) || c.IndicatorType == typeof(DoubleSmoothedRelativeStrengthIndex)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind == 0 ? new DoubleSmoothedMomenta(length)
        : new DoubleSmoothedRelativeStrengthIndex(length);
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
    [InlineData(1, 1, 1)] [InlineData(2, 5, 25)] [InlineData(65, 2, 3)]
    [InlineData(3, 65, 2)] [InlineData(3, 2, 65)] [InlineData(2, int.MaxValue, int.MaxValue)]
    public async Task IndependentMomentaPeriodsPreserveBothOutputs(int lookback, int first, int second)
    {
        await Compare([new DoubleSmoothedMomenta(lookback, first, second)], ToBars(Enumerable.Range(0, 97).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task EveryCustomAveragePrefixKeepsFallback(int count)
    {
        var root = new DoubleSmoothedMomenta(2, 3, 5, new Sma(3), count > 1 ? new Sma(5) : null,
            count > 2 ? new Sma(3) : null, count > 3 ? new Sma(5) : null, count > 4 ? new Sma(5) : null);
        Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars([1, 2, -3, 4, 0, 2, -2, 0, 1, 3, -1, 4]));
    }

    [Fact]
    public void PairedOutputsKeepPrimaryAndSignalOrder()
    {
        var momenta = new DoubleSmoothedMomenta(); var strength = new DoubleSmoothedRelativeStrengthIndex(14);
        Assert.Equal(2, momenta.Outputs.Count); Assert.Equal(2, strength.Outputs.Count);
        Assert.Same(momenta.Outputs[0], momenta.Value); Assert.Same(momenta.Outputs[1], momenta.Signal);
        Assert.Same(strength.Outputs[0], strength.Value); Assert.Same(strength.Outputs[1], strength.Signal);
    }

    [Fact]
    public async Task StrengthAliasIgnoresObsoletePeriod()
    {
        var first = new DoubleSmoothedRelativeStrengthIndex(int.MinValue); var second = new DoubleSmoothedRelativeStrengthIndex(int.MaxValue);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars([1, 3, -2, 4, 0]))).ConfigureIndicators(first, second).BuildAsync();
        for (var i = 0; i < 2; i++) Bits(run[first.Outputs[i]].ToArray(), run[second.Outputs[i]].ToArray());
    }

    [Fact]
    public async Task HugeMomentaLookbackKeepsObservedExtremaWithoutPeriodAllocation()
    {
        var prices = new double[] { 1, 3, -2, 4, 0, double.Epsilon };
        var root = new DoubleSmoothedMomenta(int.MaxValue, 5, 25);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars(prices))).ConfigureIndicators(root).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime);
        using var reference = new OoplesFinance.StockIndicators.Helpers.MomentaRangeWindow(MovingAvgType.ExponentialMovingAverage, int.MaxValue, 5, 25, prices.Length);
        var points = prices.Select(p => reference.Next(p, true)).ToArray();
        Bits(points.Select(p => p.Value).ToArray(), run[root.Outputs[0]].ToArray());
        Bits(points.Select(p => p.Signal).ToArray(), run[root.Outputs[1]].ToArray());
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new DoubleSmoothedMomenta().Of(new Sma(2)), new DoubleSmoothedRelativeStrengthIndex(14).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(0, 3); IIndicator[] roots = [repeated, Create(1, 14), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExactStatesReplayAfterReset(bool strength)
    {
        var bars = ToBars([1, 2, 3, 4, -2, 0, 1]);
        var name = strength ? "GeneratedDoubleRangeStrengthState" : "GeneratedDoubleMomentaState";
        var type = typeof(ValuesBarExecution).GetNestedType(name, System.Reflection.BindingFlags.NonPublic)!;
        var state = (IMultiOutputState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, strength ? new object[] { bars.Length } : new object[] { 2, 5, 25, bars.Length }, null)!;
        double[] Run() { var values = new List<double>(); var outputs = new double[2]; foreach (var bar in bars) { state.Update(in bar, outputs); values.AddRange(outputs); } return values.ToArray(); }
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
