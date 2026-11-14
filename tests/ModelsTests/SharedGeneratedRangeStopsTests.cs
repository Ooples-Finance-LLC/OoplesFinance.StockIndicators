using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedRangeStopsTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(AtrTrailingStops) || c.IndicatorType == typeof(AverageTrueRangeTrailingStops)
            || c.IndicatorType == typeof(ChandelierExit) || c.IndicatorType == typeof(ChandelierExitLong)
            || c.IndicatorType == typeof(ChandelierExitShort)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch {
        0 => new AtrTrailingStops(length), 1 => new AverageTrueRangeTrailingStops(length),
        2 => new ChandelierExit(length), 3 => new ChandelierExitLong(length), _ => new ChandelierExitShort(length) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 5)
        from length in new[] { int.MinValue, 1, 2, 20, 65, int.MaxValue } select new object[] { kind, length };

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
    [InlineData(-double.MaxValue)] [InlineData(-1)] [InlineData(0)]
    [InlineData(double.Epsilon)] [InlineData(3)] [InlineData(double.MaxValue)]
    public async Task FiniteMultipliersRetainBits(double factor)
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        await Compare([new AtrTrailingStops(14, factor), new AverageTrueRangeTrailingStops(14, factor), new ChandelierExit(22, factor)], bars);
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.NegativeInfinity)] [InlineData(double.PositiveInfinity)]
    public async Task NonfiniteMultipliersRetainFallbackAndErrors(double factor)
    {
        foreach (var root in new IIndicator[] { new AtrTrailingStops(14, factor), new AverageTrueRangeTrailingStops(14, factor), new ChandelierExit(22, factor) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], []); await Compare([root], ToBars([1, 3, -2, 4, 0, 2, 5, -1]));
        }
    }

    [Fact]
    public async Task AliasesRetainScalarSelectionAndFixedDefaults()
    {
        var bars = ToBars(Enumerable.Range(0, 127).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        IIndicator[] roots = [new AtrTrailingStops(14), new AverageTrueRangeTrailingStops(14), new ChandelierExit(22), new ChandelierExitLong(22), new ChandelierExitShort(22)];
        Assert.All(roots, root => Assert.Single(root.Outputs));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).BuildAsync();
        Bits(run[roots[0].Outputs[0]].ToArray(), run[roots[1].Outputs[0]].ToArray());
        Bits(run[roots[2].Outputs[0]].ToArray(), run[roots[3].Outputs[0]].ToArray());
        await Compare(roots, bars);
    }

    [Fact]
    public async Task ExplicitAveragesRetainFallback()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new AtrTrailingStops(14, 3, new Sma(3)), new AverageTrueRangeTrailingStops(14, 3, new Sma(3)),
            new ChandelierExit(22, 3, new Sma(3)), new ChandelierExitLong(22, new Sma(3)), new ChandelierExitShort(22, new Sma(3)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new AtrTrailingStops().Of(new Sma(2)), new AverageTrueRangeTrailingStops().Of(new Sma(2)),
            new ChandelierExit().Of(new Sma(2)), new ChandelierExitLong(22).Of(new Sma(2)), new ChandelierExitShort(22).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(0, 5);
        IIndicator[] roots = [repeated, Create(1, 14), Create(2, 22), Create(3, 22), Create(4, 22), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public async Task OriginalHighLowRetainBits()
    {
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 100 + i, 15 + i % 7, 1 + i % 3, 4 + i % 5, i + 1)).ToArray();
        await Compare(Enumerable.Range(0, 5).Select(i => Create(i, 14)).ToArray(), bars);
    }

    [Fact]
    public void ExactStatesReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        foreach (var config in new[] {
            (Name: "GeneratedAtrStopsState", Arguments: new object[] { 14, 3d, bars.Length }),
            (Name: "GeneratedChandelierAliasState", Arguments: new object[] { 22, 3d, false }),
            (Name: "GeneratedChandelierAliasState", Arguments: new object[] { 22, 3d, true }) })
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
