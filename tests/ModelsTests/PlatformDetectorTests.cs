using System.Diagnostics;
using OoplesFinance.StockIndicators.Exceptions;
using OoplesFinance.StockIndicators.Platform;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// Runtime capability detection (#93).
/// </summary>
public sealed class PlatformDetectorTests
{
    [Fact]
    public void CapabilitiesAreDetectedOnceAndCached()
    {
        var first = PlatformDetector.Capabilities;
        var second = PlatformDetector.Capabilities;

        second.Should().BeSameAs(first, "nothing here changes while a process runs");
    }

    /// <summary>
    /// The epic asks for detection under a millisecond once cached.
    /// </summary>
    [Fact]
    public void ReadingCachedCapabilitiesIsEffectivelyFree()
    {
        _ = PlatformDetector.Capabilities;

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < 1000; i++)
        {
            _ = PlatformDetector.Capabilities;
        }

        stopwatch.Stop();

        stopwatch.Elapsed.TotalMilliseconds.Should().BeLessThan(1,
            "a thousand reads should not add up to a millisecond, let alone one read");
    }

    [Fact]
    public void ReportsSomethingAboutTheMachineItIsRunningOn()
    {
        var capabilities = PlatformDetector.Capabilities;

        capabilities.ProcessorCount.Should().BeGreaterThan(0);
        capabilities.FrameworkDescription.Should().NotBeNullOrWhiteSpace();
        capabilities.ProcessArchitecture.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// The instruction sets are a hierarchy, so a machine claiming a later one must claim the earlier
    /// ones too. This catches a detector that reports a feature it did not actually test.
    /// </summary>
    [Fact]
    public void InstructionSetsAreInternallyConsistent()
    {
        var capabilities = PlatformDetector.Capabilities;

        if (capabilities.Supports(CpuFeature.Avx2))
        {
            capabilities.Supports(CpuFeature.Avx).Should().BeTrue("AVX2 implies AVX");
            capabilities.Supports(CpuFeature.Sse42).Should().BeTrue("AVX2 implies SSE4.2");
        }

        if (capabilities.Supports(CpuFeature.Sse42))
        {
            capabilities.Supports(CpuFeature.Sse41).Should().BeTrue();
            capabilities.Supports(CpuFeature.Sse2).Should().BeTrue();
        }

        if (capabilities.Supports(CpuFeature.Avx512F))
        {
            capabilities.Supports(CpuFeature.Avx2).Should().BeTrue("AVX-512 implies AVX2");
        }
    }

    [Fact]
    public void VectorWidthAgreesWithAcceleration()
    {
        var capabilities = PlatformDetector.Capabilities;

        if (capabilities.IsVectorHardwareAccelerated)
        {
            capabilities.VectorByteCount.Should().BeGreaterThan(0,
                "an accelerated vector has a width");
        }
    }

    [Fact]
    public void DescribeCoversEverythingItReports()
    {
        var text = PlatformDetector.PrintCapabilities();

        text.Should().Contain("Framework:");
        text.Should().Contain("Process:");
        text.Should().Contain("Vectors:");
        text.Should().Contain("CPU:");
        text.Should().Contain("Packages:");
    }

    /// <summary>
    /// None of the optional packages is installed here, so requiring one must say what to install.
    /// </summary>
    [Fact]
    public void RequiringAMissingPackageSaysHowToInstallIt()
    {
        var capabilities = PlatformDetector.Capabilities;
        if (capabilities.Has(AccelerationPackage.Cuda))
        {
            return;
        }

        var act = () => PlatformDetector.Require(AccelerationPackage.Cuda);

        act.Should().Throw<MissingAccelerationPackageException>()
            .Where(e => e.Package == AccelerationPackage.Cuda)
            .WithMessage("*dotnet add package*", "the useful part is what to do next")
            .WithMessage("*AiDotNet.Tensors.CUDA*");
    }

    [Fact]
    public void RequiringAPackageThatIsPresentDoesNotThrow()
    {
        var capabilities = PlatformDetector.Capabilities;

        foreach (var package in capabilities.AccelerationPackages)
        {
            var act = () => PlatformDetector.Require(package);
            act.Should().NotThrow();
        }
    }

    [Fact]
    public void ReportedFeaturesMatchTheQueryMethod()
    {
        var capabilities = PlatformDetector.Capabilities;

        foreach (var feature in capabilities.CpuFeatures)
        {
            capabilities.Supports(feature).Should().BeTrue(
                $"{feature} is listed, so it must also answer true when asked directly");
        }
    }
}
