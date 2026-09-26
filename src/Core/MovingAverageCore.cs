using System;
using System.Buffers;

using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Core;

internal static class MovingAverageCore
{
    internal static void SimpleMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (length == 1) { input.CopyTo(output); return; }

        double sum = 0;
        var exactRequired = false;
        double roundoff = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var previousSum = sum;
            sum += input[i];
            roundoff = MeanRoundoff.AfterAddition(roundoff, sum);
            exactRequired |= ExactMeanAccumulator.SevereCancellation(previousSum, input[i], sum);
            if (i >= length)
            {
                previousSum = sum;
                sum -= input[i - length];
                roundoff = MeanRoundoff.AfterAddition(roundoff, sum);
                exactRequired |= ExactMeanAccumulator.SevereCancellation(previousSum, -input[i - length], sum);
                // If eviction cancels a much larger accumulator, its low-order values were
                // already rounded away. Rebuild before publishing, not on a later periodic bar.
                if (Math.Abs(sum) <= 1e-4 * Math.Max(Math.Abs(input[i - length]), Math.Abs(input[i])))
                {
                    sum = 0;
                    roundoff = 0;
                    for (var j = i - length + 1; j <= i; j++)
                    {
                        sum += input[j];
                        roundoff = MeanRoundoff.AfterAddition(roundoff, sum);
                    }
                }
            }

            output[i] = i >= length - 1 ? sum / length : 0;
            if (i >= length - 1 && (exactRequired || MeanRoundoff.RequiresExact(sum, length, roundoff)))
            {
                var exact = new ExactMeanAccumulator();
                for (var j = i - length + 1; j <= i; j++) exact.Add(input[j]);
                output[i] = exact.Mean(length);
            }

