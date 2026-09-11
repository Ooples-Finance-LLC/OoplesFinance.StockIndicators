//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

using OoplesFinance.StockIndicators.Platform;

namespace OoplesFinance.StockIndicators.Exceptions;

/// <summary>
/// Thrown when something needs an optional acceleration package that is not installed.
/// </summary>
/// <remarks>
/// The message names the package and the command to install it, because the useful thing to know
/// here is what to do next rather than that something was missing.
/// </remarks>
[Serializable]
public sealed class MissingAccelerationPackageException : Exception
{
    /// <summary>
    /// Creates the exception for a package that was required and not found.
    /// </summary>
    /// <param name="package">The package that is missing.</param>
    public MissingAccelerationPackageException(AccelerationPackage package)
        : base(BuildMessage(package))
    {
        Package = package;
    }

    /// <summary>
    /// Creates the exception with a caller-supplied message.
    /// </summary>
    public MissingAccelerationPackageException(AccelerationPackage package, string message)
        : base(message)
    {
        Package = package;
    }

    /// <summary>
    /// Creates the exception with a caller-supplied message and an inner cause.
    /// </summary>
    public MissingAccelerationPackageException(AccelerationPackage package, string message, Exception innerException)
        : base(message, innerException)
    {
        Package = package;
    }

    /// <summary>
    /// The package that was required.
    /// </summary>
    public AccelerationPackage Package { get; }

    private static string BuildMessage(AccelerationPackage package)
    {
        var packageId = PlatformDetector.GetPackageId(package);

        return $"This operation needs the {package} acceleration package, which is not installed. "
            + $"Add it with: dotnet add package {packageId}"
            + Environment.NewLine
            + "PlatformDetector.PrintCapabilities() reports what is currently available.";
    }
}
