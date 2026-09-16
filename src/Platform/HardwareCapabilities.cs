//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

using System.Collections.ObjectModel;
using System.Text;

namespace OoplesFinance.StockIndicators.Platform;

/// <summary>
/// An instruction set the running processor may support.
/// </summary>
public enum CpuFeature
{
    /// <summary>Streaming SIMD Extensions.</summary>
    Sse,

    /// <summary>Streaming SIMD Extensions 2.</summary>
    Sse2,

    /// <summary>Streaming SIMD Extensions 3.</summary>
    Sse3,

    /// <summary>Streaming SIMD Extensions 4.1.</summary>
    Sse41,

    /// <summary>Streaming SIMD Extensions 4.2.</summary>
    Sse42,

    /// <summary>Advanced Vector Extensions.</summary>
    Avx,

    /// <summary>Advanced Vector Extensions 2.</summary>
    Avx2,

    /// <summary>Advanced Vector Extensions 512, foundation.</summary>
    Avx512F,

    /// <summary>Arm Advanced SIMD, commonly called NEON.</summary>
    AdvSimd
}

/// <summary>
/// An optional package that adds acceleration the core library does not carry.
/// </summary>
public enum AccelerationPackage
{
    /// <summary>OpenBLAS-backed linear algebra.</summary>
    OpenBlas,

    /// <summary>CLBlast, for OpenCL devices.</summary>
    ClBlast,

    /// <summary>CUDA, for NVIDIA devices.</summary>
    Cuda
}

/// <summary>
/// What the running machine and process can actually do.
/// </summary>
/// <remarks>
/// Read this through <see cref="PlatformDetector.Capabilities"/>, which detects once and caches.
/// </remarks>
public sealed class HardwareCapabilities
{
    private readonly HashSet<CpuFeature> _cpuFeatures;

    /// <summary>
    /// An immutable view of <see cref="_cpuFeatures"/>, built once.
    /// </summary>
    /// <remarks>
    /// The set itself stays private because <see cref="Supports"/> wants its O(1) lookup. Handing
    /// the set out as <c>IReadOnlyCollection</c> only hides mutation behind a cast: a caller can
    /// cast back to <c>HashSet&lt;CpuFeature&gt;</c> and add or remove entries, and because
    /// <see cref="PlatformDetector.Capabilities"/> detects once and caches, that would change what
    /// Supports and Describe report for the rest of the process.
    /// </remarks>
    private readonly ReadOnlyCollection<CpuFeature> _cpuFeatureView;
    private readonly Dictionary<AccelerationPackage, bool> _packageProbes = new();
    private readonly Func<AccelerationPackage, bool> _probe;
    private readonly object _probeLock = new();

    internal HardwareCapabilities(
        HashSet<CpuFeature> cpuFeatures,
        Func<AccelerationPackage, bool> probe,
        bool isVectorHardwareAccelerated,
        int vectorByteCount,
        int processorCount,
        bool is64BitProcess,
        string frameworkDescription,
        string processArchitecture)
    {
        _cpuFeatures = cpuFeatures;
        _cpuFeatureView = new List<CpuFeature>(cpuFeatures).AsReadOnly();
        _probe = probe;
        IsVectorHardwareAccelerated = isVectorHardwareAccelerated;
        VectorByteCount = vectorByteCount;
        ProcessorCount = processorCount;
        Is64BitProcess = is64BitProcess;
        FrameworkDescription = frameworkDescription;
        ProcessArchitecture = processArchitecture;
    }

    /// <summary>Whether the processor supports an instruction set.</summary>
    /// <remarks>
    /// Always false on .NET Framework, where the intrinsics classes that answer this do not exist.
    /// That is a limit of what can be asked there, not a statement about the processor.
    /// </remarks>
    public bool Supports(CpuFeature feature) => _cpuFeatures.Contains(feature);

    /// <summary>Whether an optional acceleration package is loadable in this process.</summary>
    /// <remarks>
    /// Probed the first time each package is asked about, then remembered. Probing means attempting an
    /// assembly load, which costs a few milliseconds when the package is absent - so it is not done for
    /// packages nobody asks about, which is most of them for most programs.
    /// </remarks>
    public bool Has(AccelerationPackage package)
    {
        lock (_probeLock)
        {
            if (_packageProbes.TryGetValue(package, out var known))
            {
                return known;
            }

            var found = _probe(package);
            _packageProbes[package] = found;

            return found;
        }
    }

    /// <summary>Every instruction set that was detected.</summary>
    public IReadOnlyCollection<CpuFeature> CpuFeatures => _cpuFeatureView;

    /// <summary>
    /// Every acceleration package that is available.
    /// </summary>
    /// <remarks>Reading this probes for all of them, so prefer <see cref="Has"/> when one will do.</remarks>
    public IReadOnlyCollection<AccelerationPackage> AccelerationPackages
    {
        get
        {
            var found = new List<AccelerationPackage>();
            foreach (AccelerationPackage package in Enum.GetValues(typeof(AccelerationPackage)))
            {
                if (Has(package))
                {
                    found.Add(package);
                }
            }

            // A fresh list each call, so mutating it could not corrupt anything cached - but the
            // two collection properties should promise the same thing rather than differ by
            // accident.
            return found.AsReadOnly();
        }
    }

    /// <summary>Whether <see cref="System.Numerics.Vector{T}"/> is hardware accelerated here.</summary>
    public bool IsVectorHardwareAccelerated { get; }

    /// <summary>The width of a hardware vector in bytes, or zero when unknown.</summary>
    public int VectorByteCount { get; }

    /// <summary>Logical processors available to the process.</summary>
    public int ProcessorCount { get; }

    /// <summary>Whether the process is 64 bit.</summary>
    public bool Is64BitProcess { get; }

    /// <summary>The framework this is running on, as reported by the runtime.</summary>
    public string FrameworkDescription { get; }

    /// <summary>The process architecture, for example X64 or Arm64.</summary>
    public string ProcessArchitecture { get; }

    /// <summary>
    /// A readable summary of everything above.
    /// </summary>
    /// <remarks>
    /// Returned as a string rather than written to a logger. The core package has no third-party
    /// dependencies and taking one on a logging abstraction to print six lines would not be a fair
    /// trade - hand this to whatever logger you already have.
    /// </remarks>
    public string Describe()
    {
        var builder = new StringBuilder();
        builder.Append("Framework:  ").AppendLine(FrameworkDescription);
        builder.Append("Process:    ").Append(ProcessArchitecture)
            .Append(Is64BitProcess ? " (64-bit)" : " (32-bit)")
            .Append(", ").Append(ProcessorCount).AppendLine(" logical processors");
        builder.Append("Vectors:    ")
            .Append(IsVectorHardwareAccelerated ? "hardware accelerated" : "software fallback");
        if (VectorByteCount > 0)
        {
            builder.Append(", ").Append(VectorByteCount * 8).Append("-bit wide");
        }

        builder.AppendLine();

        builder.Append("CPU:        ")
            .AppendLine(_cpuFeatures.Count == 0
                ? "no instruction sets detected"
                : string.Join(", ", _cpuFeatures.OrderBy(f => f.ToString(), StringComparer.Ordinal)));

        var packages = AccelerationPackages;
        builder.Append("Packages:   ")
            .AppendLine(packages.Count == 0
                ? "none installed"
                : string.Join(", ", packages.OrderBy(p => p.ToString(), StringComparer.Ordinal)));

        return builder.ToString();
    }
}
