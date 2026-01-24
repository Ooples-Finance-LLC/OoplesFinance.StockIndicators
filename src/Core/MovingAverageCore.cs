using System;
using System.Buffers;

namespace OoplesFinance.StockIndicators.Core;

internal static class MovingAverageCore
{
    internal static void SimpleMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double sum = 0;
        for (var i = 0; i < input.Length; i++)
        {
            sum += input[i];
            if (i >= length)
            {
                sum -= input[i - length];
            }

            output[i] = i >= length - 1 ? sum / length : 0;
        }
    }

    internal static void WeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double numerator = 0;
        double windowSum = 0;
        var weightedSumDenominator = (double)length * (length + 1) / 2;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            numerator += length * currentValue - windowSum;
            windowSum += currentValue;

            if (i >= length)
            {
                windowSum -= input[i - length];
            }

            output[i] = numerator / weightedSumDenominator;
        }
    }

    internal static void ExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var k = Math.Min(Math.Max((double)2 / (length + 1), 0.01), 0.99);
        double sum = 0;
        double prevEma = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            if (i < length)
            {
                sum += currentValue;
                var ema = sum / (i + 1);
                output[i] = ema;
                prevEma = ema;
            }
            else
            {
                var ema = (currentValue * k) + (prevEma * (1 - k));
                output[i] = ema;
                prevEma = ema;
            }
        }
    }

    internal static void WellesWilderMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var k = (double)1 / length;
        double prevWwma = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var wwma = (input[i] * k) + (prevWwma * (1 - k));
            output[i] = wwma;
            prevWwma = wwma;
        }
    }

    /// <summary>
    /// Computes Double Exponential Moving Average (DEMA = 2*EMA - EMA(EMA)).
    /// </summary>
    internal static void DoubleExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var ema1Array = pool.Rent(input.Length);
        var ema2Array = pool.Rent(input.Length);
        try
        {
            var ema1 = ema1Array.AsSpan(0, input.Length);
            var ema2 = ema2Array.AsSpan(0, input.Length);

            // First EMA
            ExponentialMovingAverage(input, ema1, length);

            // Second EMA (EMA of EMA)
            ExponentialMovingAverage(ema1, ema2, length);

            // DEMA = 2*EMA - EMA(EMA)
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (2 * ema1[i]) - ema2[i];
            }
        }
        finally
        {
            pool.Return(ema1Array);
            pool.Return(ema2Array);
        }
    }

    /// <summary>
    /// Computes Triple Exponential Moving Average (TEMA = 3*EMA - 3*EMA(EMA) + EMA(EMA(EMA))).
    /// </summary>
    internal static void TripleExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var ema1Array = pool.Rent(input.Length);
        var ema2Array = pool.Rent(input.Length);
        var ema3Array = pool.Rent(input.Length);
        try
        {
            var ema1 = ema1Array.AsSpan(0, input.Length);
            var ema2 = ema2Array.AsSpan(0, input.Length);
            var ema3 = ema3Array.AsSpan(0, input.Length);

            // First EMA
            ExponentialMovingAverage(input, ema1, length);

            // Second EMA (EMA of EMA)
            ExponentialMovingAverage(ema1, ema2, length);

            // Third EMA (EMA of EMA of EMA)
            ExponentialMovingAverage(ema2, ema3, length);

            // TEMA = 3*EMA - 3*EMA(EMA) + EMA(EMA(EMA))
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (3 * ema1[i]) - (3 * ema2[i]) + ema3[i];
            }
        }
        finally
        {
            pool.Return(ema1Array);
            pool.Return(ema2Array);
            pool.Return(ema3Array);
        }
    }

    /// <summary>
    /// Computes Hull Moving Average (HMA = WMA(2*WMA(n/2) - WMA(n), sqrt(n))).
    /// </summary>
    internal static void HullMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var halfLength = Math.Max(length / 2, 1);
        var sqrtLength = Math.Max((int)Math.Sqrt(length), 1);

        var pool = ArrayPool<double>.Shared;
        var wmaHalfArray = pool.Rent(input.Length);
        var wmaFullArray = pool.Rent(input.Length);
        var diffArray = pool.Rent(input.Length);
        try
        {
            var wmaHalf = wmaHalfArray.AsSpan(0, input.Length);
            var wmaFull = wmaFullArray.AsSpan(0, input.Length);
            var diff = diffArray.AsSpan(0, input.Length);

            // WMA with half period
            WeightedMovingAverage(input, wmaHalf, halfLength);

            // WMA with full period
            WeightedMovingAverage(input, wmaFull, length);

            // 2*WMA(n/2) - WMA(n)
            for (var i = 0; i < input.Length; i++)
            {
                diff[i] = (2 * wmaHalf[i]) - wmaFull[i];
            }

            // Final WMA with sqrt period
            WeightedMovingAverage(diff, output, sqrtLength);
        }
        finally
        {
            pool.Return(wmaHalfArray);
            pool.Return(wmaFullArray);
            pool.Return(diffArray);
        }
    }

    /// <summary>
    /// Computes Triangular Moving Average (TMA = SMA of SMA).
    /// </summary>
    internal static void TriangularMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var halfLength = (length + 1) / 2;

        var pool = ArrayPool<double>.Shared;
        var sma1Array = pool.Rent(input.Length);
        try
        {
            var sma1 = sma1Array.AsSpan(0, input.Length);

            // First SMA
            SimpleMovingAverage(input, sma1, halfLength);

            // Second SMA (SMA of SMA)
            SimpleMovingAverage(sma1, output, halfLength);
        }
        finally
        {
            pool.Return(sma1Array);
        }
    }

    /// <summary>
    /// Computes Volume Weighted Moving Average.
    /// </summary>
    internal static void VolumeWeightedMovingAverage(ReadOnlySpan<double> price, ReadOnlySpan<double> volume, Span<double> output, int length)
    {
        if (output.Length < price.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double pvSum = 0;
        double vSum = 0;

        for (var i = 0; i < price.Length; i++)
        {
            pvSum += price[i] * volume[i];
            vSum += volume[i];

            if (i >= length)
            {
                pvSum -= price[i - length] * volume[i - length];
                vSum -= volume[i - length];
            }

            output[i] = vSum != 0 ? pvSum / vSum : 0;
        }
    }

    /// <summary>
    /// Computes Linear Regression (Least Squares Moving Average).
    /// </summary>
    internal static void LinearRegression(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (var j = 0; j < length; j++)
            {
                var x = j;
                var y = input[i - length + 1 + j];
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }

            var denominator = (length * sumX2) - (sumX * sumX);
            if (denominator == 0)
            {
                output[i] = sumY / length;
            }
            else
            {
                var slope = ((length * sumXY) - (sumX * sumY)) / denominator;
                var intercept = (sumY - (slope * sumX)) / length;
                output[i] = intercept + (slope * (length - 1));
            }
        }
    }

    /// <summary>
    /// Computes Kaufman's Adaptive Moving Average (KAMA).
    /// </summary>
    internal static void KaufmanAdaptiveMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length, int fastLength = 2, int slowLength = 30)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var fastSc = 2.0 / (fastLength + 1);
        var slowSc = 2.0 / (slowLength + 1);
        double prevKama = 0;

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length)
            {
                prevKama = input[i];
                output[i] = prevKama;
                continue;
            }

            // Calculate efficiency ratio
            var change = Math.Abs(input[i] - input[i - length]);
            double volatility = 0;
            for (var j = 0; j < length; j++)
            {
                volatility += Math.Abs(input[i - j] - input[i - j - 1]);
            }

            var er = volatility != 0 ? change / volatility : 0;
            var sc = Math.Pow((er * (fastSc - slowSc)) + slowSc, 2);

            var kama = prevKama + (sc * (input[i] - prevKama));
            output[i] = kama;
            prevKama = kama;
        }
    }

    /// <summary>
    /// Computes Zero-Lag Exponential Moving Average.
    /// </summary>
    internal static void ZeroLagEma(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var lag = (length - 1) / 2;

        var pool = ArrayPool<double>.Shared;
        var adjustedArray = pool.Rent(input.Length);
        try
        {
            var adjusted = adjustedArray.AsSpan(0, input.Length);

            // Create lag-adjusted series
            for (var i = 0; i < input.Length; i++)
            {
                var lagIdx = Math.Max(i - lag, 0);
                adjusted[i] = (2 * input[i]) - input[lagIdx];
            }

            // Final EMA on adjusted series
            ExponentialMovingAverage(adjusted, output, length);
        }
        finally
        {
            pool.Return(adjustedArray);
        }
    }

    /// <summary>
    /// Computes Smoothed Moving Average (SMMA).
    /// </summary>
    internal static void SmoothedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double smma = 0;
        double sum = 0;

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                sum += input[i];
                output[i] = 0;
            }
            else if (i == length - 1)
            {
                sum += input[i];
                smma = sum / length;
                output[i] = smma;
            }
            else
            {
                smma = (smma * (length - 1) + input[i]) / length;
                output[i] = smma;
            }
        }
    }

    /// <summary>
    /// Computes McGinley Dynamic.
    /// </summary>
    internal static void McGinleyDynamic(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double md = 0;

        for (var i = 0; i < input.Length; i++)
        {
            if (i == 0)
            {
                md = input[i];
                output[i] = md;
            }
            else
            {
                var ratio = md != 0 ? input[i] / md : 1;
                var k = 0.6 * length * Math.Pow(ratio, 4);
                if (k == 0) k = 1;
                md = md + (input[i] - md) / k;
                output[i] = md;
            }
        }
    }

    /// <summary>
    /// Computes T3 Moving Average.
    /// </summary>
    internal static void T3MovingAverage(ReadOnlySpan<double> input, Span<double> output, int length, double vFactor = 0.7)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var ema1 = pool.Rent(input.Length);
        var ema2 = pool.Rent(input.Length);
        var ema3 = pool.Rent(input.Length);
        var ema4 = pool.Rent(input.Length);
        var ema5 = pool.Rent(input.Length);
        var ema6 = pool.Rent(input.Length);

        try
        {
            var e1 = ema1.AsSpan(0, input.Length);
            var e2 = ema2.AsSpan(0, input.Length);
            var e3 = ema3.AsSpan(0, input.Length);
            var e4 = ema4.AsSpan(0, input.Length);
            var e5 = ema5.AsSpan(0, input.Length);
            var e6 = ema6.AsSpan(0, input.Length);

            ExponentialMovingAverage(input, e1, length);
            ExponentialMovingAverage(e1, e2, length);
            ExponentialMovingAverage(e2, e3, length);
            ExponentialMovingAverage(e3, e4, length);
            ExponentialMovingAverage(e4, e5, length);
            ExponentialMovingAverage(e5, e6, length);

            var c1 = -vFactor * vFactor * vFactor;
            var c2 = 3 * vFactor * vFactor + 3 * vFactor * vFactor * vFactor;
            var c3 = -6 * vFactor * vFactor - 3 * vFactor - 3 * vFactor * vFactor * vFactor;
            var c4 = 1 + 3 * vFactor + vFactor * vFactor * vFactor + 3 * vFactor * vFactor;

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (c1 * e6[i]) + (c2 * e5[i]) + (c3 * e4[i]) + (c4 * e3[i]);
            }
        }
        finally
        {
            pool.Return(ema1);
            pool.Return(ema2);
            pool.Return(ema3);
            pool.Return(ema4);
            pool.Return(ema5);
            pool.Return(ema6);
        }
    }

    /// <summary>
    /// Computes Vidya (Variable Index Dynamic Average).
    /// </summary>
    internal static void Vidya(ReadOnlySpan<double> input, Span<double> output, int length, int cmoLength = 9)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var cmoArray = pool.Rent(input.Length);

        try
        {
            var cmo = cmoArray.AsSpan(0, input.Length);
            OscillatorCore.ChandeMomentumOscillator(input, cmo, cmoLength);

            var sc = 2.0 / (length + 1);
            double vidya = 0;

            for (var i = 0; i < input.Length; i++)
            {
                if (i == 0)
                {
                    vidya = input[i];
                    output[i] = vidya;
                }
                else
                {
                    var absChmo = Math.Abs(cmo[i]) / 100;
                    vidya = (sc * absChmo * input[i]) + ((1 - sc * absChmo) * vidya);
                    output[i] = vidya;
                }
            }
        }
        finally
        {
            pool.Return(cmoArray);
        }
    }

    /// <summary>
    /// Computes Variable Moving Average (VMA).
    /// </summary>
    internal static void VariableMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var stdDevArray = pool.Rent(input.Length);

        try
        {
            var stdDev = stdDevArray.AsSpan(0, input.Length);
            VolatilityCore.StandardDeviation(input, stdDev, length);

            double vma = 0;

            for (var i = 0; i < input.Length; i++)
            {
                if (i == 0)
                {
                    vma = input[i];
                    output[i] = vma;
                }
                else
                {
                    var k = stdDev[i] / (stdDev[i] + 0.001);
                    vma = (k * input[i]) + ((1 - k) * vma);
                    output[i] = vma;
                }
            }
        }
        finally
        {
            pool.Return(stdDevArray);
        }
    }

    /// <summary>
    /// Computes Arnaud Legoux Moving Average (ALMA).
    /// </summary>
    internal static void ArnaudLegouxMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 9, double offset = 0.85, double sigma = 6)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var m = offset * (length - 1);
        var s = length / sigma;

        var pool = ArrayPool<double>.Shared;
        var weightsArray = pool.Rent(length);

        try
        {
            var weights = weightsArray.AsSpan(0, length);

            // Pre-calculate weights
            double weightSum = 0;
            for (var j = 0; j < length; j++)
            {
                var weight = Math.Exp(-Math.Pow(j - m, 2) / (2 * s * s));
                weights[j] = weight;
                weightSum += weight;
            }

            // Normalize weights
            for (var j = 0; j < length; j++)
            {
                weights[j] /= weightSum;
            }

            for (var i = 0; i < input.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 0;
                    continue;
                }

                double alma = 0;
                for (var j = 0; j < length; j++)
                {
                    alma += weights[j] * input[i - length + 1 + j];
                }
                output[i] = alma;
            }
        }
        finally
        {
            pool.Return(weightsArray);
        }
    }

    /// <summary>
    /// Computes Least Squares Moving Average (LSMA / Linear Regression Curve).
    /// </summary>
    internal static void LeastSquaresMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 25)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // LSMA is essentially the same as linear regression value
        LinearRegression(input, output, length);
    }

    /// <summary>
    /// Computes Fractal Adaptive Moving Average (FRAMA).
    /// </summary>
    internal static void FractalAdaptiveMovingAverage(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 16)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var halfLength = length / 2;
        double frama = 0;

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                if (i == 0)
                {
                    frama = close[i];
                }
                continue;
            }

            // Calculate N1 (first half)
            var hh1 = double.MinValue;
            var ll1 = double.MaxValue;
            for (var j = i - length + 1; j <= i - halfLength; j++)
            {
                if (high[j] > hh1) hh1 = high[j];
                if (low[j] < ll1) ll1 = low[j];
            }
            var n1 = (hh1 - ll1) / halfLength;

            // Calculate N2 (second half)
            var hh2 = double.MinValue;
            var ll2 = double.MaxValue;
            for (var j = i - halfLength + 1; j <= i; j++)
            {
                if (high[j] > hh2) hh2 = high[j];
                if (low[j] < ll2) ll2 = low[j];
            }
            var n2 = (hh2 - ll2) / halfLength;

            // Calculate N3 (full period)
            var hh3 = double.MinValue;
            var ll3 = double.MaxValue;
            for (var j = i - length + 1; j <= i; j++)
            {
                if (high[j] > hh3) hh3 = high[j];
                if (low[j] < ll3) ll3 = low[j];
            }
            var n3 = (hh3 - ll3) / length;

            // Calculate fractal dimension
            double d = n1 > 0 && n2 > 0 && n3 > 0 ? (Math.Log(n1 + n2) - Math.Log(n3)) / Math.Log(2) : 1;

            // Calculate alpha
            var alpha = Math.Exp(-4.6 * (d - 1));
            alpha = Math.Max(0.01, Math.Min(alpha, 1));

            frama = (alpha * close[i]) + ((1 - alpha) * frama);
            output[i] = frama;
        }
    }

    /// <summary>
    /// Computes Adaptive Moving Average (Kaufman-style AMA variant).
    /// This is an alternative to KAMA with different smoothing.
    /// </summary>
    internal static void AdaptiveMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 10, int fastLength = 2, int slowLength = 30)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var fastSc = 2.0 / (fastLength + 1);
        var slowSc = 2.0 / (slowLength + 1);

        double ama = 0;

        for (var i = 0; i < input.Length; i++)
        {
            if (i == 0)
            {
                ama = input[i];
                output[i] = ama;
                continue;
            }

            if (i < length)
            {
                ama = input[i];
                output[i] = ama;
                continue;
            }

            // Calculate change and volatility
            var change = Math.Abs(input[i] - input[i - length]);
            double volatility = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                volatility += Math.Abs(input[j] - input[j - 1]);
            }

            // Calculate efficiency ratio
            var er = volatility != 0 ? change / volatility : 0;

            // Calculate smoothing constant
            var sc = Math.Pow(er * (fastSc - slowSc) + slowSc, 2);

            ama = (sc * input[i]) + ((1 - sc) * ama);
            output[i] = ama;
        }
    }

    /// <summary>
    /// Computes Sine Weighted Moving Average.
    /// </summary>
    internal static void SineWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var weightsArray = pool.Rent(length);

        try
        {
            var weights = weightsArray.AsSpan(0, length);

            // Pre-calculate sine weights
            double weightSum = 0;
            for (var j = 0; j < length; j++)
            {
                var weight = Math.Sin(Math.PI * (j + 1) / (length + 1));
                weights[j] = weight;
                weightSum += weight;
            }

            for (var i = 0; i < input.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 0;
                    continue;
                }

                double sum = 0;
                for (var j = 0; j < length; j++)
                {
                    sum += weights[j] * input[i - length + 1 + j];
                }
                output[i] = sum / weightSum;
            }
        }
        finally
        {
            pool.Return(weightsArray);
        }
    }

    /// <summary>
    /// Computes Hamming Weighted Moving Average.
    /// </summary>
    internal static void HammingMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var weightsArray = pool.Rent(length);

        try
        {
            var weights = weightsArray.AsSpan(0, length);

            // Pre-calculate Hamming window weights
            double weightSum = 0;
            for (var j = 0; j < length; j++)
            {
                var weight = 0.54 - 0.46 * Math.Cos(2 * Math.PI * j / (length - 1));
                weights[j] = weight;
                weightSum += weight;
            }

            for (var i = 0; i < input.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 0;
                    continue;
                }

                double sum = 0;
                for (var j = 0; j < length; j++)
                {
                    sum += weights[j] * input[i - length + 1 + j];
                }
                output[i] = sum / weightSum;
            }
        }
        finally
        {
            pool.Return(weightsArray);
        }
    }

    /// <summary>
    /// Computes Geometric Moving Average.
    /// </summary>
    internal static void GeometricMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            // Use logarithms for numerical stability
            double logSum = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                logSum += Math.Log(Math.Max(input[j], 0.000001));
            }
            output[i] = Math.Exp(logSum / length);
        }
    }

    /// <summary>
    /// Computes Regularized Exponential Moving Average (REMA).
    /// </summary>
    internal static void RegularizedEma(ReadOnlySpan<double> input, Span<double> output, int length = 14, double lambda = 0.5)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var k = 2.0 / (length + 1);
        double rema = 0;
        double prevRema = 0;
        double prevPrevRema = 0;

        for (var i = 0; i < input.Length; i++)
        {
            if (i == 0)
            {
                rema = input[i];
                output[i] = rema;
                prevPrevRema = rema;
                prevRema = rema;
                continue;
            }

            // Standard EMA with regularization term
            var ema = (k * input[i]) + ((1 - k) * prevRema);
            rema = ema + lambda * (2 * prevRema - prevPrevRema - ema);

            output[i] = rema;
            prevPrevRema = prevRema;
            prevRema = rema;
        }
    }

    /// <summary>
    /// Computes Modified Moving Average (MMA) - same as SMMA/Wilder's.
    /// </summary>
    internal static void ModifiedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        // MMA is the same as SMMA/Wilder's MA
        SmoothedMovingAverage(input, output, length);
    }

    /// <summary>
    /// Computes Jurik Moving Average (JMA approximation).
    /// </summary>
    internal static void JurikMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14, double phase = 0)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Jurik MA approximation using adaptive smoothing
        var beta = 0.45 * (length - 1) / (0.45 * (length - 1) + 2);
        var alpha = beta;
        var phaseRatio = phase < -100 ? 0.5 : (phase > 100 ? 2.5 : phase / 100 + 1.5);

        double jma = 0;
        double e0 = 0;
        double e1 = 0;
        double e2 = 0;

        for (var i = 0; i < input.Length; i++)
        {
            if (i == 0)
            {
                jma = input[i];
                e0 = input[i];
                e1 = 0;
                e2 = 0;
            }
            else
            {
                e0 = (1 - alpha) * input[i] + alpha * e0;
                e1 = (input[i] - e0) * (1 - beta) + beta * e1;
                e2 = (e0 + phaseRatio * e1 - jma) * Math.Pow(1 - alpha, 2) + Math.Pow(alpha, 2) * e2;
                jma = jma + e2;
            }
            output[i] = jma;
        }
    }

    /// <summary>
    /// Computes Butterworth Filter.
    /// </summary>
    internal static void ButterworthFilter(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var a = Math.Exp(-Math.Sqrt(2) * Math.PI / length);
        var b = 2 * a * Math.Cos(Math.Sqrt(2) * Math.PI / length);
        var c2 = b;
        var c3 = -a * a;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < input.Length; i++)
        {
            if (i < 2)
            {
                output[i] = input[i];
            }
            else
            {
                output[i] = c1 * input[i] + c2 * output[i - 1] + c3 * output[i - 2];
            }
        }
    }

    /// <summary>
    /// Computes SuperSmoother Filter (Ehlers).
    /// </summary>
    internal static void SuperSmoother(ReadOnlySpan<double> input, Span<double> output, int length = 10)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var a = Math.Exp(-Math.Sqrt(2) * Math.PI / length);
        var b = 2 * a * Math.Cos(Math.Sqrt(2) * 180 / length * Math.PI / 180);
        var c2 = b;
        var c3 = -a * a;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < input.Length; i++)
        {
            if (i < 2)
            {
                output[i] = input[i];
            }
            else
            {
                output[i] = c1 * (input[i] + input[i - 1]) / 2 + c2 * output[i - 1] + c3 * output[i - 2];
            }
        }
    }

    /// <summary>
    /// Computes Endpoint Moving Average.
    /// </summary>
    internal static void EndpointMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            // Linear regression endpoint
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (var j = 0; j < length; j++)
            {
                sumX += j;
                sumY += input[i - length + 1 + j];
                sumXY += j * input[i - length + 1 + j];
                sumX2 += j * j;
            }

            var denom = length * sumX2 - sumX * sumX;
            if (denom != 0)
            {
                var slope = (length * sumXY - sumX * sumY) / denom;
                var intercept = (sumY - slope * sumX) / length;
                output[i] = intercept + slope * (length - 1);
            }
            else
            {
                output[i] = input[i];
            }
        }
    }

    /// <summary>
    /// Computes Cubic Weighted Moving Average.
    /// </summary>
    internal static void CubicWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Pre-calculate cubic weights
        double weightSum = 0;
        for (var w = 1; w <= length; w++)
        {
            weightSum += w * w * w;
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            double sum = 0;
            for (var j = 0; j < length; j++)
            {
                var weight = (j + 1) * (j + 1) * (j + 1);
                sum += input[i - length + 1 + j] * weight;
            }
            output[i] = sum / weightSum;
        }
    }

    /// <summary>
    /// Computes Natural Moving Average.
    /// </summary>
    internal static void NaturalMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Natural MA uses logarithmic weights
        double logSum = 0;
        for (var w = 1; w <= length; w++)
        {
            logSum += Math.Log(w);
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            double sum = 0;
            for (var j = 0; j < length; j++)
            {
                var weight = Math.Log(j + 1);
                sum += input[i - length + 1 + j] * weight;
            }
            output[i] = sum / logSum;
        }
    }

    /// <summary>
    /// Computes Percentage Price Oscillator using MAs.
    /// </summary>
    internal static void PpoMa(ReadOnlySpan<double> input, Span<double> output, int fastLength = 12, int slowLength = 26)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var fastEmaArray = pool.Rent(input.Length);
        var slowEmaArray = pool.Rent(input.Length);

        try
        {
            var fastEma = fastEmaArray.AsSpan(0, input.Length);
            var slowEma = slowEmaArray.AsSpan(0, input.Length);

            ExponentialMovingAverage(input, fastEma, fastLength);
            ExponentialMovingAverage(input, slowEma, slowLength);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = slowEma[i] != 0 ? ((fastEma[i] - slowEma[i]) / slowEma[i]) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(fastEmaArray);
            pool.Return(slowEmaArray);
        }
    }

    /// <summary>
    /// Computes Alpha Decreasing Exponential Moving Average.
    /// EMA with alpha that decreases over time.
    /// </summary>
    internal static void AlphaDecreasingEma(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        output[0] = input[0];
        for (var i = 1; i < input.Length; i++)
        {
            // Alpha decreases as we go
            var alpha = 2.0 / (length + i);
            output[i] = alpha * input[i] + (1 - alpha) * output[i - 1];
        }
    }

    /// <summary>
    /// Computes Adaptive Exponential Moving Average.
    /// EMA with adaptive smoothing based on price movement.
    /// </summary>
    internal static void AdaptiveExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        output[0] = input[0];
        var baseAlpha = 2.0 / (length + 1);

        for (var i = 1; i < input.Length; i++)
        {
            // Adapt alpha based on absolute percentage change
            var change = input[i - 1] > 0 ? Math.Abs((input[i] - input[i - 1]) / input[i - 1]) : 0;
            var adaptedAlpha = baseAlpha * (1 + 10 * change);
            adaptedAlpha = Math.Min(1.0, adaptedAlpha);
            output[i] = adaptedAlpha * input[i] + (1 - adaptedAlpha) * output[i - 1];
        }
    }

    /// <summary>
    /// Computes Autonomous Recursive Moving Average.
    /// Self-adjusting recursive filter.
    /// </summary>
    internal static void AutonomousRecursiveMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var k = 2.0 / (length + 1);
        output[0] = input[0];

        for (var i = 1; i < input.Length; i++)
        {
            var error = input[i] - output[i - 1];
            var adaptedK = k + 0.5 * Math.Tanh(error / (Math.Abs(output[i - 1]) + 1e-10));
            adaptedK = Math.Max(0.01, Math.Min(0.99, adaptedK));
            output[i] = output[i - 1] + adaptedK * error;
        }
    }

    /// <summary>
    /// Computes Adaptive Least Squares MA.
    /// Least squares regression with adaptive window.
    /// </summary>
    internal static void AdaptiveLeastSquares(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = input[i];
                continue;
            }

            // Calculate linear regression endpoint
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (var j = 0; j < length; j++)
            {
                var x = j;
                var y = input[i - length + 1 + j];
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }

            var meanX = sumX / length;
            var meanY = sumY / length;
            var denominator = sumX2 - length * meanX * meanX;

            if (Math.Abs(denominator) > 1e-10)
            {
                var slope = (sumXY - length * meanX * meanY) / denominator;
                var intercept = meanY - slope * meanX;
                output[i] = intercept + slope * (length - 1);
            }
            else
            {
                output[i] = meanY;
            }
        }
    }

    /// <summary>
    /// Computes ATR Filtered EMA.
    /// EMA that filters based on ATR volatility.
    /// </summary>
    internal static void AtrFilteredEma(ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var atrArray = pool.Rent(close.Length);

        try
        {
            var atr = atrArray.AsSpan(0, close.Length);
            VolatilityCore.AverageTrueRange(high, low, close, atr, length);

            var alpha = 2.0 / (length + 1);
            output[0] = close[0];

            for (var i = 1; i < close.Length; i++)
            {
                // Filter: only update if change exceeds a fraction of ATR
                var change = Math.Abs(close[i] - output[i - 1]);
                var threshold = atr[i] * 0.1;

                if (change > threshold)
                {
                    output[i] = alpha * close[i] + (1 - alpha) * output[i - 1];
                }
                else
                {
                    output[i] = output[i - 1];
                }
            }
        }
        finally
        {
            pool.Return(atrArray);
        }
    }

    /// <summary>
    /// Computes Median Moving Average.
    /// Moving average using median instead of mean.
    /// </summary>
    internal static void MedianMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var windowArray = pool.Rent(length);

        try
        {
            var window = windowArray.AsSpan(0, length);

            for (var i = 0; i < input.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = input[i];
                    continue;
                }

                // Copy window values
                for (var j = 0; j < length; j++)
                {
                    window[j] = input[i - length + 1 + j];
                }

                // Sort to find median (simple insertion sort for small arrays)
                for (var j = 1; j < length; j++)
                {
                    var key = window[j];
                    var k = j - 1;
                    while (k >= 0 && window[k] > key)
                    {
                        window[k + 1] = window[k];
                        k--;
                    }
                    window[k + 1] = key;
                }

                // Get median
                output[i] = length % 2 == 0 ?
                    (window[length / 2 - 1] + window[length / 2]) / 2 :
                    window[length / 2];
            }
        }
        finally
        {
            pool.Return(windowArray);
        }
    }

    /// <summary>
    /// Computes Volume Adjusted Moving Average.
    /// MA weighted by volume.
    /// </summary>
    internal static void VolumeAdjustedMovingAverage(ReadOnlySpan<double> input, ReadOnlySpan<double> volume, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = input[i];
                continue;
            }

            double sumPriceVolume = 0;
            double sumVolume = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                sumPriceVolume += input[j] * volume[j];
                sumVolume += volume[j];
            }

            output[i] = sumVolume > 0 ? sumPriceVolume / sumVolume : input[i];
        }
    }

    /// <summary>
    /// Computes Quadratic Weighted Moving Average.
    /// MA with quadratic weight distribution.
    /// </summary>
    internal static void QuadraticWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Pre-calculate weight sum
        double weightSum = 0;
        for (var w = 1; w <= length; w++)
        {
            weightSum += w * w;
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            double sum = 0;
            for (var j = 0; j < length; j++)
            {
                var weight = (j + 1) * (j + 1);
                sum += input[i - length + 1 + j] * weight;
            }
            output[i] = sum / weightSum;
        }
    }

    /// <summary>
    /// Computes Parabolic Weighted Moving Average.
    /// MA with parabolic weight curve.
    /// </summary>
    internal static void ParabolicWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Pre-calculate weight sum using parabolic weights
        double weightSum = 0;
        for (var w = 0; w < length; w++)
        {
            var weight = length * length - w * w;
            weightSum += weight;
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            double sum = 0;
            for (var j = 0; j < length; j++)
            {
                var weight = length * length - (length - 1 - j) * (length - 1 - j);
                sum += input[i - length + 1 + j] * weight;
            }
            output[i] = sum / weightSum;
        }
    }

    #region Batch 14 - Additional Moving Averages

    /// <summary>
    /// Computes Ultimate Moving Average (T3 of T3).
    /// </summary>
    internal static void UltimateMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14, double vFactor = 0.7)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var t3Array = pool.Rent(input.Length);
        var t3ofT3Array = pool.Rent(input.Length);

        try
        {
            var t3 = t3Array.AsSpan(0, input.Length);
            var t3ofT3 = t3ofT3Array.AsSpan(0, input.Length);

            T3MovingAverage(input, t3, length, vFactor);
            T3MovingAverage(t3, t3ofT3, length, vFactor);

            t3ofT3.CopyTo(output);
        }
        finally
        {
            pool.Return(t3Array);
            pool.Return(t3ofT3Array);
        }
    }

    /// <summary>
    /// Computes Symmetrically Weighted Moving Average.
    /// Weights are symmetric around the center.
    /// </summary>
    internal static void SymmetricallyWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Calculate symmetric triangular weights
        var halfLen = (length + 1) / 2;
        double weightSum = 0;
        for (var w = 1; w <= halfLen; w++)
        {
            weightSum += w * (w <= length - w + 1 ? 2 : 1);
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            double sum = 0;
            for (var j = 0; j < length; j++)
            {
                var pos = j + 1;
                var weight = pos <= halfLen ? pos : length - pos + 1;
                sum += input[i - length + 1 + j] * weight;
            }
            output[i] = sum / weightSum;
        }
    }

    /// <summary>
    /// Computes Square Root Weighted Moving Average.
    /// Weights are square root of position.
    /// </summary>
    internal static void SquareRootWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Pre-calculate weight sum
        double weightSum = 0;
        for (var w = 1; w <= length; w++)
        {
            weightSum += Math.Sqrt(w);
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            double sum = 0;
            for (var j = 0; j < length; j++)
            {
                var weight = Math.Sqrt(j + 1);
                sum += input[i - length + 1 + j] * weight;
            }
            output[i] = sum / weightSum;
        }
    }

    /// <summary>
    /// Computes Spencer 15-Point Moving Average.
    /// Classic Henderson-type filter for smooth trends.
    /// </summary>
    internal static void Spencer15PointMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 15)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Spencer 15-point weights (symmetric)
        var weights = new double[] { -3, -6, -5, 3, 21, 46, 67, 74, 67, 46, 21, 3, -5, -6, -3 };
        double weightSum = 320; // Sum of absolute weights

        var halfLen = 7; // (15 - 1) / 2

        for (var i = 0; i < input.Length; i++)
        {
            if (i < halfLen || i >= input.Length - halfLen)
            {
                output[i] = input[i]; // Use input for edges
                continue;
            }

            double sum = 0;
            for (var j = 0; j < 15; j++)
            {
                sum += input[i - halfLen + j] * weights[j];
            }
            output[i] = sum / weightSum;
        }
    }

    /// <summary>
    /// Computes Spencer 21-Point Moving Average.
    /// Extended Henderson-type filter for smoother trends.
    /// </summary>
    internal static void Spencer21PointMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 21)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Spencer 21-point weights (symmetric)
        var weights = new double[] { -1, -3, -5, -5, -2, 6, 18, 33, 47, 57, 60, 57, 47, 33, 18, 6, -2, -5, -5, -3, -1 };
        double weightSum = 350; // Sum of weights

        var halfLen = 10; // (21 - 1) / 2

        for (var i = 0; i < input.Length; i++)
        {
            if (i < halfLen || i >= input.Length - halfLen)
            {
                output[i] = input[i]; // Use input for edges
                continue;
            }

            double sum = 0;
            for (var j = 0; j < 21; j++)
            {
                sum += input[i - halfLen + j] * weights[j];
            }
            output[i] = sum / weightSum;
        }
    }

    /// <summary>
    /// Computes Slow Smoothed Moving Average.
    /// SMA of SMA for extra smoothness.
    /// </summary>
    internal static void SlowSmoothedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var sma1Array = pool.Rent(input.Length);
        var sma2Array = pool.Rent(input.Length);

        try
        {
            var sma1 = sma1Array.AsSpan(0, input.Length);
            var sma2 = sma2Array.AsSpan(0, input.Length);

            SimpleMovingAverage(input, sma1, length);
            SimpleMovingAverage(sma1, sma2, length);

            sma2.CopyTo(output);
        }
        finally
        {
            pool.Return(sma1Array);
            pool.Return(sma2Array);
        }
    }

    /// <summary>
    /// Computes Repulsion Moving Average.
    /// Combines multiple EMAs with repulsion weighting.
    /// </summary>
    internal static void RepulsionMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var ema1Array = pool.Rent(input.Length);
        var ema2Array = pool.Rent(input.Length);
        var ema3Array = pool.Rent(input.Length);

        try
        {
            var ema1 = ema1Array.AsSpan(0, input.Length);
            var ema2 = ema2Array.AsSpan(0, input.Length);
            var ema3 = ema3Array.AsSpan(0, input.Length);

            var len1 = Math.Max(1, length / 2);
            var len2 = length;
            var len3 = length * 2;

            ExponentialMovingAverage(input, ema1, len1);
            ExponentialMovingAverage(input, ema2, len2);
            ExponentialMovingAverage(input, ema3, len3);

            // Repulsion = 3 * EMA1 - 2 * EMA2 + EMA3 / 2
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = 3 * ema1[i] - 2 * ema2[i] + ema3[i] / 2;
            }
        }
        finally
        {
            pool.Return(ema1Array);
            pool.Return(ema2Array);
            pool.Return(ema3Array);
        }
    }

    /// <summary>
    /// Computes Quick Moving Average.
    /// Fast response moving average using weighted decay.
    /// </summary>
    internal static void QuickMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Quick MA = current close + decay * (previous QMA - current close)
        var decay = 1.0 - (2.0 / (length + 1));

        output[0] = input[0];
        for (var i = 1; i < input.Length; i++)
        {
            output[i] = input[i] + decay * (output[i - 1] - input[i]);
        }
    }

    #endregion

    #region Batch 15 - Ehlers and Specialized Moving Averages

    /// <summary>
    /// Computes Ehlers Better Exponential Moving Average.
    /// Uses zero-lag calculations for improved response.
    /// </summary>
    internal static void EhlersBetterExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var alpha = 2.0 / (length + 1);
        var pool = ArrayPool<double>.Shared;
        var emaArray = pool.Rent(input.Length);
        var errArray = pool.Rent(input.Length);

        try
        {
            var ema = emaArray.AsSpan(0, input.Length);
            var err = errArray.AsSpan(0, input.Length);

            ema[0] = input[0];
            err[0] = 0;
            output[0] = input[0];

            for (var i = 1; i < input.Length; i++)
            {
                ema[i] = alpha * input[i] + (1 - alpha) * ema[i - 1];
                err[i] = input[i] - ema[i];
                output[i] = ema[i] + (1 - alpha) * err[i];
            }
        }
        finally
        {
            pool.Return(emaArray);
            pool.Return(errArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Deviation Scaled Moving Average.
    /// Adapts to volatility using standard deviation.
    /// </summary>
    internal static void EhlersDeviationScaledMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var smaArray = pool.Rent(input.Length);
        var devArray = pool.Rent(input.Length);

        try
        {
            var sma = smaArray.AsSpan(0, input.Length);
            var dev = devArray.AsSpan(0, input.Length);

            SimpleMovingAverage(input, sma, length);

            // Calculate deviation
            for (var i = 0; i < input.Length; i++)
            {
                if (i < length - 1)
                {
                    dev[i] = 0;
                    output[i] = input[i];
                    continue;
                }

                double sumSq = 0;
                for (var j = 0; j < length; j++)
                {
                    var diff = input[i - j] - sma[i];
                    sumSq += diff * diff;
                }
                dev[i] = Math.Sqrt(sumSq / length);

                // Scale factor based on deviation
                var scale = dev[i] > 0 ? (input[i] - sma[i]) / dev[i] : 0;
                var alpha = Math.Abs(scale) / (Math.Abs(scale) + 1);
                output[i] = alpha * input[i] + (1 - alpha) * (i > 0 ? output[i - 1] : input[i]);
            }
        }
        finally
        {
            pool.Return(smaArray);
            pool.Return(devArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Hann Moving Average.
    /// Uses Hann window for smoothing.
    /// </summary>
    internal static void EhlersHannMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Pre-calculate Hann weights
        var weights = new double[length];
        double weightSum = 0;
        for (var i = 0; i < length; i++)
        {
            weights[i] = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (length - 1)));
            weightSum += weights[i];
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            double sum = 0;
            for (var j = 0; j < length; j++)
            {
                sum += input[i - length + 1 + j] * weights[j];
            }
            output[i] = sum / weightSum;
        }
    }

    /// <summary>
    /// Computes Ehlers Triangle Moving Average.
    /// Uses triangular window coefficients.
    /// </summary>
    internal static void EhlersTriangleMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var halfLen = (length + 1) / 2;

        // Pre-calculate triangular weights
        var weights = new double[length];
        double weightSum = 0;
        for (var i = 0; i < length; i++)
        {
            weights[i] = i < halfLen ? i + 1 : length - i;
            weightSum += weights[i];
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            double sum = 0;
            for (var j = 0; j < length; j++)
            {
                sum += input[i - length + 1 + j] * weights[j];
            }
            output[i] = sum / weightSum;
        }
    }

    /// <summary>
    /// Computes Elastic Volume Weighted Moving Average V1.
    /// Volume-weighted with elastic adjustment.
    /// </summary>
    internal static void ElasticVolumeWeightedMovingAverageV1(ReadOnlySpan<double> price, ReadOnlySpan<double> volume, Span<double> output, int length = 14)
    {
        if (output.Length < price.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var vwmaArray = pool.Rent(price.Length);

        try
        {
            var vwma = vwmaArray.AsSpan(0, price.Length);
            VolumeWeightedMovingAverage(price, volume, vwma, length);

            // Apply elastic smoothing
            var k = 2.0 / (length + 1);
            output[0] = price[0];

            for (var i = 1; i < price.Length; i++)
            {
                // Elastic factor based on volume ratio
                double avgVol = 0;
                var startIdx = Math.Max(0, i - length + 1);
                var count = i - startIdx;
                if (count > 0)
                {
                    for (var j = startIdx; j < i; j++)
                    {
                        avgVol += volume[j];
                    }
                    avgVol /= count;
                }
                var volRatio = volume[i] > 0 && i >= length - 1 && avgVol > 0 ? volume[i] / (avgVol + 0.001) : 1.0;
                var elasticK = k * Math.Min(volRatio, 2.0);
                output[i] = elasticK * vwma[i] + (1 - elasticK) * output[i - 1];
            }
        }
        finally
        {
            pool.Return(vwmaArray);
        }
    }

    /// <summary>
    /// Computes Holt Exponential Moving Average.
    /// Double exponential smoothing with trend component.
    /// </summary>
    internal static void HoltExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14, double alpha = 0.5, double beta = 0.5)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var a = 2.0 / (length + 1);
        var b = beta * a;

        double level = input[0];
        double trend = 0;
        output[0] = level;

        for (var i = 1; i < input.Length; i++)
        {
            var prevLevel = level;
            level = a * input[i] + (1 - a) * (level + trend);
            trend = b * (level - prevLevel) + (1 - b) * trend;
            output[i] = level + trend;
        }
    }

    /// <summary>
    /// Computes Pentuple Exponential Moving Average.
    /// Five-fold EMA for extreme smoothing.
    /// </summary>
    internal static void PentupleExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var ema1 = pool.Rent(input.Length);
        var ema2 = pool.Rent(input.Length);
        var ema3 = pool.Rent(input.Length);
        var ema4 = pool.Rent(input.Length);
        var ema5 = pool.Rent(input.Length);

        try
        {
            ExponentialMovingAverage(input, ema1.AsSpan(0, input.Length), length);
            ExponentialMovingAverage(ema1.AsSpan(0, input.Length), ema2.AsSpan(0, input.Length), length);
            ExponentialMovingAverage(ema2.AsSpan(0, input.Length), ema3.AsSpan(0, input.Length), length);
            ExponentialMovingAverage(ema3.AsSpan(0, input.Length), ema4.AsSpan(0, input.Length), length);
            ExponentialMovingAverage(ema4.AsSpan(0, input.Length), ema5.AsSpan(0, input.Length), length);

            // PEMA = 5*EMA1 - 10*EMA2 + 10*EMA3 - 5*EMA4 + EMA5
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = 5 * ema1[i] - 10 * ema2[i] + 10 * ema3[i] - 5 * ema4[i] + ema5[i];
            }
        }
        finally
        {
            pool.Return(ema1);
            pool.Return(ema2);
            pool.Return(ema3);
            pool.Return(ema4);
            pool.Return(ema5);
        }
    }

    /// <summary>
    /// Computes Quadruple Exponential Moving Average.
    /// Four-fold EMA for heavy smoothing.
    /// </summary>
    internal static void QuadrupleExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var ema1 = pool.Rent(input.Length);
        var ema2 = pool.Rent(input.Length);
        var ema3 = pool.Rent(input.Length);
        var ema4 = pool.Rent(input.Length);

        try
        {
            ExponentialMovingAverage(input, ema1.AsSpan(0, input.Length), length);
            ExponentialMovingAverage(ema1.AsSpan(0, input.Length), ema2.AsSpan(0, input.Length), length);
            ExponentialMovingAverage(ema2.AsSpan(0, input.Length), ema3.AsSpan(0, input.Length), length);
            ExponentialMovingAverage(ema3.AsSpan(0, input.Length), ema4.AsSpan(0, input.Length), length);

            // QEMA = 4*EMA1 - 6*EMA2 + 4*EMA3 - EMA4
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = 4 * ema1[i] - 6 * ema2[i] + 4 * ema3[i] - ema4[i];
            }
        }
        finally
        {
            pool.Return(ema1);
            pool.Return(ema2);
            pool.Return(ema3);
            pool.Return(ema4);
        }
    }

    #endregion

    #region Batch 17 - Ehlers Laguerre and Related Filters

    /// <summary>
    /// Computes Ehlers Laguerre Filter.
    /// </summary>
    internal static void EhlersLaguerreFilter(ReadOnlySpan<double> input, Span<double> output, double alpha = 0.2)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var l0 = input.Length > 0 ? input[0] : 0.0;
        var l1 = l0;
        var l2 = l0;
        var l3 = l0;

        for (var i = 0; i < input.Length; i++)
        {
            var prevL0 = l0;
            var prevL1 = l1;
            var prevL2 = l2;

            l0 = (alpha * input[i]) + ((1 - alpha) * l0);
            l1 = (-1 * (1 - alpha) * l0) + prevL0 + ((1 - alpha) * l1);
            l2 = (-1 * (1 - alpha) * l1) + prevL1 + ((1 - alpha) * l2);
            l3 = (-1 * (1 - alpha) * l2) + prevL2 + ((1 - alpha) * l3);

            output[i] = (l0 + (2 * l1) + (2 * l2) + l3) / 6;
        }
    }

    /// <summary>
    /// Computes Ehlers Laguerre Relative Strength Index.
    /// </summary>
    internal static void EhlersLaguerreRelativeStrengthIndex(ReadOnlySpan<double> input, Span<double> output, double gamma = 0.5)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        gamma = Math.Max(0, Math.Min(1, gamma));
        var l0 = input.Length > 0 ? input[0] : 0.0;
        var l1 = l0;
        var l2 = l0;
        var l3 = l0;

        for (var i = 0; i < input.Length; i++)
        {
            var prevL0 = l0;
            var prevL1 = l1;
            var prevL2 = l2;

            l0 = ((1 - gamma) * input[i]) + (gamma * l0);
            l1 = (-gamma * l0) + prevL0 + (gamma * l1);
            l2 = (-gamma * l1) + prevL1 + (gamma * l2);
            l3 = (-gamma * l2) + prevL2 + (gamma * l3);

            var cu = (l0 >= l1 ? l0 - l1 : 0) + (l1 >= l2 ? l1 - l2 : 0) + (l2 >= l3 ? l2 - l3 : 0);
            var cd = (l0 >= l1 ? 0 : l1 - l0) + (l1 >= l2 ? 0 : l2 - l1) + (l2 >= l3 ? 0 : l3 - l2);

            output[i] = cu + cd != 0 ? Math.Max(0, Math.Min(1, cu / (cu + cd))) : 0;
        }
    }

    /// <summary>
    /// Computes Ehlers Zero Lag Exponential Moving Average.
    /// </summary>
    internal static void EhlersZeroLagExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var emaArray = pool.Rent(input.Length);
        var ecArray = pool.Rent(input.Length);

        try
        {
            var ema = emaArray.AsSpan(0, input.Length);
            var ec = ecArray.AsSpan(0, input.Length);

            ExponentialMovingAverage(input, ema, length);

            var gain = 0.0;

            for (var i = 0; i < input.Length; i++)
            {
                var prevEc = i > 0 ? ec[i - 1] : ema[i];
                var error = input[i] - prevEc;

                // Adaptive gain calculation
                if (i > 0)
                {
                    var leastError = error * error;
                    if (leastError > 0)
                    {
                        gain = Math.Max(0, Math.Min(2, gain + 0.01));
                    }
                    else
                    {
                        gain = Math.Max(0, gain - 0.01);
                    }
                }

                ec[i] = ema[i] + (gain * error);
                output[i] = ec[i];
            }
        }
        finally
        {
            pool.Return(emaArray);
            pool.Return(ecArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Fractal Adaptive Moving Average.
    /// </summary>
    internal static void EhlersFractalAdaptiveMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 16)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var halfLength = length / 2;

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = input[i];
                continue;
            }

            // Calculate fractal dimension
            var n1 = 0.0;
            var n2 = 0.0;
            var n3 = 0.0;

            var hh1 = double.MinValue;
            var ll1 = double.MaxValue;
            var hh2 = double.MinValue;
            var ll2 = double.MaxValue;
            var hh3 = double.MinValue;
            var ll3 = double.MaxValue;

            for (var j = 0; j < halfLength; j++)
            {
                var val = input[i - j];
                if (val > hh1) hh1 = val;
                if (val < ll1) ll1 = val;
            }
            n1 = (hh1 - ll1) / halfLength;

            for (var j = halfLength; j < length; j++)
            {
                var val = input[i - j];
                if (val > hh2) hh2 = val;
                if (val < ll2) ll2 = val;
            }
            n2 = (hh2 - ll2) / halfLength;

            for (var j = 0; j < length; j++)
            {
                var val = input[i - j];
                if (val > hh3) hh3 = val;
                if (val < ll3) ll3 = val;
            }
            n3 = (hh3 - ll3) / length;

            var dimen = 0.0;
            if (n1 + n2 > 0 && n3 > 0)
            {
                dimen = (Math.Log(n1 + n2) - Math.Log(n3)) / Math.Log(2);
            }

            var alpha = Math.Exp(-4.6 * (dimen - 1));
            alpha = Math.Max(0.01, Math.Min(1, alpha));

            var prevFrama = i > 0 ? output[i - 1] : input[i];
            output[i] = (alpha * input[i]) + ((1 - alpha) * prevFrama);
        }
    }

    /// <summary>
    /// Computes Ehlers Inverse Fisher Transform.
    /// </summary>
    internal static void EhlersInverseFisherTransform(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rsiArray = pool.Rent(input.Length);
        var smoothedArray = pool.Rent(input.Length);

        try
        {
            var rsi = rsiArray.AsSpan(0, input.Length);
            var smoothed = smoothedArray.AsSpan(0, input.Length);

            // Calculate RSI-like oscillator
            OscillatorCore.RelativeStrengthIndex(input, rsi, length);

            // Scale to -5 to +5 range
            for (var i = 0; i < input.Length; i++)
            {
                smoothed[i] = 0.1 * (rsi[i] - 50);
            }

            // Apply inverse Fisher transform
            for (var i = 0; i < input.Length; i++)
            {
                var x = smoothed[i];
                var exp2x = Math.Exp(2 * x);
                output[i] = (exp2x - 1) / (exp2x + 1);
            }
        }
        finally
        {
            pool.Return(rsiArray);
            pool.Return(smoothedArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Cyber Cycle.
    /// </summary>
    internal static void EhlersCyberCycle(ReadOnlySpan<double> input, Span<double> output, double alpha = 0.07)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var smoothArray = pool.Rent(input.Length);

        try
        {
            var smooth = smoothArray.AsSpan(0, input.Length);

            // 4-bar weighted moving average for smoothing
            for (var i = 0; i < input.Length; i++)
            {
                if (i < 3)
                {
                    smooth[i] = input[i];
                }
                else
                {
                    smooth[i] = (input[i] + 2 * input[i - 1] + 2 * input[i - 2] + input[i - 3]) / 6;
                }
            }

            // Cyber Cycle calculation
            for (var i = 0; i < input.Length; i++)
            {
                if (i < 7)
                {
                    output[i] = (input[i] - 2 * (i >= 1 ? input[i - 1] : 0) + (i >= 2 ? input[i - 2] : 0)) / 4;
                }
                else
                {
                    var prevCycle1 = output[i - 1];
                    var prevCycle2 = output[i - 2];
                    output[i] = ((1 - 0.5 * alpha) * (1 - 0.5 * alpha) * (smooth[i] - 2 * smooth[i - 1] + smooth[i - 2])) +
                                (2 * (1 - alpha) * prevCycle1) - ((1 - alpha) * (1 - alpha) * prevCycle2);
                }
            }
        }
        finally
        {
            pool.Return(smoothArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Stochastic.
    /// </summary>
    internal static void EhlersStochastic(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var smoothArray = pool.Rent(input.Length);
        var roofingArray = pool.Rent(input.Length);

        try
        {
            var smooth = smoothArray.AsSpan(0, input.Length);
            var roofing = roofingArray.AsSpan(0, input.Length);

            // Apply 2-pole super smoother filter first
            SuperSmoother(input, smooth, length);

            // Apply roofing filter
            EhlersRoofingFilter(smooth, roofing, 10, 48);

            // Calculate stochastic
            for (var i = 0; i < input.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 0;
                    continue;
                }

                var highest = double.MinValue;
                var lowest = double.MaxValue;

                for (var j = 0; j < length; j++)
                {
                    var val = roofing[i - j];
                    if (val > highest) highest = val;
                    if (val < lowest) lowest = val;
                }

                output[i] = highest - lowest != 0 ? (roofing[i] - lowest) / (highest - lowest) : 0;
            }
        }
        finally
        {
            pool.Return(smoothArray);
            pool.Return(roofingArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Adaptive Laguerre Filter.
    /// </summary>
    internal static void EhlersAdaptiveLaguerreFilter(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var l0 = input.Length > 0 ? input[0] : 0.0;
        var l1 = l0;
        var l2 = l0;
        var l3 = l0;

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = input[i];
                continue;
            }

            // Calculate adaptive alpha based on price range
            var highest = double.MinValue;
            var lowest = double.MaxValue;
            for (var j = 0; j < length; j++)
            {
                var val = input[i - j];
                if (val > highest) highest = val;
                if (val < lowest) lowest = val;
            }

            var diff = highest - lowest;
            var mid = (highest + lowest) / 2;
            var alpha = diff > 0 ? Math.Abs(input[i] - mid) / diff : 0.5;
            alpha = Math.Max(0.01, Math.Min(0.99, alpha));

            var prevL0 = l0;
            var prevL1 = l1;
            var prevL2 = l2;

            l0 = (alpha * input[i]) + ((1 - alpha) * l0);
            l1 = (-1 * (1 - alpha) * l0) + prevL0 + ((1 - alpha) * l1);
            l2 = (-1 * (1 - alpha) * l1) + prevL1 + ((1 - alpha) * l2);
            l3 = (-1 * (1 - alpha) * l2) + prevL2 + ((1 - alpha) * l3);

            output[i] = (l0 + (2 * l1) + (2 * l2) + l3) / 6;
        }
    }

    /// <summary>
    /// Computes Ehlers Roofing Filter (helper for other Ehlers indicators).
    /// </summary>
    internal static void EhlersRoofingFilter(ReadOnlySpan<double> input, Span<double> output, int hpLength = 10, int lpLength = 48)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var hpArray = pool.Rent(input.Length);

        try
        {
            var hp = hpArray.AsSpan(0, input.Length);

            // High-pass filter
            var alphaHp = (Math.Cos(2 * Math.PI / hpLength) + Math.Sin(2 * Math.PI / hpLength) - 1) / Math.Cos(2 * Math.PI / hpLength);

            for (var i = 0; i < input.Length; i++)
            {
                if (i < 2)
                {
                    hp[i] = 0;
                }
                else
                {
                    hp[i] = ((1 - alphaHp / 2) * (1 - alphaHp / 2) * (input[i] - 2 * input[i - 1] + input[i - 2])) +
                            (2 * (1 - alphaHp) * hp[i - 1]) - ((1 - alphaHp) * (1 - alphaHp) * hp[i - 2]);
                }
            }

            // Super smoother (low-pass filter)
            SuperSmoother(hp, output, lpLength);
        }
        finally
        {
            pool.Return(hpArray);
        }
    }

    #endregion

    #region Batch 18 - Additional Moving Averages and Filters

    /// <summary>
    /// Computes Cubed Weighted Moving Average.
    /// </summary>
    internal static void CubedWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            double sum = 0, weightedSum = 0;
            for (var j = 0; j < length && i >= j; j++)
            {
                var weight = Math.Pow(length - j, 3);
                sum += input[i - j] * weight;
                weightedSum += weight;
            }
            output[i] = weightedSum != 0 ? sum / weightedSum : 0;
        }
    }

    /// <summary>
    /// Computes Coral Trend Indicator.
    /// </summary>
    internal static void CoralTrendIndicator(ReadOnlySpan<double> input, Span<double> output, int length = 21, double cd = 0.4)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var di = ((double)(length - 1) / 2) + 1;
        var c1 = 2 / (di + 1);
        var c2 = 1 - c1;
        var c3 = 3 * ((cd * cd) + (cd * cd * cd));
        var c4 = -3 * ((2 * cd * cd) + cd + (cd * cd * cd));
        var c5 = (3 * cd) + 1 + (cd * cd * cd) + (3 * cd * cd);

        var i1 = 0.0;
        var i2 = 0.0;
        var i3 = 0.0;
        var i4 = 0.0;
        var i5 = 0.0;
        var i6 = 0.0;

        for (var i = 0; i < input.Length; i++)
        {
            i1 = (c1 * input[i]) + (c2 * i1);
            i2 = (c1 * i1) + (c2 * i2);
            i3 = (c1 * i2) + (c2 * i3);
            i4 = (c1 * i3) + (c2 * i4);
            i5 = (c1 * i4) + (c2 * i5);
            i6 = (c1 * i5) + (c2 * i6);

            output[i] = (-cd * cd * cd * i6) + (c3 * i5) + (c4 * i4) + (c5 * i3);
        }
    }

    /// <summary>
    /// Computes Damped Sine Wave Weighted Filter.
    /// </summary>
    internal static void DampedSineWaveWeightedFilter(ReadOnlySpan<double> input, Span<double> output, int length = 50)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            double wSum = 0, wvSum = 0;
            for (var j = 1; j <= length && i >= j - 1; j++)
            {
                var ratio = (double)j / length;
                var w = Math.Sin(Math.Max(0.01, Math.Min(0.99, 2 * Math.PI * ratio))) / j;
                wvSum += w * input[i - (j - 1)];
                wSum += w;
            }
            output[i] = wSum != 0 ? wvSum / wSum : 0;
        }
    }

    /// <summary>
    /// Computes End Point Moving Average (Least Squares MA).
    /// </summary>
    internal static void EndPointMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = input[i];
                continue;
            }

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (var j = 0; j < length; j++)
            {
                var x = j + 1.0;
                var y = input[i - (length - 1 - j)];
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }

            var slope = (length * sumXY - sumX * sumY) / (length * sumX2 - sumX * sumX);
            var intercept = (sumY - slope * sumX) / length;
            output[i] = intercept + slope * length; // End point value
        }
    }

    /// <summary>
    /// Computes Fibonacci Weighted Moving Average.
    /// </summary>
    internal static void FibonacciWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Pre-calculate Fibonacci weights
        var fibs = new double[length];
        fibs[0] = 1;
        if (length > 1) fibs[1] = 1;
        for (var j = 2; j < length; j++)
        {
            fibs[j] = fibs[j - 1] + fibs[j - 2];
        }

        for (var i = 0; i < input.Length; i++)
        {
            double sum = 0, weightSum = 0;
            for (var j = 0; j < length && i >= j; j++)
            {
                var weight = fibs[length - 1 - j];
                sum += input[i - j] * weight;
                weightSum += weight;
            }
            output[i] = weightSum != 0 ? sum / weightSum : 0;
        }
    }

    /// <summary>
    /// Computes Generalized Double Exponential Moving Average (GDEMA).
    /// </summary>
    internal static void GeneralizedDoubleExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14, double volumeFactor = 1.0)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var ema1Array = pool.Rent(input.Length);
        var ema2Array = pool.Rent(input.Length);

        try
        {
            var ema1 = ema1Array.AsSpan(0, input.Length);
            var ema2 = ema2Array.AsSpan(0, input.Length);

            ExponentialMovingAverage(input, ema1, length);
            ExponentialMovingAverage(ema1, ema2, length);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = ((1 + volumeFactor) * ema1[i]) - (volumeFactor * ema2[i]);
            }
        }
        finally
        {
            pool.Return(ema1Array);
            pool.Return(ema2Array);
        }
    }

    /// <summary>
    /// Computes Geometric Mean Moving Average.
    /// </summary>
    internal static void GeometricMeanMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = input[i];
                continue;
            }

            var product = 1.0;
            var count = 0;
            for (var j = 0; j < length; j++)
            {
                var val = input[i - j];
                if (val > 0)
                {
                    product *= val;
                    count++;
                }
            }
            output[i] = count > 0 ? Math.Pow(product, 1.0 / count) : 0;
        }
    }

    /// <summary>
    /// Computes Harmonic Mean Moving Average.
    /// </summary>
    internal static void HarmonicMeanMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = input[i];
                continue;
            }

            var sum = 0.0;
            var count = 0;
            for (var j = 0; j < length; j++)
            {
                var val = input[i - j];
                if (val != 0)
                {
                    sum += 1.0 / val;
                    count++;
                }
            }
            output[i] = count > 0 && sum != 0 ? count / sum : 0;
        }
    }

    #endregion

    #region Batch 19 - Ehlers Butterworth and Super Smoother Filters

    /// <summary>
    /// Computes Ehlers 2-Pole Butterworth Filter V1 using span-based computation.
    /// </summary>
    internal static void Ehlers2PoleButterworthFilterV1(ReadOnlySpan<double> input, Span<double> output, int length = 10)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var sqrt2 = Math.Sqrt(2);
        var a = Math.Exp(Math.Max(Math.Min(-sqrt2 * Math.PI / length, -0.01), -0.99));
        var b = 2 * a * Math.Cos(Math.Min(Math.Max(sqrt2 * 1.25 * Math.PI / length, 0.01), 0.99));
        var c2 = b;
        var c3 = -a * a;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevFilter1 = i >= 1 ? output[i - 1] : 0;
            var prevFilter2 = i >= 2 ? output[i - 2] : 0;

            output[i] = (c1 * currentValue) + (c2 * prevFilter1) + (c3 * prevFilter2);
        }
    }

    /// <summary>
    /// Computes Ehlers 2-Pole Butterworth Filter V2 using span-based computation.
    /// </summary>
    internal static void Ehlers2PoleButterworthFilterV2(ReadOnlySpan<double> input, Span<double> output, int length = 15)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var sqrt2 = Math.Sqrt(2);
        var a = Math.Exp(Math.Max(Math.Min(-sqrt2 * Math.PI / length, -0.01), -0.99));
        var b = 2 * a * Math.Cos(Math.Min(Math.Max(sqrt2 * Math.PI / length, 0.01), 0.99));
        var c2 = b;
        var c3 = -a * a;
        var c1 = (1 - b + Math.Pow(a, 2)) / 4;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevValue1 = i >= 1 ? input[i - 1] : 0;
            var prevValue3 = i >= 3 ? input[i - 3] : 0;
            var prevFilter1 = i >= 1 ? output[i - 1] : 0;
            var prevFilter2 = i >= 2 ? output[i - 2] : 0;

            output[i] = i < 3 ? currentValue : (c1 * (currentValue + (2 * prevValue1) + prevValue3)) + (c2 * prevFilter1) + (c3 * prevFilter2);
        }
    }

    /// <summary>
    /// Computes Ehlers 3-Pole Butterworth Filter V1 using span-based computation.
    /// </summary>
    internal static void Ehlers3PoleButterworthFilterV1(ReadOnlySpan<double> input, Span<double> output, int length = 10)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var a = Math.Exp(Math.Max(Math.Min(-Math.PI / length, -0.01), -0.99));
        var b = 2 * a * Math.Cos(Math.Min(Math.Max(1.738 * Math.PI / length, 0.01), 0.99));
        var c = a * a;
        var d2 = b + c;
        var d3 = -(c + (b * c));
        var d4 = c * c;
        var d1 = 1 - d2 - d3 - d4;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevFilter1 = i >= 1 ? output[i - 1] : 0;
            var prevFilter2 = i >= 2 ? output[i - 2] : 0;
            var prevFilter3 = i >= 3 ? output[i - 3] : 0;

            output[i] = (d1 * currentValue) + (d2 * prevFilter1) + (d3 * prevFilter2) + (d4 * prevFilter3);
        }
    }

    /// <summary>
    /// Computes Ehlers 3-Pole Butterworth Filter V2 using span-based computation.
    /// </summary>
    internal static void Ehlers3PoleButterworthFilterV2(ReadOnlySpan<double> input, Span<double> output, int length = 15)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var a1 = Math.Exp(Math.Max(Math.Min(-Math.PI / length, -0.01), -0.99));
        var b1 = 2 * a1 * Math.Cos(Math.Min(Math.Max(1.738 * Math.PI / length, 0.01), 0.99));
        var c1 = a1 * a1;
        var coef2 = b1 + c1;
        var coef3 = -(c1 + (b1 * c1));
        var coef4 = c1 * c1;
        var coef1 = (1 - b1 + c1) * (1 - c1) / 8;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevValue1 = i >= 1 ? input[i - 1] : 0;
            var prevValue2 = i >= 2 ? input[i - 2] : 0;
            var prevValue3 = i >= 3 ? input[i - 3] : 0;
            var prevFilter1 = i >= 1 ? output[i - 1] : 0;
            var prevFilter2 = i >= 2 ? output[i - 2] : 0;
            var prevFilter3 = i >= 3 ? output[i - 3] : 0;

            output[i] = i < 4 ? currentValue : (coef1 * (currentValue + (3 * prevValue1) + (3 * prevValue2) + prevValue3)) +
                                               (coef2 * prevFilter1) + (coef3 * prevFilter2) + (coef4 * prevFilter3);
        }
    }

    /// <summary>
    /// Computes Ehlers 2-Pole Super Smoother Filter V1 using span-based computation.
    /// </summary>
    internal static void Ehlers2PoleSuperSmootherFilterV1(ReadOnlySpan<double> input, Span<double> output, int length = 15)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var sqrt2 = Math.Sqrt(2);
        var a1 = Math.Exp(Math.Max(Math.Min(-sqrt2 * Math.PI / length, -0.01), -0.99));
        var b1 = 2 * a1 * Math.Cos(Math.Min(Math.Max(sqrt2 * Math.PI / length, 0.01), 0.99));
        var coef2 = b1;
        var coef3 = -a1 * a1;
        var coef1 = 1 - coef2 - coef3;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevFilter1 = i >= 1 ? output[i - 1] : 0;
            var prevFilter2 = i >= 2 ? output[i - 2] : 0;

            output[i] = i < 3 ? currentValue : (coef1 * currentValue) + (coef2 * prevFilter1) + (coef3 * prevFilter2);
        }
    }

    /// <summary>
    /// Computes Ehlers 2-Pole Super Smoother Filter V2 using span-based computation.
    /// </summary>
    internal static void Ehlers2PoleSuperSmootherFilterV2(ReadOnlySpan<double> input, Span<double> output, int length = 10)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var sqrt2 = Math.Sqrt(2);
        var a = Math.Exp(Math.Max(Math.Min(-sqrt2 * Math.PI / length, -0.01), -0.99));
        var b = 2 * a * Math.Cos(Math.Min(Math.Max(sqrt2 * Math.PI / length, 0.01), 0.99));
        var c2 = b;
        var c3 = -a * a;
        var c1 = 1 - c2 - c3;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevValue = i >= 1 ? input[i - 1] : 0;
            var prevFilter1 = i >= 1 ? output[i - 1] : 0;
            var prevFilter2 = i >= 2 ? output[i - 2] : 0;

            output[i] = (c1 * ((currentValue + prevValue) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
        }
    }

    /// <summary>
    /// Computes Ehlers 3-Pole Super Smoother Filter using span-based computation.
    /// </summary>
    internal static void Ehlers3PoleSuperSmootherFilter(ReadOnlySpan<double> input, Span<double> output, int length = 20)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var arg = Math.Min(Math.Max(Math.PI / length, 0.01), 0.99);
        var a1 = Math.Exp(-arg);
        var b1 = 2 * a1 * Math.Cos(1.738 * arg);
        var c1 = a1 * a1;
        var coef2 = b1 + c1;
        var coef3 = -(c1 + (b1 * c1));
        var coef4 = c1 * c1;
        var coef1 = 1 - coef2 - coef3 - coef4;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevFilter1 = i >= 1 ? output[i - 1] : 0;
            var prevFilter2 = i >= 2 ? output[i - 2] : 0;
            var prevFilter3 = i >= 3 ? output[i - 3] : 0;

            output[i] = i < 4 ? currentValue : (coef1 * currentValue) + (coef2 * prevFilter1) + (coef3 * prevFilter2) + (coef4 * prevFilter3);
        }
    }

    /// <summary>
    /// Computes Ehlers Decycler using span-based computation.
    /// </summary>
    internal static void EhlersDecycler(ReadOnlySpan<double> input, Span<double> output, int length = 60)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(length, 1);
        var alphaArg = Math.Min(2 * Math.PI / length, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        var alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevValue1 = i >= 1 ? input[i - 1] : 0;
            var prevDec = i >= 1 ? output[i - 1] : 0;

            output[i] = (alpha1 / 2 * (currentValue + prevValue1)) + ((1 - alpha1) * prevDec);
        }
    }

    #endregion
}
