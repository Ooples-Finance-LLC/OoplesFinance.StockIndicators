using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedPointwiseExecutionTests
{
    public static IEnumerable<object[]> Operations => Enumerable.Range(0, 15).Select(i => new object[] { i });
    private static MultiOutputIndicatorBase Create(int operation) => operation < 6
        ? new PriceCircularTransform((PriceCircularOperation)operation)
        : operation < 12 ? new PriceTranscendentalTransform((PriceTranscendentalOperation)(operation - 6))
        : new PriceRoundingTransform((PriceRoundingOperation)(operation - 12));

    [Theory, MemberData(nameof(Operations))]
    public async Task FusedTransformsPreserveEveryBitPresenceAndOwnedHistory(int operation)
    {
        double[] domain = [-0d, 0d, double.Epsilon, -double.Epsilon, -1d, 1d,
            Math.BitIncrement(1d), Math.BitDecrement(-1d), .17, -1.5, 1.5, .999999999];
        foreach (int count in new[] { 0, 1, 8193, 65537 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                3, 4, 2, domain[i % domain.Length], i)).ToArray();
            var original = bars.ToArray();
            var indicator = Create(operation);
            var state = (IMultiOutputState)indicator.CreateState();
            var expected = new[] { new double[count], new double[count] };
            var scratch = new double[2];
            for (int i = 0; i < count; i++)
            {
                state.Update(in bars[i], scratch);
                expected[0][i] = scratch[0];
                expected[1][i] = scratch[1];
            }
            using var full = await Build(bars, indicator, IndicatorHistoryMode.Full);
            using var latest = await Build(bars, indicator, IndicatorHistoryMode.LatestOnly);
            Assert.False(((IndicatorRun)full).HasLegacyRuntime);
            Array.Clear(bars);
            for (int slot = 0; slot < 2; slot++)
            {
                Bits(expected[slot], full[indicator.Outputs[slot]].ToArray());
                Bits(expected[slot], latest[indicator.Outputs[slot]].ToArray());
            }
            int index = 0;
            await foreach (var snapshot in full)
            {
                Assert.Equal(original[index], snapshot.Bar);
                index++;
            }
            Assert.Equal(count, index);
            if (count > 0) Assert.Equal(original[^1], latest.Latest.Bar);
            full.Dispose();
            latest.Dispose();
            Bits(expected[0], full[indicator.Outputs[0]].ToArray());
            Bits(expected[1], latest[indicator.Outputs[1]].ToArray());
        }
    }

    [Theory]
    [InlineData(8)] [InlineData(9)] [InlineData(10)]
    public async Task InputValidationPrecedesOverflowAcrossWorkerRegionsAndRecovery(int operation)
    {
        foreach (var history in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        {
            var bars = Enumerable.Repeat(new Bar(default, 1, 2, 0, .5, 1), 65537).ToArray();
            bars[1] = new Bar(default, 1, 2, 0, 1000, 1);
            bars[^1] = new Bar(default, 1, 2, 0, .5, double.NaN);
            var indicator = Create(operation);
            var builder = Builder(bars, indicator, history);
            var expectedInput = Record.Exception(() => OoplesFinance.StockIndicators.Validation.IndicatorInputDomain.Finite.Validate(in bars[^1]));
            var inputError = await Record.ExceptionAsync(() => builder.BuildAsync());
            Assert.Equal(expectedInput!.GetType(), inputError!.GetType());
            Assert.Equal(expectedInput.Message, inputError.Message);
            Assert.Null(builder.LastExecution);
            bars[^1] = bars[0];
            var ordinary = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars, bar => bar))
                .ConfigureIndicators(indicator).ConfigureHistory(history);
            var expectedOutput = await Record.ExceptionAsync(() => ordinary.BuildAsync());
            var outputError = await Record.ExceptionAsync(() => builder.BuildAsync());
            Assert.NotNull(outputError);
            Assert.Equal(expectedOutput!.GetType(), outputError.GetType());
            Assert.Equal(expectedOutput.Message, outputError.Message);
            Assert.Null(builder.LastExecution);
            bars[1] = bars[0];
            using var result = await builder.BuildAsync();
            Assert.Equal(bars.Length, result.BarCount);
        }
    }

    [Theory, MemberData(nameof(Operations))]
    public async Task ExtremeFiniteInputsPreservePrimitiveResults(int operation)
    {
        double limit = operation is 8 or 9 or 10 ? 700 : double.MaxValue;
        double[] inputs = [limit, -limit, double.Epsilon, -double.Epsilon, -0d, 0d];
        var bars = Enumerable.Range(0, 8193).Select(i => new Bar(default, 1, 2, 0, inputs[i % inputs.Length], 1)).ToArray();
        var indicator = Create(operation);
        var state = (IMultiOutputState)indicator.CreateState();
        using var run = await Build(bars, indicator, IndicatorHistoryMode.LatestOnly);
        var expected = new double[2];
        for (int i = 0; i < bars.Length; i++)
        {
            state.Update(in bars[i], expected);
            for (int slot = 0; slot < 2; slot++)
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot]),
                    BitConverter.DoubleToInt64Bits(run[indicator.Outputs[slot]][i]));
        }
    }

    [Fact]
    public async Task LaterLegacyBuildUsesOwnedPointwiseHistory()
    {
        var input = Enumerable.Range(0, 25).Select(i => (double)i).ToArray();
        var bars = input.Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
        var builder = Builder(bars, Create(0), IndicatorHistoryMode.Full);
        var run = await builder.BuildAsync();
        var snapshot = run.Latest;
        run.Dispose();
        Array.Clear(bars);
        SeriesHandle average = default;
        using var legacy = builder.ConfigureIndicators(catalog => average = catalog.Ema(3)).Build();
        legacy.Start();
        var expected = new double[input.Length];
        OoplesFinance.StockIndicators.Core.MovingAverageCore.ExponentialMovingAverage(input, expected, 3);
        Bits(expected, legacy.GetSeries(average).ToArray());
        Assert.Equal(24d, snapshot.Bar.Close);
    }

    [Fact]
    public async Task CancellationRequiredGpuAndComposedFallbackKeepTheirContracts()
    {
        var bars = Enumerable.Repeat(new Bar(default, 1, 2, 0, .5, 1), 8193).ToArray();
        var indicator = Create(6);
        var builder = Builder(bars, indicator, IndicatorHistoryMode.Full);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancellation.Token));
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        Assert.Null(builder.LastExecution);
        indicator.Of(new Sma(20));
        Assert.False(ValuesBarExecution.IsPointwise(indicator));
        using var actual = await builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
        using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars, b => b))
            .ConfigureIndicators(indicator).BuildAsync();
        Bits(expected[indicator.Outputs[0]].ToArray(), actual[indicator.Outputs[0]].ToArray());
        Bits(expected[indicator.Outputs[1]].ToArray(), actual[indicator.Outputs[1]].ToArray());
    }

    [Fact]
    public async Task ConcurrentTransformsKeepIndependentResults()
    {
        var tasks = Enumerable.Range(0, 15).Select(operation => Task.Run(async () =>
        {
            var indicator = Create(operation);
            var bars = Enumerable.Repeat(new Bar(default, 1, 2, 0, .5, 1), 10000).ToArray();
            using var run = await Build(bars, indicator, operation % 2 == 0 ? IndicatorHistoryMode.Full : IndicatorHistoryMode.LatestOnly);
            Array.Clear(bars);
            var state = (IMultiOutputState)indicator.CreateState();
            var expected = new double[2];
            var bar = new Bar(default, 1, 2, 0, .5, 1);
            state.Update(in bar, expected);
            foreach (var value in run[indicator.Outputs[0]].ToArray())
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[0]), BitConverter.DoubleToInt64Bits(value));
        }));
        await Task.WhenAll(tasks);
    }

    [Theory]
    [InlineData(8193)] [InlineData(65535)] [InlineData(65536)] [InlineData(65537)]
    public async Task SharedPresenceAndReusedBitmapsCannotMutateRetainedResults(int count)
    {
        var indicator = new PriceCircularTransform(PriceCircularOperation.ArcCosine);
        var bars = Enumerable.Repeat(new Bar(default, 1, 2, 0, .5, 1), count).ToArray();
        var first = await Build(bars, indicator, IndicatorHistoryMode.LatestOnly);
        first.Dispose();
        int[] missing = [0, 63, 64, count / 2 - 1, count / 2, count - 1];
        foreach (int i in missing) bars[i] = new Bar(default, 1, 2, 0, 2, 1);
        using var mixed = await Build(bars, indicator, IndicatorHistoryMode.Full);
        foreach (int i in missing) bars[i] = new Bar(default, 1, 2, 0, .5, 1);
        using var again = await Build(bars, indicator, IndicatorHistoryMode.LatestOnly);
        using var differentSize = await Build(bars.Take(count - 1).ToArray(), indicator, IndicatorHistoryMode.LatestOnly);
        var actual = mixed[indicator.IsDefined].ToArray();
        for (int i = 0; i < count; i++) Assert.Equal(missing.Contains(i) ? 0d : 1d, actual[i]);
        Assert.All(first[indicator.IsDefined].ToArray(), value => Assert.Equal(1d, value));
        Assert.All(again[indicator.IsDefined].ToArray(), value => Assert.Equal(1d, value));
        Assert.Equal(count - 1, differentSize[indicator.IsDefined].Length);
    }

    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
    private static Task<IIndicatorRun> Build(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history) =>
        Builder(bars, indicator, history).BuildAsync();
    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
