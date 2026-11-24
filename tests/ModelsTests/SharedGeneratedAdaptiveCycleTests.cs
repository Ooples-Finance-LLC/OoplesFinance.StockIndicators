using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedAdaptiveCycleTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersAdaptiveCyberCycle) || c.IndicatorType == typeof(EhlersAdaptiveCenterOfGravityOscillator) || c.IndicatorType == typeof(EhlersSmoothedAdaptiveMomentum)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    private static IIndicator Create(int kind, int length) => kind switch {
        0 => new EhlersAdaptiveCyberCycle(length), 1 => new EhlersAdaptiveCenterOfGravityOscillator(length),
        _ => new EhlersSmoothedAdaptiveMomentum(length, length, MovingAvgType.ExponentialMovingAverage) };
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2 }
        from length in new[] { int.MinValue, 1, 2, 5, 17, int.MaxValue } select new object[] { kind, length };
    private static IEnumerable<double[]> Prices() => new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
        [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
        Enumerable.Repeat(0d, 21).Concat(Enumerable.Repeat(1d, 21)).ToArray(),
        Enumerable.Range(0, 65).Select(i => (i * 13 % 101 - 50) / 8d).ToArray() };

    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsAndOutputsRetainBits(int kind, int length)
    {
        var root = Create(kind, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in Prices()) await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(double.Epsilon)] [InlineData(.07)] [InlineData(.5)] [InlineData(.9999999999999999)]
    public async Task CyberGainRetainsBoundaryArithmetic(double alpha)
    {
        var root = new EhlersAdaptiveCyberCycle(3, alpha); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in Prices()) await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(-1)] [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)] [InlineData(double.PositiveInfinity)]
    public async Task InvalidGainRetainsOptionsErrorPriority(double alpha)
    {
        var root = new EhlersAdaptiveCyberCycle(5, alpha); Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], []); await Compare([root], ToBars([1, 2, 3]));
        await Compare([Create(1, 5), root], [new(default, 1, 2, 0, 1, double.NaN)]);
    }

    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)] [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)] [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public async Task MomentumAveragesAndIndependentPeriodsRetainTheirRoutes(MovingAvgType average)
    {
        foreach (var pair in new[] { (1, 2), (2, 1), (7, 19), (19, 7), (int.MaxValue, 2), (2, int.MaxValue) })
        {
            var root = new EhlersSmoothedAdaptiveMomentum(pair.Item1, pair.Item2, average);
            Assert.Equal(average != MovingAvgType.WildersSmoothingMethod, ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray()));
        }
    }

    [Fact]
    public async Task SourcesMixedRootsAndCancellationKeepTheirContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var selected in new IIndicator[] { new EhlersAdaptiveCyberCycle().Of(new Sma(2)),
            new EhlersAdaptiveCenterOfGravityOscillator().Of(new Sma(2)),
            new EhlersSmoothedAdaptiveMomentum(5, 8, MovingAvgType.ExponentialMovingAverage).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars); }
        var repeated = Create(2, 5);
        IIndicator[] roots = [repeated, Create(0, 5), Create(1, 5), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
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
            (Name: "GeneratedAdaptiveCyberState", Arguments: new object[] { 5, .07 }, Slots: 2),
            (Name: "GeneratedAdaptiveGravityState", Arguments: new object[] { 5 }, Slots: 1),
            (Name: "GeneratedSmoothedAdaptiveMomentumState", Arguments: new object[] { 5, 8, MovingAvgType.SimpleMovingAverage }, Slots: 2),
            (Name: "GeneratedSmoothedAdaptiveMomentumState", Arguments: new object[] { 5, 8, MovingAvgType.ExponentialMovingAverage }, Slots: 2),
            (Name: "GeneratedSmoothedAdaptiveMomentumState", Arguments: new object[] { 5, 8, MovingAvgType.WeightedMovingAverage }, Slots: 2) })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(config.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, config.Arguments, null)!;
            double[] Run() {
                var result = new List<double>();
                foreach (var bar in bars) {
                    if (state is IIndicatorState scalar) result.Add(scalar.Update(in bar));
                    else { var outputs = new double[config.Slots]; ((IMultiOutputState)state).Update(in bar, outputs); result.AddRange(outputs); }
                }
                return result.ToArray();
            }
            try {
                var expected = Run();
                if (state is IIndicatorState scalar) scalar.Reset(); else ((IMultiOutputState)state).Reset();
                Bits(expected, Run());
            }
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
