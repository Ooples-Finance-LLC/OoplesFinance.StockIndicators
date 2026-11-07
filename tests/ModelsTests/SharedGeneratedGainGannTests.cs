using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedGainGannTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(GainLossMovingAverage) || c.IndicatorType == typeof(GannHiLoActivator) || c.IndicatorType == typeof(GannTrendOscillator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch { 0 => new GainLossMovingAverage(length), 1 => new GannHiLoActivator(length), _ => new GannTrendOscillator(length) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 3) from length in new[] { int.MinValue, 1, 2, 3, 14, 65, int.MaxValue } select new object[] { kind, length };

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
    [InlineData(1, 1)] [InlineData(2, 65)] [InlineData(65, 2)] [InlineData(14, 7)] [InlineData(int.MaxValue, int.MaxValue)]
    public async Task IndependentGainAndSignalPeriodsKeepBits(int length, int signal)
    { await Compare([new GainLossMovingAverage(length, signal)], ToBars(Enumerable.Range(0, 97).Select(i => Math.Sin(i * .7)).ToArray())); }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task CustomAveragePrefixKeepsFallback(bool gann, bool second)
    {
        IMovingAverage? first = new Ema(2); IMovingAverage? last = second ? new Ema(2) : null;
        IIndicator root = gann ? new GannHiLoActivator(3, first, last) : new GainLossMovingAverage(3, 7, first, last);
        Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 2, -3, 4, 0, 2, -1, 0, 1]));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SecondCustomAverageRequiresPrecedingAverage(bool gann)
    {
        var error = Assert.Throws<ArgumentException>(() =>
        {
            if (gann) _ = new GannHiLoActivator(3, null, new Ema(2));
            else _ = new GainLossMovingAverage(3, 7, null, new Ema(2));
        });
        Assert.Equal("secondAverage", error.ParamName);
    }

    [Fact]
    public async Task GannRangeMeansKeepGuardedSmaRounding()
    {
        var root = new GannHiLoActivator(2);
        var bars = ToBars([1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0]);
        await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        // Preserve the builder's add-before-eviction sum and periodic rebuild rounding.
        Bits([Math.BitDecrement(Math.BitDecrement(.5))], [run[root.Outputs[0]].ToArray()[3]]);
    }

    [Fact]
    public void PairedAndScalarShapesKeepOrder()
    { var gain = new GainLossMovingAverage(); Assert.Equal(2, gain.Outputs.Count); Assert.Same(gain.Value, gain.Outputs[0]); Assert.Same(gain.Signal, gain.Outputs[1]); Assert.Single(new GannHiLoActivator().Outputs); Assert.Single(new GannTrendOscillator().Outputs); }

    [Fact]
    public async Task GainSymmetricReturnAndZeroDenominatorKeepHandValues()
    {
        var root = new GainLossMovingAverage(1, 1); var bars = ToBars([1, 3, -3, 0, 2]); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([0, 100, 0, -200, 200], run[root.Outputs[0]].ToArray()); Bits([0, 100, 0, -200, 200], run[root.Outputs[1]].ToArray());
    }

    [Fact]
    public async Task HiLoStrictPreviousAverageBranchesKeepHandValues()
    {
        var root = new GannHiLoActivator(1); Bar[] bars = [new(default, 99, 4, 1, 2, 1), new(default, 99, 6, 2, 4, 1), new(default, 99, 7, 3, 0, 1), new(default, 99, 8, 4, 8, 1)]; await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync(); Bits([1, 1, 7, 4], run[root.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task TrendTwoBarTurnsAndHoldKeepHandValues()
    {
        var root = new GannTrendOscillator(1); double[] highs = [5, 3, 4, 4, 4], lows = [1, 2, 1, 2, 1]; var bars = Enumerable.Range(0, 5).Select(i => new Bar(default, 99, highs[i], lows[i], 2, 1)).ToArray(); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync(); Bits([0, 0, 1, 1, -1], run[root.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task LongHistoryCrossesAllWindows()
    { await Compare(Enumerable.Range(0, 3).Select(i => Create(i, 14)).ToArray(), ToBars(Enumerable.Range(0, 257).Select(i => Math.Sin(i * .7)).ToArray())); }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]); IIndicator[] selected = [new GainLossMovingAverage(3).Of(new Sma(2)), new GannHiLoActivator(3).Of(new Sma(2)), new GannTrendOscillator(3).Of(new Sma(2))];
        Assert.False(ValuesBarExecution.SupportsOwned(selected)); await Compare(selected, bars);
        var repeated = Create(0, 3); IIndicator[] roots = [repeated, Create(1, 3), Create(2, 3), repeated]; await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void WindowsReplayAfterReset(int kind)
    {
        var bars = ToBars(Enumerable.Range(0, 97).Select(i => Math.Sin(i * .7)).ToArray()); var name = kind == 0 ? "GeneratedGainLossAverageState" : kind == 1 ? "GeneratedGannHiLoState" : "GeneratedGannTrendState";
        var type = typeof(ValuesBarExecution).GetNestedType(name, System.Reflection.BindingFlags.NonPublic)!; object[] args = kind == 0 ? [3, 7, bars.Length] : kind == 1 ? [3, bars.Length] : [3];
        var state = Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, args, null)!;
        double[] Run() { var values = new List<double>(); var outputs = new double[2]; foreach (var bar in bars) { if (state is IMultiOutputState multi) { multi.Update(in bar, outputs); values.AddRange(outputs); } else values.Add(((IIndicatorState)state).Update(in bar)); } return values.ToArray(); }
        try { var expected = Run(); if (state is IMultiOutputState multi) multi.Reset(); else ((IIndicatorState)state).Reset(); Bits(expected, Run()); } finally { ((IDisposable)state).Dispose(); }
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
