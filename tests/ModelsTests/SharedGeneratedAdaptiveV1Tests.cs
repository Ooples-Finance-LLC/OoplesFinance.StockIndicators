using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedAdaptiveV1Tests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersAdaptiveRelativeStrengthIndexV1) || c.IndicatorType == typeof(EhlersAdaptiveRsiFisherTransformV1) || c.IndicatorType == typeof(EhlersAdaptiveStochasticIndicatorV1) || c.IndicatorType == typeof(EhlersAdaptiveCommodityChannelIndexV1)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    private static IIndicator Create(int kind, double fraction, double constant = .015) => kind switch {
        0 => new EhlersAdaptiveRelativeStrengthIndexV1(fraction), 1 => new EhlersAdaptiveRsiFisherTransformV1(),
        2 => new EhlersAdaptiveStochasticIndicatorV1(fraction), _ => new EhlersAdaptiveCommodityChannelIndexV1(fraction, constant) };
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 2, 3 }
        from fraction in new[] { double.Epsilon, .1, .5, 1, 2, double.MaxValue } select new object[] { kind, fraction };
    private static IEnumerable<double[]> Prices() => new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
        [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
        [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
        Enumerable.Repeat(0d, 21).Concat(Enumerable.Repeat(1d, 21)).ToArray(),
        Enumerable.Range(0, 65).Select(i => (i * 13 % 101 - 50) / 8d).ToArray() };

    [Theory, MemberData(nameof(Cases))]
    public async Task CycleFractionsAndPairedOutputsRetainBits(int kind, double fraction)
    {
        var root = Create(kind, fraction); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in Prices()) await Compare([root], ToBars(prices));
    }

    [Fact]
    public async Task ParameterlessFisherKeepsItsHiddenHalfCycle()
    {
        var root = new EhlersAdaptiveRsiFisherTransformV1(); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in Prices()) await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(double.Epsilon)] [InlineData(.001)] [InlineData(.015)] [InlineData(double.MaxValue)]
    public async Task PositiveCommodityConstantsKeepWideQuotientBehavior(double constant) =>
        await Compare([Create(3, 1, constant)], ToBars([0, 0, 1, -1, 3, 2, 0, 5, -2, double.MaxValue, -double.MaxValue]));

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.NegativeInfinity)] [InlineData(double.PositiveInfinity)]
    public async Task InvalidFractionsAndConstantsRetainOptionsErrorPriority(double invalid)
    {
        foreach (var root in new IIndicator[] { Create(0, invalid), Create(2, invalid), Create(3, invalid), Create(3, 1, invalid) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], []); await Compare([root], ToBars([1, 3, 2]));
            await Compare([new EhlersAdaptiveRsiFisherTransformV1(), root], [new(default, 1, 2, 0, 1, double.NaN)]);
        }
    }

    [Fact]
    public async Task CommodityPriceAndCycleRetainDistinctOriginalFields()
    {
        var bars = Enumerable.Range(0, 67).Select(i => new Bar(default, i % 2 == 0 ? 100 : -100,
            i % 3 == 0 ? -20 : 30, i % 5 == 0 ? 40 : -10, Math.Sin(i / 3d), i + 1)).ToArray();
        await Compare([Create(2, .5), Create(3, 1), Create(0, .5)], bars);
    }

    [Fact]
    public async Task SourcesMixedRootsAndCancellationKeepTheirContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var selected in new IIndicator[] { new EhlersAdaptiveRelativeStrengthIndexV1().Of(new Sma(2)),
            new EhlersAdaptiveRsiFisherTransformV1().Of(new Sma(2)), new EhlersAdaptiveStochasticIndicatorV1().Of(new Sma(2)),
            new EhlersAdaptiveCommodityChannelIndexV1().Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars); }
        var repeated = Create(3, 1);
        IIndicator[] roots = [repeated, Create(0, .5), Create(1, .5), Create(2, .5), new EhlersHurstCoefficient(7, 3), repeated];
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
            (Name: "GeneratedAdaptiveRsiV1State", Arguments: new object[] { .5, false }, Slots: 2),
            (Name: "GeneratedAdaptiveRsiV1State", Arguments: new object[] { .5, true }, Slots: 1),
            (Name: "GeneratedAdaptiveRangeV1State", Arguments: new object[] { .5, false, .015 }, Slots: 2),
            (Name: "GeneratedAdaptiveRangeV1State", Arguments: new object[] { 1d, true, .015 }, Slots: 2) })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(config.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = (IMultiOutputState)Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, config.Arguments, null)!;
            double[] Run() { var result = new List<double>(); foreach (var bar in bars) { var outputs = new double[config.Slots]; state.Update(in bar, outputs); result.AddRange(outputs); } return result.ToArray(); }
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
