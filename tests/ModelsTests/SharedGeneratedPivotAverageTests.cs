using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedPivotAverageTests
{
    private static PivotPointAverage Create(int length) => new PivotPointAverage(length);
    public static IEnumerable<object[]> Cases => from length in new[] { int.MinValue, 0, 1, 2, 14, 32 } select new object[] { length };
    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsKeepBitsErrorsAndOwnership(int length)
    {
        var root = Create(length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, 2, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 2, 4, 1],
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], BarsOf(prices));
    }
    [Fact]
    public async Task SourcesDuplicatesErrorsAndCancellationKeepContracts()
    {
        var root = Create(14); var selected = Create(14).Of(new Sma(2));
        Assert.False(ValuesBarExecution.SupportsOwned([selected]));
        var bars = BarsOf([1, 3, 3, 1, 2]); await Compare([selected], bars);
        IIndicator[] roots = [root, Create(3), root]; Assert.True(ValuesBarExecution.SupportsOwned(roots));
        await Compare(roots, bars);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare([root], [new(default, 1, 2, 0, 1, invalid)]);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }
    public static IEnumerable<object[]> CalendarCases => new[] { InputLength.Minute, InputLength.Hour, InputLength.Day, InputLength.Week, InputLength.Month, InputLength.Year }.Select(p => new object[] { p });
    private static Bar[] CalendarBars() => Enumerable.Range(0, 65).Select(i => new Bar(new DateTime(2023, 12, 25).AddDays(i * 21).AddMinutes(i), i - 2, i + 3, i - 5, i + 1, 1)).ToArray();
    [Theory, MemberData(nameof(CalendarCases))]
    public void IndependentCalendarMeansAndReset(InputLength period)
    {
        foreach (var length in new[] { 3, int.MaxValue })
        {
            var bars = CalendarBars();
            var expected = OoplesFinance.StockIndicators.Validation.BuiltInFormulaReferences.PivotAverageOutputs(bars, length, 1, period);
            var type = typeof(ValuesBarExecution).GetNestedType("GeneratedPivotAverageState", System.Reflection.BindingFlags.NonPublic)!;
            using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [length, period, bars.Length], null)!;
            var state = (IMultiOutputState)disposable;
            var keys = new[] { "Pivot1", "Signal1", "Pivot2", "Signal2", "Pivot3", "Signal3" };
            for (var repeat = 0; repeat < 2; repeat++)
            {
                for (var i = 0; i < bars.Length; i++)
                {
                    var outputs = new double[6]; state.Update(in bars[i], outputs);
                    Bits(keys.Select(key => expected[key][i]).ToArray(), outputs);
                }
                state.Reset();
            }
        }
    }
    [Fact]
    public async Task CustomAveragesRetainFallback()
    {
        Assert.Throws<ArgumentException>("secondAverage", () => new PivotPointAverage(3, secondAverage: new Ema()));
        Assert.Throws<ArgumentException>("thirdAverage", () => new PivotPointAverage(3, maType: new Ema(), thirdAverage: new Ema()));
        IIndicator[] roots = [new PivotPointAverage(3, maType: new Ema()),
            new PivotPointAverage(3, maType: new Ema(), secondAverage: new Ema()),
            new PivotPointAverage(3, maType: new Ema(), secondAverage: new Ema(), thirdAverage: new Ema())];
        foreach (var root in roots) { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], CalendarBars()); }
    }
    [Theory, MemberData(nameof(CalendarCases))]
    public async Task CalendarBoundariesPreserveAllSixOutputs(InputLength period)
    {
        await Compare([new PivotPointAverage(3, period), new PivotPointAverage(7, period)], CalendarBars());
        var start = new DateTime(2024, 1, 1);
        await Compare([new PivotPointAverage(2, period)], Enumerable.Range(0, 65).Select(i => new Bar(start.AddMinutes(i * 15), i, i + 3, i - 2, i + 1, 1)).ToArray());
        await Compare([new PivotPointAverage(2, period)], [new(start, 1, 2, 0, 1, 1), new(start.AddDays(3), 2, 4, -1, 3, 1), new(start.AddDays(1), 5, 7, 2, 4, 1)]);
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
