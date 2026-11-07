using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedDominantCycleTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersDualDifferentiatorDominantCycle) || c.IndicatorType == typeof(EhlersHomodyneDominantCycle)
            || c.IndicatorType == typeof(EhlersPhaseAccumulationDominantCycle)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    private static IIndicator Create(int kind, int upper, int lower, int minimum, int horizon) => kind switch {
        0 => new EhlersDualDifferentiatorDominantCycle(upper, lower, minimum),
        1 => new EhlersHomodyneDominantCycle(upper, lower, minimum),
        _ => new EhlersPhaseAccumulationDominantCycle(upper, lower, minimum, horizon) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 3)
        from length in new[] { int.MinValue, 1, 2, 7, 48, int.MaxValue } select new object[] { kind, length };
    private static IEnumerable<double[]> Prices() => new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
        [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
        [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
        Enumerable.Repeat(0d, 31).Concat(Enumerable.Repeat(1d, 31)).ToArray(),
        Enumerable.Range(0, 129).Select(i => Math.Sin(i * .7) + .25 * Math.Sin(i * .11)).ToArray() };

    [Theory, MemberData(nameof(Cases))]
    public async Task AllPeriodsAndModesRetainBits(int kind, int length)
    {
        var root = Create(kind, length, length, length, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in Prices()) await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task IndependentPeriodsAndInvertedBoundsRetainBits(int kind)
    {
        var bars = ToBars(Enumerable.Range(0, 129).Select(i => (i * 17 % 83 - 41) / 8d).ToArray());
        foreach (var periods in new[] { (1, 7, 19, 40), (7, 1, 19, 40), (7, 19, 1, 40),
            (48, 20, 8, 1), (int.MaxValue, 2, 3, 40), (2, int.MaxValue, 3, 40), (2, 3, int.MaxValue, 40), (48, 20, 8, int.MaxValue) })
            await Compare([Create(kind, periods.Item1, periods.Item2, periods.Item3, periods.Item4)], bars);
    }

    [Fact]
    public async Task PhaseHorizonSaturatesAt360WithoutChangingPublishedValues()
    {
        var bounded = new EhlersPhaseAccumulationDominantCycle(48, 20, 1, 360);
        var oversized = new EhlersPhaseAccumulationDominantCycle(48, 20, 1, int.MaxValue);
        var bars = ToBars(Enumerable.Range(0, 401).Select(i => Math.Sin(i * .03)).ToArray());
        await Compare([bounded, oversized], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(bounded, oversized).BuildAsync();
        Bits(run[bounded.Outputs[0]].ToArray(), run[oversized.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task RawInputErrorsKeepPriorityForSharedRoots()
    {
        IIndicator[] roots = [Create(0, 48, 20, 8, 40), Create(1, 48, 20, 10, 40), Create(2, 48, 20, 10, 40)];
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare(roots, [new(default, 1, 2, 0, 1, 1), new(default, 1, 2, 0, 1, invalid)]);
    }

    [Fact]
    public async Task SourcesMixedRootsAndCancellationKeepTheirContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var selected in new IIndicator[] { new EhlersDualDifferentiatorDominantCycle().Of(new Sma(2)),
            new EhlersHomodyneDominantCycle().Of(new Sma(2)), new EhlersPhaseAccumulationDominantCycle().Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars); }
        var repeated = Create(0, 48, 20, 8, 40);
        IIndicator[] roots = [repeated, Create(1, 48, 20, 10, 40), Create(2, 48, 20, 10, 40), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactStatesReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 53).Select(i => Math.Sin(2 * Math.PI * i / 7)).ToArray());
        foreach (var mode in Enumerable.Range(0, 3))
        {
            var type = typeof(ValuesBarExecution).GetNestedType("GeneratedHilbertCycleState", System.Reflection.BindingFlags.NonPublic)!;
            var state = (IIndicatorState)Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { 48, 20, 8, 40, mode }, null)!;
            double[] Run() { var result = new List<double>(); foreach (var bar in bars) result.Add(state.Update(in bar)); return result.ToArray(); }
            try { var expected = Run(); state.Reset(); Bits(expected, Run()); }
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
