using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedEdgeEfficiencyTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EdgePreservingFilter) || c.IndicatorType == typeof(EfficientAutoLine) || c.IndicatorType == typeof(EfficientPrice)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch {
        0 => new EdgePreservingFilter(length), 1 => new EfficientAutoLine(length), _ => new EfficientPrice(length) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 3)
        from length in new[] { int.MinValue, 1, 2, 14, 65, int.MaxValue } select new object[] { kind, length };

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
    [InlineData(0, 0)] [InlineData(3, 3)] [InlineData(-1, -2)]
    [InlineData(.0001, .005)] [InlineData(double.Epsilon, 1)] [InlineData(double.MaxValue, -double.MaxValue)]
    public async Task FiniteThresholdAxesKeepExactComparisons(double fast, double slow)
    {
        var root = new EfficientAutoLine(2, fast, slow); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars(Enumerable.Range(0, 39).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Theory]
    [InlineData(double.NaN, 1)] [InlineData(1, double.NaN)]
    [InlineData(double.PositiveInfinity, 1)] [InlineData(1, double.NegativeInfinity)]
    public async Task InvalidThresholdsRetainFallbackErrors(double fast, double slow)
    {
        var root = new EfficientAutoLine(2, fast, slow); Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars([1, 2, 3]));
    }

    [Fact]
    public void OutputsStayScalar()
    { for (var kind = 0; kind < 3; kind++) Assert.Single(Create(kind, 3).Outputs); }

    [Fact]
    public async Task HandValuesKeepSeedObservationLagAndStrictThreshold()
    {
        var edge = new EdgePreservingFilter(1); var price = new EfficientPrice(2);
        using var first = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars([1, 2, 3]))).ConfigureIndicators(edge, price).BuildAsync();
        Bits([1, 4d / 3, 7d / 4], first[edge.Outputs[0]].ToArray());
        Bits([0, 0, 2], first[price.Outputs[0]].ToArray());
        var line = new EfficientAutoLine(2, 3, 3);
        using var second = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars(Enumerable.Range(1, 13).Select(i => (double)i).ToArray()))).ConfigureIndicators(line).BuildAsync();
        Bits([1, 2, 3, 4, 5, 6, 7, 8, 9, 9, 9, 9, 13], second[line.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task ExplicitEdgeAverageKeepsFallback()
    {
        var root = new EdgePreservingFilter(3, new Ema(3)); Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars([1, 2, -3, 4, 0, 2, -2, 0, 1, 3, -1, 4]));
    }

    [Fact]
    public async Task LongHistoryCrossesEndpointFitAndPeakExpiry()
    {
        await Compare([Create(0, 65), Create(1, 19), Create(2, 50)], ToBars(Enumerable.Range(0, 257).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new EdgePreservingFilter(3).Of(new Sma(2)), new EfficientAutoLine(3).Of(new Sma(2)), new EfficientPrice(3).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(0, 3); IIndicator[] roots = [repeated, Create(1, 14), Create(2, 6), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void StatesReplayAfterReset(int kind)
    {
        var bars = ToBars([1, 2, 3, 4, -2, 0, 1]);
        var name = kind == 0 ? "GeneratedEdgePreservingState" : "GeneratedEfficiencyOutputState";
        var type = typeof(ValuesBarExecution).GetNestedType(name, System.Reflection.BindingFlags.NonPublic)!;
        object[] args = kind == 0 ? [3] : [3, kind == 1, .0001, .005];
        var state = (IIndicatorState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, args, null)!;
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
