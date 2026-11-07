using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedGuppyRibbonsTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(GuppyDistanceIndicator) || c.IndicatorType == typeof(GuppyMultipleMovingAverage)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static MultiOutputIndicatorBase Create(int kind, int length) => kind == 0 ? new GuppyDistanceIndicator(length, length, length, length, length, length, length, length, length, length, length, length) : new GuppyMultipleMovingAverage(length, length, length, length, length, length, length, length, length, length, length, length, length, length, length, length, length, length, length, length, length, length, length, length, length, length, length);
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1 } from length in new[] { int.MinValue, 1, 2, 3, 14, 65, int.MaxValue } select new object[] { kind, length };

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

    private static Type RootType(int kind) => kind == 0 ? typeof(GuppyDistanceIndicator) : typeof(GuppyMultipleMovingAverage);
    private static MultiOutputIndicatorBase WithArgument(int kind, int index, object value)
    {
        var constructor = RootType(kind).GetConstructors().Single(); var arguments = constructor.GetParameters().Select(p => p.DefaultValue).ToArray();
        if (value is IMovingAverage)
            foreach (var parameter in constructor.GetParameters().Take(index).Where(p => p.ParameterType == typeof(IMovingAverage)))
                arguments[parameter.Position] = new Ema(2);
        arguments[index] = value; return (MultiOutputIndicatorBase)constructor.Invoke(arguments);
    }
    public static IEnumerable<object[]> PublicPeriods => from kind in new[] { 0, 1 }
        from parameter in RootType(kind).GetConstructors().Single().GetParameters() where parameter.ParameterType == typeof(int)
        select new object[] { kind, parameter.Position };
    public static IEnumerable<object[]> CustomAverageSlots => from kind in new[] { 0, 1 }
        from parameter in RootType(kind).GetConstructors().Single().GetParameters() where parameter.ParameterType == typeof(IMovingAverage)
        select new object[] { kind, parameter.Position };

    [Theory, MemberData(nameof(PublicPeriods))]
    public async Task EveryPublicPeriodRetainsItsMappingAndIgnoredSlots(int kind, int index)
    {
        var root = WithArgument(kind, index, 2); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        var bars = ToBars(Enumerable.Range(0, 97).Select(i => Math.Sin(i * .7)).ToArray()); await Compare([root], bars);
        if (kind == 1 && new[] { 4, 6, 8, 17, 20, 24, 27 }.Contains(index + 1))
        {
            var defaults = new GuppyMultipleMovingAverage();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root, defaults).BuildAsync();
            for (var i = 0; i < 2; i++) Bits(run[defaults.Outputs[i]].ToArray(), run[root.Outputs[i]].ToArray());
        }
    }

    public static IEnumerable<object[]> MissingAveragePrefixes => CustomAverageSlots.Where(row =>
        RootType((int)row[0]).GetConstructors().Single().GetParameters()
            .Take((int)row[1]).Any(p => p.ParameterType == typeof(IMovingAverage)));

    [Theory, MemberData(nameof(MissingAveragePrefixes))]
    public void LaterCustomAverageRequiresPrecedingAverage(int kind, int index)
    {
        var constructor = RootType(kind).GetConstructors().Single();
        var parameters = constructor.GetParameters();
        var arguments = parameters.Select(p => p.DefaultValue).ToArray();
        arguments[index] = new Ema(2);
        var wrapper = Assert.Throws<System.Reflection.TargetInvocationException>(() => constructor.Invoke(arguments));
        var error = Assert.IsType<ArgumentException>(wrapper.InnerException);
        Assert.Equal(parameters[index].Name, error.ParamName);
    }

    [Theory, MemberData(nameof(CustomAverageSlots))]
    public async Task EveryCustomAverageRetainsComponentFallback(int kind, int index)
    { var root = WithArgument(kind, index, new Ema(2)); Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([0, 1, -2, 4, 0, 2, -1])); }

    [Fact]
    public async Task AdjacentRoundedEmaGapsKeepFastAndSlowHandValues()
    {
        var root = new GuppyDistanceIndicator(1, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 1); var bars = ToBars([2, 4, 8]); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([0, 1, 8d - 19d / 3], run[root.Outputs[0]].ToArray()); Bits([0, 1, 8d - 14d / 3], run[root.Outputs[1]].ToArray());
    }

    [Fact]
    public async Task FixedLongRibbonPeriodsAndRawSignalKeepHandValues()
    {
        var root = Create(1, 1); var bars = ToBars([2, 4]); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([0, 700d / 57], run[root.Outputs[0]].ToArray()); Bits([0, (700d / 57) / 2], run[root.Outputs[1]].ToArray());
    }

    [Fact]
    public void OutputShapesKeepRibbonAndSignalOrder()
    {
        var distance = new GuppyDistanceIndicator(); var multiple = new GuppyMultipleMovingAverage(); Assert.Equal(2, distance.Outputs.Count); Assert.Equal(2, multiple.Outputs.Count);
        Assert.Same(distance.Value, distance.Outputs[0]); Assert.Same(distance.SlowDistance, distance.Outputs[1]); Assert.Same(multiple.Value, multiple.Outputs[0]); Assert.Same(multiple.SuperGmmaSignal, multiple.Outputs[1]);
    }

    [Fact]
    public async Task LongHistoryCrossesEveryFixedAndCustomPeriod()
    { await Compare([new GuppyDistanceIndicator(), new GuppyMultipleMovingAverage(), Create(0, 11), Create(1, 65)], ToBars(Enumerable.Range(0, 257).Select(i => Math.Sin(i * .7)).ToArray())); }

    [Theory] [InlineData(0)] [InlineData(1)]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts(int kind)
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = Create(kind, 3).Of(new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        var repeated = Create(kind, 3); IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Theory] [InlineData(0)] [InlineData(1)]
    public void EveryEmaAndSignalReplaysAfterReset(int kind)
    {
        var bars = ToBars(Enumerable.Range(0, 97).Select(i => Math.Sin(i * .7)).ToArray());
        var type = typeof(ValuesBarExecution).GetNestedType(kind == 0 ? "GeneratedGuppyDistanceState" : "GeneratedGuppyMultipleState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IMultiOutputState)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { Enumerable.Range(1, kind == 0 ? 12 : 27).ToArray() }, null)!;
        double[] Run() { var values = new List<double>(); foreach (var bar in bars) { var output = new double[2]; state.Update(in bar, output); values.AddRange(output); } return values.ToArray(); }
        try { var expected = Run(); state.Reset(); Bits(expected, Run()); } finally { (state as IDisposable)?.Dispose(); }
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
