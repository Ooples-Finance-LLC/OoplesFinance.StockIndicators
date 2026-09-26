using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class EhlersRestoringPullTests
{
    [Theory]
    [InlineData(8, 50, 40, 10, false)]
    [InlineData(8, 50, 40, 10, true)]
    [InlineData(6, 24, 20, 5, false)]
    [InlineData(8, 8, 40, 1, false)]
    public void CoreCyclesStayInBandAndMatchBatchIncludingWarmupAndUnderflow(
        int min, int max, int highPass, int median, bool flat)
    {
        var prices = Prices(flat ? 5000 : 400, flat);
        var expected = Batch(prices).CalculateEhlersSpectrumDerivedFilterBank(min, max, highPass, median)
            .CustomValuesList;
        var actual = new double[prices.Length];
        // Repetition exercises recycled pool storage as well as fresh buffers.
        for (var run = 0; run < 2; run++)
        {
            OscillatorCore.EhlersSpectrumDerivedFilterBank(prices, actual, min, max, highPass, median);
            for (var i = 0; i < actual.Length; i++)
            {
                Assert.True(double.IsFinite(actual[i]) && actual[i] >= min && actual[i] <= max,
                    $"Cycle outside scanned band at bar {i}: {actual[i]}");
                Assert.Equal(expected[i], actual[i]);
            }
        }

        if (flat)
            Assert.True(actual.Skip(4900).Max() - actual.Skip(4900).Min() < 1e-9);
    }

    [Theory]
    [InlineData(false, 8, 50, 40, 10)]
    [InlineData(true, 6, 24, 20, 5)]
    public async Task PublishedPullAndSignalMatchTheirDefinitions(bool weighted, int min, int max, int highPass, int median)
    {
        var prices = Prices(400, false);
        var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i),
            p, p + 1, p - 1, p, Volume(i))).ToList();
        var indicator = new EhlersRestoringPullIndicator(min, max, highPass, median,
            weighted ? new Wma() : null);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator).BuildAsync();
        var actual = run[indicator.Value].ToArray();
        var signal = run[indicator.Signal].ToArray();
        var batch = Batch(prices).CalculateEhlersRestoringPullIndicator(
            weighted ? MovingAvgType.WeightedMovingAverage : MovingAvgType.ExponentialMovingAverage,
            min, max, highPass, median);
        actual.Should().Equal(batch.OutputValues["Rpi"]);
        signal.Should().Equal(batch.OutputValues["Signal"]);
        signal.Should().NotEqual(actual);

        var core = new double[prices.Length];
        var scaled = new double[prices.Length];
        var volumes = Enumerable.Range(0, prices.Length).Select(i => (double)Volume(i)).ToArray();
        OscillatorCore.EhlersRestoringPullIndicator(prices, volumes, core, min, max, highPass, median);
        OscillatorCore.EhlersRestoringPullIndicator(prices, volumes.Select(v => v * 2).ToArray(),
            scaled, min, max, highPass, median);
        for (var i = 0; i < prices.Length; i++)
        {
            Assert.Equal(actual[i], core[i]);
            Assert.Equal(2 * core[i], scaled[i]);
            Assert.True(double.IsFinite(core[i]) && core[i] >= volumes[i] * Math.Pow(2 * Math.PI / max, 2) - 1e-9
                && core[i] <= volumes[i] * Math.Pow(2 * Math.PI / Math.Max(3, min), 2) + 1e-9);
        }
    }

    [Theory]
    [InlineData(8, 50, 40, 10)]
    [InlineData(6, 24, 20, 5)]
    [InlineData(8, 8, 40, 1)]
    [InlineData(1, 3, 1, 1)]
    public void SpectrumMatchesIndependentComplexTrajectoriesAndPreservesPreviews(int min, int max, int cutoff, int median)
    {
        var prices = Prices(300, false);
        var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
        var expected = BuiltInFormulaReferences.SpectrumCycles(bars, min, max, cutoff, median);
        using var state = new EhlersSpectrumDerivedFilterBankEngine(min, max, cutoff, median);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                state.Next(-100, false);
                var preview = state.Next(prices[i], false);
                var actual = state.Next(prices[i], true);
                Assert.Equal(actual, preview);
                Assert.InRange(Math.Abs(actual - expected[i]), 0, 1e-8);
            }
        }
    }

    [Theory]
    [InlineData(12)]
    [InlineData(17)]
    [InlineData(30)]
    public void SpectrumResolvesASingleSinusoidalCycle(int period)
    {
        var prices = Enumerable.Range(0, 1200).Select(i => 100 + 4 * Math.Sin(2 * Math.PI * i / period)).ToArray();
        var cycles = new double[prices.Length];
        OscillatorCore.EhlersSpectrumDerivedFilterBank(prices, cycles);
        foreach (var cycle in cycles.Skip(1000)) Assert.InRange(cycle, period - 1d, period + 1d);
    }

    private static double[] Prices(int count, bool flat) => Enumerable.Range(0, count)
        .Select(i => flat ? 100d : 100 + 4 * Math.Sin(2 * Math.PI * i / 17) + 2 * Math.Cos(i / 7d)).ToArray();

    private static int Volume(int i) => i % 19 == 0 ? 0 : 1000 + i * 17;

    private static StockData Batch(double[] prices) => new(prices.ToList(),
        prices.Select(p => p + 1).ToList(), prices.Select(p => p - 1).ToList(), prices.ToList(),
        Enumerable.Range(0, prices.Length).Select(i => (double)Volume(i)).ToList(),
        Enumerable.Range(0, prices.Length).Select(i => DateTime.UnixEpoch.AddMinutes(i)).ToList());
}
