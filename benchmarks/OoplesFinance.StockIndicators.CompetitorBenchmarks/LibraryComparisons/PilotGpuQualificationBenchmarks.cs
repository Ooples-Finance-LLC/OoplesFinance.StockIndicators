using System.Diagnostics;
using AiDotNet.Tensors.Engines;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Restricted benchmark-only candidate: period 20, values on a /64 grid in [-1,1].
// Every cooperative prefix contains <=147 such values, hence is exactly representable.
// This proof does not authorize dispatch on arbitrary production inputs.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class PilotGpuQualificationBenchmarks
{
    [Params(10_000, 1_000_000)] public int Count { get; set; }
    [Params(false, true)] public bool Composed { get; set; }
    private double[] _input = null!;
    private Bar[] _owned = null!;
    private OpenClContext _context = null!;
    private OpenClBuffer<double> _deviceInput = null!, _deviceOutput = null!;
    private OpenClProgram _blockedProgram = null!, _tiledProgram = null!;
    private OpenClKernel _blocked = null!, _tiled = null!;

    [GlobalSetup]
    public void Setup()
    {
        var source = Enumerable.Range(0, Count).Select(i =>
        {
            var value = (i % 127 - 63) / 64d;
            return new Bar(default, value, value, value, value, 1);
        }).ToArray();
        var total = Stopwatch.StartNew();
        var timer = Stopwatch.StartNew();
        _owned = GC.AllocateUninitializedArray<Bar>(Count);
        source.CopyTo(_owned, 0);
        _input = GC.AllocateUninitializedArray<double>(Count);
        for (var i = 0; i < Count; i++)
        {
            IndicatorInputDomain.Finite.Validate(in _owned[i]);
            var value = _owned[i].Close;
            if (Math.Abs(value) > 1 || value * 64 != Math.Truncate(value * 64))
                throw new InvalidOperationException("Candidate input violates the exact tile proof.");
            _input[i] = value;
        }
        var hostMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        _context = new OpenClContext(OpenClNative.ClDeviceType.Gpu);
        _deviceInput = new OpenClBuffer<double>(_context, Count);
        _deviceOutput = new OpenClBuffer<double>(_context, Count);
        var allocationMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        _blockedProgram = new OpenClProgram(_context, Source(false));
        _blockedProgram.Build();
        _blocked = new OpenClKernel(_context, _blockedProgram, "calculate");
        _tiledProgram = new OpenClProgram(_context, Source(true));
        _tiledProgram.Build();
        _tiled = new OpenClKernel(_context, _tiledProgram, "calculate");
        foreach (var kernel in new[] { _blocked, _tiled })
        {
            kernel.SetArg(0, _deviceInput.Handle);
            kernel.SetArg(1, _deviceOutput.Handle);
            kernel.SetArg(2, Count);
        }
        var compileMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        _deviceInput.CopyFromHost(_input);
        var uploadMs = timer.Elapsed.TotalMilliseconds;
        Console.WriteLine($"PREPARATION count={Count} composed={Composed} device={_context.DeviceName} hostMs={hostMs:F4} contextAndBuffersMs={allocationMs:F4} twoProgramsMs={compileMs:F4} uploadMs={uploadMs:F4} totalMs={total.Elapsed.TotalMilliseconds:F4}");
        var expected = new double[Count];
        CpuFeasibilityPrototypes.CurrentSma(_input, expected, 20);
        if (Composed) for (var i = 0; i < Count; i++) expected[i] = Math.Asin(expected[i]);
        Verify(expected, FreshBlocked());
        Verify(expected, FreshTiled());
        Verify(expected, ResidentTiled());

        // Intrusive stage diagnostics: Finish adds a barrier. Do not sum these
        // medians or substitute them for the complete timed benchmark methods.
        for (var sample = 0; sample < 5; sample++)
        {
            var output = new double[Count];
            timer.Restart(); _deviceInput.CopyFromHost(_input);
            uploadMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); Launch(true); _context.Finish();
            var kernelMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart(); _deviceOutput.CopyToHost(output);
            Console.WriteLine($"STAGES count={Count} composed={Composed} uploadMs={uploadMs:F4} tiledKernelWithFinishMs={kernelMs:F4} readbackMs={timer.Elapsed.TotalMilliseconds:F4}");
        }
    }

    private void Verify(double[] expected, double[] actual)
    {
        for (var i = 0; i < Count; i++)
        {
            if (!Composed)
            {
                if (BitConverter.DoubleToInt64Bits(expected[i]) != BitConverter.DoubleToInt64Bits(actual[i]))
                    throw new InvalidOperationException($"SMA candidate changed bits at {i}.");
            }
            else if (!double.IsFinite(actual[i]) || (expected[i] == 0
                ? BitConverter.DoubleToInt64Bits(expected[i]) != BitConverter.DoubleToInt64Bits(actual[i])
                : Math.Abs(expected[i] - actual[i]) > 4e-15 * Math.Abs(expected[i])))
                throw new InvalidOperationException($"Asin candidate exceeded its budget at {i}.");
        }
    }

    private void Launch(bool tiled)
    {
        if (tiled) _tiled.Enqueue(new[] { ((ulong)Count + 127) / 128 * 128 }, new ulong[] { 128 });
        else _blocked.Enqueue(new[] { ((ulong)Count + 63) / 64 });
    }

    private double[] Run(bool tiled, bool upload)
    {
        var output = new double[Count];
        if (upload) _deviceInput.CopyFromHost(_input);
        Launch(tiled);
        _deviceOutput.CopyToHost(output);
        return output;
    }

    [Benchmark(Baseline = true)] public double[] FreshBlocked() => Run(false, true);
    [Benchmark] public double[] FreshTiled() => Run(true, true);
    [Benchmark] public double[] ResidentTiled() => Run(true, false);
    [Benchmark] public void DeviceOnlyBlocked() { Launch(false); _context.Finish(); }
    [Benchmark] public void DeviceOnlyTiled() { Launch(true); _context.Finish(); }

    [GlobalCleanup]
    public void Cleanup()
    {
        _blocked?.Dispose(); _tiled?.Dispose(); _blockedProgram?.Dispose(); _tiledProgram?.Dispose();
        _deviceInput?.Dispose(); _deviceOutput?.Dispose(); _context?.Dispose();
    }

    private string Source(bool tiled)
    {
        var publish = Composed ? "asin(mean)" : "mean";
        return "#pragma OPENCL EXTENSION cl_khr_fp64 : enable\n#pragma OPENCL FP_CONTRACT OFF\n" + (tiled ? $$"""
            __kernel void calculate(__global const double* input, __global double* output, int count) {
                int lane = get_local_id(0);
                long start = (long)get_group_id(0) * 128 - 19;
                __local double prefix[256];
                for (int j = lane; j < 256; j += 128) {
                    long index = start + j;
                    prefix[j] = j < 147 && index >= 0 && index < count ? input[index] : 0.0;
                }
                barrier(CLK_LOCAL_MEM_FENCE);
                for (int offset = 1; offset < 256; offset *= 2) {
                    double first = lane >= offset ? prefix[lane - offset] : 0.0;
                    double second = lane + 128 >= offset ? prefix[lane + 128 - offset] : 0.0;
                    barrier(CLK_LOCAL_MEM_FENCE);
                    prefix[lane] += first;
                    prefix[lane + 128] += second;
                    barrier(CLK_LOCAL_MEM_FENCE);
                }
                long i = start + 19 + lane;
                if (i < count) {
                    double mean = i < 19 ? 0.0 : (prefix[lane + 19] - (lane ? prefix[lane - 1] : 0.0)) / 20.0;
                    output[i] = {{publish}};
                }
            }
            """ : $$"""
            __kernel void calculate(__global const double* input, __global double* output, int count) {
                size_t start = get_global_id(0) * 64;
                if (start >= (size_t)count) return;
                size_t end = min(start + (size_t)64, (size_t)count);
                double sum = 0.0;
                for (size_t j = start >= 19 ? start - 19 : 0; j < start; ++j) sum += input[j];
                for (size_t i = start; i < end; ++i) {
                    sum += input[i];
                    if (i > start && i >= 20) sum -= input[i - 20];
                    double mean = i < 19 ? 0.0 : sum / 20.0;
                    output[i] = {{publish}};
                }
            }
            """);
    }
}
