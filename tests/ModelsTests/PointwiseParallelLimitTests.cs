using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class PointwiseParallelLimitTests
{
    [Theory]
    [InlineData("Ln")]
    [InlineData("Log10")]
    [InlineData("Sin")]
    [InlineData("Cos")]
    [InlineData("Tan")]
    [InlineData("Acos")]
    [InlineData("Atan")]
    [InlineData("Sinh")]
    [InlineData("Cosh")]
    [InlineData("Tanh")]
    public async Task ParallelLimitsPreserveStateBitsPresenceAndOwnedHistory(string operation)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 2, 8 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                foreach (int count in new[] { 8193, 65537 })
                {
                    double limit = operation is "Sinh" or "Cosh" ? 700 : double.MaxValue;
                    double[] domain = [-0d, 0d, double.Epsilon, -double.Epsilon, -1d, 1d,
                        Math.BitIncrement(1d), .125, .875, 64d, limit, -limit];
                    var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                        3, 4, 2, domain[i % domain.Length], i)).ToArray();
                    var original = bars.ToArray();
                    var indicator = Create(operation);
                    var state = Assert.IsAssignableFrom<IMultiOutputState>(indicator.CreateState());
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
                    Assert.IsType<LatestOnlyIndicatorRun>(latest);
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
                    Bits(expected[0], full[indicator.Outputs[0]].ToArray());
                    Bits(expected[1], latest[indicator.Outputs[1]].ToArray());
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Theory]
    [InlineData("Sinh")]
    [InlineData("Cosh")]
    public async Task ChangedDispatchPreservesCancellationRawPriorityOverflowAndRecovery(string operation)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 2, 8 })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var bars = Enumerable.Repeat(new Bar(default, 1, 2, 0, .5, 1), 8193).ToArray();
                bars[1] = new Bar(default, 1, 2, 0, 1000, 1);
                bars[^1] = new Bar(default, 1, 2, 0, .5, double.NaN);
                var indicator = Create(operation);
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
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
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    private static MultiOutputIndicatorBase Create(string operation) => operation switch
    {
        "Ln" => new PriceTranscendentalTransform(PriceTranscendentalOperation.NaturalLogarithm),
        "Log10" => new PriceTranscendentalTransform(PriceTranscendentalOperation.CommonLogarithm),
        "Sin" => new PriceCircularTransform(PriceCircularOperation.Sine),
        "Cos" => new PriceCircularTransform(PriceCircularOperation.Cosine),
        "Tan" => new PriceCircularTransform(PriceCircularOperation.Tangent),
        "Acos" => new PriceCircularTransform(PriceCircularOperation.ArcCosine),
        "Atan" => new PriceCircularTransform(PriceCircularOperation.ArcTangent),
        "Sinh" => new PriceTranscendentalTransform(PriceTranscendentalOperation.HyperbolicSine),
        "Cosh" => new PriceTranscendentalTransform(PriceTranscendentalOperation.HyperbolicCosine),
        "Tanh" => new PriceTranscendentalTransform(PriceTranscendentalOperation.HyperbolicTangent),
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    private static Task<IIndicatorRun> Build(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();

    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
