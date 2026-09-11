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

        var pool = ArrayPool<double>.Shared;
        var sma1Array = pool.Rent(input.Length);
        try
        {
            var sma1 = sma1Array.AsSpan(0, input.Length);

            // First SMA with full length
            SimpleMovingAverage(input, sma1, length);

            // Second SMA (SMA of SMA) with full length
            SimpleMovingAverage(sma1, output, length);
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

        // Rolling sums for incremental computation
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var currentY = input[i];
            var currentX = (double)i;

            // Add current values to sums
            sumX += currentX;
            sumY += currentY;
            sumXY += currentX * currentY;
            sumX2 += currentX * currentX;

            // Remove old values if window is full
            if (i >= length)
            {
                var oldX = (double)(i - length);
                var oldY = input[i - length];
                sumX -= oldX;
                sumY -= oldY;
                sumXY -= oldX * oldY;
                sumX2 -= oldX * oldX;
            }

            var n = Math.Min(i + 1, length);
            var denominator = (n * sumX2) - (sumX * sumX);

            if (denominator == 0)
            {
                output[i] = n > 0 ? sumY / n : 0;
            }
            else
            {
                var slope = ((n * sumXY) - (sumX * sumY)) / denominator;
                var intercept = (sumY - (slope * sumX)) / n;
                output[i] = intercept + (slope * currentX);
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

        var pool = ArrayPool<double>.Shared;
        var ema1Array = pool.Rent(input.Length);
        var ema2Array = pool.Rent(input.Length);
        try
        {
            var ema1 = ema1Array.AsSpan(0, input.Length);
            var ema2 = ema2Array.AsSpan(0, input.Length);

            // First EMA of input
            ExponentialMovingAverage(input, ema1, length);

            // Second EMA of first EMA
            ExponentialMovingAverage(ema1, ema2, length);

            // ZEMA = 2*ema1 - ema2
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
    internal static void Vidya(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // The same VIDYA as CalculateVariableIndexDynamicAverage: the CMO over `length` bars, and the average
        // seeded at 0. This fast path used a fixed 9-bar CMO and seeded at the first price, so
        // MovingAvgType.VariableIndexDynamicAverage meant a different average here than in the indicator.
        var resolved = Math.Max(1, length);
        var alpha = 2d / (resolved + 1);
        var pool = ArrayPool<double>.Shared;
        var changesArray = pool.Rent(input.Length * 2);

        try
        {
            var pos = changesArray.AsSpan(0, input.Length);
            var neg = changesArray.AsSpan(input.Length, input.Length);
            double posSum = 0, negSum = 0, vidya = 0;

            for (var i = 0; i < input.Length; i++)
            {
                var diff = i >= 1 ? input[i] - input[i - 1] : 0;
                pos[i] = diff > 0 ? diff : 0;
                neg[i] = diff < 0 ? Math.Abs(diff) : 0;
                posSum += pos[i];
                negSum += neg[i];
                if (i >= resolved)
                {
                    posSum -= pos[i - resolved];
                    negSum -= neg[i - resolved];
                }

                var cmo = posSum + negSum != 0 ? Math.Min(Math.Max((posSum - negSum) / (posSum + negSum) * 100, -100), 100) : 0;
                var currentCmo = Math.Abs(cmo / 100);
                vidya = (input[i] * alpha * currentCmo) + (vidya * (1 - (alpha * currentCmo)));
                output[i] = vidya;
            }
        }
        finally
        {
            pool.Return(changesArray);
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

        // Chande's VMA as LazyBear writes it, the same algorithm as CalculateVariableMovingAverage. This fast
        // path used to compute a different average altogether - an EMA weighted by sd / (sd + 0.001), seeded at
        // the first price - so MovingAvgType.VariableMovingAverage meant one thing here and another there.
        var resolved = Math.Max(1, length);
        var k = 1d / resolved;
        var pool = ArrayPool<double>.Shared;
        var isArray = pool.Rent(input.Length);

        try
        {
            var isSeries = isArray.AsSpan(0, input.Length);
            double pdmS = 0, mdmS = 0, pdiS = 0, mdiS = 0, iS = 0, vma = 0;

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var prevValue = i >= 1 ? input[i - 1] : 0;
                var pdm = i >= 1 ? Math.Max(currentValue - prevValue, 0) : 0;
                var mdm = i >= 1 ? Math.Max(prevValue - currentValue, 0) : 0;

                pdmS = ((1 - k) * pdmS) + (k * pdm);
                mdmS = ((1 - k) * mdmS) + (k * mdm);
                var s = pdmS + mdmS;
                var pdi = s != 0 ? pdmS / s : 0;
                var mdi = s != 0 ? mdmS / s : 0;

                pdiS = ((1 - k) * pdiS) + (k * pdi);
                mdiS = ((1 - k) * mdiS) + (k * mdi);
                var d = Math.Abs(pdiS - mdiS);
                var s1 = pdiS + mdiS;
                var dS1 = s1 != 0 ? d / s1 : 0;

                iS = ((1 - k) * iS) + (k * dS1);
                isSeries[i] = iS;

                var hhv = iS;
                var llv = iS;
                for (var j = Math.Max(0, i - resolved + 1); j < i; j++)
                {
                    hhv = Math.Max(hhv, isSeries[j]);
                    llv = Math.Min(llv, isSeries[j]);
                }

                var d1 = hhv - llv;
                var vI = d1 != 0 ? (iS - llv) / d1 : 0;
                vma = ((1 - (k * vI)) * vma) + (k * vI * currentValue);
                output[i] = vma;
            }
        }
        finally
        {
            pool.Return(isArray);
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
    /// Weights are symmetric around the center, handles partial data.
    /// </summary>
    internal static void SymmetricallyWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var floorLength = length / 2;
        var roundLength = (length + 1) / 2;

        for (var i = 0; i < input.Length; i++)
        {
            double nr = 0, nl = 0, sr = 0, sl = 0;

            if (floorLength == roundLength)
            {
                for (var j = 0; j <= floorLength - 1; j++)
                {
                    double wr = (length - (length - 1 - j)) * length;
                    var prevVal = i >= j ? input[i - j] : 0;
                    nr += wr;
                    sr += prevVal * wr;
                }

                for (var j = floorLength; j <= length - 1; j++)
                {
                    double wl = (length - j) * length;
                    var prevVal = i >= j ? input[i - j] : 0;
                    nl += wl;
                    sl += prevVal * wl;
                }
            }
            else
            {
                for (var j = 0; j <= floorLength; j++)
                {
                    double wr = (length - (length - 1 - j)) * length;
                    var prevVal = i >= j ? input[i - j] : 0;
                    nr += wr;
                    sr += prevVal * wr;
                }

                for (var j = roundLength; j <= length - 1; j++)
                {
                    double wl = (length - j) * length;
                    var prevVal = i >= j ? input[i - j] : 0;
                    nl += wl;
                    sl += prevVal * wl;
                }
            }

            output[i] = nr + nl != 0 ? (sr + sl) / (nr + nl) : 0;
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
    /// Combines SMA1, SMA2, SMA3 with formula: sma3 + sma2 - sma1
    /// </summary>
    internal static void RepulsionMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var sma1Array = pool.Rent(input.Length);
        var sma2Array = pool.Rent(input.Length);
        var sma3Array = pool.Rent(input.Length);

        try
        {
            var sma1 = sma1Array.AsSpan(0, input.Length);
            var sma2 = sma2Array.AsSpan(0, input.Length);
            var sma3 = sma3Array.AsSpan(0, input.Length);

            // SMA periods: length, length*2, length*3
            SimpleMovingAverage(input, sma1, length);
            SimpleMovingAverage(input, sma2, length * 2);
            SimpleMovingAverage(input, sma3, length * 3);

            // RMA = sma3 + sma2 - sma1
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = sma3[i] + sma2[i] - sma1[i];
            }
        }
        finally
        {
            pool.Return(sma1Array);
            pool.Return(sma2Array);
            pool.Return(sma3Array);
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
        // For GetMovingAverageList compatibility, use fastLength=length, slowLength=length*2
        EhlersDeviationScaledMovingAverage(input, output, fastLength: length, slowLength: length * 2);
    }

    /// <summary>
    /// Computes Ehlers Deviation Scaled Moving Average with explicit parameters.
    /// </summary>
    internal static void EhlersDeviationScaledMovingAverage(ReadOnlySpan<double> input, Span<double> output, int fastLength, int slowLength)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var zerosArray = pool.Rent(input.Length);
        var avgZerosArray = pool.Rent(input.Length);
        var ssfArray = pool.Rent(input.Length);
        var stdDevArray = pool.Rent(input.Length);

        try
        {
            var zeros = zerosArray.AsSpan(0, input.Length);
            var avgZeros = avgZerosArray.AsSpan(0, input.Length);
            var ssf = ssfArray.AsSpan(0, input.Length);
            var stdDev = stdDevArray.AsSpan(0, input.Length);

            // Step 1: Compute zeros = input[i] - input[i-2] (with warmup handling)
            for (var i = 0; i < input.Length; i++)
            {
                var prevValue = i >= 2 ? input[i - 2] : 0;
                zeros[i] = i >= 2 ? input[i] - prevValue : 0;
            }

            // Step 2: Compute avgZeros = (zeros + prevZeros) / 2
            for (var i = 0; i < input.Length; i++)
            {
                var prevZeros = i > 0 ? zeros[i - 1] : 0;
                avgZeros[i] = (zeros[i] + prevZeros) / 2;
            }

            // Step 3: Apply Ehlers 2-Pole Super Smoother Filter V2 to avgZeros
            Ehlers2PoleSuperSmootherFilterV2(avgZeros, ssf, fastLength);

            // Step 4: Compute standard deviation of ssf using rolling window
            for (var i = 0; i < input.Length; i++)
            {
                if (i < slowLength - 1)
                {
                    stdDev[i] = 0;
                }
                else
                {
                    double sum = 0, sumSq = 0;
                    for (var j = 0; j < slowLength; j++)
                    {
                        var val = ssf[i - j];
                        sum += val;
                        sumSq += val * val;
                    }
                    var mean = sum / slowLength;
                    var variance = (sumSq / slowLength) - (mean * mean);
                    stdDev[i] = Math.Sqrt(Math.Max(0, variance));
                }
            }

            // Step 5: Compute scaled filter, alpha, and EDSMA
            double prevScaledFilter = 0;
            double prevEdsma = 0;

            for (var i = 0; i < input.Length; i++)
            {
                var currentSsf = ssf[i];
                var currentStdDev = stdDev[i];

                // Scaled filter = ssf / stdDev (with fallback to previous)
                var scaledFilter = currentStdDev != 0 ? currentSsf / currentStdDev : prevScaledFilter;
                prevScaledFilter = scaledFilter;

                // Alpha = clamp(5 * |scaledFilter| / slowLength, 0.01, 0.99)
                var alpha = 5 * Math.Abs(scaledFilter) / slowLength;
                alpha = Math.Max(0.01, Math.Min(0.99, alpha));

                // EDSMA = alpha * input + (1 - alpha) * prevEdsma
                var edsma = (alpha * input[i]) + ((1 - alpha) * prevEdsma);
                output[i] = edsma;
                prevEdsma = edsma;
            }
        }
        finally
        {
            pool.Return(zerosArray);
            pool.Return(avgZerosArray);
            pool.Return(ssfArray);
            pool.Return(stdDevArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Hann Moving Average.
    /// Uses Hann window: cos = 1 - cos(2*pi*j/(length+1)) for j=1 to length.
    /// </summary>
    internal static void EhlersHannMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            double filtSum = 0, coefSum = 0;

            for (var j = 1; j <= length; j++)
            {
                var prevV = i >= j - 1 ? input[i - (j - 1)] : 0;
                var cos = 1 - Math.Cos(2 * Math.PI * ((double)j / (length + 1)));
                filtSum += cos * prevV;
                coefSum += cos;
            }

            output[i] = coefSum != 0 ? filtSum / coefSum : 0;
        }
    }

    /// <summary>
    /// Computes Ehlers Triangle Moving Average.
    /// Uses triangular window coefficients with partial data handling.
    /// </summary>
    internal static void EhlersTriangleMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var l2 = (double)length / 2;

        for (var i = 0; i < input.Length; i++)
        {
            double filtSum = 0, coefSum = 0;

            for (var j = 1; j <= length; j++)
            {
                var prevV = i >= j - 1 ? input[i - (j - 1)] : 0;
                var c = j < l2 ? j : j > l2 ? length + 1 - j : l2;
                filtSum += c * prevV;
                coefSum += c;
            }

            output[i] = coefSum != 0 ? filtSum / coefSum : 0;
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

    #region Batch 20 - Additional Ehlers Filters and Moving Averages

    /// <summary>
    /// Computes Ehlers Hamming Moving Average using span-based computation.
    /// </summary>
    internal static void EhlersHammingMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 20, double pedestal = 3)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            double filtSum = 0, coefSum = 0;
            for (var j = 0; j < length; j++)
            {
                var prevV = i >= j ? input[i - j] : 0;
                var sine = Math.Sin(pedestal + ((Math.PI - (2 * pedestal)) * ((double)j / (length - 1))));
                filtSum += sine * prevV;
                coefSum += sine;
            }

            output[i] = coefSum != 0 ? filtSum / coefSum : 0;
        }
    }

    /// <summary>
    /// Computes Ehlers Leading Indicator using span-based computation.
    /// </summary>
    internal static void EhlersLeadingIndicator(ReadOnlySpan<double> input, Span<double> output, double alpha1 = 0.25, double alpha2 = 0.33)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        Span<double> lead = stackalloc double[input.Length];

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevValue = i >= 1 ? input[i - 1] : 0;
            var prevLead = i >= 1 ? lead[i - 1] : 0;

            lead[i] = (2 * currentValue) + ((alpha1 - 2) * prevValue) + ((1 - alpha1) * prevLead);

            var prevLeadIndicator = i >= 1 ? output[i - 1] : 0;
            output[i] = (alpha2 * lead[i]) + ((1 - alpha2) * prevLeadIndicator);
        }
    }

    /// <summary>
    /// Computes Ehlers High Pass Filter V1 using span-based computation.
    /// </summary>
    internal static void EhlersHighPassFilterV1(ReadOnlySpan<double> input, Span<double> output, int length = 125, double mult = 1)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(length, 1);
        var sqrt2 = Math.Sqrt(2);
        var alphaArg = Math.Min(Math.Max(2 * Math.PI / (mult * length * sqrt2), 0.01), 0.99);
        var alphaCos = Math.Cos(alphaArg);
        var alpha = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevValue1 = i >= 1 ? input[i - 1] : 0;
            var prevValue2 = i >= 2 ? input[i - 2] : 0;
            var prevHp1 = i >= 1 ? output[i - 1] : 0;
            var prevHp2 = i >= 2 ? output[i - 2] : 0;
            var pow1 = Math.Pow(1 - (alpha / 2), 2);
            var pow2 = Math.Pow(1 - alpha, 2);

            output[i] = (pow1 * (currentValue - (2 * prevValue1) + prevValue2)) + (2 * (1 - alpha) * prevHp1) - (pow2 * prevHp2);
        }
    }

    /// <summary>
    /// Computes Ehlers High Pass Filter V2 using span-based computation.
    /// </summary>
    internal static void EhlersHighPassFilterV2(ReadOnlySpan<double> input, Span<double> output, int length = 48)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(length, 1);
        var alphaArg = Math.Min(Math.Max(2 * Math.PI / length, 0.01), 0.99);
        var alphaCos = Math.Cos(alphaArg);
        var alpha = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevValue1 = i >= 1 ? input[i - 1] : 0;
            var prevHp = i >= 1 ? output[i - 1] : 0;

            output[i] = ((1 - (alpha / 2)) * (currentValue - prevValue1)) + ((1 - alpha) * prevHp);
        }
    }

    /// <summary>
    /// Computes Distance Weighted Moving Average using span-based computation.
    /// </summary>
    internal static void DistanceWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            double sum = 0, weightedSum = 0;
            for (var j = 0; j < length; j++)
            {
                var prevValue = i >= j ? input[i - j] : 0;

                double distanceSum = 0;
                for (var k = 0; k < length; k++)
                {
                    var prevValue2 = i >= k ? input[i - k] : 0;
                    distanceSum += Math.Abs(prevValue - prevValue2);
                }

                var weight = distanceSum != 0 ? 1 / distanceSum : 0;
                sum += prevValue * weight;
                weightedSum += weight;
            }

            output[i] = weightedSum != 0 ? sum / weightedSum : 0;
        }
    }

    /// <summary>
    /// Computes Ehlers Filter using span-based computation.
    /// </summary>
    internal static void EhlersFilter(ReadOnlySpan<double> input, Span<double> output, int length = 15)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var sqrt2 = Math.Sqrt(2);
        var a = Math.Exp(-sqrt2 * Math.PI / length);
        var b = 2 * a * Math.Cos(sqrt2 * Math.PI / length);
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
    /// Computes Ehlers Finite Impulse Response Filter using span-based computation.
    /// </summary>
    internal static void EhlersFiniteImpulseResponseFilter(ReadOnlySpan<double> input, Span<double> output, int length = 20)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            double sum = 0, coefSum = 0;
            for (var j = 0; j < length; j++)
            {
                var prevValue = i >= j ? input[i - j] : 0;
                var coef = 1 - ((double)j / length);
                sum += coef * prevValue;
                coefSum += coef;
            }

            output[i] = coefSum != 0 ? sum / coefSum : 0;
        }
    }

    /// <summary>
    /// Computes Ehlers Infinite Impulse Response Filter using span-based computation.
    /// </summary>
    internal static void EhlersInfiniteImpulseResponseFilter(ReadOnlySpan<double> input, Span<double> output, int length = 15)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var alpha = 2.0 / (length + 1);

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevFilter = i >= 1 ? output[i - 1] : 0;

            output[i] = (alpha * currentValue) + ((1 - alpha) * prevFilter);
        }
    }

    /// <summary>
    /// Computes Ahrens Moving Average using span-based computation.
    /// Formula: ahma = prevAhma + ((currentValue - ((prevAhma + priorAhma) / 2)) / length)
    /// </summary>
    internal static void AhrensMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 9)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevAhma = i >= 1 ? output[i - 1] : 0;
            var priorAhma = i >= length ? output[i - length] : currentValue;

            output[i] = prevAhma + ((currentValue - ((prevAhma + priorAhma) / 2)) / length);
        }
    }

    /// <summary>
    /// Computes Double Exponential Smoothing using span-based computation.
    /// </summary>
    internal static void DoubleExponentialSmoothing(ReadOnlySpan<double> input, Span<double> output, int length = 14, double alpha = 0.01, double gamma = 0.9)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            var x = input[i];
            var prevS = i >= 1 ? output[i - 1] : 0;
            var prevS2 = i >= 2 ? output[i - 2] : 0;
            var sChg = prevS - prevS2;

            output[i] = (alpha * x) + ((1 - alpha) * (prevS + (gamma * (sChg + ((1 - gamma) * sChg)))));
        }
    }

    /// <summary>
    /// Computes Compound Ratio Moving Average using span-based computation.
    /// Uses geometric weighting based on length.
    /// </summary>
    internal static void CompoundRatioMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 20)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var r = Math.Pow(length, (1.0 / (length - 1)) - 1);
        var bas = 1 + (r * 2);
        var smoothLength = Math.Max((int)Math.Round(Math.Sqrt(length)), 1);

        // First pass: compute raw weighted average
        var rawBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var raw = rawBuffer.AsSpan(0, input.Length);
            for (var i = 0; i < input.Length; i++)
            {
                double sum = 0, weightedSum = 0;
                for (var j = 0; j <= length - 1; j++)
                {
                    var weight = Math.Pow(bas, length - j);
                    var prevValue = i >= j ? input[i - j] : 0;
                    sum += prevValue * weight;
                    weightedSum += weight;
                }
                raw[i] = weightedSum != 0 ? sum / weightedSum : 0;
            }

            // Second pass: smooth with WMA
            WeightedMovingAverage(raw, output, smoothLength);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rawBuffer);
        }
    }

    /// <summary>
    /// Computes Corrected Moving Average using span-based computation.
    /// Uses SMA + variance with iterative k calculation.
    /// </summary>
    internal static void CorrectedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 35)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // First compute SMA
        var smaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var varianceBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var sma = smaBuffer.AsSpan(0, input.Length);
            var variance = varianceBuffer.AsSpan(0, input.Length);

            SimpleMovingAverage(input, sma, length);

            // Compute variance
            double sum = 0, sqSum = 0;
            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var oldValue = i >= length ? input[i - length] : 0;
                sum += currentValue - oldValue;
                sqSum += (currentValue * currentValue) - (oldValue * oldValue);

                var n = Math.Min(i + 1, length);
                var mean = n > 0 ? sum / n : 0;
                var meanSq = n > 0 ? sqSum / n : 0;
                variance[i] = meanSq - (mean * mean);
                variance[i] = Math.Max(0, variance[i]);
            }

            for (var i = 0; i < input.Length; i++)
            {
                var smaVal = sma[i];
                var prevCma = i >= 1 ? output[i - 1] : smaVal;
                var v1 = variance[i];
                var v2 = Math.Pow(prevCma - smaVal, 2);
                var v3 = v1 == 0 || v2 == 0 ? 1 : v2 / (v1 + v2);

                // Iterative k calculation
                double tolerance = Math.Pow(10, -5), err = 1, kPrev = 1, k = 1;
                for (var j = 0; j <= 5000 && err > tolerance; j++)
                {
                    k = v3 * kPrev * (2 - kPrev);
                    err = Math.Abs(kPrev - k);
                    kPrev = k;
                }

                output[i] = prevCma + (k * (smaVal - prevCma));
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(smaBuffer);
            ArrayPool<double>.Shared.Return(varianceBuffer);
        }
    }

    /// <summary>
    /// Computes Dynamically Adjustable Filter using span-based computation.
    /// </summary>
    internal static void DynamicallyAdjustableFilter(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var srcBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var kBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var src = srcBuffer.AsSpan(0, input.Length);
            var kList = kBuffer.AsSpan(0, input.Length);
            double sum = 0, sqSum = 0;

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var prevOut = i >= 1 ? output[i - 1] : currentValue;
                var prevK = i >= 1 ? kList[i - 1] : 0;

                var srcVal = currentValue + (currentValue - prevOut);
                src[i] = srcVal;

                // Rolling stddev calculation
                var oldSrc = i >= length ? src[i - length] : 0;
                sum += srcVal - oldSrc;
                sqSum += (srcVal * srcVal) - (oldSrc * oldSrc);

                var n = Math.Min(i + 1, length);
                var srcSma = n > 0 ? sum / n : 0;
                var meanSq = n > 0 ? sqSum / n : 0;
                var variance = Math.Max(0, meanSq - (srcSma * srcSma));
                var srcStdDev = Math.Sqrt(variance);

                var outVal = prevOut + (prevK * (srcVal - prevOut));
                output[i] = outVal;

                var diff = Math.Abs(srcVal - outVal);
                var k = diff != 0 ? diff / (diff + (srcStdDev * length)) : 0;
                kList[i] = k;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(srcBuffer);
            ArrayPool<double>.Shared.Return(kBuffer);
        }
    }

    /// <summary>
    /// Computes Dynamically Adjustable Moving Average using span-based computation.
    /// </summary>
    internal static void DynamicallyAdjustableMovingAverage(ReadOnlySpan<double> input, Span<double> output, int fastLength = 6, int slowLength = 200)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Compute short and long standard deviations
        var shortStdDevBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var longStdDevBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var cumSumBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var shortStdDev = shortStdDevBuffer.AsSpan(0, input.Length);
            var longStdDev = longStdDevBuffer.AsSpan(0, input.Length);
            var cumSum = cumSumBuffer.AsSpan(0, input.Length);

            // Compute rolling stddev for both windows
            ComputeRollingStdDev(input, shortStdDev, fastLength);
            ComputeRollingStdDev(input, longStdDev, slowLength);

            // Compute cumulative sum
            double cs = 0;
            for (var i = 0; i < input.Length; i++)
            {
                cs += input[i];
                cumSum[i] = cs;
            }

            for (var i = 0; i < input.Length; i++)
            {
                var a = shortStdDev[i];
                var b = longStdDev[i];
                var v = a != 0 ? (b / a) + fastLength : fastLength;
                var p = (int)Math.Round(Math.Min(Math.Max(v, fastLength), slowLength));

                var prevCumSum = i >= p ? cumSum[i - p] : 0;
                output[i] = p != 0 ? (cumSum[i] - prevCumSum) / p : 0;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(shortStdDevBuffer);
            ArrayPool<double>.Shared.Return(longStdDevBuffer);
            ArrayPool<double>.Shared.Return(cumSumBuffer);
        }
    }

    /// <summary>
    /// Helper method to compute rolling standard deviation.
    /// </summary>
    private static void ComputeRollingStdDev(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        double sum = 0, sqSum = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var oldValue = i >= length ? input[i - length] : 0;
            sum += currentValue - oldValue;
            sqSum += (currentValue * currentValue) - (oldValue * oldValue);

            var n = Math.Min(i + 1, length);
            var mean = n > 0 ? sum / n : 0;
            var meanSq = n > 0 ? sqSum / n : 0;
            var variance = Math.Max(0, meanSq - (mean * mean));
            output[i] = Math.Sqrt(variance);
        }
    }

    /// <summary>
    /// Computes Linear Weighted Moving Average (same as WMA).
    /// </summary>
    internal static void LinearWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        WeightedMovingAverage(input, output, length);
    }

    /// <summary>
    /// Computes Leo Moving Average using span-based computation.
    /// Formula: 2 * WMA - SMA
    /// </summary>
    internal static void LeoMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var wmaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var smaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var wma = wmaBuffer.AsSpan(0, input.Length);
            var sma = smaBuffer.AsSpan(0, input.Length);

            WeightedMovingAverage(input, wma, length);
            SimpleMovingAverage(input, sma, length);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (2 * wma[i]) - sma[i];
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(wmaBuffer);
            ArrayPool<double>.Shared.Return(smaBuffer);
        }
    }

    /// <summary>
    /// Computes McNicholl Moving Average using span-based computation.
    /// </summary>
    internal static void McNichollMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 20)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var emaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var ema = emaBuffer.AsSpan(0, input.Length);
            ExponentialMovingAverage(input, ema, length);

            // Compute difference and EMA of difference
            var diffBuffer = ArrayPool<double>.Shared.Rent(input.Length);
            var diffEmaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
            try
            {
                var diff = diffBuffer.AsSpan(0, input.Length);
                var diffEma = diffEmaBuffer.AsSpan(0, input.Length);

                for (var i = 0; i < input.Length; i++)
                {
                    diff[i] = input[i] - ema[i];
                }

                ExponentialMovingAverage(diff, diffEma, length);

                for (var i = 0; i < input.Length; i++)
                {
                    output[i] = ema[i] + diffEma[i];
                }
            }
            finally
            {
                ArrayPool<double>.Shared.Return(diffBuffer);
                ArrayPool<double>.Shared.Return(diffEmaBuffer);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(emaBuffer);
        }
    }

    /// <summary>
    /// Computes _3HMA (Three Hull Moving Average) using span-based computation.
    /// </summary>
    internal static void ThreeHMA(ReadOnlySpan<double> input, Span<double> output, int length = 50)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var p = Math.Max((int)Math.Ceiling((double)length / 2), 1);
        var p1 = Math.Max((int)Math.Ceiling((double)p / 3), 1);
        var p2 = Math.Max((int)Math.Ceiling((double)p / 2), 1);

        var wma1Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        var wma2Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        var wma3Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        var midBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var wma1 = wma1Buffer.AsSpan(0, input.Length);
            var wma2 = wma2Buffer.AsSpan(0, input.Length);
            var wma3 = wma3Buffer.AsSpan(0, input.Length);
            var mid = midBuffer.AsSpan(0, input.Length);

            WeightedMovingAverage(input, wma1, p1);
            WeightedMovingAverage(input, wma2, p2);
            WeightedMovingAverage(input, wma3, p);

            for (var i = 0; i < input.Length; i++)
            {
                mid[i] = (wma1[i] * 3) - wma2[i] - wma3[i];
            }

            WeightedMovingAverage(mid, output, p);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(wma1Buffer);
            ArrayPool<double>.Shared.Return(wma2Buffer);
            ArrayPool<double>.Shared.Return(wma3Buffer);
            ArrayPool<double>.Shared.Return(midBuffer);
        }
    }

    /// <summary>
    /// Computes Zero Lag Triple Exponential Moving Average using span-based computation.
    /// </summary>
    internal static void ZeroLagTripleExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // First compute TEMA
        var temaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var ema1Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var tema = temaBuffer.AsSpan(0, input.Length);
            var ema1 = ema1Buffer.AsSpan(0, input.Length);

            TripleExponentialMovingAverage(input, tema, length);
            ExponentialMovingAverage(tema, ema1, length);

            // Zero lag = 2*TEMA - EMA(TEMA)
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (2 * tema[i]) - ema1[i];
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(temaBuffer);
            ArrayPool<double>.Shared.Return(ema1Buffer);
        }
    }

    /// <summary>
    /// Computes Zero Low Lag Moving Average using span-based computation.
    /// </summary>
    internal static void ZeroLowLagMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 32)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Lag-compensated input
        var lag = (length - 1) / 2;
        var alpha = 2.0 / (length + 1);

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var lagValue = i >= lag ? input[i - lag] : input[0];
            var compensatedInput = (2 * currentValue) - lagValue;

            var prevOut = i >= 1 ? output[i - 1] : compensatedInput;
            output[i] = (alpha * compensatedInput) + ((1 - alpha) * prevOut);
        }
    }

    /// <summary>
    /// Computes Wilders Summation Method using span-based computation.
    /// </summary>
    internal static void WildersSummationMethod(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevSum = i >= 1 ? output[i - 1] : 0;

            if (i < length)
            {
                output[i] = prevSum + currentValue;
            }
            else
            {
                output[i] = prevSum - (prevSum / length) + currentValue;
            }
        }
    }

    /// <summary>
    /// Computes Simplified Weighted Moving Average using span-based computation.
    /// </summary>
    internal static void SimplifiedWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 20)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var priorValue = i >= length ? input[i - length] : 0;
            var prevSwma = i >= 1 ? output[i - 1] : currentValue;

            output[i] = prevSwma + ((currentValue - priorValue) / length);
        }
    }

    /// <summary>
    /// Computes Simplified Least Squares Moving Average using span-based computation.
    /// </summary>
    internal static void SimplifiedLeastSquaresMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 25)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var wmaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var smaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var wma = wmaBuffer.AsSpan(0, input.Length);
            var sma = smaBuffer.AsSpan(0, input.Length);

            WeightedMovingAverage(input, wma, length);
            SimpleMovingAverage(input, sma, length);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (2 * wma[i]) - sma[i];
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(wmaBuffer);
            ArrayPool<double>.Shared.Return(smaBuffer);
        }
    }

    /// <summary>
    /// Computes Sharp Modified Moving Average using span-based computation.
    /// </summary>
    internal static void SharpModifiedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14, double factor = 0.7)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var smaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var sma = smaBuffer.AsSpan(0, input.Length);
            SimpleMovingAverage(input, sma, length);

            var alpha = 2.0 / (length + 1);
            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var smaVal = sma[i];
                var prevSmma = i >= 1 ? output[i - 1] : currentValue;
                var diff = currentValue - smaVal;

                output[i] = prevSmma + (alpha * diff * factor);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(smaBuffer);
        }
    }

    /// <summary>
    /// Computes Tillson IE2 using span-based computation.
    /// </summary>
    internal static void TillsonIE2(ReadOnlySpan<double> input, Span<double> output, int length = 15, double vFactor = 0.7)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var ema1Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        var ema2Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var ema1 = ema1Buffer.AsSpan(0, input.Length);
            var ema2 = ema2Buffer.AsSpan(0, input.Length);

            ExponentialMovingAverage(input, ema1, length);
            ExponentialMovingAverage(ema1, ema2, length);

            for (var i = 0; i < input.Length; i++)
            {
                var dema = (2 * ema1[i]) - ema2[i];
                output[i] = ((1 - vFactor) * ema1[i]) + (vFactor * dema);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(ema1Buffer);
            ArrayPool<double>.Shared.Return(ema2Buffer);
        }
    }

    /// <summary>
    /// Computes Recursive Moving Trend Average using span-based computation.
    /// </summary>
    internal static void RecursiveMovingTrendAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var alpha = 2.0 / (length + 1);

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevRmta = i >= 1 ? output[i - 1] : currentValue;
            var priorRmta = i >= length ? output[i - length] : currentValue;

            var rmtaTrend = (prevRmta - priorRmta) / length;
            output[i] = (alpha * currentValue) + ((1 - alpha) * (prevRmta + rmtaTrend));
        }
    }

    /// <summary>
    /// Computes Quadratic Moving Average using span-based computation.
    /// </summary>
    internal static void QuadraticMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var sma1Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        var sma2Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var sma1 = sma1Buffer.AsSpan(0, input.Length);
            var sma2 = sma2Buffer.AsSpan(0, input.Length);

            SimpleMovingAverage(input, sma1, length);
            SimpleMovingAverage(sma1, sma2, length);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (2 * sma1[i]) - sma2[i];
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(sma1Buffer);
            ArrayPool<double>.Shared.Return(sma2Buffer);
        }
    }

    /// <summary>
    /// Computes Multi-Depth Zero Lag EMA using span-based computation.
    /// </summary>
    internal static void MultiDepthZeroLagExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 32, int depth = 3)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var alpha = 2.0 / (length + 1);
        var lag = (length - 1) / 2;

        // Apply zero-lag EMA multiple times based on depth
        var tempBuffer1 = ArrayPool<double>.Shared.Rent(input.Length);
        var tempBuffer2 = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            // Copy input to first temp buffer
            input.CopyTo(tempBuffer1.AsSpan(0, input.Length));
            var current = tempBuffer1.AsSpan(0, input.Length);
            var next = tempBuffer2.AsSpan(0, input.Length);

            for (var d = 0; d < depth; d++)
            {
                for (var i = 0; i < input.Length; i++)
                {
                    var currentValue = current[i];
                    var lagValue = i >= lag ? current[i - lag] : current[0];
                    var compensatedInput = (2 * currentValue) - lagValue;

                    var prevOut = i >= 1 ? next[i - 1] : compensatedInput;
                    next[i] = (alpha * compensatedInput) + ((1 - alpha) * prevOut);
                }

                // Swap buffers
                var temp = current;
                current = next;
                next = temp;
            }

            // Copy result to output
            current.CopyTo(output);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(tempBuffer1);
            ArrayPool<double>.Shared.Return(tempBuffer2);
        }
    }

    /// <summary>
    /// Computes Hull Estimate using span-based computation.
    /// </summary>
    internal static void HullEstimate(ReadOnlySpan<double> input, Span<double> output, int length = 50)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var halfLength = Math.Max(length / 2, 1);

        var wma1Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        var wma2Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var wma1 = wma1Buffer.AsSpan(0, input.Length);
            var wma2 = wma2Buffer.AsSpan(0, input.Length);

            WeightedMovingAverage(input, wma1, halfLength);
            WeightedMovingAverage(input, wma2, length);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (3 * wma1[i]) - (2 * wma2[i]);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(wma1Buffer);
            ArrayPool<double>.Shared.Return(wma2Buffer);
        }
    }

    /// <summary>
    /// Computes Inverse Distance Weighted Moving Average using span-based computation.
    /// </summary>
    internal static void InverseDistanceWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            double sum = 0, weightedSum = 0;
            for (var j = 0; j < length; j++)
            {
                var prevValue = i >= j ? input[i - j] : 0;
                var weight = 1.0 / (j + 1);
                sum += prevValue * weight;
                weightedSum += weight;
            }
            output[i] = weightedSum != 0 ? sum / weightedSum : 0;
        }
    }

    /// <summary>
    /// Computes Trimean using span-based computation.
    /// </summary>
    internal static void Trimean(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var windowBuffer = ArrayPool<double>.Shared.Rent(length);
        try
        {
            for (var i = 0; i < input.Length; i++)
            {
                var n = Math.Min(i + 1, length);
                for (var j = 0; j < n; j++)
                {
                    windowBuffer[j] = input[i - j];
                }

                // Sort window for quartile calculation
                Array.Sort(windowBuffer, 0, n);

                // Calculate Q1, Median, Q3
                var q1Idx = (n - 1) * 0.25;
                var medIdx = (n - 1) * 0.5;
                var q3Idx = (n - 1) * 0.75;

                var q1 = InterpolateQuartile(windowBuffer, n, q1Idx);
                var median = InterpolateQuartile(windowBuffer, n, medIdx);
                var q3 = InterpolateQuartile(windowBuffer, n, q3Idx);

                // Trimean = (Q1 + 2*Median + Q3) / 4
                output[i] = (q1 + (2 * median) + q3) / 4;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(windowBuffer);
        }
    }

    /// <summary>
    /// Helper method to interpolate quartile values.
    /// </summary>
    private static double InterpolateQuartile(double[] sorted, int length, double index)
    {
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper || upper >= length)
        {
            return sorted[Math.Min(lower, length - 1)];
        }
        var fraction = index - lower;
        return sorted[lower] + (fraction * (sorted[upper] - sorted[lower]));
    }

    /// <summary>
    /// Computes Well Rounded Moving Average using span-based computation.
    /// </summary>
    internal static void WellRoundedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var smaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var emaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var sma = smaBuffer.AsSpan(0, input.Length);
            var ema = emaBuffer.AsSpan(0, input.Length);

            SimpleMovingAverage(input, sma, length);
            ExponentialMovingAverage(input, ema, length);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (sma[i] + ema[i]) / 2;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(smaBuffer);
            ArrayPool<double>.Shared.Return(emaBuffer);
        }
    }

    /// <summary>
    /// Computes Linear Regression Line using span-based computation.
    /// </summary>
    internal static void LinearRegressionLine(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Linear regression line is the same as LSMA
        LinearRegression(input, output, length);
    }

    /// <summary>
    /// Computes Linear Extrapolation using span-based computation.
    /// </summary>
    internal static void LinearExtrapolation(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < 1)
            {
                output[i] = input[i];
                continue;
            }

            // Simple linear extrapolation: 2*current - prior
            var n = Math.Min(i + 1, length);
            var currentValue = input[i];
            var priorValue = i >= n ? input[i - n + 1] : input[0];
            var slope = (currentValue - priorValue) / (n - 1);

            output[i] = currentValue + slope;
        }
    }

    /// <summary>
    /// Computes JSA Moving Average using span-based computation.
    /// </summary>
    internal static void JsaMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var smaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var emaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var wmaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var sma = smaBuffer.AsSpan(0, input.Length);
            var ema = emaBuffer.AsSpan(0, input.Length);
            var wma = wmaBuffer.AsSpan(0, input.Length);

            SimpleMovingAverage(input, sma, length);
            ExponentialMovingAverage(input, ema, length);
            WeightedMovingAverage(input, wma, length);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (sma[i] + ema[i] + wma[i]) / 3;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(smaBuffer);
            ArrayPool<double>.Shared.Return(emaBuffer);
            ArrayPool<double>.Shared.Return(wmaBuffer);
        }
    }

    /// <summary>
    /// Computes Self Weighted Moving Average using span-based computation.
    /// </summary>
    internal static void SelfWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        for (var i = 0; i < input.Length; i++)
        {
            double sum = 0, weightSum = 0;
            for (var j = 0; j < length; j++)
            {
                var value = i >= j ? input[i - j] : 0;
                // Weight is the value from length periods before
                var weight = i >= length + j ? input[i - length - j] : 0;
                weightSum += weight;
                sum += weight * value;
            }

            output[i] = weightSum != 0 ? sum / weightSum : 0;
        }
    }

    /// <summary>
    /// Computes Henderson Weighted Moving Average using span-based computation.
    /// </summary>
    internal static void HendersonWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 7)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var termMult = Math.Max(1, (int)Math.Floor((double)(length - 1) / 2));

        for (var i = 0; i < input.Length; i++)
        {
            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var m = termMult;
                var n = j - termMult;
                var m1 = (double)(m + 1);
                var m2 = (double)(m + 2);
                var m3 = (double)(m + 3);

                var numerator = 315 * (m1 * m1 - n * n) * (m2 * m2 - n * n) * (m3 * m3 - n * n) *
                    ((3 * m2 * m2) - (11 * n * n) - 16);
                var denominator = 8 * m2 * (m2 * m2 - 1) * ((4 * m2 * m2) - 1) * ((4 * m2 * m2) - 9) *
                    ((4 * m2 * m2) - 25);
                var weight = denominator != 0 ? numerator / denominator : 0;
                var prevValue = i >= j ? input[i - j] : 0;

                sum += prevValue * weight;
                weightedSum += weight;
            }

            output[i] = weightedSum != 0 ? sum / weightedSum : 0;
        }
    }

    /// <summary>
    /// Computes Farey Sequence Weighted Moving Average using span-based computation.
    /// </summary>
    internal static void FareySequenceWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 5)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        // Generate Farey sequence weights
        var array = new double[4] { 0, 1, 1, length };
        var resList = new System.Collections.Generic.List<double>();

        while (array[2] <= length)
        {
            var a = array[0];
            var b = array[1];
            var c = array[2];
            var d = array[3];
            var k = Math.Floor((length + b) / array[3]);

            array[0] = c;
            array[1] = d;
            array[2] = (k * c) - a;
            array[3] = (k * d) - b;

            var res = array[1] != 0 ? Math.Round(array[0] / array[1], 3) : 0;
            resList.Insert(0, res);
        }

        for (var i = 0; i < input.Length; i++)
        {
            double sum = 0, weightedSum = 0;
            for (var j = 0; j < resList.Count; j++)
            {
                var prevValue = i >= j ? input[i - j] : 0;
                var weight = resList[j];
                sum += prevValue * weight;
                weightedSum += weight;
            }

            output[i] = weightedSum != 0 ? sum / weightedSum : 0;
        }
    }

    /// <summary>
    /// Computes Right Sided Ricker Moving Average using span-based computation.
    /// </summary>
    internal static void RightSidedRickerMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 50, double pctWidth = 60)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var width = pctWidth / 100 * length;

        for (var i = 0; i < input.Length; i++)
        {
            double w = 0, vw = 0;
            for (var j = 0; j < length; j++)
            {
                var prevV = i >= j ? input[i - j] : 0;
                var jOverWidth = j / width;
                var jSquared = (double)j * j;
                var widthSquared = width * width;
                var weight = (1 - jOverWidth * jOverWidth) * Math.Exp(-(jSquared / (2 * widthSquared)));
                w += weight;
                vw += prevV * weight;
            }

            output[i] = w != 0 ? vw / w : 0;
        }
    }

    /// <summary>
    /// Computes Hampel Filter using span-based computation.
    /// </summary>
    internal static void HampelFilter(ReadOnlySpan<double> input, Span<double> output, int length = 14, double scalingFactor = 3)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var windowBuffer = ArrayPool<double>.Shared.Rent(length);
        try
        {
            for (var i = 0; i < input.Length; i++)
            {
                var n = Math.Min(i + 1, length);
                for (var j = 0; j < n; j++)
                {
                    windowBuffer[j] = input[i - j];
                }

                Array.Sort(windowBuffer, 0, n);
                var median = InterpolateQuartile(windowBuffer, n, 0.5);

                // Calculate MAD (Median Absolute Deviation)
                for (var j = 0; j < n; j++)
                {
                    windowBuffer[j] = Math.Abs(input[i - j] - median);
                }
                Array.Sort(windowBuffer, 0, n);
                var mad = InterpolateQuartile(windowBuffer, n, 0.5);

                // Scale factor * MAD
                var threshold = scalingFactor * 1.4826 * mad;
                var currentValue = input[i];

                output[i] = Math.Abs(currentValue - median) > threshold ? median : currentValue;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(windowBuffer);
        }
    }

    /// <summary>
    /// Computes Sequentially Filtered Moving Average using span-based computation.
    /// </summary>
    internal static void SequentiallyFilteredMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 50)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var ema1Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        var ema2Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        var ema3Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var ema1 = ema1Buffer.AsSpan(0, input.Length);
            var ema2 = ema2Buffer.AsSpan(0, input.Length);
            var ema3 = ema3Buffer.AsSpan(0, input.Length);

            // Sequential EMA filtering
            ExponentialMovingAverage(input, ema1, length);
            ExponentialMovingAverage(ema1, ema2, length);
            ExponentialMovingAverage(ema2, ema3, length);

            // Final output: 3*EMA1 - 3*EMA2 + EMA3
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (3 * ema1[i]) - (3 * ema2[i]) + ema3[i];
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(ema1Buffer);
            ArrayPool<double>.Shared.Return(ema2Buffer);
            ArrayPool<double>.Shared.Return(ema3Buffer);
        }
    }

    /// <summary>
    /// Computes Kalman Smoother using span-based computation.
    /// </summary>
    internal static void KalmanSmoother(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var sharpness = Math.Max(1, length);
        var k = 1.0 / sharpness;
        double velocity = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevKf = i >= 1 ? output[i - 1] : currentValue;

            var distance = currentValue - prevKf;
            velocity += distance * k * k;
            var kf = prevKf + velocity;
            velocity *= (1 - k);

            output[i] = kf;
        }
    }

    /// <summary>
    /// Computes Modular Filter using span-based computation.
    /// </summary>
    internal static void ModularFilter(ReadOnlySpan<double> input, Span<double> output, int length = 200, double beta = 0.8, double z = 0.5)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var alpha = 2.0 / (length + 1);
        double b = 0, c = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevMf = i >= 1 ? output[i - 1] : currentValue;

            b = currentValue > (alpha * currentValue) + ((1 - alpha) * prevMf) ?
                currentValue : (alpha * currentValue) + ((1 - alpha) * prevMf);
            c = currentValue < (alpha * currentValue) + ((1 - alpha) * prevMf) ?
                currentValue : (alpha * currentValue) + ((1 - alpha) * prevMf);

            var os = (((1 - beta) * currentValue) + (beta * prevMf) - prevMf) * (1 - alpha) + prevMf;
            var mf = (z * os) + ((1 - z) * (((b - c) > 0 ? b : c)));
            output[i] = mf;
        }
    }

    /// <summary>
    /// Computes Retention Acceleration Filter using span-based computation.
    /// </summary>
    internal static void RetentionAccelerationFilter(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var alpha = 2.0 / (length + 1);

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevRaf = i >= 1 ? output[i - 1] : currentValue;

            var prevValue = i >= 1 ? input[i - 1] : 0;
            var prevValue2 = i >= 2 ? input[i - 2] : 0;

            var acceleration = currentValue - (2 * prevValue) + prevValue2;
            var retentionFactor = Math.Abs(currentValue - prevRaf);

            var raf = prevRaf + (alpha * (currentValue - prevRaf + acceleration * retentionFactor));
            output[i] = raf;
        }
    }

    /// <summary>
    /// Computes Setting Less Trend Step Filtering using span-based computation.
    /// </summary>
    internal static void SettingLessTrendStepFiltering(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        // Compute rolling stddev for ATR-like step
        var atrBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            ComputeRollingStdDev(input, atrBuffer.AsSpan(0, input.Length), length);

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var atr = atrBuffer[i];
                var prevSltsf = i >= 1 ? output[i - 1] : currentValue;

                var step = atr * 0.5; // Configurable step factor
                double sltsf;
                if (currentValue > prevSltsf + step)
                    sltsf = currentValue - step;
                else if (currentValue < prevSltsf - step)
                    sltsf = currentValue + step;
                else
                    sltsf = prevSltsf;

                output[i] = sltsf;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(atrBuffer);
        }
    }

    /// <summary>
    /// Computes Shapeshifting Moving Average using span-based computation.
    /// </summary>
    internal static void ShapeshiftingMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 50, double factor = 0.5)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var alpha = 2.0 / (length + 1);

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevSsma = i >= 1 ? output[i - 1] : currentValue;

            // Adaptive factor based on volatility
            var change = Math.Abs(currentValue - prevSsma);
            var adaptiveFactor = factor * (1 + change / (Math.Abs(prevSsma) + 0.00001));

            var ssma = (alpha * adaptiveFactor * currentValue) + ((1 - alpha * adaptiveFactor) * prevSsma);
            output[i] = ssma;
        }
    }

    /// <summary>
    /// Computes Variable Length Moving Average using span-based computation.
    /// </summary>
    internal static void VariableLengthMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var maxLength = length * 2;
        var stdDevBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            ComputeRollingStdDev(input, stdDevBuffer.AsSpan(0, input.Length), length);

            // Get max stdDev for normalization
            double maxStdDev = 0;
            for (var i = 0; i < input.Length; i++)
            {
                if (stdDevBuffer[i] > maxStdDev) maxStdDev = stdDevBuffer[i];
            }

            for (var i = 0; i < input.Length; i++)
            {
                // Variable length based on volatility
                var volatilityRatio = maxStdDev > 0 ? stdDevBuffer[i] / maxStdDev : 0;
                var varLength = (int)Math.Max(2, length + (volatilityRatio * (maxLength - length)));

                double sum = 0;
                var n = Math.Min(i + 1, varLength);
                for (var j = 0; j < n; j++)
                {
                    sum += input[i - j];
                }

                output[i] = sum / n;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(stdDevBuffer);
        }
    }

    /// <summary>
    /// Computes Ehlers Gaussian Filter using span-based computation.
    /// </summary>
    internal static void EhlersGaussianFilter(ReadOnlySpan<double> input, Span<double> output, int length = 14, int poles = 3)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        poles = Math.Min(Math.Max(poles, 1), 4);
        var beta = (1 - Math.Cos(2 * Math.PI / length)) / (Math.Pow(2, 1.0 / poles) - 1);
        var alpha = -beta + Math.Sqrt(beta * beta + 2 * beta);

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prev1 = i >= 1 ? output[i - 1] : currentValue;
            var prev2 = i >= 2 ? output[i - 2] : currentValue;
            var prev3 = i >= 3 ? output[i - 3] : currentValue;
            var prev4 = i >= 4 ? output[i - 4] : currentValue;

            double result;
            if (poles == 1)
            {
                result = alpha * currentValue + (1 - alpha) * prev1;
            }
            else if (poles == 2)
            {
                var c0 = alpha * alpha;
                var c1 = 2 * (1 - alpha);
                var c2 = -(1 - alpha) * (1 - alpha);
                result = c0 * currentValue + c1 * prev1 + c2 * prev2;
            }
            else if (poles == 3)
            {
                var c0 = alpha * alpha * alpha;
                var c1 = 3 * (1 - alpha);
                var c2 = -3 * (1 - alpha) * (1 - alpha);
                var c3 = (1 - alpha) * (1 - alpha) * (1 - alpha);
                result = c0 * currentValue + c1 * prev1 + c2 * prev2 + c3 * prev3;
            }
            else
            {
                var a1 = 1 - alpha;
                var c0 = alpha * alpha * alpha * alpha;
                var c1 = 4 * a1;
                var c2 = -6 * a1 * a1;
                var c3 = 4 * a1 * a1 * a1;
                var c4 = -a1 * a1 * a1 * a1;
                result = c0 * currentValue + c1 * prev1 + c2 * prev2 + c3 * prev3 + c4 * prev4;
            }

            output[i] = result;
        }
    }

    /// <summary>
    /// Computes Ehlers Recursive Median Filter using span-based computation.
    /// </summary>
    internal static void EhlersRecursiveMedianFilter(ReadOnlySpan<double> input, Span<double> output, int length = 5, double alpha = 0.5)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var windowBuffer = ArrayPool<double>.Shared.Rent(length);
        try
        {
            for (var i = 0; i < input.Length; i++)
            {
                var n = Math.Min(i + 1, length);
                for (var j = 0; j < n; j++)
                {
                    windowBuffer[j] = input[i - j];
                }

                Array.Sort(windowBuffer, 0, n);
                var median = InterpolateQuartile(windowBuffer, n, 0.5);

                var prevRmf = i >= 1 ? output[i - 1] : input[i];
                output[i] = (alpha * median) + ((1 - alpha) * prevRmf);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(windowBuffer);
        }
    }

    /// <summary>
    /// Computes 1LC Least Squares Moving Average using span-based computation.
    /// </summary>
    internal static void OneLCLeastSquaresMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 32)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        // Using linear least squares fit
        var lsmaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            LeastSquaresMovingAverage(input, lsmaBuffer.AsSpan(0, input.Length), length);

            // Apply 1LC correction: 2*LSMA - SMA
            var smaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
            try
            {
                SimpleMovingAverage(input, smaBuffer.AsSpan(0, input.Length), length);

                for (var i = 0; i < input.Length; i++)
                {
                    output[i] = (2 * lsmaBuffer[i]) - smaBuffer[i];
                }
            }
            finally
            {
                ArrayPool<double>.Shared.Return(smaBuffer);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(lsmaBuffer);
        }
    }

    /// <summary>
    /// Computes Ehlers Deviation Scaled Super Smoother using span-based computation.
    /// </summary>
    internal static void EhlersDeviationScaledSuperSmoother(ReadOnlySpan<double> input, Span<double> output, int length = 20, int poles = 2)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        // First compute deviation scaling
        var stdDevBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var ssBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            ComputeRollingStdDev(input, stdDevBuffer.AsSpan(0, input.Length), length);

            // Apply super smoother
            var a1 = Math.Exp(-1.414 * Math.PI / length);
            var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / length);
            var coef2 = b1;
            var coef3 = -a1 * a1;
            var coef1 = 1 - coef2 - coef3;

            for (var i = 0; i < input.Length; i++)
            {
                var prev1 = i >= 1 ? ssBuffer[i - 1] : input[i];
                var prev2 = i >= 2 ? ssBuffer[i - 2] : input[i];

                // Scale by deviation
                var scaling = stdDevBuffer[i] > 0 ? 1.0 / stdDevBuffer[i] : 1;
                var scaledInput = input[i] * Math.Min(scaling, 10);

                ssBuffer[i] = (coef1 * scaledInput) + (coef2 * prev1) + (coef3 * prev2);
            }

            // Copy result
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = ssBuffer[i];
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(stdDevBuffer);
            ArrayPool<double>.Shared.Return(ssBuffer);
        }
    }

    /// <summary>
    /// Computes Ehlers Optimum Elliptic Filter using span-based computation.
    /// </summary>
    internal static void EhlersOptimumEllipticFilter(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var a1 = 0.13785;
        var a2 = 0.0007;
        var b1 = 1.2075;
        var b2 = -0.5587;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prev1 = i >= 1 ? input[i - 1] : currentValue;
            var prev2 = i >= 2 ? input[i - 2] : currentValue;
            var prevEf1 = i >= 1 ? output[i - 1] : currentValue;
            var prevEf2 = i >= 2 ? output[i - 2] : currentValue;

            output[i] = (a1 * (currentValue + prev1)) + (a2 * prev2) + (b1 * prevEf1) + (b2 * prevEf2);
        }
    }

    /// <summary>
    /// Computes Ehlers Modified Optimum Elliptic Filter using span-based computation.
    /// </summary>
    internal static void EhlersModifiedOptimumEllipticFilter(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevValue1 = i >= 1 ? input[i - 1] : currentValue;
            var prevValue2 = i >= 2 ? input[i - 2] : prevValue1;
            var prevValue3 = i >= 3 ? input[i - 3] : prevValue2;
            var prevMoef1 = i >= 1 ? output[i - 1] : currentValue;
            var prevMoef2 = i >= 2 ? output[i - 2] : prevMoef1;

            output[i] = (0.13785 * ((2 * currentValue) - prevValue1)) + (0.0007 * ((2 * prevValue1) - prevValue2)) +
                (0.13785 * ((2 * prevValue2) - prevValue3)) + (1.2103 * prevMoef1) - (0.4867 * prevMoef2);
        }
    }

    /// <summary>
    /// Computes Ehlers Chebyshev Low Pass Filter using span-based computation.
    /// </summary>
    internal static void EhlersChebyshevLowPassFilter(ReadOnlySpan<double> input, Span<double> output, int length = 14, double ripple = 0.5)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        // Chebyshev Type I filter coefficients
        var beta = 1.0 / (1.0 + Math.Tan(Math.PI / length));
        var alpha = (1.0 - beta) / 2.0;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prev1 = i >= 1 ? input[i - 1] : currentValue;
            var prevCf1 = i >= 1 ? output[i - 1] : currentValue;

            output[i] = (alpha * currentValue) + (alpha * prev1) + (beta * prevCf1);
        }
    }

    /// <summary>
    /// Computes Ehlers Average Error Filter using span-based computation.
    /// </summary>
    internal static void EhlersAverageErrorFilter(ReadOnlySpan<double> input, Span<double> output, int length = 27)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var emaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            ExponentialMovingAverage(input, emaBuffer.AsSpan(0, input.Length), length);

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var ema = emaBuffer[i];
                var error = currentValue - ema;
                var prevAef = i >= 1 ? output[i - 1] : currentValue;

                // Average error correction
                var errorAvg = 0.0;
                var n = Math.Min(i + 1, length);
                for (var j = 0; j < n; j++)
                {
                    errorAvg += Math.Abs(input[i - j] - emaBuffer[i - j]);
                }
                errorAvg /= n;

                var aef = ema + (errorAvg > 0 ? error / errorAvg * (currentValue - prevAef) : 0);
                output[i] = double.IsNaN(aef) || double.IsInfinity(aef) ? currentValue : aef;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(emaBuffer);
        }
    }

    /// <summary>
    /// Computes Ehlers All Pass Phase Shifter using span-based computation.
    /// </summary>
    internal static void EhlersAllPassPhaseShifter(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var a = (double)(length - 1) / (length + 1);

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prev1 = i >= 1 ? input[i - 1] : currentValue;
            var prev2 = i >= 2 ? input[i - 2] : currentValue;
            var prevAp1 = i >= 1 ? output[i - 1] : currentValue;
            var prevAp2 = i >= 2 ? output[i - 2] : currentValue;

            output[i] = ((a * a) * (currentValue - prev2)) + (a * a * prevAp2) - (2 * a * prevAp1) + prev2;
        }
    }

    /// <summary>
    /// Computes Polynomial Least Squares Moving Average using span-based computation.
    /// </summary>
    internal static void PolynomialLeastSquaresMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 20, int degree = 2)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        // For polynomial regression, use simple approximation
        // Higher degree approximated with weighted moving averages
        var lsmaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var smaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            LeastSquaresMovingAverage(input, lsmaBuffer.AsSpan(0, input.Length), length);
            SimpleMovingAverage(input, smaBuffer.AsSpan(0, input.Length), length);

            for (var i = 0; i < input.Length; i++)
            {
                // Polynomial approximation: LSMA + degree * (LSMA - SMA)
                var lsma = lsmaBuffer[i];
                var sma = smaBuffer[i];
                output[i] = lsma + (degree * (lsma - sma));
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(lsmaBuffer);
            ArrayPool<double>.Shared.Return(smaBuffer);
        }
    }

    /// <summary>
    /// Computes Quadratic Least Squares Moving Average using span-based computation.
    /// </summary>
    internal static void QuadraticLeastSquaresMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 50)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        // Using LSMA with quadratic correction
        PolynomialLeastSquaresMovingAverage(input, output, length, 2);
    }

    /// <summary>
    /// Computes Quadratic Regression using span-based computation.
    /// </summary>
    internal static void QuadraticRegression(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        for (var i = 0; i < input.Length; i++)
        {
            var n = Math.Min(i + 1, length);

            // Compute sums for quadratic regression
            double sumX = 0, sumX2 = 0, sumX3 = 0, sumX4 = 0;
            double sumY = 0, sumXY = 0, sumX2Y = 0;

            for (var j = 0; j < n; j++)
            {
                var x = (double)j;
                var y = input[i - j];

                sumX += x;
                sumX2 += x * x;
                sumX3 += x * x * x;
                sumX4 += x * x * x * x;
                sumY += y;
                sumXY += x * y;
                sumX2Y += x * x * y;
            }

            // Simplified quadratic regression using normal equations
            // For simplicity, fall back to linear regression approach
            var avgX = sumX / n;
            var avgY = sumY / n;
            var avgXY = sumXY / n;
            var avgX2 = sumX2 / n;

            var slope = avgX2 != avgX * avgX ? (avgXY - avgX * avgY) / (avgX2 - avgX * avgX) : 0;
            var intercept = avgY - slope * avgX;

            output[i] = intercept; // Value at x=0 (current)
        }
    }

    /// <summary>
    /// Computes Following Adaptive Moving Average using span-based computation.
    /// </summary>
    internal static void FollowingAdaptiveMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var alpha = 2.0 / (length + 1);

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevFama = i >= 1 ? output[i - 1] : currentValue;

            // Adaptive factor based on price direction
            var direction = currentValue > prevFama ? 1.0 : currentValue < prevFama ? -1.0 : 0.0;
            var followFactor = 1 + Math.Abs(currentValue - prevFama) / (Math.Abs(prevFama) + 0.00001);

            output[i] = prevFama + (alpha * followFactor * (currentValue - prevFama));
        }
    }

    /// <summary>
    /// Computes Variable Adaptive Moving Average using span-based computation.
    /// </summary>
    internal static void VariableAdaptiveMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var stdDevBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            ComputeRollingStdDev(input, stdDevBuffer.AsSpan(0, input.Length), length);

            double maxStdDev = 0;
            for (var i = 0; i < input.Length; i++)
            {
                if (stdDevBuffer[i] > maxStdDev) maxStdDev = stdDevBuffer[i];
            }

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var prevVama = i >= 1 ? output[i - 1] : currentValue;

                var volatilityRatio = maxStdDev > 0 ? stdDevBuffer[i] / maxStdDev : 0;
                var fastAlpha = 2.0 / (Math.Max(2, length / 2) + 1);
                var slowAlpha = 2.0 / (length * 2 + 1);
                var adaptiveAlpha = slowAlpha + (volatilityRatio * (fastAlpha - slowAlpha));

                output[i] = prevVama + (adaptiveAlpha * (currentValue - prevVama));
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(stdDevBuffer);
        }
    }

    /// <summary>
    /// Computes Vertical Horizontal Moving Average using span-based computation.
    /// </summary>
    internal static void VerticalHorizontalMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevVhma = i >= 1 ? output[i - 1] : currentValue;

            var n = Math.Min(i + 1, length);

            // Calculate VHF (Vertical Horizontal Filter)
            double highest = double.MinValue, lowest = double.MaxValue;
            double changeSum = 0;
            for (var j = 0; j < n; j++)
            {
                var val = input[i - j];
                if (val > highest) highest = val;
                if (val < lowest) lowest = val;
                if (j > 0)
                {
                    changeSum += Math.Abs(input[i - j] - input[i - j + 1]);
                }
            }

            var range = highest - lowest;
            var vhf = changeSum > 0 && n > 1 ? range / changeSum : 0;

            // Adaptive EMA based on VHF
            var alpha = Math.Min(Math.Max(vhf, 0.01), 0.99);
            output[i] = prevVhma + (alpha * (currentValue - prevVhma));
        }
    }

    /// <summary>
    /// Computes Edge Preserving Filter using span-based computation.
    /// </summary>
    internal static void EdgePreservingFilter(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var stdDevBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            ComputeRollingStdDev(input, stdDevBuffer.AsSpan(0, input.Length), length);

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var prevEpf = i >= 1 ? output[i - 1] : currentValue;
                var stdDev = stdDevBuffer[i];

                // Edge detection based on deviation from previous value
                var edge = Math.Abs(currentValue - prevEpf);
                var threshold = 2 * stdDev;

                if (edge > threshold)
                {
                    // Preserve edge - use current value directly
                    output[i] = currentValue;
                }
                else
                {
                    // Smooth
                    var alpha = 2.0 / (length + 1);
                    output[i] = prevEpf + (alpha * (currentValue - prevEpf));
                }
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(stdDevBuffer);
        }
    }

    /// <summary>
    /// Computes Auto Filter using span-based computation.
    /// </summary>
    internal static void AutoFilter(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var stdDevBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            ComputeRollingStdDev(input, stdDevBuffer.AsSpan(0, input.Length), length);

            // Calculate average stddev for normalization
            double avgStdDev = 0;
            for (var i = 0; i < input.Length; i++)
            {
                avgStdDev += stdDevBuffer[i];
            }
            avgStdDev /= input.Length;

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var prevAf = i >= 1 ? output[i - 1] : currentValue;

                // Auto-adjust alpha based on current volatility vs average
                var volatilityRatio = avgStdDev > 0 ? stdDevBuffer[i] / avgStdDev : 1;
                var alpha = Math.Min(Math.Max(2.0 / (length + 1) * volatilityRatio, 0.01), 0.99);

                output[i] = prevAf + (alpha * (currentValue - prevAf));
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(stdDevBuffer);
        }
    }

    /// <summary>
    /// Computes Falling Rising Filter using span-based computation.
    /// </summary>
    internal static void FallingRisingFilter(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var alpha = 2.0 / (length + 1);

        // Track min/max over window for trend detection
        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevFrf = i >= 1 ? output[i - 1] : currentValue;

            var n = Math.Min(i + 1, length);
            double highest = double.MinValue, lowest = double.MaxValue;
            for (var j = 0; j < n; j++)
            {
                var val = i >= j ? input[i - j] : 0;
                if (val > highest) highest = val;
                if (val < lowest) lowest = val;
            }

            var range = highest - lowest;
            var error = currentValue - prevFrf;

            // Adjust alpha based on whether we're rising or falling
            var adjustedAlpha = range > 0 ? alpha * (1 + Math.Abs(error) / range) : alpha;
            adjustedAlpha = Math.Min(adjustedAlpha, 0.99);

            output[i] = prevFrf + (adjustedAlpha * error);
        }
    }

    /// <summary>
    /// Computes Hybrid Convolution Filter using span-based computation.
    /// </summary>
    internal static void HybridConvolutionFilter(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        // Combine EMA and WMA for hybrid filtering
        var emaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var wmaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            ExponentialMovingAverage(input, emaBuffer.AsSpan(0, input.Length), length);
            WeightedMovingAverage(input, wmaBuffer.AsSpan(0, input.Length), length);

            for (var i = 0; i < input.Length; i++)
            {
                // Hybrid: adaptive blend based on volatility
                var currentValue = input[i];
                var emaVal = emaBuffer[i];
                var wmaVal = wmaBuffer[i];

                var emaError = Math.Abs(currentValue - emaVal);
                var wmaError = Math.Abs(currentValue - wmaVal);
                var totalError = emaError + wmaError;

                // Weight towards the filter with less error
                var emaWeight = totalError > 0 ? wmaError / totalError : 0.5;
                output[i] = (emaWeight * emaVal) + ((1 - emaWeight) * wmaVal);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(emaBuffer);
            ArrayPool<double>.Shared.Return(wmaBuffer);
        }
    }

    /// <summary>
    /// Computes IIR Least Squares Estimate using span-based computation.
    /// </summary>
    internal static void IIRLeastSquaresEstimate(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        // IIR filter with least squares fitting
        var lsmaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            LeastSquaresMovingAverage(input, lsmaBuffer.AsSpan(0, input.Length), length);

            var alpha = 2.0 / (length + 1);
            for (var i = 0; i < input.Length; i++)
            {
                var prevIir = i >= 1 ? output[i - 1] : lsmaBuffer[i];
                output[i] = (alpha * lsmaBuffer[i]) + ((1 - alpha) * prevIir);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(lsmaBuffer);
        }
    }

    /// <summary>
    /// Computes General Filter Estimator using span-based computation.
    /// </summary>
    internal static void GeneralFilterEstimator(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        // General purpose adaptive filter
        var smaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var stdDevBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            SimpleMovingAverage(input, smaBuffer.AsSpan(0, input.Length), length);
            ComputeRollingStdDev(input, stdDevBuffer.AsSpan(0, input.Length), length);

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var sma = smaBuffer[i];
                var stdDev = stdDevBuffer[i];

                // Adaptive estimation with bounded adjustment
                var deviation = currentValue - sma;
                var normalizedDev = stdDev > 0 ? deviation / stdDev : 0;
                normalizedDev = Math.Max(-3, Math.Min(3, normalizedDev)); // Clamp to 3 stddev

                output[i] = sma + (normalizedDev * stdDev * 0.5);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(smaBuffer);
            ArrayPool<double>.Shared.Return(stdDevBuffer);
        }
    }

    /// <summary>
    /// Computes Moving Average V3 using span-based computation.
    /// </summary>
    internal static void MovingAverageV3(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        // Triple smoothed average
        var ema1Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        var ema2Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        var ema3Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var halfLength = Math.Max(2, length / 2);
            var quarterLength = Math.Max(2, length / 4);

            ExponentialMovingAverage(input, ema1Buffer.AsSpan(0, input.Length), length);
            ExponentialMovingAverage(ema1Buffer.AsSpan(0, input.Length), ema2Buffer.AsSpan(0, input.Length), halfLength);
            ExponentialMovingAverage(ema2Buffer.AsSpan(0, input.Length), ema3Buffer.AsSpan(0, input.Length), quarterLength);

            for (var i = 0; i < input.Length; i++)
            {
                // Weighted combination
                output[i] = (3 * ema1Buffer[i]) - (3 * ema2Buffer[i]) + ema3Buffer[i];
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(ema1Buffer);
            ArrayPool<double>.Shared.Return(ema2Buffer);
            ArrayPool<double>.Shared.Return(ema3Buffer);
        }
    }

    /// <summary>
    /// Computes Moving Average Adaptive Q using span-based computation.
    /// </summary>
    internal static void MovingAverageAdaptiveQ(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var stdDevBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            ComputeRollingStdDev(input, stdDevBuffer.AsSpan(0, input.Length), length);

            double maxStdDev = 0;
            for (var i = 0; i < input.Length; i++)
            {
                if (stdDevBuffer[i] > maxStdDev) maxStdDev = stdDevBuffer[i];
            }

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var prevMaaq = i >= 1 ? output[i - 1] : currentValue;

                // Q-factor based on volatility
                var q = maxStdDev > 0 ? Math.Max(0.01, 1 - stdDevBuffer[i] / maxStdDev) : 0.5;
                var alpha = 2.0 / (length + 1) * q;

                output[i] = prevMaaq + (alpha * (currentValue - prevMaaq));
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(stdDevBuffer);
        }
    }

    /// <summary>
    /// Computes T-Step Least Squares Moving Average using span-based computation.
    /// </summary>
    internal static void TStepLeastSquaresMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14, int tStep = 3)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var lsmaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            LeastSquaresMovingAverage(input, lsmaBuffer.AsSpan(0, input.Length), length);

            for (var i = 0; i < input.Length; i++)
            {
                // Apply T-step ahead projection
                var currentLsma = lsmaBuffer[i];
                var prevLsma = i >= tStep ? lsmaBuffer[i - tStep] : currentLsma;
                var slope = (currentLsma - prevLsma) / tStep;

                output[i] = currentLsma + (slope * tStep);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(lsmaBuffer);
        }
    }

    /// <summary>
    /// Computes Parametric Corrective Linear Moving Average using span-based computation.
    /// </summary>
    internal static void ParametricCorrectiveLinearMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var lsmaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var emaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            LeastSquaresMovingAverage(input, lsmaBuffer.AsSpan(0, input.Length), length);
            ExponentialMovingAverage(input, emaBuffer.AsSpan(0, input.Length), length);

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var lsma = lsmaBuffer[i];
                var ema = emaBuffer[i];

                // Parametric correction based on deviation
                var error = currentValue - lsma;
                var emaError = currentValue - ema;

                // Blend LSMA with correction factor
                var correction = error != 0 ? Math.Min(Math.Abs(emaError / error), 2) : 1;
                output[i] = lsma + (error * correction * 0.5);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(lsmaBuffer);
            ArrayPool<double>.Shared.Return(emaBuffer);
        }
    }

    /// <summary>
    /// Computes Parametric Kalman Filter using span-based computation.
    /// </summary>
    internal static void ParametricKalmanFilter(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var stdDevBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            ComputeRollingStdDev(input, stdDevBuffer.AsSpan(0, input.Length), length);

            double p = 1.0; // Error covariance estimate
            double q = 0.01; // Process noise
            double r = 0.1; // Measurement noise

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var prevEstimate = i >= 1 ? output[i - 1] : currentValue;

                // Adaptive measurement noise based on volatility
                var adaptiveR = stdDevBuffer[i] > 0 ? r * stdDevBuffer[i] : r;

                // Predict
                p = p + q;

                // Update (Kalman gain)
                var k = p / (p + adaptiveR);
                var estimate = prevEstimate + k * (currentValue - prevEstimate);
                p = (1 - k) * p;

                output[i] = estimate;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(stdDevBuffer);
        }
    }

    /// <summary>
    /// Computes R2 Adaptive Regression using span-based computation.
    /// </summary>
    internal static void R2AdaptiveRegression(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var lsmaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            LeastSquaresMovingAverage(input, lsmaBuffer.AsSpan(0, input.Length), length);

            for (var i = 0; i < input.Length; i++)
            {
                var n = Math.Min(i + 1, length);

                // Calculate R-squared
                double sumResiduals = 0, sumTotal = 0;
                double mean = 0;
                for (var j = 0; j < n; j++)
                {
                    mean += input[i - j];
                }
                mean /= n;

                for (var j = 0; j < n; j++)
                {
                    var val = input[i - j];
                    var predicted = lsmaBuffer[i - j];
                    sumResiduals += (val - predicted) * (val - predicted);
                    sumTotal += (val - mean) * (val - mean);
                }

                var r2 = sumTotal > 0 ? 1 - (sumResiduals / sumTotal) : 0;
                r2 = Math.Max(0, Math.Min(1, r2));

                // Blend LSMA with current value based on R2
                var currentValue = input[i];
                var lsma = lsmaBuffer[i];
                output[i] = (r2 * lsma) + ((1 - r2) * currentValue);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(lsmaBuffer);
        }
    }

    /// <summary>
    /// Computes Svama (Simple Variable Adaptive Moving Average) using span-based computation.
    /// </summary>
    internal static void Svama(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        VariableAdaptiveMovingAverage(input, output, length);
    }

    /// <summary>
    /// Computes Volatility Moving Average using span-based computation.
    /// </summary>
    internal static void VolatilityMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var stdDevBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var smaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            SimpleMovingAverage(input, smaBuffer.AsSpan(0, input.Length), length);
            ComputeRollingStdDev(input, stdDevBuffer.AsSpan(0, input.Length), length);

            for (var i = 0; i < input.Length; i++)
            {
                // Volatility-weighted SMA
                var sma = smaBuffer[i];
                var stdDev = stdDevBuffer[i];
                var currentValue = input[i];

                // Adjust based on current deviation from mean
                var deviation = currentValue - sma;
                var normalizedDev = stdDev > 0 ? deviation / stdDev : 0;
                normalizedDev = Math.Max(-2, Math.Min(2, normalizedDev));

                output[i] = sma + (normalizedDev * stdDev * 0.5);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(stdDevBuffer);
            ArrayPool<double>.Shared.Return(smaBuffer);
        }
    }

    /// <summary>
    /// Computes Volatility Wave Moving Average using span-based computation.
    /// </summary>
    internal static void VolatilityWaveMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var stdDevBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            ComputeRollingStdDev(input, stdDevBuffer.AsSpan(0, input.Length), length);

            double avgStdDev = 0;
            for (var i = 0; i < input.Length; i++)
            {
                avgStdDev += stdDevBuffer[i];
            }
            avgStdDev /= input.Length;

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var prevVwma = i >= 1 ? output[i - 1] : currentValue;

                // Wave-like adaptive response
                var volatilityRatio = avgStdDev > 0 ? stdDevBuffer[i] / avgStdDev : 1;
                var wave = Math.Sin(volatilityRatio * Math.PI / 2);
                var alpha = 2.0 / (length + 1) * (1 + wave * 0.5);
                alpha = Math.Min(Math.Max(alpha, 0.01), 0.99);

                output[i] = prevVwma + (alpha * (currentValue - prevVwma));
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(stdDevBuffer);
        }
    }

    /// <summary>
    /// Computes Powered Kaufman Adaptive Moving Average using span-based computation.
    /// </summary>
    internal static void PoweredKaufmanAdaptiveMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14, double power = 2)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var kamaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            KaufmanAdaptiveMovingAverage(input, kamaBuffer.AsSpan(0, input.Length), length);

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var kama = kamaBuffer[i];
                var prevPkama = i >= 1 ? output[i - 1] : currentValue;

                // Apply power to the adaptive factor
                var diff = currentValue - prevPkama;
                var adaptiveFactor = kama != 0 ? Math.Pow(Math.Abs(currentValue - kama) / Math.Abs(kama), 1.0 / power) : 0;
                adaptiveFactor = Math.Min(adaptiveFactor, 1);

                var alpha = 2.0 / (length + 1) * (1 + adaptiveFactor);
                alpha = Math.Min(alpha, 0.99);

                output[i] = prevPkama + (alpha * diff);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(kamaBuffer);
        }
    }

    /// <summary>
    /// Computes Ehlers Leading Indicator using span-based computation.
    /// </summary>
    internal static void EhlersLeadingIndicator(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var emaBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        var ema2Buffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            ExponentialMovingAverage(input, emaBuffer.AsSpan(0, input.Length), length);
            ExponentialMovingAverage(emaBuffer.AsSpan(0, input.Length), ema2Buffer.AsSpan(0, input.Length), length);

            for (var i = 0; i < input.Length; i++)
            {
                // Leading indicator: 2*EMA - EMA of EMA
                output[i] = (2 * emaBuffer[i]) - ema2Buffer[i];
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(emaBuffer);
            ArrayPool<double>.Shared.Return(ema2Buffer);
        }
    }

    /// <summary>
    /// Computes Ehlers Median Average Adaptive Filter using span-based computation.
    /// </summary>
    internal static void EhlersMedianAverageAdaptiveFilter(ReadOnlySpan<double> input, Span<double> output, int length = 39, double threshold = 0.002)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var windowBuffer = ArrayPool<double>.Shared.Rent(length);
        try
        {
            for (var i = 0; i < input.Length; i++)
            {
                var n = Math.Min(i + 1, length);

                // Copy window values
                for (var j = 0; j < n; j++)
                {
                    windowBuffer[j] = input[i - j];
                }

                // Sort and get median
                Array.Sort(windowBuffer, 0, n);
                var median = InterpolateQuartile(windowBuffer, n, 0.5);

                var prevMaaf = i >= 1 ? output[i - 1] : median;

                // Adaptive factor based on deviation from median
                var deviation = Math.Abs(input[i] - median);
                var adaptiveFactor = deviation > threshold ? 1.0 : deviation / threshold;

                output[i] = prevMaaf + (adaptiveFactor * (median - prevMaaf));
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(windowBuffer);
        }
    }

    /// <summary>
    /// Computes Ehlers Distance Coefficient Filter using span-based computation.
    /// </summary>
    internal static void EhlersDistanceCoefficientFilter(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        for (var i = 0; i < input.Length; i++)
        {
            var n = Math.Min(i + 1, length);

            // Distance coefficient weighting
            double sumWeights = 0, sumWeighted = 0;
            for (var j = 0; j < n; j++)
            {
                var value = input[i - j];

                // Calculate distance from current value
                double distanceSum = 0;
                for (var k = 0; k < n; k++)
                {
                    if (k != j)
                    {
                        distanceSum += Math.Abs(value - input[i - k]);
                    }
                }

                var weight = distanceSum > 0 ? 1.0 / distanceSum : 1;
                sumWeights += weight;
                sumWeighted += weight * value;
            }

            output[i] = sumWeights > 0 ? sumWeighted / sumWeights : input[i];
        }
    }

    /// <summary>
    /// Computes Ehlers Noise Elimination Technology using span-based computation.
    /// </summary>
    internal static void EhlersNoiseEliminationTechnology(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var alpha = 2.0 / (length + 1);

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevNet = i >= 1 ? output[i - 1] : currentValue;

            // Noise elimination through adaptive smoothing
            var change = Math.Abs(currentValue - prevNet);
            var prevChange = i >= 1 ? Math.Abs(input[i - 1] - (i >= 2 ? output[i - 2] : input[i - 1])) : 0;

            // Reduce alpha when changes are small (noise)
            var noiseRatio = prevChange > 0 ? Math.Min(change / prevChange, 2) : 1;
            var adaptiveAlpha = alpha * Math.Min(noiseRatio, 1);

            output[i] = prevNet + (adaptiveAlpha * (currentValue - prevNet));
        }
    }

    /// <summary>
    /// Computes Bryant Adaptive Moving Average using span-based computation.
    /// </summary>
    internal static void BryantAdaptiveMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var stdDevBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            ComputeRollingStdDev(input, stdDevBuffer.AsSpan(0, input.Length), length);

            double maxStdDev = 0;
            for (var i = 0; i < input.Length; i++)
            {
                if (stdDevBuffer[i] > maxStdDev) maxStdDev = stdDevBuffer[i];
            }

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var prevBama = i >= 1 ? output[i - 1] : currentValue;

                // Bryant's adaptive factor
                var volatilityRatio = maxStdDev > 0 ? stdDevBuffer[i] / maxStdDev : 0;
                var fastPeriod = Math.Max(2, length / 4);
                var slowPeriod = length * 2;

                var adaptivePeriod = slowPeriod - (volatilityRatio * (slowPeriod - fastPeriod));
                var alpha = 2.0 / (adaptivePeriod + 1);

                output[i] = prevBama + (alpha * (currentValue - prevBama));
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(stdDevBuffer);
        }
    }

    /// <summary>
    /// Computes Adaptive Autonomous Recursive Moving Average using span-based computation.
    /// </summary>
    internal static void AdaptiveAutonomousRecursiveMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        // Compute efficiency ratio for adaptation
        var stdDevBuffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            ComputeRollingStdDev(input, stdDevBuffer.AsSpan(0, input.Length), length);

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var prevAarma = i >= 1 ? output[i - 1] : currentValue;

                // Calculate efficiency ratio
                var n = Math.Min(i + 1, length);
                var change = i >= n - 1 ? Math.Abs(input[i] - input[i - n + 1]) : 0;

                double volatility = 0;
                for (var j = 1; j < n; j++)
                {
                    volatility += Math.Abs(input[i - j + 1] - input[i - j]);
                }

                var er = volatility > 0 ? change / volatility : 0;

                // Autonomous adaptation
                var fastSc = 2.0 / 3.0;
                var slowSc = 2.0 / 31.0;
                var sc = er * (fastSc - slowSc) + slowSc;
                var alpha = sc * sc;

                output[i] = prevAarma + (alpha * (currentValue - prevAarma));
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(stdDevBuffer);
        }
    }

    /// <summary>
    /// Computes Ehlers Variable Index Dynamic Average using span-based computation.
    /// </summary>
    internal static void EhlersVariableIndexDynamicAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        // Same as VIDYA but with Ehlers's formulation
        Vidya(input, output, length);
    }

    /// <summary>
    /// Computes Ehlers Kaufman Adaptive Moving Average using span-based computation.
    /// </summary>
    internal static void EhlersKaufmanAdaptiveMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        // Ehlers's version of KAMA
        KaufmanAdaptiveMovingAverage(input, output, length);
    }

    /// <summary>
    /// Computes Ehlers MESA Adaptive Moving Average using span-based computation.
    /// </summary>
    internal static void EhlersMesaAdaptiveMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14, double fastLimit = 0.5, double slowLimit = 0.05)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        // MESA adaptive moving average with simplified adaptive EMA approach
        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevMama = i >= 1 ? output[i - 1] : currentValue;

            // Simplified MESA - use standard adaptive EMA
            var n = Math.Min(i + 1, length);
            var change = i >= n - 1 ? Math.Abs(input[i] - input[i - n + 1]) : 0;

            double volatility = 0;
            for (var j = 1; j < n; j++)
            {
                volatility += Math.Abs(input[i - j + 1] - input[i - j]);
            }

            var er = volatility > 0 ? change / volatility : 0;
            var alpha = Math.Max(slowLimit, Math.Min(fastLimit, er * fastLimit));

            output[i] = prevMama + (alpha * (currentValue - prevMama));
        }
    }

    #endregion

    #region Multi-Input MovingAvgType Fast Path Methods

    /// <summary>
    /// Computes Elastic Volume Weighted Moving Average V1.
    /// </summary>
    internal static void ElasticVolumeWeightedMovingAverageV1(ReadOnlySpan<double> price, ReadOnlySpan<double> volume, Span<double> output, int length = 40, double mult = 20)
    {
        if (output.Length < price.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var pool = ArrayPool<double>.Shared;
        var volumeSmaArray = pool.Rent(price.Length);

        try
        {
            var volumeSma = volumeSmaArray.AsSpan(0, price.Length);
            SimpleMovingAverage(volume, volumeSma, length);

            double prevEvwma = price.Length > 0 ? price[0] : 0;
            for (var i = 0; i < price.Length; i++)
            {
                var currentAvgVolume = volumeSma[i];
                var currentVolume = volume[i];
                var n = currentAvgVolume * mult;

                var evwma = n > 0 ? (((n - currentVolume) * prevEvwma) + (currentVolume * price[i])) / n : 0;
                output[i] = evwma;
                prevEvwma = evwma;
            }
        }
        finally
        {
            pool.Return(volumeSmaArray);
        }
    }

    /// <summary>
    /// Computes Elastic Volume Weighted Moving Average V2.
    /// </summary>
    internal static void ElasticVolumeWeightedMovingAverageV2(ReadOnlySpan<double> price, ReadOnlySpan<double> volume, Span<double> output, int length = 14)
    {
        if (output.Length < price.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        double volumeSum = 0;
        double evwma = price.Length > 0 ? price[0] : 0;

        for (var i = 0; i < price.Length; i++)
        {
            var currentVolume = volume[i];
            volumeSum += currentVolume;

            if (i >= length)
                volumeSum -= volume[i - length];

            var nbv = Math.Min(volumeSum, currentVolume);
            evwma = volumeSum > 0 ? (((volumeSum - nbv) * evwma) + (nbv * price[i])) / volumeSum : 0;
            output[i] = evwma;
        }
    }

    /// <summary>
    /// Computes Volume Adjusted Moving Average.
    /// </summary>
    internal static void VolumeAdjustedMovingAverage(ReadOnlySpan<double> price, ReadOnlySpan<double> volume, Span<double> output, int length = 14, double factor = 0.67)
    {
        if (output.Length < price.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var pool = ArrayPool<double>.Shared;
        var volumeSmaArray = pool.Rent(price.Length);

        try
        {
            var volumeSma = volumeSmaArray.AsSpan(0, price.Length);
            SimpleMovingAverage(volume, volumeSma, length);

            double volumeRatioSum = 0;
            double priceVolumeRatioSum = 0;
            var volumeRatioWindow = new double[length];
            var priceVolumeRatioWindow = new double[length];
            var windowIdx = 0;
            var windowCount = 0;

            for (var i = 0; i < price.Length; i++)
            {
                var currentVolume = volume[i];
                var volumeIncrement = volumeSma[i] * factor;
                var volumeRatio = volumeIncrement != 0 ? currentVolume / volumeIncrement : 0;
                var priceVolumeRatio = price[i] * volumeRatio;

                // Update rolling sums
                if (windowCount >= length)
                {
                    volumeRatioSum -= volumeRatioWindow[windowIdx];
                    priceVolumeRatioSum -= priceVolumeRatioWindow[windowIdx];
                }
                volumeRatioWindow[windowIdx] = volumeRatio;
                priceVolumeRatioWindow[windowIdx] = priceVolumeRatio;
                volumeRatioSum += volumeRatio;
                priceVolumeRatioSum += priceVolumeRatio;
                windowIdx = (windowIdx + 1) % length;
                if (windowCount < length) windowCount++;

                output[i] = volumeRatioSum != 0 ? priceVolumeRatioSum / volumeRatioSum : 0;
            }
        }
        finally
        {
            pool.Return(volumeSmaArray);
        }
    }

    /// <summary>
    /// Computes Windowed Volume Weighted Moving Average.
    /// </summary>
    internal static void WindowedVolumeWeightedMovingAverage(ReadOnlySpan<double> price, ReadOnlySpan<double> volume, Span<double> output, int length = 14)
    {
        if (output.Length < price.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        double pvSum = 0;
        double vSum = 0;

        for (var i = 0; i < price.Length; i++)
        {
            var n = Math.Min(i + 1, length);
            var currentPv = price[i] * volume[i];
            pvSum += currentPv;
            vSum += volume[i];

            if (i >= length)
            {
                pvSum -= price[i - length] * volume[i - length];
                vSum -= volume[i - length];
            }

            output[i] = vSum != 0 ? pvSum / vSum : price[i];
        }
    }

    /// <summary>
    /// Computes Middle High Low Moving Average.
    /// </summary>
    internal static void MiddleHighLowMovingAverage(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length1 = 14, int length2 = 10)
    {
        if (output.Length < high.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var pool = ArrayPool<double>.Shared;
        var midpointArray = pool.Rent(high.Length);
        var midpointSmaArray = pool.Rent(high.Length);

        try
        {
            var midpoint = midpointArray.AsSpan(0, high.Length);
            var midpointSma = midpointSmaArray.AsSpan(0, high.Length);

            // Calculate midpoint (high + low) / 2 with rolling min/max
            double highestHigh = double.MinValue;
            double lowestLow = double.MaxValue;
            var highWindow = new double[length2];
            var lowWindow = new double[length2];

            for (var i = 0; i < high.Length; i++)
            {
                highWindow[i % length2] = high[i];
                lowWindow[i % length2] = low[i];

                var windowSize = Math.Min(i + 1, length2);
                highestHigh = double.MinValue;
                lowestLow = double.MaxValue;
                for (var j = 0; j < windowSize; j++)
                {
                    var idx = (i - j + length2) % length2;
                    if (j <= i)
                    {
                        if (highWindow[idx] > highestHigh) highestHigh = highWindow[idx];
                        if (lowWindow[idx] < lowestLow) lowestLow = lowWindow[idx];
                    }
                }
                midpoint[i] = (highestHigh + lowestLow) / 2;
            }

            // Apply EMA to midpoint
            ExponentialMovingAverage(midpoint, output, length1);
        }
        finally
        {
            pool.Return(midpointArray);
            pool.Return(midpointSmaArray);
        }
    }

    /// <summary>
    /// Computes Equity Moving Average.
    /// </summary>
    internal static void EquityMovingAverage(ReadOnlySpan<double> price, ReadOnlySpan<double> volume, Span<double> output, int length = 14)
    {
        if (output.Length < price.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var pool = ArrayPool<double>.Shared;
        var buyPowerArray = pool.Rent(price.Length);
        var totalPowerArray = pool.Rent(price.Length);

        try
        {
            var buyPower = buyPowerArray.AsSpan(0, price.Length);
            var totalPower = totalPowerArray.AsSpan(0, price.Length);

            double buyPowerSum = 0, totalPowerSum = 0;

            for (var i = 0; i < price.Length; i++)
            {
                var currentVolume = volume[i];
                var prevPrice = i >= 1 ? price[i - 1] : price[i];
                var change = price[i] - prevPrice;

                var bp = change > 0 ? currentVolume * change : 0;
                var tp = currentVolume * Math.Abs(change);

                buyPowerSum += bp;
                totalPowerSum += tp;

                if (i >= length)
                {
                    var oldChange = price[i - length + 1] - (i >= length ? price[i - length] : price[i - length + 1]);
                    var oldBp = oldChange > 0 ? volume[i - length + 1] * oldChange : 0;
                    var oldTp = volume[i - length + 1] * Math.Abs(oldChange);
                    buyPowerSum -= oldBp;
                    totalPowerSum -= oldTp;
                }

                var emv = totalPowerSum != 0 ? (2 * buyPowerSum / totalPowerSum) - 1 : 0;
                output[i] = emv;
            }
        }
        finally
        {
            pool.Return(buyPowerArray);
            pool.Return(totalPowerArray);
        }
    }

    /// <summary>
    /// Computes Ratio OCHL Averager.
    /// </summary>
    internal static void RatioOchlAverager(ReadOnlySpan<double> open, ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output)
    {
        if (output.Length < close.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        for (var i = 0; i < close.Length; i++)
        {
            var o = open[i];
            var c = close[i];
            var h = high[i];
            var l = low[i];

            var oc = Math.Abs(o - c);
            var hl = h - l;

            var ratio = hl != 0 ? oc / hl : 0;
            var avg = (o + c + h + l) / 4;

            output[i] = avg * (1 + ratio);
        }
    }

    /// <summary>
    /// Computes Volume Weighted Average Price.
    /// </summary>
    internal static void VolumeWeightedAveragePrice(ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (output.Length < close.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        double tpvSum = 0;
        double volumeSum = 0;

        for (var i = 0; i < close.Length; i++)
        {
            var typicalPrice = (close[i] + high[i] + low[i]) / 3;
            var currentVolume = volume[i];

            tpvSum += typicalPrice * currentVolume;
            volumeSum += currentVolume;

            output[i] = volumeSum != 0 ? tpvSum / volumeSum : 0;
        }
    }

    /// <summary>
    /// Computes True Range Adjusted Exponential Moving Average.
    /// </summary>
    internal static void TrueRangeAdjustedExponentialMovingAverage(ReadOnlySpan<double> price, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 14, double mult = 1.5)
    {
        if (output.Length < price.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var pool = ArrayPool<double>.Shared;
        var trArray = pool.Rent(price.Length);
        var atrArray = pool.Rent(price.Length);
        var emaArray = pool.Rent(price.Length);

        try
        {
            var tr = trArray.AsSpan(0, price.Length);
            var atr = atrArray.AsSpan(0, price.Length);
            var ema = emaArray.AsSpan(0, price.Length);

            // Calculate True Range
            for (var i = 0; i < price.Length; i++)
            {
                var prevClose = i >= 1 ? price[i - 1] : price[i];
                var highLow = high[i] - low[i];
                var highPrevClose = Math.Abs(high[i] - prevClose);
                var lowPrevClose = Math.Abs(low[i] - prevClose);
                tr[i] = Math.Max(highLow, Math.Max(highPrevClose, lowPrevClose));
            }

            // Calculate ATR
            ExponentialMovingAverage(tr, atr, length);

            // Calculate base EMA
            ExponentialMovingAverage(price, ema, length);

            // Apply TR adjustment
            for (var i = 0; i < price.Length; i++)
            {
                var currentTr = tr[i];
                var currentAtr = atr[i];
                var ratio = currentAtr != 0 ? currentTr / currentAtr : 1;
                var adjustedAlpha = 2.0 / (length + 1) * Math.Min(ratio * mult, 2);

                if (i == 0)
                {
                    output[i] = price[i];
                }
                else
                {
                    output[i] = output[i - 1] + adjustedAlpha * (price[i] - output[i - 1]);
                }
            }
        }
        finally
        {
            pool.Return(trArray);
            pool.Return(atrArray);
            pool.Return(emaArray);
        }
    }

    /// <summary>
    /// Computes ATR Filtered Exponential Moving Average.
    /// </summary>
    internal static void AtrFilteredExponentialMovingAverage(ReadOnlySpan<double> price, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 45, int atrLength = 20, int stdDevLength = 10, int lbLength = 20, double min = 5)
    {
        if (output.Length < price.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var pool = ArrayPool<double>.Shared;
        var trValArray = pool.Rent(price.Length);
        var atrValArray = pool.Rent(price.Length);
        var atrValPowArray = pool.Rent(price.Length);
        var stdDevAArray = pool.Rent(price.Length);

        try
        {
            var trVal = trValArray.AsSpan(0, price.Length);
            var atrVal = atrValArray.AsSpan(0, price.Length);
            var atrValPow = atrValPowArray.AsSpan(0, price.Length);
            var stdDevA = stdDevAArray.AsSpan(0, price.Length);

            // Calculate TR/price ratio
            for (var i = 0; i < price.Length; i++)
            {
                var prevClose = i >= 1 ? price[i - 1] : 0;
                var highLow = high[i] - low[i];
                var highPrevClose = Math.Abs(high[i] - prevClose);
                var lowPrevClose = Math.Abs(low[i] - prevClose);
                var tr = Math.Max(highLow, Math.Max(highPrevClose, lowPrevClose));
                trVal[i] = price[i] != 0 ? tr / price[i] : tr;
            }

            // Calculate ATR of normalized TR
            SimpleMovingAverage(trVal, atrVal, atrLength);

            // Calculate squared ATR values
            for (var i = 0; i < price.Length; i++)
            {
                atrValPow[i] = atrVal[i] * atrVal[i];
            }

            // Calculate SMA of squared ATR
            SimpleMovingAverage(atrValPow, stdDevA, stdDevLength);

            // Calculate adaptive EMA
            double atrValSum = 0;
            var atrValWindow = new double[stdDevLength];
            var stdDevWindow = new double[lbLength];
            var windowIdx = 0;
            var windowCount = 0;
            var stdDevIdx = 0;
            var stdDevCount = 0;

            double emaAFP = price.Length > 0 ? price[0] : 0;
            double emaCTP = price.Length > 0 ? price[0] : 0;

            for (var i = 0; i < price.Length; i++)
            {
                // Rolling ATR sum
                if (windowCount >= stdDevLength)
                    atrValSum -= atrValWindow[windowIdx];
                atrValWindow[windowIdx] = atrVal[i];
                atrValSum += atrVal[i];
                windowIdx = (windowIdx + 1) % stdDevLength;
                if (windowCount < stdDevLength) windowCount++;

                var stdDevB = windowCount > 0 ? (atrValSum * atrValSum) / (windowCount * windowCount) : 0;
                var stdDev = stdDevA[i] - stdDevB >= 0 ? Math.Sqrt(stdDevA[i] - stdDevB) : 0;

                // Track stdDev for min/max
                if (stdDevCount >= lbLength)
                {
                    // Recalculate min
                }
                stdDevWindow[stdDevIdx] = stdDev;
                stdDevIdx = (stdDevIdx + 1) % lbLength;
                if (stdDevCount < lbLength) stdDevCount++;

                var lowestStdDev = double.MaxValue;
                for (var j = 0; j < stdDevCount; j++)
                {
                    if (stdDevWindow[j] < lowestStdDev) lowestStdDev = stdDevWindow[j];
                }

                var atrP = lowestStdDev != 0 ? stdDev / lowestStdDev : 0;
                var afP = Math.Max(atrP, min) / min;
                var ctP = afP != 0 ? 1 / afP : 0;

                var prevEmaAFP = emaAFP;
                var prevEmaCTP = emaCTP;

                var scAFP = afP != 0 ? 2.0 / (1 + (length * afP)) : 0;
                var scCTP = ctP != 0 ? 2.0 / (1 + (length * ctP)) : 0;

                emaAFP = prevEmaAFP + (scAFP * (price[i] - prevEmaAFP));
                emaCTP = prevEmaCTP + (scCTP * (price[i] - prevEmaCTP));

                output[i] = (emaAFP + emaCTP) / 2;
            }
        }
        finally
        {
            pool.Return(trValArray);
            pool.Return(atrValArray);
            pool.Return(atrValPowArray);
            pool.Return(stdDevAArray);
        }
    }

    #endregion

    #region Remaining MovingAvgType Fast Path Methods

    /// <summary>
    /// Computes Reverse Engineering RSI - calculates the price needed to reach a specific RSI level.
    /// </summary>
    internal static void ReverseEngineeringRsi(ReadOnlySpan<double> input, Span<double> output, int length = 14, double rsiLevel = 50)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        double expPeriod = (2 * length) - 1;
        var k = 2 / (expPeriod + 1);
        double prevAuc = 1;
        double prevAdc = 1;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevValue = i >= 1 ? input[i - 1] : 0;
            var change = currentValue - prevValue;

            double auc, adc;
            if (currentValue > prevValue)
            {
                var gain = i >= 1 ? change : 0;
                auc = (k * gain) + ((1 - k) * prevAuc);
                adc = (1 - k) * prevAdc;
            }
            else
            {
                var loss = i >= 1 ? Math.Abs(change) : 0;
                auc = (1 - k) * prevAuc;
                adc = (k * loss) + ((1 - k) * prevAdc);
            }

            var rsiValue = (length - 1) * ((adc * rsiLevel / (100 - rsiLevel)) - auc);
            var revRsi = rsiValue >= 0 ? currentValue + rsiValue : currentValue + (rsiValue * (100 - rsiLevel) / rsiLevel);
            output[i] = revRsi;

            prevAuc = auc;
            prevAdc = adc;
        }
    }

    /// <summary>
    /// Computes Reverse MACD - calculates the price needed to reach a specific MACD level.
    /// </summary>
    internal static void ReverseMovingAverageConvergenceDivergence(ReadOnlySpan<double> input, Span<double> output, int fastLength = 12, int slowLength = 26, double macdLevel = 0)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var fastAlpha = 2.0 / (1 + fastLength);
        var slowAlpha = 2.0 / (1 + slowLength);

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
                var prevFastEma = i >= 1 ? fastEma[i - 1] : 0;
                var prevSlowEma = i >= 1 ? slowEma[i - 1] : 0;

                var pMacdEq = fastAlpha - slowAlpha != 0
                    ? ((prevFastEma * fastAlpha) - (prevSlowEma * slowAlpha)) / (fastAlpha - slowAlpha)
                    : 0;
                output[i] = pMacdEq;
            }
        }
        finally
        {
            pool.Return(fastEmaArray);
            pool.Return(slowEmaArray);
        }
    }

    /// <summary>
    /// Computes Optimal Weighted Moving Average.
    /// </summary>
    internal static void OptimalWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        // Rolling correlation state
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
        var corrWindow = new double[length * 2]; // Store x,y pairs
        var corrIndex = 0;
        var corrCount = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevOwma = i >= 1 ? output[i - 1] : 0;

            // Update rolling correlation (input vs prevOwma)
            var oldIdx = corrIndex;
            if (corrCount >= length)
            {
                // Remove oldest values
                var oldX = corrWindow[oldIdx * 2];
                var oldY = corrWindow[oldIdx * 2 + 1];
                sumX -= oldX;
                sumY -= oldY;
                sumXY -= oldX * oldY;
                sumX2 -= oldX * oldX;
                sumY2 -= oldY * oldY;
            }

            // Add new values
            corrWindow[corrIndex * 2] = currentValue;
            corrWindow[corrIndex * 2 + 1] = prevOwma;
            sumX += currentValue;
            sumY += prevOwma;
            sumXY += currentValue * prevOwma;
            sumX2 += currentValue * currentValue;
            sumY2 += prevOwma * prevOwma;

            corrIndex = (corrIndex + 1) % length;
            if (corrCount < length) corrCount++;

            // Calculate correlation
            var n = corrCount;
            var numerator = (n * sumXY) - (sumX * sumY);
            var denomX = (n * sumX2) - (sumX * sumX);
            var denomY = (n * sumY2) - (sumY * sumY);
            var denominator = Math.Sqrt(denomX * denomY);
            var corr = denominator != 0 ? numerator / denominator : 0;
            if (double.IsNaN(corr) || double.IsInfinity(corr)) corr = 0;

            // Calculate weighted sum
            double sum = 0, weightedSum = 0;
            for (var j = 0; j <= length - 1 && i >= j; j++)
            {
                var weight = Math.Pow(length - j, corr);
                var prevValue = input[i - j];
                sum += prevValue * weight;
                weightedSum += weight;
            }

            output[i] = weightedSum != 0 ? sum / weightedSum : 0;
        }
    }

    /// <summary>
    /// Computes Light Least Squares Moving Average.
    /// Uses SMA, half-length SMA, and standard deviations of both input and index.
    /// </summary>
    internal static void LightLeastSquaresMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 250)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var length1 = Math.Max(1, (int)Math.Ceiling((double)length / 2));

        var pool = ArrayPool<double>.Shared;
        var sma1Array = pool.Rent(input.Length);
        var sma2Array = pool.Rent(input.Length);
        var indexArray = pool.Rent(input.Length);
        var indexSmaArray = pool.Rent(input.Length);

        try
        {
            var sma1 = sma1Array.AsSpan(0, input.Length);
            var sma2 = sma2Array.AsSpan(0, input.Length);
            var indexSpan = indexArray.AsSpan(0, input.Length);
            var indexSma = indexSmaArray.AsSpan(0, input.Length);

            // Create index array
            for (var i = 0; i < input.Length; i++)
                indexSpan[i] = i;

            // Calculate SMAs
            SimpleMovingAverage(input, sma1, length);
            SimpleMovingAverage(input, sma2, length1);
            SimpleMovingAverage(indexSpan, indexSma, length);

            // Calculate standard deviations manually for each point
            for (var i = 0; i < input.Length; i++)
            {
                var n = Math.Min(i + 1, length);

                // StdDev of input
                double inputSum = 0, inputSum2 = 0;
                for (var j = 0; j < n; j++)
                {
                    var val = input[i - j];
                    inputSum += val;
                    inputSum2 += val * val;
                }
                var inputMean = inputSum / n;
                var inputVariance = (inputSum2 / n) - (inputMean * inputMean);
                var stdDev = inputVariance > 0 ? Math.Sqrt(inputVariance) : 0;

                // StdDev of index
                double indexSum = 0, indexSum2 = 0;
                for (var j = 0; j < n; j++)
                {
                    var val = (double)(i - j);
                    indexSum += val;
                    indexSum2 += val * val;
                }
                var indexMean = indexSum / n;
                var indexVariance = (indexSum2 / n) - (indexMean * indexMean);
                var indexStdDev = indexVariance > 0 ? Math.Sqrt(indexVariance) : 0;

                var c = stdDev != 0 ? (sma2[i] - sma1[i]) / stdDev : 0;
                var z = indexStdDev != 0 && c != 0 ? (i - indexSma[i]) / indexStdDev * c : 0;

                output[i] = sma1[i] + (z * stdDev);
            }
        }
        finally
        {
            pool.Return(sma1Array);
            pool.Return(sma2Array);
            pool.Return(indexArray);
            pool.Return(indexSmaArray);
        }
    }

    /// <summary>
    /// Computes Fisher Least Squares Moving Average.
    /// </summary>
    internal static void FisherLeastSquaresMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 100)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var pool = ArrayPool<double>.Shared;
        var smaArray = pool.Rent(input.Length);
        var indexArray = pool.Rent(input.Length);
        var indexSmaArray = pool.Rent(input.Length);

        try
        {
            var sma = smaArray.AsSpan(0, input.Length);
            var indexSpan = indexArray.AsSpan(0, input.Length);
            var indexSma = indexSmaArray.AsSpan(0, input.Length);

            // Create index array
            for (var i = 0; i < input.Length; i++)
                indexSpan[i] = i;

            // Calculate SMAs
            SimpleMovingAverage(input, sma, length);
            SimpleMovingAverage(indexSpan, indexSma, length);

            double prevB = input.Length > 0 ? input[0] : 0;
            double diffSum = 0, absDiffSum = 0;
            var diffWindow = new double[length];
            var absDiffWindow = new double[length];
            var windowIdx = 0;
            var windowCount = 0;

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var diff = currentValue - prevB;
                var absDiff = Math.Abs(diff);

                // Update rolling sums
                if (windowCount >= length)
                {
                    diffSum -= diffWindow[windowIdx];
                    absDiffSum -= absDiffWindow[windowIdx];
                }
                diffWindow[windowIdx] = diff;
                absDiffWindow[windowIdx] = absDiff;
                diffSum += diff;
                absDiffSum += absDiff;
                windowIdx = (windowIdx + 1) % length;
                if (windowCount < length) windowCount++;

                var n = Math.Min(i + 1, length);

                // StdDev of input
                double inputSum = 0, inputSum2 = 0;
                for (var j = 0; j < n; j++)
                {
                    var val = input[i - j];
                    inputSum += val;
                    inputSum2 += val * val;
                }
                var inputMean = inputSum / n;
                var inputVariance = (inputSum2 / n) - (inputMean * inputMean);
                var stdDevSrc = inputVariance > 0 ? Math.Sqrt(inputVariance) : 0;

                // StdDev of index
                double indexSum = 0, indexSum2 = 0;
                for (var j = 0; j < n; j++)
                {
                    var val = (double)(i - j);
                    indexSum += val;
                    indexSum2 += val * val;
                }
                var indexMeanVal = indexSum / n;
                var indexVariance = (indexSum2 / n) - (indexMeanVal * indexMeanVal);
                var indexStdDev = indexVariance > 0 ? Math.Sqrt(indexVariance) : 0;

                var e = absDiffSum / windowCount;
                var z = e != 0 ? (diffSum / windowCount) / e : 0;
                var expVal = Math.Exp(2 * z);
                var r = expVal + 1 != 0 ? (expVal - 1) / (expVal + 1) : 0;
                var a = indexStdDev != 0 && r != 0 ? (i - indexSma[i]) / indexStdDev * r : 0;

                var b = sma[i] + (a * stdDevSrc);
                output[i] = b;
                prevB = b;
            }
        }
        finally
        {
            pool.Return(smaArray);
            pool.Return(indexArray);
            pool.Return(indexSmaArray);
        }
    }

    /// <summary>
    /// Computes Overshoot Reduction Moving Average.
    /// </summary>
    internal static void OvershootReductionMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var length1 = (int)Math.Ceiling((double)length / 2);

        var pool = ArrayPool<double>.Shared;
        var smaArray = pool.Rent(input.Length);
        var indexArray = pool.Rent(input.Length);
        var indexSmaArray = pool.Rent(input.Length);
        var bSmaArray = pool.Rent(input.Length);

        try
        {
            var sma = smaArray.AsSpan(0, input.Length);
            var indexSpan = indexArray.AsSpan(0, input.Length);
            var indexSma = indexSmaArray.AsSpan(0, input.Length);
            var bSma = bSmaArray.AsSpan(0, input.Length);

            // Create index array
            for (var i = 0; i < input.Length; i++)
                indexSpan[i] = i;

            // Calculate SMAs
            SimpleMovingAverage(input, sma, length);
            SimpleMovingAverage(indexSpan, indexSma, length);

            // Rolling correlation state
            double corrSumX = 0, corrSumY = 0, corrSumXY = 0, corrSumX2 = 0, corrSumY2 = 0;
            var corrWindowX = new double[length];
            var corrWindowY = new double[length];
            var corrIdx = 0;
            var corrCount = 0;

            // Rolling sum for b
            double bSum = 0;
            var bWindow = new double[length1];
            var bIdx = 0;
            var bCount = 0;

            // Max tracking for bSma
            var bSmaMax = new double[length];
            var bSmaIdx = 0;
            var bSmaCount = 0;

            double prevD = input.Length > 0 ? input[0] : 0;

            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var index = (double)i;

                // Update rolling correlation (index vs input)
                if (corrCount >= length)
                {
                    corrSumX -= corrWindowX[corrIdx];
                    corrSumY -= corrWindowY[corrIdx];
                    corrSumXY -= corrWindowX[corrIdx] * corrWindowY[corrIdx];
                    corrSumX2 -= corrWindowX[corrIdx] * corrWindowX[corrIdx];
                    corrSumY2 -= corrWindowY[corrIdx] * corrWindowY[corrIdx];
                }
                corrWindowX[corrIdx] = index;
                corrWindowY[corrIdx] = currentValue;
                corrSumX += index;
                corrSumY += currentValue;
                corrSumXY += index * currentValue;
                corrSumX2 += index * index;
                corrSumY2 += currentValue * currentValue;
                corrIdx = (corrIdx + 1) % length;
                if (corrCount < length) corrCount++;

                // Calculate correlation
                var n = corrCount;
                var numerator = (n * corrSumXY) - (corrSumX * corrSumY);
                var denomX = (n * corrSumX2) - (corrSumX * corrSumX);
                var denomY = (n * corrSumY2) - (corrSumY * corrSumY);
                var denominator = Math.Sqrt(denomX * denomY);
                var corr = denominator != 0 ? numerator / denominator : 0;
                if (double.IsNaN(corr) || double.IsInfinity(corr)) corr = 0;

                // StdDev of input
                var nStd = Math.Min(i + 1, length);
                double inputSum = 0, inputSum2 = 0;
                for (var j = 0; j < nStd; j++)
                {
                    var val = input[i - j];
                    inputSum += val;
                    inputSum2 += val * val;
                }
                var inputMean = inputSum / nStd;
                var inputVariance = (inputSum2 / nStd) - (inputMean * inputMean);
                var stdDev = inputVariance > 0 ? Math.Sqrt(inputVariance) : 0;

                // StdDev of index
                double indexSum = 0, indexSum2 = 0;
                for (var j = 0; j < nStd; j++)
                {
                    var val = (double)(i - j);
                    indexSum += val;
                    indexSum2 += val * val;
                }
                var indexMeanVal = indexSum / nStd;
                var indexVariance = (indexSum2 / nStd) - (indexMeanVal * indexMeanVal);
                var indexStdDev = indexVariance > 0 ? Math.Sqrt(indexVariance) : 0;

                var a = indexStdDev != 0 && corr != 0 ? (index - indexSma[i]) / indexStdDev * corr : 0;

                var b = Math.Abs(prevD - currentValue);

                // Update b rolling sum
                if (bCount >= length1)
                {
                    bSum -= bWindow[bIdx];
                }
                bWindow[bIdx] = b;
                bSum += b;
                bIdx = (bIdx + 1) % length1;
                if (bCount < length1) bCount++;

                var bSmaVal = bSum / bCount;
                bSma[i] = bSmaVal;

                // Track max of bSma over length window
                if (bSmaCount >= length)
                {
                    // Need to recalculate max if we removed the max
                    bSmaMax[bSmaIdx] = bSmaVal;
                }
                else
                {
                    bSmaMax[bSmaCount] = bSmaVal;
                }
                bSmaIdx = (bSmaIdx + 1) % length;
                if (bSmaCount < length) bSmaCount++;

                var highest = double.MinValue;
                for (var j = 0; j < bSmaCount; j++)
                {
                    if (bSmaMax[j] > highest) highest = bSmaMax[j];
                }

                var c = highest > 0 ? b / highest : 0;
                var d = sma[i] + (a * (stdDev * c));
                output[i] = d;
                prevD = d != 0 ? d : currentValue;
            }
        }
        finally
        {
            pool.Return(smaArray);
            pool.Return(indexArray);
            pool.Return(indexSmaArray);
            pool.Return(bSmaArray);
        }
    }

    /// <summary>
    /// Computes Kaufman Adaptive Least Squares Moving Average.
    /// Combines KAMA efficiency ratio with LSMA regression.
    /// </summary>
    internal static void KaufmanAdaptiveLeastSquaresMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 100)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));

        var pool = ArrayPool<double>.Shared;
        var kamaArray = pool.Rent(input.Length);
        var smaArray = pool.Rent(input.Length);
        var indexArray = pool.Rent(input.Length);
        var kamaSmaArray = pool.Rent(input.Length);

        try
        {
            var kama = kamaArray.AsSpan(0, input.Length);
            var sma = smaArray.AsSpan(0, input.Length);
            var indexSpan = indexArray.AsSpan(0, input.Length);
            var kamaSma = kamaSmaArray.AsSpan(0, input.Length);

            // Create index array
            for (var i = 0; i < input.Length; i++)
                indexSpan[i] = i;

            // Calculate KAMA and SMA
            KaufmanAdaptiveMovingAverage(input, kama, length);
            SimpleMovingAverage(input, sma, length);
            SimpleMovingAverage(indexSpan, kamaSma, length);

            for (var i = 0; i < input.Length; i++)
            {
                var n = Math.Min(i + 1, length);

                // Calculate KACO-like statistics (correlation between input and index, weighted by KAMA)
                double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
                for (var j = 0; j < n; j++)
                {
                    var x = (double)(i - j);
                    var y = input[i - j];
                    sumX += x;
                    sumY += y;
                    sumXY += x * y;
                    sumX2 += x * x;
                    sumY2 += y * y;
                }

                // Correlation
                var numerator = (n * sumXY) - (sumX * sumY);
                var denomX = (n * sumX2) - (sumX * sumX);
                var denomY = (n * sumY2) - (sumY * sumY);
                var denominator = Math.Sqrt(denomX * denomY);
                var r = denominator != 0 ? numerator / denominator : 0;
                if (double.IsNaN(r) || double.IsInfinity(r)) r = 0;

                // StdDev calculations
                var inputMean = sumY / n;
                var inputVariance = (sumY2 / n) - (inputMean * inputMean);
                var srcSt = inputVariance > 0 ? Math.Sqrt(inputVariance) : 0;

                var indexMean = sumX / n;
                var indexVariance = (sumX2 / n) - (indexMean * indexMean);
                var indexSt = indexVariance > 0 ? Math.Sqrt(indexVariance) : 0;

                var alpha = indexSt != 0 ? srcSt / indexSt * r : 0;
                var beta = sma[i] - (alpha * kamaSma[i]);

                output[i] = (alpha * i) + beta;
            }
        }
        finally
        {
            pool.Return(kamaArray);
            pool.Return(smaArray);
            pool.Return(indexArray);
            pool.Return(kamaSmaArray);
        }
    }

    /// <summary>
    /// Computes 3HMA (Triple Hull Moving Average).
    /// </summary>
    internal static void ThreeHma(ReadOnlySpan<double> input, Span<double> output, int length = 50)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var p = Math.Max(1, (int)Math.Ceiling((double)length / 2));
        var p1 = Math.Max(1, (int)Math.Ceiling((double)p / 3));
        var p2 = Math.Max(1, (int)Math.Ceiling((double)p / 2));
        var sqrtP = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(p)));

        var wma1Array = pool.Rent(input.Length);
        var wma2Array = pool.Rent(input.Length);
        var wma3Array = pool.Rent(input.Length);
        var midArray = pool.Rent(input.Length);

        try
        {
            var wma1 = wma1Array.AsSpan(0, input.Length);
            var wma2 = wma2Array.AsSpan(0, input.Length);
            var wma3 = wma3Array.AsSpan(0, input.Length);
            var mid = midArray.AsSpan(0, input.Length);

            WeightedMovingAverage(input, wma1, p1);
            WeightedMovingAverage(input, wma2, p2);
            WeightedMovingAverage(input, wma3, p);

            for (var i = 0; i < input.Length; i++)
            {
                mid[i] = (3 * wma1[i]) - wma2[i] - wma3[i];
            }

            WeightedMovingAverage(mid, output, sqrtP);
        }
        finally
        {
            pool.Return(wma1Array);
            pool.Return(wma2Array);
            pool.Return(wma3Array);
            pool.Return(midArray);
        }
    }

    /// <summary>
    /// Computes Alpha Decreasing Exponential Moving Average.
    /// </summary>
    internal static void AlphaDecreasingExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            var alpha = i > 0 ? (double)length / (length + i) : 1;
            var prevEma = i > 0 ? output[i - 1] : input[i];
            output[i] = (alpha * input[i]) + ((1 - alpha) * prevEma);
        }
    }

    /// <summary>
    /// Computes Adaptive Autonomous Recursive Trailing Stop.
    /// </summary>
    internal static void AdaptiveAutonomousRecursiveTrailingStop(ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 14, double lambda = 1)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var armaArray = pool.Rent(close.Length);

        try
        {
            var arma = armaArray.AsSpan(0, close.Length);
            TrendCore.AdaptiveAutonomousRecursiveMovingAverage(close, arma, length, lambda);

            for (var i = 0; i < close.Length; i++)
            {
                var currentArma = arma[i];
                var prevArma = i > 0 ? arma[i - 1] : currentArma;
                var currentHigh = high[i];
                var currentLow = low[i];

                if (currentArma > prevArma)
                {
                    output[i] = currentLow;
                }
                else if (currentArma < prevArma)
                {
                    output[i] = currentHigh;
                }
                else
                {
                    output[i] = i > 0 ? output[i - 1] : close[i];
                }
            }
        }
        finally
        {
            pool.Return(armaArray);
        }
    }

    /// <summary>
    /// Computes Adaptive Trailing Stop.
    /// </summary>
    internal static void AdaptiveTrailingStop(ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 14, double multiplier = 2)
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

            var trend = 1;
            for (var i = 0; i < close.Length; i++)
            {
                var currentClose = close[i];
                var currentAtr = atr[i] * multiplier;
                var prevStop = i > 0 ? output[i - 1] : currentClose;

                if (trend == 1)
                {
                    var newStop = currentClose - currentAtr;
                    output[i] = Math.Max(newStop, prevStop);
                    if (currentClose < output[i])
                    {
                        trend = -1;
                        output[i] = currentClose + currentAtr;
                    }
                }
                else
                {
                    var newStop = currentClose + currentAtr;
                    output[i] = Math.Min(newStop, prevStop);
                    if (currentClose > output[i])
                    {
                        trend = 1;
                        output[i] = currentClose - currentAtr;
                    }
                }
            }
        }
        finally
        {
            pool.Return(atrArray);
        }
    }

    /// <summary>
    /// Computes Average True Range Trailing Stops.
    /// </summary>
    internal static void AverageTrueRangeTrailingStops(ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 14, double multiplier = 3)
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

            for (var i = 0; i < close.Length; i++)
            {
                var currentClose = close[i];
                var currentAtr = atr[i] * multiplier;
                var prevStop = i > 0 ? output[i - 1] : currentClose;
                var prevClose = i > 0 ? close[i - 1] : currentClose;

                if (currentClose > prevStop && prevClose > prevStop)
                {
                    output[i] = Math.Max(prevStop, currentClose - currentAtr);
                }
                else if (currentClose < prevStop && prevClose < prevStop)
                {
                    output[i] = Math.Min(prevStop, currentClose + currentAtr);
                }
                else if (currentClose > prevStop)
                {
                    output[i] = currentClose - currentAtr;
                }
                else
                {
                    output[i] = currentClose + currentAtr;
                }
            }
        }
        finally
        {
            pool.Return(atrArray);
        }
    }

    /// <summary>
    /// Computes Welles Wilder Summation.
    /// Formula: sum = prevSum - (prevSum / length) + currentValue
    /// </summary>
    internal static void WellesWilderSummation(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        double sum = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            sum = sum - (sum / length) + currentValue;
            output[i] = sum;
        }
    }

    /// <summary>
    /// Computes Contract High - cumulative maximum of high prices.
    /// </summary>
    internal static void ContractHigh(ReadOnlySpan<double> high, Span<double> output)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double runningMax = double.MinValue;

        for (var i = 0; i < high.Length; i++)
        {
            var currentHigh = high[i];
            runningMax = Math.Max(runningMax, currentHigh);
            output[i] = runningMax;
        }
    }

    /// <summary>
    /// Computes Contract Low - cumulative minimum of low prices.
    /// </summary>
    internal static void ContractLow(ReadOnlySpan<double> low, Span<double> output)
    {
        if (output.Length < low.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double runningMin = double.MaxValue;

        for (var i = 0; i < low.Length; i++)
        {
            var currentLow = low[i];
            runningMin = Math.Min(runningMin, currentLow);
            output[i] = runningMin;
        }
    }

    /// <summary>
    /// Computes Damping Index oscillator.
    /// Formula: Measures trend persistence using cumulative direction.
    /// </summary>
    internal static void DampingIndex(ReadOnlySpan<double> input, Span<double> output, int length = 5)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
                continue;
            }

            int upCount = 0;
            int downCount = 0;

            for (var j = i - length + 1; j <= i; j++)
            {
                var currentValue = input[j];
                var prevValue = j > 0 ? input[j - 1] : currentValue;
                if (currentValue > prevValue) upCount++;
                else if (currentValue < prevValue) downCount++;
            }

            output[i] = (double)(upCount - downCount) / length;
        }
    }

    /// <summary>
    /// Computes Didi Index.
    /// Formula: Compares short, medium, and long term averages.
    /// </summary>
    internal static void DidiIndex(ReadOnlySpan<double> input, Span<double> output, int shortLength = 3, int mediumLength = 8, int longLength = 20)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var shortMa = pool.Rent(input.Length);
        var mediumMa = pool.Rent(input.Length);
        var longMa = pool.Rent(input.Length);

        try
        {
            SimpleMovingAverage(input, shortMa.AsSpan(0, input.Length), shortLength);
            SimpleMovingAverage(input, mediumMa.AsSpan(0, input.Length), mediumLength);
            SimpleMovingAverage(input, longMa.AsSpan(0, input.Length), longLength);

            for (var i = 0; i < input.Length; i++)
            {
                var medium = mediumMa[i];
                // Didi Index: ratio of short MA to medium MA minus ratio of long MA to medium MA
                output[i] = medium != 0 ? (shortMa[i] / medium) - (longMa[i] / medium) : 0;
            }
        }
        finally
        {
            pool.Return(shortMa);
            pool.Return(mediumMa);
            pool.Return(longMa);
        }
    }

    #endregion
}
