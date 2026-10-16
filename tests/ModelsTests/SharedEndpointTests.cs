using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedEndpointTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FixedWeightsAndStartupMeansSurviveWorkerReplay(bool meanStartup)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        try
        {
            foreach (int period in new[] { 1, 2, 20, 4096, 4097, 1073741824, 1073741825, int.MaxValue })
            foreach (int count in new[] { 0, 1, 511, 512, 513, period is 4096 or 4097 ? 8193 : 1025 })
            {
                var endpoint = new EndpointWeightedAverage(period, meanStartup);
                var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1,
                    i == 0 ? double.Epsilon : i % 7 == 0 ? -0d : (i % 53 - 26) / 8d, 1)).ToArray();
                using var ordinary = await Builder(bars, endpoint).ConfigureBehavior(_ => { }).BuildAsync();
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    using var run = await Builder(bars, endpoint).ConfigureHistory(history).BuildAsync();
                    using var mixed = await Builder(bars, endpoint).ConfigureHistory(history)
                        .ConfigureIndicators(endpoint, new LowestLow(7)).BuildAsync();
                    Bits(ordinary[endpoint].ToArray(), run[endpoint].ToArray());
                    Bits(ordinary[endpoint].ToArray(), mixed[endpoint].ToArray());
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AffineStateAgreesWithIndependentFractionsAcrossExpiryAndReset(bool meanStartup)
    {
        double[] values = [double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 0, -0d, 1, -1, 13.125];
        var bars = Enumerable.Range(0, 41).Select(i => new Bar(default, 0, 1, -1, values[i % values.Length], 1)).ToArray();
        foreach (int period in new[] { 1, 2, 7, 1073741824 })
        {
            var expected = NormalizedKernelReference.Values(bars, period,
                lag => new ReferenceFraction(2L * period - 1 - 3L * lag), meanStartup);
            var state = new AffineConvolutionState(period, (int)(2L * period - 1), -3, meanStartup);
            for (int pass = 0; pass < 2; pass++)
            {
                state.Reset();
                Bits(expected, bars.Select(b => state.Update(in b)));
            }
        }
    }

    [Fact]
    public async Task OverflowFailureInputOrderingAndRecoveryPreserveOwnership()
    {
        var endpoint = new EndpointWeightedAverage(3);
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, 0, 1, -1, i % 31, 1)).ToArray();
        var original = bars.ToArray(); var builder = Builder(bars, endpoint);
        using var saved = await builder.BuildAsync();
        bars[500] = new Bar(default, 0, 1, -1, -double.MaxValue, 1);
        bars[501] = new Bar(default, 0, 1, -1, double.MaxValue, 1);
        bars[502] = new Bar(default, 0, 1, -1, double.MaxValue, 1);
        // P=3 weights [5,2,-1] make the final positive plateau overflow.
        foreach (bool invalidInput in new[] { false, true })
        {
            if (invalidInput) bars[900] = new Bar(default, 0, 1, -1, 0, double.NaN);
            var expected = await Record.ExceptionAsync(() => Builder(bars, endpoint).ConfigureBehavior(_ => { }).BuildAsync());
            var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
            Assert.NotNull(expected); Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
            Assert.Null(builder.LastExecution);
        }
        original.CopyTo(bars, 0);
        using var recovered = await builder.BuildAsync();
        Bits(saved[endpoint].ToArray(), recovered[endpoint].ToArray());
        Array.Clear(bars); int index = 0;
        await foreach (var snapshot in saved) Assert.Equal(original[index++], snapshot.Bar);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }
    private static void Bits(IEnumerable<double> expected, IEnumerable<double> actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
}
