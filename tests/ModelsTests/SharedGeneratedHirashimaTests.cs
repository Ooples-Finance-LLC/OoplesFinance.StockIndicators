using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedHirashimaTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(HirashimaSugitaRS)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => new HirashimaSugitaRS(length);
    public static IEnumerable<object[]> Cases => new[] { int.MinValue, 1, 2, 3, 14, 65, int.MaxValue }.Select(length => new object[] { 0, length });

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
    public async Task CustomWidthAverageRetainsComponentFallback()
    { var root = new HirashimaSugitaRS(3, new Ema(2)); Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 7, 2, -3, 4, 0, 2])); }

    [Fact]
    public async Task HandResidualFitsKeepFiveBandWidthsAndCenter()
    {
        var root = new HirashimaSugitaRS(3); var bars = ToBars([0, 0, 9]); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        var center = 53d / 6; var offsets = new[] { 1, 2, 0, -1, -2 };
        for (var i = 0; i < offsets.Length; i++) Bits([0, 0, (ReferenceFraction.FromDouble(center) + new ReferenceFraction(3 * offsets[i])).ToDouble()], run[root.Outputs[i]].ToArray());
    }

    [Fact]
    public void OutputsKeepAllFiveBandsAndMiddlePrimary()
    {
        var root = new HirashimaSugitaRS(); Assert.Equal(5, root.Outputs.Count);
        Assert.Same(root.UpperBand1, root.Outputs[0]); Assert.Same(root.UpperBand2, root.Outputs[1]); Assert.Same(root.Value, root.Outputs[2]); Assert.Same(root.LowerBand1, root.Outputs[3]); Assert.Same(root.LowerBand2, root.Outputs[4]); Assert.Same(root.Value, root.PrimaryOutput);
    }

    [Fact]
    public void SpanWriterPreservesIndependentBandsTradePreviewAndReset()
    {
        var bars = ToBars([4, -2, 7, 0, 9, -3, 1, 2]); var keys = new[] { "UpperBand1", "UpperBand2", "MiddleBand", "LowerBand1", "LowerBand2" };
        var expected = BuiltInFormulaReferences.HirashimaValues(bars, 3, 2); var window = new HirashimaWindow(MovingAvgType.WeightedMovingAverage, 3);
        for (var repeat = 0; repeat < 2; repeat++)
        {
            window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                window.Next(-999, false, new double[5]);
                foreach (var final in new[] { false, false, true })
                {
                    var output = new double[5]; var trade = window.Next(bars[i].Close, final, output); Assert.Equal(expected.Signals[i], trade);
                    for (var slot = 0; slot < 5; slot++) Bits([expected.Outputs[keys[slot]][i]], [output[slot]]);
                }
            }
        }
    }

    [Fact]
    public async Task SeveralRegressionAndWidthExpirationsRetainBits()
    { await Compare([new HirashimaSugitaRS(11), new HirashimaSugitaRS(65)], ToBars(Enumerable.Range(0, 257).Select(i => Math.Sin(i * .7)).ToArray())); }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = new HirashimaSugitaRS(3).Of(new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        var repeated = new HirashimaSugitaRS(3); IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void RegressionStateReplaysAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedHirashimaState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IMultiOutputState)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new object[] { 3 }, null)!;
        double[] Run() { var values = new List<double>(); foreach (var bar in bars) { var output = new double[5]; state.Update(in bar, output); values.AddRange(output); } return values.ToArray(); }
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
