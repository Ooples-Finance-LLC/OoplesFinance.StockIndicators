using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class LogarithmParallelLimitTests
{
    [Theory]
    [InlineData(PriceTranscendentalOperation.NaturalLogarithm)]
    [InlineData(PriceTranscendentalOperation.CommonLogarithm)]
    public async Task ParallelLimitsPreserveStateBitsPresenceAndOwnedHistory(PriceTranscendentalOperation operation)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 2, 8 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                foreach (int count in new[] { 8193, 65537 })
                {
                    double[] domain = [-0d, 0d, double.Epsilon, -double.Epsilon, -1d, 1d,
                        Math.BitIncrement(1d), .125, .875, 64d, double.MaxValue, -double.MaxValue];
                    var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                        3, 4, 2, domain[i % domain.Length], i)).ToArray();
                    var original = bars.ToArray();
                    var indicator = new PriceTranscendentalTransform(operation);
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
                    Assert.False(((IndicatorRun)latest).HasLegacyRuntime);
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
                        Assert.Equal(BitConverter.DoubleToInt64Bits(original[index].Close),
                            BitConverter.DoubleToInt64Bits(snapshot.Bar.Close));
                        index++;
                    }
                    Assert.Equal(count, index);
                    Assert.Equal(original[^1], latest.Latest.Bar);
                    full.Dispose();
                    latest.Dispose();
                    Bits(expected[0], full[indicator.Value].ToArray());
                    Bits(expected[1], latest[indicator.IsDefined].ToArray());
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    private static Task<IIndicatorRun> Build(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();

    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
