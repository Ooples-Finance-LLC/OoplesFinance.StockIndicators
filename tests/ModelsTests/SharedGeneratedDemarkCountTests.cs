using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedDemarkCountTests
{
    public static IEnumerable<object[]> Cases =>
        from family in Enumerable.Range(0, 3)
        from period in new[] { int.MinValue, -3, 0, 1, 2, 3, 20, 4096 }
        select new object[] { family, period };

    private static IIndicator Create(int family, int period) => family switch
    {
        0 => new DemarkSetupIndicator(period), 1 => new DemarkReversalPoints(period, 4),
        2 => new DemarkReversalPoints(9, period),
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };

    [Theory, MemberData(nameof(Cases))]
    public async Task StagedRoundingOwnershipAndExceptionalValuesMatchLegacy(int family, int period)
    {
        foreach (var values in new double[][]
        {
            [], [1], [-0d, 0d, -double.Epsilon, 0d, double.Epsilon],
            [double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 0],
            [double.Epsilon, double.MaxValue, 1, 2, 3, 4, 5, 6, 7],
            [1, -1, 0, 0, 2, -2, 0, 3, -3],
            Enumerable.Range(0, 257).Select(i => 100 + (i * 13 % 101) / 8d).ToArray(),
            Enumerable.Range(0, 65).Select(i => Math.ScaleB(i % 13 - 6d, i % 2 == 0 ? -1022 : 900)).ToArray()
        })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = values.Select((v, i) => new Bar(default, v / 4, Math.Abs(v), -Math.Abs(v) / 2, v, i % 7 == 0 ? 0 : i % 7 == 1 ? double.Epsilon : i % 7 == 2 ? double.MaxValue : i % 7 == 3 ? -7 : i % 7)).ToArray();
            var indicator = Create(family, period);
            Assert.True(ValuesBarExecution.SupportsOwned([indicator]));
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(history);
            IIndicatorRun? expected = null, actual = null;
            try
            {
                var referenceBuilder = Builder().ConfigureBehavior(_ => { });
                var expectedError = await Record.ExceptionAsync(async () => expected = await referenceBuilder.BuildAsync());
                var builder = Builder();
                var actualError = await Record.ExceptionAsync(async () => actual = await builder.BuildAsync());
                Assert.Equal(expectedError?.GetType(), actualError?.GetType());
                Assert.Equal(expectedError?.Message, actualError?.Message);
                if (expectedError is not null) { Assert.Null(builder.LastExecution); continue; }
                Assert.NotNull(expected);
                Assert.NotNull(actual);
                if (history == IndicatorHistoryMode.Full && ValuesBarExecution.SupportsOwned([indicator])) Assert.False(((IndicatorRun)actual!).HasLegacyRuntime);
                foreach (var output in indicator.Outputs) Bits(expected![output].ToArray(), actual![output].ToArray());
                if (bars.Length > 0)
                {
                    Assert.Equal(expected.Latest.Bar, actual.Latest.Bar);
                    Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                }
                actual.Dispose();
                Array.Clear(bars);
                foreach (var output in indicator.Outputs) Bits(expected[output].ToArray(), actual![output].ToArray());
            }
            finally { expected?.Dispose(); actual?.Dispose(); }
        }
    }

    [Fact]
    public async Task MixedRootsRepeatedBuildsAndInvalidTailPreserveStateAndValidationPriority()
    {
        IIndicator[] roots = [new DemarkSetupIndicator(3), new DemarkReversalPoints(3, 2), new Cci(3), new Wwma(3)];
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 0, 1, -1, i % 11 - 5d, 1)).ToArray();
        StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators([.. roots, roots[0]]);
        using var expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync();
        var builder = Builder();
        for (int repeat = 0; repeat < 2; repeat++)
        {
            using var actual = await builder.BuildAsync();
            foreach (var indicator in roots) foreach (var output in indicator.Outputs) Bits(expected[output].ToArray(), actual![output].ToArray());
        }
        bars[0] = new(default, 0, 1, -1, double.MaxValue, 1);
        bars[1] = bars[0];
        bars[^1] = new(default, 0, 1, -1, 0, double.NaN);
        var referenceError = await Record.ExceptionAsync(() => Builder().ConfigureBehavior(_ => { }).BuildAsync());
        var actualError = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.IsType<ArgumentOutOfRangeException>(referenceError);
        Assert.Equal(referenceError!.Message, actualError!.Message);
        Assert.Equal(referenceError.GetType(), actualError.GetType());
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
    }

    [Fact]
    public async Task LargeValidWindowsAndLagsUseBoundedIndependentStartup()
    {
        var bars = new[] { -0d, 0d, double.Epsilon, -double.Epsilon, 1, -1, double.MaxValue, -double.MaxValue }
            .Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
        IIndicator[] indicators = [new DemarkSetupIndicator(1_073_741_824),
            new DemarkReversalPoints(int.MaxValue, 1), new DemarkReversalPoints(1, int.MaxValue)];
        Assert.True(ValuesBarExecution.SupportsOwned(indicators));
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicators).ConfigureHistory(history).BuildAsync();
            Bits(new double[bars.Length], actual[indicators[0].Outputs[0]].ToArray());
            Bits(new double[bars.Length], actual[indicators[1].Outputs[0]].ToArray());
            Bits(bars.Select(b => b.Close == 0 ? 0d : b.Close).ToArray(), actual[indicators[2].Outputs[0]].ToArray());
        }
    }

    [Fact]
    public async Task OverflowingLegacyIndexRetainsFallbackAndError()
    {
        Assert.False(ValuesBarExecution.SupportsOwned([new DemarkSetupIndicator(int.MaxValue)]));
        Assert.False(ValuesBarExecution.SupportsOwned([new DemarkReversalPoints(int.MaxValue, 2)]));
        var indicator = new DemarkReversalPoints(2, int.MaxValue);
        Assert.False(ValuesBarExecution.SupportsOwned([indicator]));
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(new[] { new Bar(default, 0, 1, -1, 1, 1) }))
                .ConfigureIndicators(indicator).ConfigureHistory(history);
            var expected = await Record.ExceptionAsync(() => Builder().ConfigureBehavior(_ => { }).BuildAsync());
            var actual = await Record.ExceptionAsync(() => Builder().BuildAsync());
            Assert.NotNull(expected);
            Assert.Equal(expected.GetType(), actual?.GetType());
            Assert.Equal(expected.Message, actual?.Message);
        }
    }

    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(DemarkSetupIndicator) || c.IndicatorType == typeof(DemarkReversalPoints)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(NumericalCases))]
    public async Task ExistingIndependentNumericalAndSourceContractsRemainValid(IndicatorValidationCase testCase)
    {
        var checks = new OrdinalFamilyNumericalTests();
        await checks.PublicConfigurationsPassEveryNumericalClass(testCase);
        await checks.SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
        checks.EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
        foreach (var route in new[] { "batch", "fast", "native", "streaming" })
            checks.AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(testCase, route);
    }

    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
