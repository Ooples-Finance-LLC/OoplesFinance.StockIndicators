using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedBoundedWindowTests
{
    public static IEnumerable<object[]> Configurations => from operation in Enumerable.Range(0, 5)
        from period in new[] { 1, 3, 20, 4096, 4097, int.MaxValue } select new object[] { operation, period };
    private static IndicatorBase Create(int operation, int period) => operation switch
    {
        0 => new Wma(period), 1 => new WilliamsR(period), 2 => new HighestHigh(period),
        3 => new LowestLow(period), _ => new RollingPriceSum(period)
    };

    [Theory, MemberData(nameof(Configurations))]
    public async Task WorkerSeedsMatchEvaluatorBitsAndOwnedSnapshots(int operation, int period)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            double[] values = [-0d, 0d, 1, 1, -.25, 2, -2, .25, 1, -0d, 0d];
            int count = period == int.MaxValue ? 65 : 8193;
            var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0,
                values[i % values.Length], values[(i + 3) % values.Length], values[(i + 5) % values.Length], 1)).ToArray();
            var indicator = Create(operation, period);
            using var expected = await Build(bars, [indicator]).ConfigureBehavior(_ => { }).BuildAsync();
            foreach (int workers in new[] { 1, 8 })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var builder = Build(bars, [indicator]).ConfigureHistory(history);
                using var actual = await builder.BuildAsync();
                Compare(expected, actual, indicator);
                Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                Assert.Equal(bars[^1], actual.Latest.Bar);
                if (history == IndicatorHistoryMode.Full)
                {
                    Assert.Contains("Fused CPU values", builder.LastExecution!.Reason);
                    int index = 0;
                    await foreach (var snapshot in actual) Assert.Equal(bars[index++], snapshot.Bar);
                    Assert.Equal(count, index);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task NewFamiliesPreserveEmptyClampedPeriodsWideValuesMixedAndComposedRoutes(int operation)
    {
        double[] values = [-0d, 0d, double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 1];
        foreach (int period in new[] { -3, 0, 1, 3, int.MaxValue })
        foreach (int count in new[] { 0, 1, 65 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0,
                values[i % 7], values[(i + 1) % 7], values[(i + 2) % 7], 1)).ToArray();
            var indicator = Create(operation, period);
            await CompareRoutes(bars, [indicator]);
            await CompareRoutes(bars, [indicator, new FirstValueEma(3)]);
        }
        var ordinaryBars = Enumerable.Range(0, 65).Select(i => new Bar(default, i, i + 3, i - 1, i + 1, 1)).ToArray();
        var composed = Create(operation, 3).Of(new Sma(3));
        Assert.False(ValuesBarExecution.SupportsOwned([composed]));
        await CompareRoutes(ordinaryBars, [composed]);
    }

    [Theory]
    [InlineData(1004)] [InlineData(1005)] [InlineData(1023)] [InlineData(1024)] [InlineData(8192)]
    public async Task InvalidCapturedTailsPrecedeEarlierOverflowAndDoNotEscapePoolLifetime(int invalidIndex)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, 8193).Select(i => new Bar(default, 0, 1, 0, .5, 1)).ToArray();
            var indicator = new WilliamsR(20);
            using var saved = await Build(bars, [indicator]).BuildAsync();
            var savedValues = saved[indicator].ToArray();
            var original = bars.ToArray();
            bars[0] = new Bar(default, 0, double.Epsilon, 0, double.MaxValue, 1);
            await CompareRoutes(bars, [indicator]);
            bars[invalidIndex] = new Bar(default, 0, 1, 0, .5, double.NaN);
            await CompareRoutes(bars, [indicator]);
            Array.Copy(original, bars, bars.Length);
            using var again = await Build(bars, [indicator]).BuildAsync();
            Assert.Equal(savedValues, saved[indicator].ToArray());
            int index = 0;
            await foreach (var snapshot in saved) Assert.Equal(original[index++], snapshot.Bar);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            var builder = Build(bars, [indicator]);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
            Assert.Null(builder.LastExecution);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(int.MaxValue)]
    public async Task SumStartupWideCancellationAndMixedRootsMatchTheOwnedEvaluator(int period)
    {
        double[] values = [double.MaxValue, double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, -0d, 0d, 1];
        foreach (int count in new[] { 0, 1, 65 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, 0, values[i % values.Length], 1)).ToArray();
            var sum = new RollingPriceSum(period);
            await CompareRoutes(bars, [sum]);
            await CompareRoutes(bars, [sum, new FirstValueEma(3)]);
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(-1)]
    public async Task SumParallelInputErrorsPrecedeOverflowAndSnapshotsSurviveLaterRuns(int sign)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Repeat(new Bar(default, 0, 1, 0, .5, 1), 8193).ToArray();
            var sum = new RollingPriceSum(20);
            using var saved = await Build(bars, [sum]).BuildAsync();
            var values = saved[sum].ToArray();
            for (int i = 0; i < 20; i++) bars[i] = new Bar(default, 0, 1, 0, sign * double.MaxValue, 1);
            await CompareRoutes(bars, [sum]);
            bars[1023] = new Bar(default, 0, 1, 0, .5, double.NaN);
            await CompareRoutes(bars, [sum]);
            Array.Fill(bars, new Bar(default, 0, 1, 0, .25, 1));
            using var again = await Build(bars, [sum]).BuildAsync();
            Assert.Equal(values, saved[sum].ToArray());
            await foreach (var snapshot in saved) Assert.Equal(.5, snapshot.Bar.Close);
            var builder = Build(bars, [sum]);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
            Assert.Null(builder.LastExecution);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    private static async Task CompareRoutes(Bar[] bars, IIndicator[] indicators)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            IIndicatorRun? expected = null, actual = null;
            var expectedError = await Record.ExceptionAsync(async () => expected =
                await Build(bars, indicators).ConfigureHistory(history).ConfigureBehavior(_ => { }).BuildAsync());
            var builder = Build(bars, indicators).ConfigureHistory(history);
            var actualError = await Record.ExceptionAsync(async () => actual = await builder.BuildAsync());
            using (expected) using (actual)
            {
                if (expectedError is not null)
                {
                    Assert.NotNull(actualError);
                    Assert.Equal(expectedError.GetType(), actualError.GetType());
                    Assert.Equal(expectedError.Message, actualError.Message);
                    Assert.Null(builder.LastExecution);
                }
                else
                {
                    Assert.Null(actualError);
                    foreach (var indicator in indicators) Compare(expected!, actual!, indicator);
                }
            }
        }
    }
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator[] indicators) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators);
    private static void Compare(IIndicatorRun expected, IIndicatorRun actual, IIndicator indicator) => Assert.Equal(
        expected[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits),
        actual[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
}