            // Rebuilt from its window every length bars, once the bar's value is taken. A running sum otherwise
            // keeps the rounding error of every value it has ever held: after prices near 100,000 it was still
            // off by 1e-9 at prices near 10, which a deviation from the mean of a tenth turns into 1e-8.
            if (length > 0 && (i + 1) % length == 0)
            {
                sum = 0;
                exactRequired = false;
                roundoff = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    previousSum = sum;
                    sum += input[j];
                    roundoff = MeanRoundoff.AfterAddition(roundoff, sum);
                    exactRequired |= ExactMeanAccumulator.SevereCancellation(previousSum, input[j], sum);
                }
            }
        }
    }

    /// <summary>
    /// The variable-length average's next length: the one decision, in one place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The length moves on where the value sits against four levels drawn at 0.25 and 1.75 deviations either
    /// side of the average. Inside the inner pair it lengthens, outside the outer pair it shortens, between
    /// them it holds. A deviation of 0 holds too: until the window fills there is no deviation, which would
    /// collapse all four levels onto the average and make any value not exactly on it "outside" by
    /// construction - shortening on the absence of a measurement rather than on one. A genuinely flat window
    /// reads 0 as well and carries no dispersion signal either. See issue #190.
    /// </para>
    /// <para>
    /// This is here because three places computed it: the batch calculation, the variable-length streaming
    /// state, and UltimateMovingAverageState, which repeats the decision inline because the batch ultimate
    /// moving average reads the variable-length average's own Length output. Converting the deviation in only
    /// some of them made the engines choose different lengths, which compounds because each bar's length
    /// carries into the next, and the streaming parity sweeps caught it at bar 49. One copy cannot drift from
    /// another.
    /// </para>
    /// </remarks>
    /// <param name="value">The bar's resolved input value.</param>
    /// <param name="average">The moving average the levels are measured from.</param>
    /// <param name="deviation">The deviation of the window about its own mean, or 0 where none is known yet.</param>
    /// <param name="previousLength">The length carried in from the previous bar.</param>
    /// <param name="minLength">The shortest length allowed.</param>
    /// <param name="maxLength">The longest length allowed.</param>
    internal static double VariableLength(double value, double average, double deviation, double previousLength,
        int minLength, int maxLength)
    {
        if (deviation == 0)
        {
            return previousLength;
        }

        var inner = 0.25 * deviation;
        var outer = 1.75 * deviation;

        double next;
        if (value >= average - inner && value <= average + inner)
        {
            next = previousLength + 1;
        }
        else if (value < average - outer || value > average + outer)
        {
            next = previousLength - 1;
        }
        else
        {
            next = previousLength;
        }

        // Max then min, as both call sites clamped it.
        return MathHelper.MinOrMax(next, maxLength, minLength);
    }

    internal static void WeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var numerator = new ExactMeanAccumulator();
        var sum = new ExactMeanAccumulator();
        var denominator = (long)length * (length + 1L) / 2;
        for (var i = 0; i < input.Length; i++)
        {
            numerator.Subtract(sum);
            numerator.Add(input[i], length);
            sum.Add(input[i]);
            if (i >= length) sum.Add(input[i - length], -1);
            output[i] = numerator.Mean(denominator);
        }
    }

    internal static void ExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var state = new Streaming.EmaState(length);
        for (var i = 0; i < input.Length; i++)
            output[i] = state.GetNext(input[i], commit: true);
    }

    internal static void WellesWilderMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        double prevWwma = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var wwma = RoundedWilder.Next(input[i], prevWwma, length);
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
                output[i] = ExponentialExtrapolation.Double(ema1[i], ema2[i]);
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
                output[i] = ExponentialExtrapolation.Triple(ema1[i], ema2[i], ema3[i]);
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

        using var first = new Streaming.RoundedSimpleMovingAverageSmoother(length);
        using var second = new Streaming.RoundedSimpleMovingAverageSmoother(length);
        for (var i = 0; i < input.Length; i++)
            output[i] = second.Next(first.Next(input[i], true), true);
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

        if (volume.Length < price.Length)
            throw new ArgumentException("Volume span must be at least input length.", nameof(volume));
        using var mean = new RollingVolumeMean(length);
        for (var i = 0; i < price.Length; i++)
            output[i] = mean.Next(price[i], volume[i], true);
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

        // The line through the trailing window, x counted from its first value: running sums over the bar index
        // cancel catastrophically and drift, see ExactLinearFitWindow. A one-value window returns the value.
        using var regression = new ExactLinearFitWindow(length);
        for (var i = 0; i < input.Length; i++)
        {
            output[i] = regression.Next(input[i], isFinal: true).Last;
        }
    }

    /// <summary>
    /// Computes Kaufman's Adaptive Moving Average (KAMA).
    /// </summary>
    internal static void KaufmanAdaptiveMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length, int fastLength = 2, int slowLength = 30)
    {
        RoundedKaufmanWindow.Compute(input, output, length, fastLength, slowLength);
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
                output[i] = ExponentialExtrapolation.Double(ema1[i], ema2[i]);
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
                k = Math.Max(1, k);
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
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        if (input.IsEmpty) return;
        var resolved = Math.Max(1, length);
        var alpha = 2d / (resolved + 1d);
        using var momentum = new ChandeMomentumWindow(Math.Min(resolved, input.Length));
        var vidya = input[0];
        for (var i = 0; i < input.Length; i++)
        {
            var currentCmo = Math.Abs(momentum.Next(input[i], true) / 100);
            vidya = VidyaBlend.Compute(vidya, input[i], alpha * currentCmo);
            output[i] = vidya;
        }
    }

    /// <summary>
    /// Computes Variable Moving Average (VMA).
    /// </summary>
    internal static void VariableMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        using var engine = new Streaming.VariableMovingAverageEngine(length);
        for (var i = 0; i < input.Length; i++) output[i] = engine.Next(input[i], true);
    }

    /// <summary>
    /// Computes Arnaud Legoux Moving Average (ALMA).
    /// </summary>
    internal static void ArnaudLegouxMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 9, double offset = 0.85, double sigma = 6)
    {
        AlmaWindowMean.Compute(input, output, length, offset, sigma);
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

        using var weighted = new Streaming.WmaState(length);
        using var simple = new Streaming.RoundedSimpleMovingAverageSmoother(length);
        for (var i = 0; i < input.Length; i++)
            output[i] = LeastSquaresAverage.Combine(weighted.GetNext(input[i], true), simple.Next(input[i], true));
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

        length = Math.Max(2, length);
        length = checked(length + (length & 1));
        var halfLength = length / 2;
        double dimension = 0;
        double frama = 0;

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = close[i];
                frama = close[i];
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
            if (n1 > 0 && n2 > 0 && n3 > 0)
                dimension = (Math.Log(n1 + n2) - Math.Log(n3)) / Math.Log(2);

            // Calculate alpha
            var alpha = Math.Exp(-4.6 * (dimension - 1));
            alpha = Math.Max(0.01, Math.Min(alpha, 1));

            frama = i < length ? close[i] : (alpha * close[i]) + ((1 - alpha) * frama);
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
        SineWindowMean.Compute(input, output, length);
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
                // Bars before the series starts count as zero and the divisor stays the full weight sum,
                // which is what the batch indicator does, so the run-in is damped rather than blank.
                double sum = 0;
                for (var j = 0; j < length; j++)
                {
                    var index = i - length + 1 + j;
                    sum += index >= 0 ? weights[j] * input[index] : 0;
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
        RollingGeometricMean.Compute(input, output, length);
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
        IntegerPowerWindowMean.Compute(input, output, length, 3);
    }

    /// <summary>
    /// Computes Natural Moving Average.
    /// </summary>
    internal static void NaturalMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        NaturalWindowMean.Compute(input, output, length);
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
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        var window = new AlphaDecreasingWindow();
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true);
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
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        length = Math.Max(1, length);
        var candidates = new RollingSum();
        var firstMean = new RollingSum();
        double accumulatedDeviation = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var previous = i == 0 ? input[i] : output[i - 1];
            var lagged = i < 7 ? 0 : input[i - 7];
            accumulatedDeviation += Math.Abs(lagged - previous);
            var radius = i == 0 ? 0 : accumulatedDeviation / i * 3;
            var candidate = input[i] > previous + radius ? input[i] + radius
                : input[i] < previous - radius ? input[i] - radius : previous;
            candidates.Add(candidate);
            firstMean.Add(candidates.Average(length));
            output[i] = firstMean.Average(length);
        }
    }

    /// <summary>
    /// Computes Adaptive Least Squares MA.
    /// Weighted least squares endpoint with true-range-dependent exponential forgetting.
    /// </summary>
    internal static void AdaptiveLeastSquares(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        var ranges = new RollingMinMax(Math.Max(1, length));
        var regression = new AdaptiveLeastSquaresMoments();
        for (var i = 0; i < input.Length; i++)
        {
            var range = i == 0 ? 0 : Math.Abs(input[i] - input[i - 1]);
            ranges.Add(range);
            var gain = ranges.Max == 0 ? .01 : MathHelper.MinOrMax(Math.Pow(range / ranges.Max, 1.5), .99, .01);
            output[i] = regression.Next(input[i], gain);
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
                    PriceMean.Of(window[length / 2 - 1], window[length / 2]) :
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
        IntegerPowerWindowMean.Compute(input, output, length, 2);
    }

    /// <summary>
    /// Computes Parabolic Weighted Moving Average.
    /// MA with parabolic weight curve.
    /// </summary>
    internal static void ParabolicWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        // Parabolic weights are squared distances from the oldest end of the window.
        QuadraticWeightedMovingAverage(input, output, length);
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

        length = Math.Max(1, length);
        var weight = SymmetricWindowMean.TotalWeight(length);
        for (var i = 0; i < input.Length; i++)
        {
            var sum = new ExactMeanAccumulator();
            for (var lag = 0; lag < length && lag <= i; lag++)
                sum.Add(input[i - lag], Math.Min(lag + 1, length - lag));
            output[i] = sum.Mean(weight);
        }
    }

    /// <summary>
    /// Computes Square Root Weighted Moving Average.
    /// Weights are square root of position.
    /// </summary>
    internal static void SquareRootWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        SquareRootWindowMean.Compute(input, output, length);
    }

    /// <summary>
    /// Computes Spencer 15-Point Moving Average.
    /// Classic Henderson-type filter for smooth trends.
    /// </summary>
    internal static void Spencer15PointMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 15)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        using var window = new SpencerWindow(false);
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true);
    }

    /// <summary>
    /// Computes Spencer 21-Point Moving Average.
    /// Extended Henderson-type filter for smoother trends.
    /// </summary>
    internal static void Spencer21PointMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 21)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        using var window = new SpencerWindow(true);
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true);
    }

    /// <summary>
    /// Computes Slow Smoothed Moving Average.
    /// Three sequential averages over the documented split windows.
    /// </summary>
    internal static void SlowSmoothedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 15,
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        length = Math.Max(1, length);
        var w2 = Math.Max(1, Math.Min(530, (int)Math.Ceiling(length / 3d)));
        var w1 = Math.Max(1, Math.Min(530, (int)Math.Ceiling((length - w2) / 2d)));
        var w3 = Math.Max(1, Math.Min(530, (int)Math.Floor((length - w2) / 2d)));
        Streaming.IMovingAverageSmoother Stage(int period) => maType == MovingAvgType.SimpleMovingAverage
            ? new Streaming.RoundedSimpleMovingAverageSmoother(period) : Streaming.MovingAverageSmootherFactory.Create(maType, period);
        using var first = Stage(w1);
        using var second = Stage(w2);
        using var third = Stage(w3);
        for (var i = 0; i < input.Length; i++)
            output[i] = third.Next(second.Next(first.Next(input[i], true), true), true);
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
        QuickWindowMean.Compute(input, output, length);
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
        HannWindowMean.Compute(input, output, length);
    }

    /// <summary>
    /// Computes Ehlers Triangle Moving Average.
    /// Uses triangular window coefficients with partial data handling.
    /// </summary>
    internal static void EhlersTriangleMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
        => SymmetricallyWeightedMovingAverage(input, output, length);

    /// <summary>
    /// Computes Elastic Volume Weighted Moving Average V1.
    /// Volume-weighted with elastic adjustment.
    /// </summary>
    internal static void ElasticVolumeWeightedMovingAverageV1(ReadOnlySpan<double> price, ReadOnlySpan<double> volume, Span<double> output, int length = 14)
        => ElasticVolumeWeightedMovingAverageV1(price, volume, output, length, 20);

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
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        using var window = new BinomialCascadeWindow(MovingAvgType.ExponentialMovingAverage, length, true);
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true);
    }

    /// <summary>
    /// Computes Quadruple Exponential Moving Average.
    /// Four-fold EMA for heavy smoothing.
    /// </summary>
    internal static void QuadrupleExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        using var window = new BinomialCascadeWindow(MovingAvgType.ExponentialMovingAverage, length, false);
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true);
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
        var baseline = input.Length > 0 ? input[0] : 0;
        double l0 = 0;
        var l1 = l0;
        var l2 = l0;
        var l3 = l0;

        for (var i = 0; i < input.Length; i++)
        {
            var prevL0 = l0;
            var prevL1 = l1;
            var prevL2 = l2;

            l0 = ((1 - gamma) * (input[i] - baseline)) + (gamma * l0);
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
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        length = Math.Max(1, length);
        var lag = (length - 1) / 2;
        var corrected = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            for (var i = 0; i < input.Length; i++)
                corrected[i] = i < lag ? input[i] : input[i] + (input[i] - input[i - lag]);
            ExponentialMovingAverage(corrected.AsSpan(0, input.Length), output, length);
        }
        finally { ArrayPool<double>.Shared.Return(corrected); }
    }

    /// <summary>
    /// Computes Ehlers Fractal Adaptive Moving Average.
    /// </summary>
    internal static void EhlersFractalAdaptiveMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 16)
        => FractalAdaptiveMovingAverage(input, input, input, output, length);

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
        IntegerPowerWindowMean.Compute(input, output, length, 3);
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

        length = Math.Max(3, length);
        for (var i = 0; i < input.Length; i++)
        {
            // Bars before the series starts count as zero and the divisor stays the full weight sum,
            // which is what the batch indicator does, so the run-in is damped rather than blank.
            double wSum = 0, wvSum = 0;
            for (var j = 1; j <= length; j++)
            {
                var ratio = (double)j / length;
                var w = Math.Sin(2 * Math.PI * ratio) / j;
                wvSum += i >= j - 1 ? w * input[i - (j - 1)] : 0;
                wSum += w;
            }
            output[i] = wSum != 0 ? wvSum / wSum : 0;
        }
    }

    /// <summary>
    /// Computes the zero-padded, offset-weighted End Point Moving Average.
    /// </summary>
    internal static void EndPointMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 11, int offset = 4)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        if (input.IsEmpty) return;
        using var window = new AffineAverageWindow(length, offset, capacityHint: input.Length);
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i]);
    }

    /// <summary>
    /// Computes Fibonacci Weighted Moving Average.
    /// </summary>
    internal static void FibonacciWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        FibonacciWindowMean.Compute(input, output, length);
    }

    /// <summary>
    /// Computes Generalized Double Exponential Moving Average (GDEMA).
    /// </summary>
    internal static void GeneralizedDoubleExponentialMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14, double volumeFactor = 0.7)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        using var window = new GeneralizedDoubleWindow(MovingAvgType.ExponentialMovingAverage, length, volumeFactor);
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true);
    }

    /// <summary>
    /// Computes Geometric Mean Moving Average.
    /// </summary>
    internal static void GeometricMeanMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        RollingGeometricMean.Compute(input, output, length, positiveOnly: true);
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

            var sum = new ExactReciprocalSum();
            for (var j = 0; j < length; j++) sum.Add(input[i - j]);
            output[i] = sum.Mean;
        }
    }

    #endregion

    #region Batch 19 - Ehlers Butterworth and Super Smoother Filters

    /// <summary>
    /// Computes Ehlers 2-Pole Butterworth Filter V1 using span-based computation.
    /// </summary>
    internal static void Ehlers2PoleButterworthFilterV1(ReadOnlySpan<double> input, Span<double> output, int length = 10)
    {
        length = Math.Max(2, length);
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var sqrt2 = Math.Sqrt(2);
        var a = Math.Exp(-sqrt2 * Math.PI / length);
        var b = 2 * a * Math.Cos(sqrt2 * 1.25 * Math.PI / length);
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
        length = Math.Max(2, length);
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var sqrt2 = Math.Sqrt(2);
        var a = Math.Exp(-sqrt2 * Math.PI / length);
        var b = 2 * a * Math.Cos(sqrt2 * Math.PI / length);
        var c2 = b;
        var c3 = -a * a;
        var c1 = (1 - b + Math.Pow(a, 2)) / 4;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevValue1 = i >= 1 ? input[i - 1] : 0;
            var prevValue2 = i >= 2 ? input[i - 2] : 0;
            var prevFilter1 = i >= 1 ? output[i - 1] : 0;
            var prevFilter2 = i >= 2 ? output[i - 2] : 0;

            output[i] = i < 3 ? currentValue : (c1 * (currentValue + (2 * prevValue1) + prevValue2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
        }
    }

    /// <summary>
    /// Computes Ehlers 3-Pole Butterworth Filter V1 using span-based computation.
    /// </summary>
    internal static void Ehlers3PoleButterworthFilterV1(ReadOnlySpan<double> input, Span<double> output, int length = 10)
    {
        length = Math.Max(2, length);
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var a = Math.Exp(-Math.PI / length);
        var b = 2 * a * Math.Cos(1.738 * Math.PI / length);
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
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        var kernel = new Streaming.ButterworthThreePoleKernel(length);
        for (var i = 0; i < input.Length; i++) output[i] = kernel.Next(input[i], true);
    }

    /// <summary>
    /// Computes Ehlers 2-Pole Super Smoother Filter V1 using span-based computation.
    /// </summary>
    internal static void Ehlers2PoleSuperSmootherFilterV1(ReadOnlySpan<double> input, Span<double> output, int length = 15)
    {
        length = Math.Max(2, length);
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var sqrt2 = Math.Sqrt(2);
        var a1 = Math.Exp(-sqrt2 * Math.PI / length);
        var b1 = 2 * a1 * Math.Cos(sqrt2 * Math.PI / length);
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
        length = Math.Max(2, length);
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
        length = Math.Max(2, length);
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var kernel = new Streaming.ButterworthThreePoleKernel(length, smoothInput: false);
        for (var i = 0; i < input.Length; i++) output[i] = kernel.Next(input[i], true);
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
        var alpha1 = EhlersFirstOrderCoefficient.Alpha(length);

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
        HammingWindowMean.Compute(input, output, length, pedestal);
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
        var alpha = EhlersFirstOrderCoefficient.Alpha(mult * length * Math.Sqrt(2));

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
    internal static void EhlersHighPassFilterV2(ReadOnlySpan<double> input, Span<double> output, int length = 20)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        length = Math.Max(length, 1);
        var angle = Math.Sqrt(2) * Math.PI / length;
        var decay = Math.Exp(-angle);
        var c2 = 2 * decay * Math.Cos(angle);
        var c3 = -decay * decay;
        var c1 = (1 + c2 - c3) / 4;
        var filtered = ArrayPool<double>.Shared.Rent(input.Length);
        var smoothed = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var hp = filtered.AsSpan(0, input.Length);
            for (var i = 0; i < input.Length; i++)
                hp[i] = i < 4 ? 0 : c1 * (input[i] - 2 * input[i - 1] + input[i - 2])
                    + c2 * hp[i - 1] + c3 * hp[i - 2];
            WeightedMovingAverage(hp, smoothed.AsSpan(0, input.Length), length);
            WeightedMovingAverage(smoothed.AsSpan(0, input.Length), output, length);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(filtered);
            ArrayPool<double>.Shared.Return(smoothed);
        }
    }

    /// <summary>
    /// Computes Distance Weighted Moving Average using span-based computation.
    /// </summary>
    internal static void DistanceWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
        => DistanceMassWindowMean.Compute(input, output, length, reciprocal: true);


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
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        using var window = new EhlersFirWindow();
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true);
    }

    /// <summary>
    /// Computes Ehlers Infinite Impulse Response Filter using span-based computation.
    /// </summary>
    internal static void EhlersInfiniteImpulseResponseFilter(ReadOnlySpan<double> input, Span<double> output, int length = 15)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        using var window = new EhlersIirWindow(length);
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true);
    }

    /// <summary>
    /// Computes Ahrens Moving Average using span-based computation.
    /// Formula: ahma = prevAhma + ((currentValue - ((prevAhma + priorAhma) / 2)) / length)
    /// </summary>
    internal static void AhrensMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 9)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        using var window = new AhrensWindow(length);
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true);
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
            using var simple = new Streaming.RoundedSimpleMovingAverageSmoother(length);
            for (var i = 0; i < input.Length; i++) sma[i] = simple.Next(input[i], true);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = LeoAverage.Combine(wma[i], sma[i]);
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
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        using var window = new McNichollWindow(MovingAvgType.ExponentialMovingAverage, length);
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true);
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
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        using var window = new ZeroLagTripleWindow(MovingAvgType.TripleExponentialMovingAverage, length);
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true);
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
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        var window = new WilderSummationWindow(length);
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true);
    }

    /// <summary>
    /// Computes Simplified Weighted Moving Average using span-based computation.
    /// </summary>
    internal static void SimplifiedWeightedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 20)
        => WeightedMovingAverage(input, output, Math.Max(1, length));


    /// <summary>
    /// Computes Simplified Least Squares Moving Average using span-based computation.
    /// </summary>
    internal static void SimplifiedLeastSquaresMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 25)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        using var window = new SimplifiedLeastSquaresWindow(length);
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true);
    }

    /// <summary>
    /// Computes Sharp Modified Moving Average using span-based computation.
    /// </summary>
    internal static void SharpModifiedMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        if (input.IsEmpty) return;
        length = Math.Max(1, length);
        var buffer = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var average = buffer.AsSpan(0, input.Length);
            SimpleMovingAverage(input, average, length);
            using var window = new AffineAverageWindow(length, sharp: true, capacityHint: input.Length, exactSimple: true);
            for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], average[i]);
        }
        finally { ArrayPool<double>.Shared.Return(buffer); }
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
        RollingRootMeanSquare.Compute(input, output, length);
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
        => DistanceMassWindowMean.Compute(input, output, length);


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

                // The public contract uses nearest-rank quartiles, including partial windows.
                var q1 = windowBuffer[(n + 3) / 4 - 1];
                var median = windowBuffer[(2 * n + 3) / 4 - 1];
                var q3 = windowBuffer[(3 * n + 3) / 4 - 1];
                output[i] = PriceMean.Of(q1, median, median, q3);
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
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        length = Math.Max(1, length);
        using var regression = new ExactLinearFitWindow(length);
        for (var i = 0; i < input.Length; i++)
        {
            var fit = regression.Next(input[i], true);
            output[i] = fit.Count < length ? 0 : fit.Last;
        }
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

        length = Math.Max(1, length);
        for (var i = 0; i < input.Length; i++)
            output[i] = PriceMean.Of(input[i], i >= length ? input[i - length] : 0);
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

        var termMult = Math.Max(2, Math.Min(530, (length - 1) / 2));

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

        FareyWindowMean.Compute(input, output, length);
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

            output[i] = w != 0 ? vw / w : input[i];
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
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        if (input.Length == 0) return;
        using var mean = new Streaming.RoundedSimpleMovingAverageSmoother(length);
        using var gate = new SequentialMeanGate(length);
        for (var i = 0; i < input.Length; i++) output[i] = gate.Next(input[i], mean.Next(input[i], true), true);
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
        poles = Math.Max(1, Math.Min(4, poles));
        var gain = EhlersGaussian.Gain(length, poles);
        var stages = new double[poles];
        for (var i = 0; i < input.Length; i++)
            output[i] = EhlersGaussian.Next(input[i], gain, stages, true);
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
            // Preserve this regression-based variant's partial-window fit.
            LinearRegression(input, lsmaBuffer.AsSpan(0, input.Length), length);

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
            // Preserve this regression-based variant's partial-window fit.
            LinearRegression(input, lsmaBuffer.AsSpan(0, input.Length), length);
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

        // The same filter CalculateHybridConvolutionFilter runs: its own previous output blended with each of
        // the last length values under a raised cosine, whose weights telescope across the window to exactly
        // 1. This was an error-weighted blend of an EMA and a WMA - a different filter altogether, so
        // MovingAvgType.HybridConvolutionFilter meant one thing on the span path and another on the list one.
        var resolved = Math.Max(length, 1);
        {
            for (var i = 0; i < input.Length; i++)
            {
                var prevOutput = i >= 1 ? output[i - 1] : input[i];

                double value = 0;
                for (var j = 1; j <= resolved; j++)
                {
                    var sign = 0.5 * (1 - Math.Cos((double)j / resolved * Math.PI));
                    var d = sign - (0.5 * (1 - Math.Cos((double)(j - 1) / resolved * Math.PI)));
                    var previousValue = i >= j - 1 ? input[i - (j - 1)] : 0;
                    value += ((sign * prevOutput) + ((1 - sign) * previousValue)) * d;
                }

                output[i] = value;
            }
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
            // Preserve this regression-based variant's partial-window fit.
            LinearRegression(input, lsmaBuffer.AsSpan(0, input.Length), length);

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
            // Preserve this regression-based variant's partial-window fit.
            LinearRegression(input, lsmaBuffer.AsSpan(0, input.Length), length);

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
            // Preserve this regression-based variant's partial-window fit.
            LinearRegression(input, lsmaBuffer.AsSpan(0, input.Length), length);
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
            // Preserve this regression-based variant's partial-window fit.
            LinearRegression(input, lsmaBuffer.AsSpan(0, input.Length), length);

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

        // Ehlers' noise elimination technology, the same as CalculateEhlersNoiseEliminationTechnology: a
        // Kendall-style count of how the last `length` values are ordered, scaled to [-1, 1]. This fast path
        // was an adaptive EMA of the input instead - a price-scale average where the indicator is an oscillator.
        length = Math.Max(length, 1);
        var denom = 0.5 * length * (length - 1);
        var xArray = new double[length + 1];

        for (var i = 0; i < input.Length; i++)
        {
            for (var j = 1; j <= length; j++)
            {
                xArray[j] = i >= j - 1 ? input[i - (j - 1)] : 0;
            }

            double num = 0;
            for (var j = 2; j <= length; j++)
            {
                for (var k = 1; k <= j - 1; k++)
                {
                    num -= Math.Sign(xArray[j] - xArray[k]);
                }
            }

            output[i] = denom != 0 ? num / denom : 0;
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

                var evwma = n > 0 ? prevEvwma + currentVolume / n * (price[i] - prevEvwma) : prevEvwma;
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

        length = Math.Max(1, length);
        double volumeSum = 0;
        double evwma = price.Length > 0 ? price[0] : 0;

        for (var i = 0; i < price.Length; i++)
        {
            var currentVolume = volume[i];
            volumeSum += currentVolume;

            if (i >= length)
                volumeSum -= volume[i - length];

            evwma = volumeSum > 0 ? evwma + currentVolume / volumeSum * (price[i] - evwma) : evwma;
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
    internal static void WindowedVolumeWeightedMovingAverage(ReadOnlySpan<double> price, ReadOnlySpan<double> volume, Span<double> output, int length = 100)
    {
        if (output.Length < price.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        if (volume.Length < price.Length)
            throw new ArgumentException("Volume span must be at least input length.", nameof(volume));
        length = Math.Max(1, length);
        for (var i = 0; i < price.Length; i++)
        {
            var mean = new ExactVolumeMean();
            for (var lag = 0; lag < length && lag <= i; lag++)
            {
                // Periodic Bartlett window: its common 2/length normalization cancels in the ratio.
                var taper = length == 1 ? 1 : Math.Min(lag, length - lag);
                mean.Add(price[i - lag], volume[i - lag], taper);
            }
            output[i] = mean.Value();
        }
    }

    /// <summary>
    /// Computes Middle High Low Moving Average.
    /// </summary>
    internal static void MiddleHighLowMovingAverage(ReadOnlySpan<double> input, Span<double> output, int length1 = 14, int length2 = 10)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        var midpointArray = ArrayPool<double>.Shared.Rent(input.Length);
        try
        {
            var midpoint = midpointArray.AsSpan(0, input.Length);
            TrendCore.Midpoint(input, midpoint, Math.Max(1, length2));
            ExponentialMovingAverage(midpoint, output, Math.Max(1, length1));
        }
        finally { ArrayPool<double>.Shared.Return(midpointArray); }
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
        => VolumeCore.VolumeWeightedAveragePrice(high, low, close, volume, output);


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

        if (input.IsEmpty) return;
        fastLength = Math.Max(1, fastLength);
        slowLength = Math.Max(1, slowLength);
        var fastAlpha = 2.0 / (1d + fastLength);
        var slowAlpha = 2.0 / (1d + slowLength);

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

                var pMacdEq = RoundedReverseMacd.Equilibrium(prevFastEma, prevSlowEma, fastAlpha, slowAlpha);
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
                // CalculateLightLeastSquaresMovingAverage draws its spread from GetStandardDeviationList,
                // which is zero until its window fills, so the correction and the base average are both zero
                // through the run-in. The shorter of the two averages fills first, and measuring it against
                // a partial spread here published a value from the halfway bar that the indicator never has.
                if (i < length - 1)
                {
                    output[i] = 0;
                    continue;
                }

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

        using var state = new Streaming.OvershootReductionMovingAverageState(length: length);
        for (var i = 0; i < input.Length; i++)
            output[i] = state.NextValue(input[i], true);
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
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        var window = new WilderSummationWindow(length);
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true);
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
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        if (input.Length == 0) return;
        using var shortMean = new Streaming.RoundedSimpleMovingAverageSmoother(Math.Max(1, shortLength));
        using var mediumMean = new Streaming.RoundedSimpleMovingAverageSmoother(Math.Max(1, mediumLength));
        for (var i = 0; i < input.Length; i++)
        {
            var first = shortMean.Next(input[i], true);
            var middle = mediumMean.Next(input[i], true);
            // The primary Curta output is independent of the separate Longa series.
            output[i] = middle == 0 ? 0 : first / middle;
        }
    }

    #endregion
}
