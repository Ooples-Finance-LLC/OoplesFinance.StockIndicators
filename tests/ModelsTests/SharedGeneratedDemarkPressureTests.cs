using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedDemarkPressureTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(DemarkPressureRatioV1) || c.IndicatorType == typeof(DemarkPressureRatioV2) || c.IndicatorType == typeof(DemarkRangeExpansionIndex)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch {
        0 => new DemarkPressureRatioV1(length), 1 => new DemarkPressureRatioV2(length), _ => new DemarkRangeExpansionIndex(length) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 3)
        from length in new[] { int.MinValue, 1, 2, 5, 14, 1024 } select new object[] { kind, length };

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

    [Fact]
    public void ScalarOutputsKeepShape()
    { for (var kind = 0; kind < 3; kind++) Assert.Single(Create(kind, 3).Outputs); }

    [Fact]
    public async Task ZeroDenominatorsKeepDistinctNeutralDefaults()
    {
        var bars = ToBars(new double[13]);
        for (var kind = 0; kind < 3; kind++)
        {
            var root = Create(kind, 3); await Compare([root], bars);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
            Bits(Enumerable.Repeat(kind == 1 ? 50d : 0d, bars.Length).ToArray(), run[root.Outputs[0]].ToArray());
        }
    }

    [Fact]
    public async Task FifteenPercentUpGapRemainsStrict()
    {
        foreach (var open in new[] { Math.BitDecrement(115), 115d, Math.BitIncrement(115) })
        {
            Bar[] bars = [new(default, 100, 100, 100, 100, 0), new(default, open, 120, 90, 110, 1)];
            var root = new DemarkPressureRatioV1(1); await Compare([root], bars);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
            var value = run[root.Outputs[0]].ToArray()[1];
            if (open <= 115) Assert.Equal(0, value); else Assert.True(value > 80);
        }
    }

    [Fact]
    public async Task OriginalCandlesVolumeAndEightBarGatesSurviveLongHistory()
    {
        var bars = Enumerable.Range(0, 97).Select(i => new Bar(default, 20 + i % 4, 30 + i % 5,
            12 - i % 3, 15 + i * 7 % 14, i % 3 == 0 ? -2 : i % 4 + 1)).ToArray();
        await Compare([new DemarkPressureRatioV1(13), new DemarkPressureRatioV2(10), new DemarkRangeExpansionIndex(5)], bars);
    }

    [Fact]
    public async Task RangeGatesKeepZeroPaddedHistoryAndEviction()
    {
        var bars = Enumerable.Range(0, 4).Select(i => new Bar(default, 2 + i, 3 + i, 1 + i, 2 + i, 1)).ToArray();
        var root = new DemarkRangeExpansionIndex(2); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([0, 0, 40, 100], run[root.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task HugePeriodsUseObservedStorageWithoutPrematureEviction()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => 2 + Math.Sin(i * .7)).ToArray());
        for (var kind = 0; kind < 3; kind++)
        {
            var bounded = Create(kind, bars.Length); var huge = Create(kind, int.MaxValue);
            using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(bounded).ConfigureBehavior(_ => { }).BuildAsync();
            using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(huge).BuildAsync();
            Assert.False(((IndicatorRun)actual).HasLegacyRuntime);
            Bits(expected[bounded.Outputs[0]].ToArray(), actual[huge.Outputs[0]].ToArray());
        }
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new DemarkPressureRatioV1(3).Of(new Sma(2)), new DemarkPressureRatioV2(3).Of(new Sma(2)), new DemarkRangeExpansionIndex(3).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(0, 3); IIndicator[] roots = [repeated, Create(1, 3), Create(2, 3), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactWindowsReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 25).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        (string Name, object[] Args)[] cases = [ ("GeneratedDemarkPressureState", [3, bars.Length, false]),
            ("GeneratedDemarkPressureState", [3, bars.Length, true]), ("GeneratedDemarkRangeState", [3]) ];
        foreach (var item in cases)
        {
            var type = typeof(ValuesBarExecution).GetNestedType(item.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = (IIndicatorState)Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, item.Args, null)!;
            double[] Run() { var result = new List<double>(); foreach (var bar in bars) result.Add(state.Update(in bar)); return result.ToArray(); }
            try { var expected = Run(); state.Reset(); Bits(expected, Run()); } finally { ((IDisposable)state).Dispose(); }
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
