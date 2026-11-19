using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedInsyncTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(InsyncIndex)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static InsyncIndex Create(int slot, int length) => slot switch
    {
        0 => new InsyncIndex(fastLength: length),
        1 => new InsyncIndex(slowLength: length),
        2 => new InsyncIndex(signalLength: length),
        3 => new InsyncIndex(emoLength: length),
        4 => new InsyncIndex(mfiLength: length),
        5 => new InsyncIndex(bbLength: length),
        6 => new InsyncIndex(cciLength: length),
        7 => new InsyncIndex(dpoLength: length),
        8 => new InsyncIndex(rocLength: length),
        9 => new InsyncIndex(rsiLength: length),
        10 => new InsyncIndex(stochLength: length),
        11 => new InsyncIndex(stochKLength: length),
        12 => new InsyncIndex(stochDLength: length),
        13 => new InsyncIndex(smaLength: length),
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };
    public static IEnumerable<object[]> PeriodSlots => Enumerable.Range(0, 14).Select(slot => new object[] { slot });
    [Theory, MemberData(nameof(PeriodSlots))]
    public async Task EveryPublicPeriodRetainsItsMapping(int slot)
    { var root = Create(slot, 3); Assert.True(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray())); }
    [Theory] [InlineData(int.MinValue)] [InlineData(1)] [InlineData(2)] [InlineData(int.MaxValue)]
    public async Task AllPeriodExtremesRetainBoundedHistory(int length)
    {
        var root = new InsyncIndex(length, length, length, length, length, length, length, length, length, length, length, length, length, length);
        Assert.True(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([0, 1, -2, 4, 0, 2, -1]));
    }
    [Theory] [InlineData(-1d)] [InlineData(0d)] [InlineData(double.Epsilon)] [InlineData(double.MaxValue)]
    public async Task FiniteMultipliersAndDivisorsRetainExactVotes(double value)
    { await Compare([new InsyncIndex(stdDevMult: value), new InsyncIndex(divisor: value)], ToBars([0, 1, -2, 4, 0, 2, -1])); }
    [Theory] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public async Task NonfiniteParametersRetainFallbackErrors(double value)
    {
        foreach (var root in new[] { new InsyncIndex(stdDevMult: value), new InsyncIndex(divisor: value) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 2, 3])); }
    }
    [Fact]
    public async Task CustomAverageRetainsFallback()
    { var root = new InsyncIndex(maType: new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 3, -2, 4, 0, 2, -1])); }
    [Fact]
    public async Task UnusedSignalPeriodsDoNotChangePublishedVotes()
    {
        var a = new InsyncIndex(); var b = new InsyncIndex(signalLength: 1, emoLength: int.MaxValue); var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        await Compare([a, b], bars); using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(a, b).BuildAsync();
        Bits(run[a.Value].ToArray(), run[b.Value].ToArray());
    }
    [Fact]
    public void OutputRemainsScalar() => Assert.Single(new InsyncIndex().Outputs);

    [Fact]
    public async Task LongHistoryCrossesSeveralExpirationsWithoutChangingRounding()
    {
        await Compare([new InsyncIndex(11), new InsyncIndex(65)], ToBars(Enumerable.Range(0, 257).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = new InsyncIndex(3).Of(new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        var repeated = new InsyncIndex(3); IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExtremaAndErrorsReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 39).Select(i => Math.Sin(i * .7)).ToArray());
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedInsyncState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { new InsyncIndex(3) }, null)!;
        double[] Run() { var values = new List<double>(); foreach (var bar in bars) values.Add(state.Update(in bar)); return values.ToArray(); }
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
