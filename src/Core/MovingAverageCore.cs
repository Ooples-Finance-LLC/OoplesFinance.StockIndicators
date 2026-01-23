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
}
