using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class AccumulationPairPrefixTests
{
    [Fact]
    public async Task LargeCapturedPrefixesPreserveAllColumnsAndOwnedInputs()
    {
        foreach (int count in new[] { 8191, 8192, 10003 })
        {
            var bars = Rich(count);
            foreach (int? period in new int?[] { null, 1, 20 })
            {
                var expected = Reference(bars, period);
                var summary = Summary(expected[1]);
                Assert.False(summary.Certifies(count));
                Assert.True(summary.CertifiesCompensatedPrefix(count));
                await Check(bars, period, expected);
            }
        }
    }

    [Fact]
    public async Task ExactCancellationAtCertificateBoundaryAndRejectedGridsMatchFractions()
    {
        double small = Math.BitIncrement(Math.ScaleB(1d, -398));
        double large = Math.BitIncrement(Math.ScaleB(1d, -362));
        double[][] sequences =
        [
            [large, small, -large, -small],
            [2 * large, small, -2 * large, -small],
            [double.Epsilon, double.Epsilon, -double.Epsilon, -double.Epsilon],
            [Math.BitIncrement(Math.ScaleB(1d, 500)), -Math.BitIncrement(Math.ScaleB(1d, 500))],
            [Math.BitIncrement(1d), Math.BitIncrement(Math.ScaleB(1d, -100)),
                -Math.BitIncrement(1d), -Math.BitIncrement(Math.ScaleB(1d, -100))]
        ];
        for (int scenario = 0; scenario < sequences.Length; scenario++)
        {
            var flows = sequences[scenario];
            var bars = Enumerable.Range(0, 10003).Select(i =>
            {
                double value = flows[i % flows.Length];
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 1, -1, Math.Sign(value), Math.Abs(value));
            }).ToArray();
            var expected = Reference(bars, 20);
            var summary = Summary(expected[1]);
            Assert.False(summary.Certifies(bars.Length));
            Assert.Equal(scenario == 0, summary.CertifiesCompensatedPrefix(bars.Length));
            await Check(bars, 20, expected);
        }
    }

    [Fact]
    public async Task InputErrorsStillPrecedeDeferredOutputFailuresAndRecovery()
    {
        foreach (int scenario in Enumerable.Range(0, 3))
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Rich(10003);
            var expected = Reference(bars, 20);
            var saved = bars.Take(3).ToArray();
            var first = scenario switch
            {
                0 => new Bar(default, 0, double.Epsilon, 0, double.MaxValue, 0),
                1 => new Bar(default, 0, 1, 0, 2, double.MaxValue),
                _ => new Bar(default, 0, 1, 0, 1, double.MaxValue)
            };
            bars[0] = bars[1] = first;
            if (scenario == 2) bars[2] = new(default, 0, 1, 0, 0, double.MaxValue);
            var indicator = new AccumulationDistributionWithAverage(20);
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
                .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            var last = bars[^1];
            bars[^1] = new(default, double.NaN, 1, 0, 1, 1);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            bars[^1] = last;
            await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            saved.CopyTo(bars, 0);
            using var recovered = await builder.BuildAsync();
            AssertColumns(recovered, indicator, expected);
        }
    }

    private static Bar[] Rich(int count) => Enumerable.Range(0, count).Select(i =>
    {
        double close = 10 + (i * 13 % 101) / 8d;
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), close, close + i % 3 + 1.5,
            close - i % 5 - .25, close, (i % 11 == 0 ? -1 : 1) * (1000 + i * 47 % 10001));
    }).ToArray();

    private static SmaCpuKernel.GridSummary Summary(double[] values)
    {
        var summary = new SmaCpuKernel.GridSummary();
        foreach (double value in values) summary.Include(value);
        return summary;
    }

    private static double[][] Reference(Bar[] bars, int? period)
    {
        var result = Enumerable.Range(0, 5).Select(_ => new double[bars.Length]).ToArray();
        var total = new ReferenceFraction(0);
        var mean = new ReferenceFraction(0);
        for (int i = 0; i < bars.Length; i++)
        {
            var bar = bars[i];
            var high = ReferenceFraction.FromDouble(bar.High);
            var low = ReferenceFraction.FromDouble(bar.Low);
            var balance = ReferenceFraction.FromDouble(bar.Close) * new ReferenceFraction(2) - high - low;
            result[0][i] = (balance / (high - low)).ToDouble();
            result[1][i] = (balance * ReferenceFraction.FromDouble(bar.Volume) / (high - low)).ToDouble();
            total += ReferenceFraction.FromDouble(result[1][i]);
            result[2][i] = total.ToDouble();
            if (period is not int length) continue;
            mean += ReferenceFraction.FromDouble(result[2][i]);
            if (i >= length) mean -= ReferenceFraction.FromDouble(result[2][i - length]);
            if (i < length - 1) continue;
            result[3][i] = (mean / new ReferenceFraction(length)).ToDouble();
            result[4][i] = 1;
        }
        return result;
    }

    private static async Task Check(Bar[] bars, int? period, double[][] expected)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            var indicator = new AccumulationDistributionWithAverage(period);
            using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicator).ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            Array.Clear(input);
            AssertColumns(actual, indicator, expected);
            Assert.Equal(bars[^1], actual.Latest.Bar);
        }
    }

    private static void AssertColumns(IIndicatorRun actual, AccumulationDistributionWithAverage indicator, double[][] expected)
    {
        for (int slot = 0; slot < expected.Length; slot++)
            Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),
                actual[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }
}
