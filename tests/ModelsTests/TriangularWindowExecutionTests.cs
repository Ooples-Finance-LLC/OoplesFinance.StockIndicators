using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class TriangularWindowExecutionTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(20)]
    [InlineData(21)] [InlineData(80)] [InlineData(81)] [InlineData(int.MaxValue)]
    public void CompactWorkerMatchesExactConvolutionAcrossRejectionAndReset(int period)
    {
        foreach (int rejection in new[] { -1, 0, 5, 10, 19, 47, 79 })
        {
            var values = Enumerable.Range(0, 80).Select(i => (i % 19 - 9) / 8d).ToArray();
            if (rejection >= 0) values[rejection] = double.Epsilon;
            var state = new ValuesBarExecution.TriangularValueState(period, values.Length);
            foreach (var input in new[] { values, values.Reverse().ToArray(), new double[80] })
            {
                state.Reset();
                var expected = Reference(input, period);
                for (int i = 0; i < input.Length; i++)
                    Bits(expected[i], state.Update(new Bar(default, 0, 1, 0, input[i], 1)));
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public void CertificatePromotionAndResetPreserveExactWindow(int period)
    {
        foreach (int rejection in new[] { 0, 5, 10, 19, 47 })
        {
            var values = Enumerable.Range(0, 80).Select(i => (i % 19 - 9) / 8d).ToArray();
            values[rejection] = double.MaxValue;
            values[rejection + 1] = -double.MaxValue;
            values[rejection + 2] = double.Epsilon;
            values[rejection + 3] = -double.Epsilon;
            var state = (IIndicatorState)new TriangularWindowAverage(period).CreateState();
            foreach (var input in new[] { values, values.Reverse().ToArray(), new double[80] })
            {
                state.Reset();
                var expected = Reference(input, period);
                for (int i = 0; i < input.Length; i++)
                    Bits(expected[i], state.Update(new Bar(default, 0, 1, 0, input[i], 1)));
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(511)]
    [InlineData(512)]
    [InlineData(513)]
    [InlineData(8193)]
    public async Task ParallelHistoryWindowsMatchIndependentConvolution(int count)
    {
        var prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var values = Enumerable.Range(0, count).Select(i => (i % 97 - 48) / 8d).ToArray();
            if (count > 512) { values[255] = double.MaxValue; values[256] = -double.MaxValue; values[511] = double.Epsilon; }
            var bars = values.Select(v => new Bar(default, 0, 1, 0, v, 1)).ToArray();
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            foreach (int period in new[] { 3, 20 })
            {
                var indicator = new TriangularWindowAverage(period);
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
                Assert.Equal(Reference(values, period).Select(BitConverter.DoubleToInt64Bits),
                    run[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task ComposedWindowsAndInvalidInputsKeepTheirContracts()
    {
        var values = Enumerable.Range(0, 513).Select(i => i / 8d).ToArray();
        var bars = values.Select(v => new Bar(default, 0, 1, 0, v, 1)).ToArray();
        var first = new TriangularWindowAverage(3); var second = new TriangularWindowAverage(20);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(first, second).BuildAsync();
        Assert.Equal(Reference(values, 3), run[first].ToArray());
        Assert.Equal(Reference(values, 20), run[second].ToArray());
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(first);
        bars[512] = new Bar(default, 0, 1, 0, 1, double.NaN);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => builder.BuildAsync());
    }

    private static double[] Reference(double[] values, int period)
    {
        var output = new double[values.Length];
        for (int i = period - 1; i < values.Length; i++)
        {
            var sum = new ReferenceFraction(0); long mass = 0;
            for (int j = 0; j < period; j++)
            {
                int weight = Math.Min(j + 1, period - j);
                sum += ReferenceFraction.FromDouble(values[i - period + 1 + j]) * new ReferenceFraction(weight);
                mass += weight;
            }
            output[i] = (sum / new ReferenceFraction(mass)).ToDouble();
        }
        return output;
    }
    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
