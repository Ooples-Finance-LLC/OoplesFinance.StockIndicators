using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedFmDemodulatorTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersFMDemodulatorIndicator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    [Theory]
    [InlineData(int.MinValue)] [InlineData(1)] [InlineData(2)] [InlineData(20)] [InlineData(65)] [InlineData(int.MaxValue)]
    public async Task PeriodsKeepDerivativeClampAndSmoothingBits(int length)
    {
        var root = new EhlersFMDemodulatorIndicator(length, length);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(1, 30)] [InlineData(10, 1)] [InlineData(int.MaxValue, 30)] [InlineData(10, int.MaxValue)]
    public async Task IndependentFastAndSlowLengthsKeepBits(int fast, int slow)
    {
        await Compare([new EhlersFMDemodulatorIndicator(fast, slow)],
            ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Fact]
    public async Task OriginalOpenControlsTheDemodulatedInput()
    {
        var root = new EhlersFMDemodulatorIndicator(10, 30);
        var flat = Enumerable.Range(0, 12).Select(_ => new Bar(default, 1, 2, 0, 1, 1)).ToArray();
        var changed = Enumerable.Range(0, 12).Select(_ => new Bar(default, 0, 2, 0, 1, 1)).ToArray();
        await Compare([root], flat); await Compare([root], changed);
        using var zero = await new StockIndicatorBuilder().ConfigureSource(Bars.From(flat)).ConfigureIndicators(root).BuildAsync();
        using var nonzero = await new StockIndicatorBuilder().ConfigureSource(Bars.From(changed)).ConfigureIndicators(root).BuildAsync();
        Assert.All(zero[root.Outputs[0]].ToArray(), value => Assert.Equal(0d, value));
        Assert.Contains(nonzero[root.Outputs[0]].ToArray(), value => value != 0);
        await Compare([root], [new(default, -double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, 1),
            new(default, double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 1)]);
    }

    [Fact]
    public async Task ExplicitAveragesRetainComponentPath()
    {
        var bars = ToBars(Enumerable.Range(0, 33).Select(i => Math.Sin(i * .7)).ToArray());
        foreach (var average in new IMovingAverage[] { new Sma(3), new Ema(4), new Wma(5) })
        {
            var root = new EhlersFMDemodulatorIndicator(10, 30, average);
            Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars);
        }
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = new EhlersFMDemodulatorIndicator().Of(new Sma(2));
        Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        var repeated = new EhlersFMDemodulatorIndicator();
        IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactStateReplaysAfterReset()
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedFmDemodulatorState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { 10, 30 }, null)!;
        var bars = ToBars(Enumerable.Range(0, 33).Select(i => Math.Sin(i * .7)).ToArray());
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
