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
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Core.Registry;

/// <summary>
/// Registry for MovingAvgType to IMovingAverageCore mapping.
/// Provides O(1) lookup for MA implementations.
/// </summary>
public static class MovingAverageRegistry
{
    private static readonly Dictionary<MovingAvgType, IMovingAverageCore> _cores;

    static MovingAverageRegistry()
    {
        _cores = new Dictionary<MovingAvgType, IMovingAverageCore>
        {
            // Standard single-input MAs
            [MovingAvgType.SimpleMovingAverage] = new SmaCore(),
            [MovingAvgType.ExponentialMovingAverage] = new EmaCore(),
            [MovingAvgType.DoubleExponentialMovingAverage] = new DemaCore(),
            [MovingAvgType.TripleExponentialMovingAverage] = new TemaCore(),
            [MovingAvgType.WeightedMovingAverage] = new WmaCore(),
            [MovingAvgType.HullMovingAverage] = new HmaCore(),
            [MovingAvgType.TriangularMovingAverage] = new TmaCore(),
            [MovingAvgType.ArnaudLegouxMovingAverage] = new AlmaCore(),
            [MovingAvgType.KaufmanAdaptiveMovingAverage] = new KamaCore(),
            [MovingAvgType.TillsonT3MovingAverage] = new T3Core(),
            [MovingAvgType.ZeroLagExponentialMovingAverage] = new ZlemaCore(),
            [MovingAvgType.LinearRegression] = new LinearRegressionCore(),
            [MovingAvgType.WildersSmoothingMethod] = new WilderCore(),
            // Add more as needed...
        };
    }

    /// <summary>
    /// Gets the IMovingAverageCore implementation for the specified MovingAvgType.
    /// </summary>
    /// <param name="type">The moving average type.</param>
    /// <returns>The core implementation, or null if not registered.</returns>
    public static IMovingAverageCore? Get(MovingAvgType type)
    {
        return _cores.TryGetValue(type, out var core) ? core : null;
    }

    /// <summary>
    /// Gets the IMovingAverageCore implementation for the specified MovingAvgType.
    /// Throws if not found.
    /// </summary>
    /// <param name="type">The moving average type.</param>
    /// <returns>The core implementation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the type is not registered.</exception>
    public static IMovingAverageCore GetRequired(MovingAvgType type)
    {
        if (_cores.TryGetValue(type, out var core))
            return core;
        throw new KeyNotFoundException($"MovingAvgType.{type} is not registered in MovingAverageRegistry.");
    }

    /// <summary>
    /// Checks if a MovingAvgType is registered.
    /// </summary>
    public static bool IsRegistered(MovingAvgType type) => _cores.ContainsKey(type);

    /// <summary>
    /// Gets the count of registered MA types.
    /// </summary>
    public static int Count => _cores.Count;
}

#region Single-Input MA Implementations

/// <summary>
/// Simple Moving Average core implementation.
/// </summary>
public readonly struct SmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.SimpleMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Exponential Moving Average core implementation.
/// </summary>
public readonly struct EmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.ExponentialMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Double Exponential Moving Average core implementation.
/// </summary>
public readonly struct DemaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.DoubleExponentialMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Triple Exponential Moving Average core implementation.
/// </summary>
public readonly struct TemaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.TripleExponentialMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Weighted Moving Average core implementation.
/// </summary>
public readonly struct WmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.WeightedMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Hull Moving Average core implementation.
/// </summary>
public readonly struct HmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.HullMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Triangular Moving Average core implementation.
/// </summary>
public readonly struct TmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.TriangularMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Zero-Lag Exponential Moving Average core implementation.
/// </summary>
public readonly struct ZlemaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.ZeroLagEma(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Linear Regression core implementation.
/// </summary>
public readonly struct LinearRegressionCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.LinearRegression(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Wilder's Smoothing Method core implementation.
/// </summary>
public readonly struct WilderCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.WellesWilderMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

#endregion

#region MAs with Extra Parameters

/// <summary>
/// Arnaud Legoux Moving Average core implementation.
/// Extra params: [0] = offset (default 0.85), [1] = sigma (default 6)
/// </summary>
public readonly struct AlmaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => true;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.ArnaudLegouxMovingAverage(input, output, length, 0.85, 6);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
    {
        var offset = extraParams.Length > 0 ? extraParams[0] : 0.85;
        var sigma = extraParams.Length > 1 ? extraParams[1] : 6.0;
        MovingAverageCore.ArnaudLegouxMovingAverage(input, output, length, offset, sigma);
    }

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length, extraParams);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// Kaufman Adaptive Moving Average core implementation.
/// </summary>
public readonly struct KamaCore : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.KaufmanAdaptiveMovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length, extraParams);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

/// <summary>
/// T3 Moving Average core implementation.
/// </summary>
public readonly struct T3Core : IMovingAverageCore
{
    public bool RequiresOhlc => false;
    public bool RequiresVolume => false;
    public bool HasExtraParams => false;

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
        => MovingAverageCore.T3MovingAverage(input, output, length);

    public void Compute(ReadOnlySpan<double> input, Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(input, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length)
        => Compute(close, output, length);

    public void ComputeOhlc(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> output, int length, ReadOnlySpan<double> extraParams)
        => Compute(close, output, length, extraParams);

    public void ComputeWithVolume(ReadOnlySpan<double> input, ReadOnlySpan<double> volume,
        Span<double> output, int length)
        => Compute(input, output, length);
}

#endregion
