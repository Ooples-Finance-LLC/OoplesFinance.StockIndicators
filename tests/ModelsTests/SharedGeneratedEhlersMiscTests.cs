using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedEhlersMiscTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(SuperSmoother) || c.IndicatorType == typeof(EhlersVariableIndexDynamicAverage)
            || c.IndicatorType == typeof(EhlersSquelchIndicator) || c.IndicatorType == typeof(EhlersSwissArmyKnife)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch {
        0 => new SuperSmoother(length), 1 => new EhlersVariableIndexDynamicAverage(length),
        2 => new EhlersSquelchIndicator(length, length, length), _ => new EhlersSwissArmyKnife(length) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 4)
        from length in new[] { int.MinValue, 1, 2, 20, 65, int.MaxValue } select new object[] { kind, length };

    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsAndSlotsRetainBits(int kind, int length)
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
    [InlineData(0)] [InlineData(double.Epsilon)] [InlineData(.1)] [InlineData(1)] [InlineData(double.MaxValue)]
    public async Task SwissBandwidthRetainsAllNineFilters(double delta)
    {
        var root = new EhlersSwissArmyKnife(20, delta); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Theory]
    [InlineData(-double.Epsilon)] [InlineData(double.NaN)] [InlineData(double.NegativeInfinity)] [InlineData(double.PositiveInfinity)]
    public async Task SwissInvalidBandwidthRetainsFallbackAndPriority(double invalid)
    {
        var root = new EhlersSwissArmyKnife(20, invalid); Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], []); await Compare([root], ToBars([1, 2, 3]));
        await Compare([Create(0, 20), root], [new(default, 1, 2, 0, 1, double.NaN)]);
    }

    [Theory]
    [InlineData(1, 7, 3)] [InlineData(7, 1, 3)] [InlineData(int.MaxValue, 20, 40)] [InlineData(6, int.MaxValue, 40)]
    public Task SquelchIndependentPeriodsRetainBits(int lag, int threshold, int horizon) =>
        Compare([new EhlersSquelchIndicator(lag, threshold, horizon)], ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray()));

    [Fact]
    public async Task SquelchHorizonSaturatesAt360()
    {
        var capped = new EhlersSquelchIndicator(6, 20, 360); var huge = new EhlersSquelchIndicator(6, 20, int.MaxValue);
        var bars = ToBars(Enumerable.Range(0, 401).Select(i => Math.Sin(i * .7)).ToArray());
        await Compare([capped, huge], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(capped, huge).BuildAsync();
        Bits(run[capped.Outputs[0]].ToArray(), run[huge.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task VidyaObsoleteLengthKeepsFixedCalculationPeriods()
    {
        var shortRoot = new EhlersVariableIndexDynamicAverage(1); var longRoot = new EhlersVariableIndexDynamicAverage(int.MaxValue);
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        await Compare([shortRoot, longRoot], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(shortRoot, longRoot).BuildAsync();
        Bits(run[shortRoot.Outputs[0]].ToArray(), run[longRoot.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task VidyaExplicitAveragesRetainBothComponentPaths()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        foreach (var root in new IIndicator[] { new EhlersVariableIndexDynamicAverage(9, new Sma(3)),
            new EhlersVariableIndexDynamicAverage(9, new Sma(3), new Wma(5)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
    }

    [Fact]
    public void SwissPrimaryRemainsSmaInSecondSlot()
    {
        var root = new EhlersSwissArmyKnife(); Assert.Equal(9, root.Outputs.Count);
        Assert.Same(root.Outputs[1], root.Value);
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new SuperSmoother(10).Of(new Sma(2)), new EhlersVariableIndexDynamicAverage(9).Of(new Sma(2)),
            new EhlersSquelchIndicator().Of(new Sma(2)), new EhlersSwissArmyKnife().Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(3, 20);
        IIndicator[] roots = [repeated, Create(0, 10), Create(1, 9), Create(2, 20), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactStatesReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 53).Select(i => Math.Sin(i * .7)).ToArray());
        foreach (var config in new[] {
            (Name: "GeneratedSuperSmootherAliasState", Arguments: new object[] { 10 }),
            (Name: "GeneratedEhlersVidyaState", Arguments: Array.Empty<object>()),
            (Name: "GeneratedSquelchState", Arguments: new object[] { 6, 20, 40 }),
            (Name: "GeneratedSwissArmyState", Arguments: new object[] { 20, .1 }) })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(config.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, config.Arguments, null)!;
            double[] Run() {
                var result = new List<double>(); var outputs = new double[9];
                foreach (var bar in bars) {
                    if (state is IIndicatorState scalar) result.Add(scalar.Update(in bar));
                    else { ((IMultiOutputState)state).Update(in bar, outputs); result.AddRange(outputs); }
                }
                return result.ToArray();
            }
            try { var expected = Run(); if (state is IIndicatorState scalar) scalar.Reset(); else ((IMultiOutputState)state).Reset(); Bits(expected, Run()); }
            finally { (state as IDisposable)?.Dispose(); }
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
