//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

using System.Numerics;
using System.Runtime.InteropServices;

#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
#endif

namespace OoplesFinance.StockIndicators.Platform;

/// <summary>
/// Reports what the running machine and process can do, so that a program can say why it is taking
/// the slow path rather than leaving you to guess.
/// </summary>
/// <remarks>
/// <para>
/// Detection runs once, on first access, and the result is cached for the life of the process. None
/// of it changes while a process is running.
/// </para>
/// <code>
/// var capabilities = PlatformDetector.Capabilities;
/// if (!capabilities.Supports(CpuFeature.Avx2))
/// {
///     Console.WriteLine(PlatformDetector.PrintCapabilities());
/// }
/// </code>
/// </remarks>
public static class PlatformDetector
{
    private static readonly Lazy<HardwareCapabilities> LazyCapabilities =
        new(Detect, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// What this machine and process support. Detected once and cached.
    /// </summary>
    public static HardwareCapabilities Capabilities => LazyCapabilities.Value;

    /// <summary>
    /// A readable summary of <see cref="Capabilities"/>, for diagnostics.
    /// </summary>
    public static string PrintCapabilities() => Capabilities.Describe();

    /// <summary>
    /// Throws when an acceleration package is not installed, naming what to install.
    /// </summary>
    /// <param name="package">The package the caller needs.</param>
    /// <exception cref="MissingAccelerationPackageException">Thrown when it is not available.</exception>
    public static void Require(AccelerationPackage package)
    {
        if (!Capabilities.Has(package))
        {
            throw new MissingAccelerationPackageException(package);
        }
    }

    private static HardwareCapabilities Detect()
    {
        var cpuFeatures = new HashSet<CpuFeature>();

#if NET8_0_OR_GREATER
        // These are compile-time constants the JIT folds away, so this costs nothing at run time.
        // On .NET Framework the types do not exist at all, which is why the set comes back empty
        // there - a limit of what can be asked, not a claim about the processor.
        if (Sse.IsSupported) { cpuFeatures.Add(CpuFeature.Sse); }
        if (Sse2.IsSupported) { cpuFeatures.Add(CpuFeature.Sse2); }
        if (Sse3.IsSupported) { cpuFeatures.Add(CpuFeature.Sse3); }
        if (Sse41.IsSupported) { cpuFeatures.Add(CpuFeature.Sse41); }
        if (Sse42.IsSupported) { cpuFeatures.Add(CpuFeature.Sse42); }
        if (Avx.IsSupported) { cpuFeatures.Add(CpuFeature.Avx); }
        if (Avx2.IsSupported) { cpuFeatures.Add(CpuFeature.Avx2); }
        if (Avx512F.IsSupported) { cpuFeatures.Add(CpuFeature.Avx512F); }
        if (AdvSimd.IsSupported) { cpuFeatures.Add(CpuFeature.AdvSimd); }
#endif

        var vectorBytes = 0;
        try
        {
            vectorBytes = Vector<double>.Count * sizeof(double);
        }
        catch (NotSupportedException)
        {
            // Vector<T> refuses some element types on some runtimes; the width is a nicety, not a
            // reason to fail detection.
        }

        return new HardwareCapabilities(
            cpuFeatures,
            IsPackagePresent,
            Vector.IsHardwareAccelerated,
            vectorBytes,
            Environment.ProcessorCount,
            IntPtr.Size == 8,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture.ToString());
    }

    /// <summary>
    /// Whether the native library behind an acceleration package can be loaded in this process.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These packages carry no managed assembly - the published AiDotNet.Native.OpenBLAS contains a
    /// .targets file and <c>runtimes/win-x64/native/libopenblas.dll</c>, and nothing under lib/ - so
    /// there is no assembly to load and asking for one would report every package as missing however
    /// it was named. What can be asked is whether the operating system will load the native library
    /// the package deploys, which is how AiDotNet.Tensors itself decides, in
    /// <c>NativeLibraryDetector</c>.
    /// </para>
    /// <para>
    /// On .NET Framework there is no NativeLibrary, so this reports false rather than guessing.
    /// </para>
    /// </remarks>
    private static bool IsPackagePresent(AccelerationPackage package)
    {
#if NET8_0_OR_GREATER
        foreach (var candidate in GetNativeLibraryNames(package))
        {
            if (NativeLibrary.TryLoad(candidate, out var handle))
            {
                NativeLibrary.Free(handle);

                return true;
            }
        }
#endif

        return false;
    }

    /// <summary>
    /// The native libraries a package deploys, by platform.
    /// </summary>
    /// <remarks>
    /// Taken from what the published packages actually contain and from the names
    /// AiDotNet.Tensors loads, rather than from the package identifier - the two do not match.
    /// </remarks>
    private static IEnumerable<string> GetNativeLibraryNames(AccelerationPackage package)
    {
        switch (package)
        {
            case AccelerationPackage.OpenBlas:
                yield return "libopenblas";
                yield return "openblas";
                break;
            case AccelerationPackage.ClBlast:
                yield return "clblast";
                break;
            case AccelerationPackage.Cuda:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    yield return "nvcuda.dll";
                    yield return "cublas64_12";
                }
                else
                {
                    yield return "libcuda.so.1";
                    yield return "libcuda.so";
                    yield return "libcublas.so.12";
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(package), package, "Unknown acceleration package.");
        }
    }

    internal static string GetPackageId(AccelerationPackage package) => package switch
    {
        AccelerationPackage.OpenBlas => "AiDotNet.Native.OpenBLAS",
        AccelerationPackage.ClBlast => "AiDotNet.Native.CLBlast",
        AccelerationPackage.Cuda => "AiDotNet.Native.CUDA",
        _ => throw new ArgumentOutOfRangeException(nameof(package), package, "Unknown acceleration package.")
    };
}
