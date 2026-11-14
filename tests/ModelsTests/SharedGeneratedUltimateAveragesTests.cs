using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedUltimateAveragesTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(UltimateMovingAverage) || c.IndicatorType == typeof(UltimateMovingAverageBands)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind == 0 ? new UltimateMovingAverage(length) : new UltimateMovingAverageBands(length, 5);
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1 } from length in new[] { int.MinValue, 1, 2, 3, 14, 65 } select new object[] { kind, length };

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
    public async Task ScalarObsoleteLengthKeepsFixedDefaults()
    {
        var bars = ToBars([1, 3, 2, 0]); var shortRoot = new UltimateMovingAverage(1); var longRoot = new UltimateMovingAverage(int.MaxValue);
        await Compare([shortRoot, longRoot], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(shortRoot, longRoot).BuildAsync();
        Bits(run[shortRoot.Value].ToArray(), run[longRoot.Value].ToArray());
    }
    [Fact]
    public async Task ExactPowerWeightsAndBandsKeepHandValues()
    {
        Bar[] bars = [new(default, 1, 1, 1, 1, 1), new(default, 3, 3, 3, 3, 1), new(default, 5, 5, 5, 5, 1)];
        var root = new UltimateMovingAverageBands(2, 2, 2); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([32d / 33, 97d / 33, 163d / 33], run[root.Value].ToArray());
        Bits([32d / 33, 163d / 33, 229d / 33], run[root.UpperBand].ToArray());
        Bits([32d / 33, 31d / 33, 97d / 33], run[root.LowerBand].ToArray());
    }
    [Fact]
    public async Task ScalarStartupUsesFullDefaultPowerDenominator()
    {
        var root = new UltimateMovingAverage(1); Bar[] bars = [new(default, 1, 1, 1, 1, 1)]; await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([312500000d / 2763020625], run[root.Value].ToArray());
    }
    [Theory] [InlineData(-2d)] [InlineData(0d)] [InlineData(2d)]
    public async Task FiniteBandMultipliersKeepBits(double multiplier)
    { await Compare([new UltimateMovingAverageBands(2, 3, multiplier)], ToBars([1, 3, -2, 4])); }
    [Fact]
    public void NonfiniteMultipliersKeepFallback()
    { foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) Assert.False(ValuesBarExecution.SupportsOwned([new UltimateMovingAverageBands(2, 3, value)])); }
    [Fact]
    public async Task CustomAveragesKeepFallback()
    {
        foreach (var root in new IIndicator[] { new UltimateMovingAverage(3, new Sma(2)), new UltimateMovingAverageBands(2, 3, maType: new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 3, -2, 4])); }
    }
    [Fact]
    public void OutputOrderKeepsMiddlePrimary()
    {
        Assert.Single(new UltimateMovingAverage(14).Outputs); var root = new UltimateMovingAverageBands(); Assert.Equal(3, root.Outputs.Count);
        Assert.Same(root.UpperBand, root.Outputs[0]); Assert.Same(root.Value, root.Outputs[1]); Assert.Same(root.LowerBand, root.Outputs[2]); Assert.Same(root.Value, root.PrimaryOutput);
    }
    [Theory] [InlineData(0)] [InlineData(1)]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts(int kind)
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = Create(kind, 3) switch { IndicatorBase scalar => (IIndicator)scalar.Of(new Sma(2)), MultiOutputIndicatorBase multi => multi.Of(new Sma(2)), _ => throw new InvalidOperationException() }; Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        var repeated = Create(kind, 3); IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }
    [Theory] [InlineData(0)] [InlineData(1)]
    public void BothKernelsReplayAfterReset(int kind)
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        var type = typeof(ValuesBarExecution).GetNestedType(kind == 0 ? "GeneratedUltimateAverageState" : "GeneratedUltimateBandsState", System.Reflection.BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, kind == 0 ? Array.Empty<object>() : new object[] { 2, 3, 2d }, null)!;
        double[] Run() { var values = new List<double>(); foreach (var bar in bars) { if (state is IIndicatorState scalar) values.Add(scalar.Update(in bar)); else { var output = new double[3]; ((IMultiOutputState)state).Update(in bar, output); values.AddRange(output); } } return values.ToArray(); }
        try { var expected = Run(); if (state is IIndicatorState scalar) scalar.Reset(); else ((IMultiOutputState)state).Reset(); Bits(expected, Run()); } finally { ((IDisposable)state).Dispose(); }
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
