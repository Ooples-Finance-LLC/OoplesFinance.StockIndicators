using System.Text.Json;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

/// <summary>Allocation-only observations of the validated shared benchmark arms.</summary>
public static class SharedAllocationProbe
{
    public sealed record Arm(string Method, long[] Bytes);
    public sealed record Report(string Operation, int Count, int Repetitions, Arm[] Arms);

    public static Report Run(string operation, int count, int repetitions, bool includeGpu = false)
    {
        if (count < 64) throw new ArgumentOutOfRangeException(nameof(count));
        if (repetitions is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(repetitions));
        bool gpuRegression = operation is "TaLib.Functions.LinearReg" or "TaLib.Functions.LinearRegSlope"
            or "TaLib.Functions.LinearRegIntercept" or "TaLib.Functions.Tsf";
        bool gpuPathRatio = operation is "Skender.GetCmo" or "Trady.Indicator.EfficiencyRatio";
        if (includeGpu && !gpuPathRatio && !gpuRegression && operation is not ("QuanTAlib.Epma" or "QuanTAlib.Mma" or "QuanTAlib.Convolution"))
            throw new ArgumentException("No GPU allocation fixture for this operation.", nameof(operation));
        Action setup, cleanup;
        Func<object>[] methods;
        string[] names = ["BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel"];
        if (includeGpu && gpuRegression)
        {
            var fixture = new SharedGpuRegressionBenchmarks { Operation = operation[(operation.LastIndexOf('.') + 1)..], Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.GpuFull, fixture.GpuLatestOnly,
                fixture.NativeSingle, fixture.NativeParallel];
            names = ["BuilderFull", "BuilderLatestOnly", "GpuFull", "GpuLatestOnly", "NativeSingle", "NativeParallel"];
        }
        else if (includeGpu && gpuPathRatio)
        {
            var fixture = new SharedGpuPathRatioBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.GpuFull, fixture.GpuLatestOnly,
                fixture.NativeSingle, fixture.NativeParallel];
            names = ["BuilderFull", "BuilderLatestOnly", "GpuFull", "GpuLatestOnly", "NativeSingle", "NativeParallel"];
        }
        else if (operation is "QuanTAlib.Historical" or "QuanTAlib.Realized")
        {
            var fixture = new SharedLogVolatilityBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation is "QuanTAlib.Dema" or "QuanTAlib.Tema" or "QuanTAlib.Qema")
        {
            var fixture = new SharedNativeEmaBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation == "TaLib.Functions.Beta")
        {
            var fixture = new SharedPairInputBenchmarks { Operation = "Beta", Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation is "TaLib.Functions.Rsi" or "TaLib.Functions.Cmo")
        {
            var fixture = new SharedWilderStrengthBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation is "TaLib.Functions.HtDcPeriod" or "TaLib.Functions.HtPhasor" or "TaLib.Functions.Mama")
        {
            var fixture = new SharedHilbertBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation is "Skender.GetCmo" or "Trady.Indicator.EfficiencyRatio")
        {
            var fixture = new SharedPathRatioBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation == "Skender.GetFisherTransform")
        {
            var fixture = new SharedFisherBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation == "Skender.GetConnorsRsi")
        {
            var fixture = new SharedConnorsBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation == "Trady.Indicator.DynamicMomentumIndex")
        {
            var fixture = new SharedDynamicMomentumBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation == "Skender.GetUlcerIndex")
        {
            var fixture = new SharedUlcerBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation == "QuanTAlib.Rvi")
        {
            var fixture = new SharedRelativeVolatilityBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation is "QuanTAlib.Rema" or "QuanTAlib.Zlema")
        {
            var fixture = new SharedExpandingRecurrenceBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation == "QuanTAlib.Hwma")
        {
            var fixture = new SharedHoltWinterBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation == "QuanTAlib.Ltma")
        {
            var fixture = new SharedZeroSeedLaguerreBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation == "QuanTAlib.Maaf")
        {
            var fixture = new SharedMedianAdaptiveBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation == "Skender.GetKvo")
        {
            var fixture = new SharedKlingerBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation is "TaLib.Functions.LinearReg" or "TaLib.Functions.LinearRegSlope"
            or "TaLib.Functions.LinearRegIntercept" or "TaLib.Functions.LinearRegAngle" or "TaLib.Functions.Tsf")
        {
            var fixture = new SharedRegressionBenchmarks { Operation = operation[(operation.LastIndexOf('.') + 1)..], Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation is "QuanTAlib.Slope" or "Skender.GetSlope")
        {
            var fixture = new SharedRegressionStatisticsBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation == "Skender.GetStc")
        {
            var fixture = new SharedSchaffCycleBenchmarks { Operation = operation, Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.NativeSingle, fixture.NativeParallel];
        }
        else if (operation is "QuanTAlib.Epma" or "QuanTAlib.Mma")
        {
            var fixture = new SharedGpuEndpointBenchmarks { Operation = operation == "QuanTAlib.Epma" ? "Epma" : "Mma", Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.GpuFull, fixture.GpuLatestOnly,
                fixture.NativeSingle, fixture.NativeParallel];
            names = ["BuilderFull", "BuilderLatestOnly", "GpuFull", "GpuLatestOnly", "NativeSingle", "NativeParallel"];
        }
        else if (operation == "QuanTAlib.Convolution")
        {
            var fixture = new SharedGpuConvolutionBenchmarks { Operation = "Convolution", Count = count };
            setup = fixture.Setup; cleanup = fixture.Cleanup;
            methods = [fixture.BuilderFull, fixture.BuilderLatestOnly, fixture.GpuFull, fixture.GpuLatestOnly,
                fixture.NativeSingle, fixture.NativeParallel];
            names = ["BuilderFull", "BuilderLatestOnly", "GpuFull", "GpuLatestOnly", "NativeSingle", "NativeParallel"];
        }
        else throw new ArgumentException("Unsupported shared allocation operation.", nameof(operation));

        var arms = new Arm[methods.Length];
        try
        {
            // Reuse each benchmark's agreement checks and prepare identical inputs
            // outside the measured arms. Exact contracts have separate reference tests.
            setup();
            for (int arm = 0; arm < methods.Length; arm++)
            {
                for (int warmup = 0; warmup < 10; warmup++) Measure(methods[arm]);
                var samples = new long[repetitions];
                for (int sample = 0; sample < samples.Length; sample++) samples[sample] = Measure(methods[arm]);
                arms[arm] = new Arm(names[arm], samples);
            }
        }
        finally { cleanup(); }
        return new Report(operation, count, repetitions, arms);
    }

    // Count all managed threads because native/builder parallel arms can allocate
    // on workers. No timings are collected. Retain the owned result through the
    // end of each sample and release disposable results outside that sample.
    public static long Measure(Func<object> action)
    {
        object? result = null;
        long before = GC.GetTotalAllocatedBytes(precise: true);
        try
        {
            result = action();
            long bytes = GC.GetTotalAllocatedBytes(precise: true) - before;
            GC.KeepAlive(result);
            return bytes;
        }
        finally { (result as IDisposable)?.Dispose(); }
    }

    public static void Write(TextWriter output, string operation, int count, int repetitions, bool includeGpu = false) =>
        output.WriteLine(JsonSerializer.Serialize(Run(operation, count, repetitions, includeGpu)));
}
