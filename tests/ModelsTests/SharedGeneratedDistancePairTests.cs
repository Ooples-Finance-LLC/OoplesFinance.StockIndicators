using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedDistancePairTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(DistanceWeightedMovingAverage) || c.IndicatorType == typeof(InverseDistanceWeightedMovingAverage)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind == 0 ? new DistanceWeightedMovingAverage(length)
        : new InverseDistanceWeightedMovingAverage(length);
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

    [Fact]
    public async Task HugeOwnedPeriodsKeepFullZeroPaddingWithoutPeriodSizedStorage()
    {
        var prices = new double[] { 2, 4, 8, -2, 0, double.Epsilon };
        var bars = ToBars(prices);
        IIndicator[] roots = [Create(0, int.MaxValue), Create(1, int.MaxValue)];
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime);
        for (var kind = 0; kind < roots.Length; kind++)
        {
            var expected = new double[prices.Length];
            OoplesFinance.StockIndicators.Helpers.DistanceMassWindowMean.Compute(prices, expected, int.MaxValue, kind == 0);
            Bits(expected, run[roots[kind].Outputs[0]].ToArray());
        }
    }

    [Fact]
    public async Task DistanceMassHandValuesAndScalarShapeStayExplicit()
    {
        var root = new InverseDistanceWeightedMovingAverage(3);
        Assert.Single(root.Outputs); Assert.Single(new DistanceWeightedMovingAverage(3).Outputs);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars([2, 4, 8]))).ConfigureIndicators(root).BuildAsync();
        Bits([1, 2, 5], run[root.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new DistanceWeightedMovingAverage(3).Of(new Sma(2)), new InverseDistanceWeightedMovingAverage(3).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(0, 14); IIndicator[] roots = [repeated, Create(1, 14), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExactStateReplaysAfterReset(bool reciprocal)
    {
        var bars = ToBars(Enumerable.Range(0, 25).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedDistanceMassState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { 3, bars.Length, reciprocal }, null)!;
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
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    [InlineData(false, 65)]
    [InlineData(true, 65)]
    [InlineData(false, int.MaxValue)]
    [InlineData(true, int.MaxValue)]
    public void FiniteCapacityPreservesMathematicalPeriodAndPreview(bool reciprocal, int period)
    {
        double[] input = [2, 4, 8, -2, 0, double.Epsilon, -double.Epsilon, 5, 5];
        var expected = new double[input.Length];
        OoplesFinance.StockIndicators.Helpers.DistanceMassWindowMean.Compute(input, expected, period, reciprocal);
        using var bounded = new OoplesFinance.StockIndicators.Helpers.DistanceMassWindowMean(period, reciprocal, input.Length);
        for (var replay = 0; replay < 2; replay++)
        {
            bounded.Reset();
            for (var i = 0; i < input.Length; i++)
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]),
                    BitConverter.DoubleToInt64Bits(bounded.Next(input[i], false)));
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]),
                    BitConverter.DoubleToInt64Bits(bounded.Next(input[i], true)));
            }
        }
    }

    [Fact]
    public void HugePeriodRetainsStartupZeroMass()
    {
        using var bounded = new OoplesFinance.StockIndicators.Helpers.DistanceMassWindowMean(int.MaxValue, maximumObservations: 1);
        Assert.Equal(1d, bounded.Next(2d, true));
    }
}
