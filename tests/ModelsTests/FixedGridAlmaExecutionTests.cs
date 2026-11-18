using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class FixedGridAlmaExecutionTests
{
    private static StockIndicatorBuilder Build(Bar[] bars, ArnaudLegouxWindow indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
            .ConfigureExecution(IndicatorExecutionBackend.Cpu);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WordDotsPreserveBothStartupModesEveryColumnAndOwnedHistory(bool fullWindow)
    {
        var bars = Enumerable.Range(0, 8193).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), i, 3, -2, (i * 13 % 101 - 50) / 8d, i % 7 - 3)).ToArray();
        var indicator = new ArnaudLegouxWindow(20, fullWindowOnly: fullWindow);
        using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 8 })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var input = bars.ToArray();
                using var actual = await Build(input, indicator).ConfigureHistory(history).BuildAsync();
                Array.Clear(input); actual.Dispose();
                AssertColumns(expected, actual, indicator);
                var values = actual[indicator.Value].ToArray();
                var present = actual[indicator.IsDefined].ToArray();
                foreach (int row in new[] { 0, 18, 19, 511, 512, 1023, 1024, 4095, 4096, bars.Length - 1 })
                    AssertIndependent(bars, indicator, row, values[row], present[row]);
                Assert.Equal(bars[^1], actual.Latest.Bar);
                if (history == IndicatorHistoryMode.Full)
                {
                    int row = 0;
                    await foreach (var snapshot in actual) Assert.Equal(bars[row++], snapshot.Bar);
                    Assert.Equal(bars.Length, row);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(55)] [InlineData(56)]
    public async Task ProductMassBoundAcceptsTheLastWordGridAndRejectsTheNext(int power)
    {
        // The period20 mass has72 bits on the original Gaussian weight grid.
        // A retained unit close fixes the price grid; 2^55*mass fits signed127
        // bits while 2^56*mass does not. Negative closes also exercise signs.
        var bars = Enumerable.Range(0, 8192).Select(i =>
            new Bar(default, 0, 1, 0, i == 0 ? 1 : -Math.ScaleB(1, power), 1)).ToArray();
        var indicator = new ArnaudLegouxWindow(20);
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            // Keep the initial fine scale after its unit close expires.
            CpuParallelSettings.MaxDegreeOfParallelism = 1;
            using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var actual = await Build(bars, indicator).ConfigureHistory(history).BuildAsync();
                AssertColumns(expected, actual, indicator);
                Assert.Equal(BitConverter.DoubleToInt64Bits(-Math.ScaleB(1, power)),
                    BitConverter.DoubleToInt64Bits(actual[indicator.Value].ToArray()[^1]));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SubnormalMeanKeepsNegativeUnderflowAndCanonicalExactZero(bool fullWindow)
    {
        var bars = Enumerable.Range(0, 8192).Select(i =>
            new Bar(default, 0, 1, 0, i % 20 == 0 ? -double.Epsilon : BitConverter.Int64BitsToDouble(long.MinValue), 1)).ToArray();
        var indicator = new ArnaudLegouxWindow(20, 0, 0, fullWindow);
        using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var actual = await Build(bars, indicator).ConfigureHistory(history).BuildAsync();
            AssertColumns(expected, actual, indicator);
            var values = actual[indicator.Value].ToArray();
            var present = actual[indicator.IsDefined].ToArray();
            for (int row = 19; row < bars.Length; row++)
            {
                Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(values[row]));
                Assert.Equal(1d, present[row]);
            }
        }
        Array.Fill(bars, new Bar(default, 0, 1, 0, BitConverter.Int64BitsToDouble(long.MinValue), 1));
        using var zero = await Build(bars, indicator).BuildAsync();
        Assert.All(zero[indicator.Value].ToArray(), value => Assert.Equal(0L, BitConverter.DoubleToInt64Bits(value)));
    }

    [Theory]
    [InlineData(0d, 0d, false)] [InlineData(2d, -6d, false)]
    [InlineData(.85, 20d, false)] [InlineData(.5, double.MaxValue, true)]
    public async Task OriginalNonDefaultWeightsAndWideWeightFallbackKeepExactRounding(double offset, double sigma, bool fullWindow)
    {
        var bars = Enumerable.Range(0, 8192).Select(i =>
            new Bar(default, 0, 1, 0, (i % 29 - 14) / 8d, 1)).ToArray();
        var indicator = new ArnaudLegouxWindow(20, offset, sigma, fullWindow);
        using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var actual = await Build(bars, indicator).ConfigureHistory(history).BuildAsync();
            AssertColumns(expected, actual, indicator);
            var values = actual[indicator.Value].ToArray(); var present = actual[indicator.IsDefined].ToArray();
            foreach (int row in new[] { 0, 18, 19, 1023, 1024, bars.Length - 1 })
                AssertIndependent(bars, indicator, row, values[row], present[row]);
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(4096)]
    public async Task SmallestAndLargestEnabledPeriodsKeepExactNewestPeakAndPresence(int period)
    {
        var bars = Enumerable.Range(0, 8192).Select(i =>
            new Bar(default, 0, 1, 0, i % 17 == 0 ? BitConverter.Int64BitsToDouble(long.MinValue) : (i % 101 - 50) / 8d, 1)).ToArray();
        // Exact center at the newest index and extreme inverse width leave
        // only that coefficient equal to1; every other coefficient is zero.
        var indicator = new ArnaudLegouxWindow(period, 1, double.MaxValue);
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var actual = await Build(bars, indicator).ConfigureHistory(history).BuildAsync();
                var values = actual[indicator.Value].ToArray(); var present = actual[indicator.IsDefined].ToArray();
                for (int row = 0; row < bars.Length; row++)
                {
                    bool defined = row >= period - 1;
                    double expected = !defined || bars[row].Close == 0 ? 0 : bars[row].Close;
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(values[row]));
                    Assert.Equal(defined ? 1d : 0d, present[row]);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CapturedFallbackValidatesUnusedFieldsBeforeArithmeticAndRecovers(bool fullWindow)
    {
        var bars = Enumerable.Range(0, 8192).Select(i => new Bar(default, 0, 1, 0, i / 8d, 1)).ToArray();
        bars[3] = new Bar(default, 0, 1, 0, double.MaxValue, 1);
        bars[^1] = new Bar(default, 0, 1, 0, 1, double.NaN);
        var indicator = new ArnaudLegouxWindow(20, fullWindowOnly: fullWindow);
        var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
        Assert.NotNull(expected);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var actual = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureHistory(history).BuildAsync());
            Assert.NotNull(actual); Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                Build(bars, indicator).ConfigureHistory(history).BuildAsync(new CancellationToken(true)));
        }
        bars[3] = bars[^1] = new Bar(default, 0, 1, 0, 1, 1);
        using var recovered = await Build(bars, indicator).BuildAsync();
        Assert.All(recovered[indicator.Value].ToArray(), value => Assert.True(double.IsFinite(value)));
    }

    private static void AssertColumns(IIndicatorRun expected, IIndicatorRun actual, ArnaudLegouxWindow indicator)
    {
        foreach (var output in indicator.Outputs)
            Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }

    private static void AssertIndependent(Bar[] bars, ArnaudLegouxWindow indicator, int row, double value, double present)
    {
        int n = Math.Min(indicator.Period, row + 1);
        if (indicator.FullWindowOnly && n < indicator.Period)
        { Assert.Equal(0d, present); Assert.Equal(0L, BitConverter.DoubleToInt64Bits(value)); return; }
        var center = ReferenceFraction.FromDouble(indicator.Offset) * new ReferenceFraction(n - 1);
        var distances = Enumerable.Range(0, n).Select(i =>
        { var delta = new ReferenceFraction(i) - center; return delta * delta; }).ToArray();
        var minimum = distances.Min();
        var sigma = ReferenceFraction.FromDouble(indicator.Sigma);
        var scale = sigma * sigma / new ReferenceFraction(2L * n * n);
        ReferenceFraction numerator = new(0), denominator = new(0);
        for (int i = 0; i < n; i++)
        {
            var weight = ReferenceFraction.FromDouble(Math.Exp(-((distances[i] - minimum) * scale).ToDouble()));
            numerator += weight * ReferenceFraction.FromDouble(bars[row - n + 1 + i].Close);
            denominator += weight;
        }
        Assert.Equal(1d, present);
        Assert.Equal(BitConverter.DoubleToInt64Bits((numerator / denominator).ToDouble()), BitConverter.DoubleToInt64Bits(value));
    }
}
