using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

/// <summary>Fails fast if benchmark output retention or history resets change the measured workload.</summary>
internal static class WorkloadCheck
{
    public static void Run(TextWriter output)
    {
        var assertions = 0;
        foreach (var count in new[] { 7, 1_000, 10_000 })
        {
            var data = CompetitorData.Create(count);
            var benchmark = new BatchBenchmarks { Bars = count };
            benchmark.Setup();
            var sma = new QuanTAlib.Sma(20);
            var ema = new QuanTAlib.Ema(20, true);
            var atr = new QuanTAlib.Atr(14);
            var expectedSma = new double[count];
            var expectedEma = new double[count];
            var expectedAtr = new double[count];
            for (var i = 0; i < count; i++)
            {
                expectedSma[i] = sma.Calc(new QuanTAlib.TValue(data.Closes[i], true, false)).Value;
                expectedEma[i] = ema.Calc(new QuanTAlib.TValue(data.Closes[i], true, false)).Value;
                expectedAtr[i] = atr.Calc(data.Bars[i]).Value;
            }

            // Compare every position, including warmup. Poisoning the shared destination before a repeat
            // also catches skipped writes that would otherwise leave a plausible value from the prior call.
            CheckSeries(benchmark.SmaQuanTAlib, expectedSma, ref assertions);
            CheckSeries(benchmark.EmaQuanTAlib, expectedEma, ref assertions);
            CheckSeries(benchmark.AtrQuanTAlib, expectedAtr, ref assertions);
        }

        var setup = typeof(IncrementalBenchmarks).GetMethod(nameof(IncrementalBenchmarks.ResetHistory))!;
        if (!Attribute.IsDefined(setup, typeof(IterationSetupAttribute)))
            throw new InvalidOperationException("Fixed-history reset must be registered with BenchmarkDotNet.");
        assertions++;

        foreach (var history in new[] { 1_000, 10_000 })
        {
            // Independent uninterrupted replay gives the expected next update for each library's own
            // convention. Cross-library agreement is a separate check, especially for QuanTAlib ATR.
            var data = CompetitorData.Create(history + 1);
            using var sma = new SimpleMovingAverageState(20);
            var rsi = new RelativeStrengthIndexState(14);
            var atr = new AverageTrueRangeState(14);
            var quanSma = new QuanTAlib.Sma(20);
            var quanAtr = new QuanTAlib.Atr(14);
            var expected = new double[5];
            for (var i = 0; i < data.Count; i++)
            {
                var bar = new OhlcvBar("BENCH", BarTimeframe.Minutes(1), data.Dates[i], data.Dates[i],
                    data.Opens[i], data.Highs[i], data.Lows[i], data.Closes[i], data.Volumes[i], true);
                expected[0] = sma.Update(bar, true, false).Value;
                expected[1] = rsi.Update(bar, true, false).Value;
                expected[2] = atr.Update(bar, true, false).Value;
                expected[3] = quanSma.Calc(new QuanTAlib.TValue(data.Closes[i], true, false)).Value;
                expected[4] = quanAtr.Calc(data.Bars[i]).Value;
            }

            var benchmark = new IncrementalBenchmarks { History = history };
            benchmark.Setup();
            try
            {
                Func<double>[] updates = [benchmark.SmaOoplesStreaming, benchmark.RsiOoplesStreaming,
                    benchmark.AtrOoplesStreaming, benchmark.SmaQuanTAlibIncremental,
                    benchmark.AtrQuanTAlibIncremental];
                for (var trial = 0; trial < 3; trial++)
                {
                    benchmark.ResetHistory();
                    for (var arm = 0; arm < updates.Length; arm++)
                    {
                        Equal(expected[arm], updates[arm](), ref assertions);
                        // Leave deliberately advanced state for the next reset to discard.
                        for (var extra = 0; extra < 31; extra++) updates[arm]();
                    }
                }
            }
            finally
            {
                benchmark.Cleanup();
            }
        }

        output.WriteLine($"PASS: {assertions} workload assertions (full retained series, poisoned repeat writes, fixed-history resets).");
    }

    private static void CheckSeries(Func<double[]> run, double[] expected, ref int assertions)
    {
        var actual = run();
        if (actual.Length != expected.Length) throw new InvalidOperationException("Incomplete retained series.");
        assertions++;
        for (var i = 0; i < expected.Length; i++) Equal(expected[i], actual[i], ref assertions);
        Array.Fill(actual, double.NaN);
        actual = run();
        for (var i = 0; i < expected.Length; i++) Equal(expected[i], actual[i], ref assertions);
    }

    private static void Equal(double expected, double actual, ref int assertions)
    {
        if (!double.IsFinite(expected) || !expected.Equals(actual))
            throw new InvalidOperationException($"Workload mismatch: expected {expected:R}, actual {actual:R}.");
        assertions++;
    }
}
