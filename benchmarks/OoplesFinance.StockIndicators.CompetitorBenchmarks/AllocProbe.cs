using OoplesFinance.StockIndicators.Builder;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

/// <summary>
/// Splits the allocation BenchmarkDotNet reports for a v2 arm into the part that is indicator work and the
/// part that is getting the bars into the shape the builder demands.
///
/// <para>This exists because the headline number is misleading without it. Every competitor receives its input
/// pre-built in a GlobalSetup, but the v2 and v1 arms must call <c>NewStockData()</c> inside the measured
/// method - the batch API writes its results back into the instance it is handed, so reusing one would measure
/// a partly-populated object. The adapter is therefore charged to this library and to nobody else, and it is
/// most of the total: at 10,000 bars it is 961,168 of the 1,208,528 bytes.</para>
/// </summary>
internal static class AllocProbe
{
    public static void Run(TextWriter output)
    {
        foreach (var bars in new[] { 1_000, 10_000 })
        {
            var data = CompetitorData.Create(bars);

            // Warm every path once so the numbers are steady-state, not first-call JIT and statics.
            _ = data.NewStockData();
            _ = EmaV2(data);
            _ = EmaV2Columns(data);
            var warmTaLib = new double[bars];
            Functions.Ema<double>(data.Closes, Range.All, warmTaLib, out _, 20);

            output.WriteLine(bars + " bars");
            output.WriteLine("  [.. arrays] copy only      " + Measure(() => { _ = Copy(data); }) + " B");
            output.WriteLine("  NewStockData()             " + Measure(() => { _ = data.NewStockData(); }) + " B");
            output.WriteLine("  v2 EMA whole arm           " + Measure(() => { _ = EmaV2(data); }) + " B");
            output.WriteLine("  v2 EMA over columns        " + Measure(() => { _ = EmaV2Columns(data); }) + " B");
            var repeats = new long[5];
            for (var r = 0; r < repeats.Length; r++)
            {
                repeats[r] = Measure(() => { _ = EmaV2Columns(data); });
            }

            output.WriteLine("    repeated runs            " + string.Join(", ", repeats) + " B");
            output.WriteLine("    pool rent+return 10k     " + Measure(() =>
            {
                var a = System.Buffers.ArrayPool<double>.Shared.Rent(bars);
                System.Buffers.ArrayPool<double>.Shared.Return(a);
            }) + " B");
            output.WriteLine("    of which: build only     " + Measure(() =>
            {
                using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromColumns(
                        data.Opens, data.Highs, data.Lows, data.Closes, data.Volumes, data.Dates))
                    .ConfigureIndicators(indicators => _ = indicators.Ema(20))
                    .Build();
            }) + " B");
            output.WriteLine("    of which: build + start  " + Measure(() =>
            {
                using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromColumns(
                        data.Opens, data.Highs, data.Lows, data.Closes, data.Volumes, data.Dates))
                    .ConfigureIndicators(indicators => _ = indicators.Ema(20))
                    .Build();
                runtime.Start();
            }) + " B");
            output.WriteLine("  TA-Lib EMA into own buffer " + Measure(() =>
            {
                var buffer = new double[bars];
                Functions.Ema<double>(data.Closes, Range.All, buffer, out _, 20);
            }) + " B");
            output.WriteLine();
        }
    }

    private static double[][] Copy(CompetitorData data) =>
        [[.. data.Opens], [.. data.Highs], [.. data.Lows], [.. data.Closes], [.. data.Volumes]];

    private static int EmaV2(CompetitorData data)
    {
        var handle = default(SeriesHandle);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(data.NewStockData()))
            .ConfigureIndicators(indicators => handle = indicators.Ema(20))
            .Build();
        runtime.Start();
        return runtime.GetSeries(handle).AsSpan().Length;
    }

    private static int EmaV2Columns(CompetitorData data)
    {
        var handle = default(SeriesHandle);
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromColumns(
                data.Opens, data.Highs, data.Lows, data.Closes, data.Volumes, data.Dates))
            .ConfigureIndicators(indicators => handle = indicators.Ema(20))
            .Build();
        runtime.Start();
        return runtime.GetSeries(handle).AsSpan().Length;
    }

    private static long Measure(Action action)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}