using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedKaseStopsTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(KaseDevStopV1) || c.IndicatorType == typeof(KaseDevStopV2)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind == 0
        ? new KaseDevStopV1(length, 21, 20) : new KaseDevStopV2(length, 21, 20);
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 2)
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
    [InlineData(1, 1, 1)] [InlineData(5, int.MaxValue, 20)] [InlineData(5, 21, int.MaxValue)]
    [InlineData(int.MaxValue, 1, 2)] [InlineData(7, 3, 5)]
    public async Task IndependentPeriodsRetainAllFourLines(int fast, int slow, int length)
    {
        await Compare([new KaseDevStopV1(fast, slow, length), new KaseDevStopV2(fast, slow, length)],
            ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray()));
    }

    private static IIndicator Deviations(int version, double[] values) => version == 1
        ? new KaseDevStopV1(5, 21, 20, values[0], values[1], values[2], values[3])
        : new KaseDevStopV2(10, 21, 20, values[0], values[1], values[2], values[3]);

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(double.Epsilon)]
    [InlineData(1)] [InlineData(2.2)] [InlineData(double.MaxValue)]
    public async Task EachDeviationSlotRetainsItsArithmetic(double multiplier)
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        for (var slot = 0; slot < 4; slot++)
        {
            double[] values = [0, 1, 2.2, 3.6]; values[slot] = multiplier;
            await Compare([Deviations(1, values), Deviations(2, values)], bars);
        }
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.NegativeInfinity)] [InlineData(double.PositiveInfinity)]
    public async Task NonfiniteDeviationsRetainFallbackAndErrorPriority(double invalid)
    {
        for (var version = 1; version <= 2; version++) for (var slot = 0; slot < 4; slot++)
        {
            double[] values = [0, 1, 2.2, 3.6]; values[slot] = invalid;
            var root = Deviations(version, values); Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], []); await Compare([root], ToBars([1, 3, -2, 4]));
        }
    }

    [Fact]
    public void FourSlotsKeepDev1Primary()
    {
        var first = new KaseDevStopV1(); var second = new KaseDevStopV2();
        Assert.Equal(4, first.Outputs.Count); Assert.Equal(4, second.Outputs.Count);
        Assert.Same(first.Outputs[0], first.Value); Assert.Same(second.Outputs[0], second.Value);
    }

    [Fact]
    public async Task ExplicitAveragesRetainFallback()
    {
        foreach (var root in new IIndicator[] { new KaseDevStopV1(5, 21, 20, 0, 1, 2.2, 3.6, new Ema(3)),
            new KaseDevStopV2(10, 21, 20, 0, 1, 2.2, 3.6, new Ema(3)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0])); }
    }

    [Fact]
    public async Task OriginalCloseTypicalInputAndCustomRangesRetainBits()
    {
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 100 + i, i % 4 == 0 ? 2 : 15 + i % 7,
            i % 4 == 0 ? 1 : 1 + i % 3, i % 4 == 0 ? -100 : 4 + i % 5, i + 1)).ToArray();
        await Compare([new KaseDevStopV1(), new KaseDevStopV2()], bars);
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new KaseDevStopV1().Of(new Sma(2)), new KaseDevStopV2().Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(0, 5);
        IIndicator[] roots = [repeated, Create(1, 14), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactStatesReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => i % 5 == 0 ? -100 : 10 + Math.Sin(i * .7)).ToArray());
        foreach (var config in new[] {
            (Name: "GeneratedKaseStopV1State", Arguments: new object[] { new KaseDevStopV1() }),
            (Name: "GeneratedKaseStopV2State", Arguments: new object[] { new KaseDevStopV2() }) })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(config.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = (IMultiOutputState)Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, config.Arguments, null)!;
            double[] Run() { var result = new List<double>(); var outputs = new double[4]; foreach (var bar in bars) { state.Update(in bar, outputs); result.AddRange(outputs); } return result.ToArray(); }
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
