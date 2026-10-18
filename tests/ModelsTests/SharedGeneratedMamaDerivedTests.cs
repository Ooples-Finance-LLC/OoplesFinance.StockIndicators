using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedMamaDerivedTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersSineWaveIndicatorV1) || c.IndicatorType == typeof(EhlersHilbertOscillator)
            || c.IndicatorType == typeof(EhlersInstantaneousTrendlineV1) || c.IndicatorType == typeof(EhlersSineWaveIndicatorV2))
        .Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    private static IIndicator Create(int kind, int length = 5) => kind switch {
        0 => new EhlersSineWaveIndicatorV1(), 1 => new EhlersHilbertOscillator(length),
        2 => new EhlersInstantaneousTrendlineV1(), _ => new EhlersSineWaveIndicatorV2(length) };
    public static IEnumerable<object[]> Cases => from kind in new[] { 1, 3 }
        from length in new[] { int.MinValue, 1, 2, 5, 17, int.MaxValue } select new object[] { kind, length };
    private static IEnumerable<double[]> Prices() => new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
        [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
        [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
        Enumerable.Repeat(0d, 31).Concat(Enumerable.Repeat(1d, 31)).ToArray(),
        Enumerable.Range(0, 129).Select(i => Math.Sin(i * .7) + .25 * Math.Sin(i * .11)).ToArray() };

    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsAndPairedSlotsRetainBits(int kind, int length)
    {
        var root = Create(kind, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in Prices()) await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(0)] [InlineData(2)]
    public async Task ParameterlessMamaModesKeepTheirHiddenCoefficients(int kind)
    {
        var root = Create(kind); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in Prices()) await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(-1)] [InlineData(double.Epsilon)]
    [InlineData(.07)] [InlineData(.9)] [InlineData(double.MaxValue)] [InlineData(-double.MaxValue)]
    public async Task AllFiniteCyberSineGainsKeepExactPeriodAndFixedCycleBehavior(double alpha)
    {
        var root = new EhlersSineWaveIndicatorV2(3, alpha); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.NegativeInfinity)] [InlineData(double.PositiveInfinity)]
    public async Task NonfiniteGainKeepsFallbackAndValidationPriority(double invalid)
    {
        var root = new EhlersSineWaveIndicatorV2(5, invalid); Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], []); await Compare([root], ToBars([1, 3, 2]));
        await Compare([Create(0), root], [new(default, 1, 2, 0, 1, double.NaN)]);
    }

    [Fact]
    public async Task HilbertLengthRetainsItsWarmupWhileValueRemainsTheSecondSlot()
    {
        var shortRoot = new EhlersHilbertOscillator(2); var longRoot = new EhlersHilbertOscillator(17);
        Assert.Same(shortRoot.Outputs[0], shortRoot.I3); Assert.Same(shortRoot.Outputs[1], shortRoot.Value);
        var bars = ToBars(Enumerable.Range(0, 53).Select(i => Math.Sin(i * .7)).ToArray());
        await Compare([shortRoot, longRoot], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(shortRoot, longRoot).BuildAsync();
        Bits(run[shortRoot.I3].ToArray(), run[longRoot.I3].ToArray());
        Bits(run[shortRoot.Value].ToArray(), run[longRoot.Value].ToArray());
        Assert.Equal(2, shortRoot.WarmupBars); Assert.Equal(17, longRoot.WarmupBars);
    }

    [Fact]
    public async Task SourcesMixedRootsAndCancellationKeepTheirContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var selected in new IIndicator[] { new EhlersSineWaveIndicatorV1().Of(new Sma(2)),
            new EhlersHilbertOscillator().Of(new Sma(2)), new EhlersInstantaneousTrendlineV1().Of(new Sma(2)),
            new EhlersSineWaveIndicatorV2().Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars); }
        var repeated = Create(1);
        IIndicator[] roots = [repeated, Create(0), Create(2), Create(3), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars);
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactStatesReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 53).Select(i => Math.Sin(2 * Math.PI * i / 7)).ToArray());
        foreach (var config in new[] {
            (Name: "GeneratedMamaDerivedState", Arguments: new object[] { 0 }),
            (Name: "GeneratedMamaDerivedState", Arguments: new object[] { 1 }),
            (Name: "GeneratedMamaDerivedState", Arguments: new object[] { 2 }),
            (Name: "GeneratedCyberSineState", Arguments: new object[] { 5, .07 }) })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(config.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = (IMultiOutputState)Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, config.Arguments, null)!;
            double[] Run() { var result = new List<double>(); foreach (var bar in bars) { var outputs = new double[2]; state.Update(in bar, outputs); result.AddRange(outputs); } return result.ToArray(); }
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
