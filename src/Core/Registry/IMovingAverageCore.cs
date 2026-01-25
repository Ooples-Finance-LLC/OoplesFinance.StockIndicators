//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

using System;

namespace OoplesFinance.StockIndicators.Core.Registry;

/// <summary>
/// Interface for moving average core computation.
/// Implementations should be readonly structs to avoid allocation.
/// </summary>
public interface IMovingAverageCore
{
    /// <summary>
    /// Gets whether this moving average requires OHLC data instead of single input.
    /// </summary>
    bool RequiresOhlc { get; }

    /// <summary>
    /// Gets whether this moving average requires volume data.
    /// </summary>
    bool RequiresVolume { get; }

    /// <summary>
    /// Gets whether this moving average has additional parameters beyond length.
    /// </summary>
    bool HasExtraParams { get; }

    /// <summary>
    /// Computes the moving average for single-input data with default parameters.
    /// </summary>
    /// <param name="input">Input price series.</param>
    /// <param name="output">Output buffer for MA values.</param>
    /// <param name="length">Moving average period length.</param>
    void Compute(ReadOnlySpan<double> input, Span<double> output, int length);

    /// <summary>
    /// Computes the moving average for single-input data with extra parameters.
    /// </summary>
    /// <param name="input">Input price series.</param>
    /// <param name="output">Output buffer for MA values.</param>
    /// <param name="length">Moving average period length.</param>
    /// <param name="extraParams">Additional parameters specific to this MA type (e.g., offset, sigma for ALMA).</param>
    void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams);

    /// <summary>
    /// Computes the moving average for OHLC data with default parameters.
    /// </summary>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="close">Close prices.</param>
    /// <param name="output">Output buffer for MA values.</param>
    /// <param name="length">Moving average period length.</param>
    void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length);

    /// <summary>
    /// Computes the moving average for OHLC data with extra parameters.
    /// </summary>
    void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams);

    /// <summary>
    /// Computes the moving average requiring volume data.
    /// </summary>
    /// <param name="input">Input price series.</param>
    /// <param name="volume">Volume series.</param>
    /// <param name="output">Output buffer for MA values.</param>
    /// <param name="length">Moving average period length.</param>
    void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length);
}
