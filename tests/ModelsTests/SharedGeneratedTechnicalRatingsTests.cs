using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedTechnicalRatingsTests
{
    private static TechnicalRatings Create(int operation, int length) => new TechnicalRatings(length, 34, 14, 14, 3, 3);
    public static IEnumerable<object[]> Cases => from operation in new[] { 0 } from length in new[] { int.MinValue, 0, 1, 2, 14, 32 } select new object[] { operation, length };
    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsKeepBitsErrorsAndOwnership(int operation, int length)
    {
        var root = Create(operation, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, 2, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 2, 4, 1],
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], BarsOf(prices));
    }
    [Theory, InlineData(0)]
    public async Task SourcesDuplicatesErrorsAndCancellationKeepContracts(int operation)
    {
        var root = Create(operation, 14); var selected = Create(operation, 14).Of(new Sma(2));
        Assert.False(ValuesBarExecution.SupportsOwned([selected]));
        var bars = BarsOf([1, 3, 3, 1, 2]); await Compare([selected], bars);
        IIndicator[] roots = [root, Create(operation, 3), root]; Assert.True(ValuesBarExecution.SupportsOwned(roots));
        await Compare(roots, bars);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare([root], [new(default, 1, 2, 0, 1, invalid)]);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }
    [Fact]
    public void IndependentVotesKeepOutputOrderAndReset()
    {
        var bars = BarsOf([1, 4, -2, 5, 0, 2, 4, -1]);
        var root = new TechnicalRatings(2, 3, 2, 3, 2, 1);
        var expected = BuiltInFormulaReferences.TechnicalRatingValues(bars, (IBuiltInIndicator)root);
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedTechnicalRatingsState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [2, 3, 2, 3, 2, 1], null)!;
        var state = (IMultiOutputState)disposable;
        string[] keys = ["Tr", "Or", "Mr"];
        for (var repeat = 0; repeat < 2; repeat++)
        {
            for (var i = 0; i < bars.Length; i++)
            {
                var outputs = new double[3]; state.Update(in bars[i], outputs);
                Bits(keys.Select(key => expected[key][i]).ToArray(), outputs);
            }
            state.Reset();
        }
    }
    [Fact]
    public void InternalValuesPreservePublicPreviewCommitAndReset()
    {
        using var values = new TechnicalRatingsState(); using var published = new TechnicalRatingsState();
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var price in new[] { 1d, 4, -2, 5, 0, 2 })
            {
                var bar = new OhlcvBar("TEST", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, price, price + 2, price - 2, price, 1, true);
                foreach (var final in new[] { false, false, true })
                {
                    var a = values.NextValues(bar, final); var b = published.Update(bar, final, true);
                    Bits([b.Outputs!["Tr"], b.Outputs["Or"], b.Outputs["Mr"]], [a.Total, a.Oscillator, a.Averages]);
                }
            }
            values.Reset(); published.Reset();
        }
    }
    [Fact]
    public async Task CustomAverageRetainsFallback()
    {
        var root = new TechnicalRatings(3, 5, 3, 3, 2, 2, new Sma());
        Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], BarsOf([1, 3, 2, 4]));
    }
    [Fact]
    public async Task AllSixBuilderPeriodsKeepTheirOriginalRoles()
    {
        await Compare([new TechnicalRatings(2, 7, 4, 5, 3, 1), new TechnicalRatings(5, 2, 7, 3, 2, int.MaxValue)],
            BarsOf([1, 3, 2, 4, -1, 2, 5, 1]));
    }
    private static Bar[] BarsOf(double[] prices) => prices.Select(v => new Bar(default, v / 2, Math.Abs(v) + 1, -Math.Abs(v) - 2, v, 1)).ToArray();
    private static async Task Compare(IIndicator[] roots, Bar[] bars)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(roots).ConfigureHistory(history);
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
                        if (bars.Length > 0)
                        {
                            Assert.Equal(expected.Latest.Bar, actual.Latest.Bar);
                            Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                        }
                        actual.Dispose();
                        foreach (var root in roots) foreach (var output in root.Outputs)
                            Assert.Equal(bars.Length, expected[output].ToArray().Length);
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
