using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedSyntheticPriceTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(DetrendedSyntheticPrice)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => new DetrendedSyntheticPrice(length);
    public static IEnumerable<object[]> Cases => new[] { int.MinValue, 1, 2, 3, 14, int.MaxValue }
        .Select(length => new object[] { 0, length });

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
    public void ScalarShapeIsRetained() => Assert.Single(new DetrendedSyntheticPrice(14).Outputs);

    [Fact]
    public async Task TwoBarExtremaAndSeededEmaMatchHandValues()
    {
        var bars = new[] { 1d, 2, 4 }.Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
        var root = new DetrendedSyntheticPrice(3); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([0, .25, .6875], run[root.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task ExtendedOriginalRangesPreserveRoundedStages()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 0,
                i % 3 == 0 ? scale : 0, i % 3 == 1 ? -scale : 0, 0, i + 1)).ToArray();
            await Compare([new DetrendedSyntheticPrice(1), new DetrendedSyntheticPrice(3), new DetrendedSyntheticPrice(int.MaxValue)], bars);
        }
    }

    [Fact]
    public async Task OneAndTwoPeriodsRetainPointSixSevenCoefficient()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        var one = new DetrendedSyntheticPrice(1); var two = new DetrendedSyntheticPrice(2);
        await Compare([one, two], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(one, two).BuildAsync();
        Bits(run[one.Outputs[0]].ToArray(), run[two.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task SelectedSourceRetainsRangeProjectionFallback()
    {
        var root = new DetrendedSyntheticPrice(3).Of(new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]));
    }

    [Fact]
    public async Task RepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]); var repeated = new DetrendedSyntheticPrice(3);
        IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void StateReplaysAfterResetIncludingFirstBarSeed()
    {
        var bars = ToBars(Enumerable.Range(0, 25).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedSyntheticPriceState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { 3 }, null)!;
        double[] Run() { var result = new List<double>(); foreach (var bar in bars) result.Add(state.Update(in bar)); return result.ToArray(); }
        var expected = Run(); state.Reset(); Bits(expected, Run());
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
