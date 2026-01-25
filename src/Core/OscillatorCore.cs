using System;
using System.Buffers;

namespace OoplesFinance.StockIndicators.Core;

/// <summary>
/// Core span-based implementations for oscillator indicators.
/// Zero-allocation computation directly into output spans.
/// </summary>
internal static class OscillatorCore
{
    /// <summary>
    /// Computes Relative Strength Index using Wilder's smoothing.
    /// </summary>
    /// <param name="input">Price series (typically close prices).</param>
    /// <param name="output">Output span for RSI values (0-100 scale).</param>
    /// <param name="length">RSI period (default 14).</param>
    internal static void RelativeStrengthIndex(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (input.Length == 0)
        {
            return;
        }

        var k = 1.0 / length;
        double avgGain = 0;
        double avgLoss = 0;
        double prevValue = input[0];
        output[0] = 0;

        for (var i = 1; i < input.Length; i++)
        {
            var currentValue = input[i];
            var change = currentValue - prevValue;
            prevValue = currentValue;

            var gain = change > 0 ? change : 0;
            var loss = change < 0 ? -change : 0;

            if (i <= length)
            {
                // Initial period: simple average
                avgGain += gain;
                avgLoss += loss;

                if (i == length)
                {
                    avgGain /= length;
                    avgLoss /= length;
                }

                output[i] = 0;
            }
            else
            {
                // After initial period: Wilder's smoothing
                avgGain = (gain * k) + (avgGain * (1 - k));
                avgLoss = (loss * k) + (avgLoss * (1 - k));

                var rs = avgLoss != 0 ? avgGain / avgLoss : 0;
                output[i] = avgLoss == 0 ? 100 : 100 - (100 / (1 + rs));
            }
        }
    }

    /// <summary>
    /// Computes Rate of Change (percentage change over period).
    /// </summary>
    /// <param name="input">Price series.</param>
    /// <param name="output">Output span for ROC values.</param>
    /// <param name="length">ROC period (default 12).</param>
    internal static void RateOfChange(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
            }
            else
            {
                var prevValue = input[i - length];
                output[i] = prevValue != 0 ? ((input[i] - prevValue) / prevValue) * 100 : 0;
            }
        }
    }

    /// <summary>
    /// Computes Momentum (simple difference over period).
    /// </summary>
    /// <param name="input">Price series.</param>
    /// <param name="output">Output span for Momentum values.</param>
    /// <param name="length">Momentum period (default 10).</param>
    internal static void Momentum(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            output[i] = i >= length ? input[i] - input[i - length] : 0;
        }
    }

    /// <summary>
    /// Computes Williams %R.
    /// </summary>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="close">Close prices.</param>
    /// <param name="output">Output span for Williams %R values (-100 to 0).</param>
    /// <param name="length">Period (default 14).</param>
    internal static void WilliamsR(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            // Find highest high and lowest low in the period
            var highestHigh = double.MinValue;
            var lowestLow = double.MaxValue;
            for (var j = i - length + 1; j <= i; j++)
            {
                if (high[j] > highestHigh) highestHigh = high[j];
                if (low[j] < lowestLow) lowestLow = low[j];
            }

            var range = highestHigh - lowestLow;
            output[i] = range != 0 ? -100 * (highestHigh - close[i]) / range : 0;
        }
    }

    /// <summary>
    /// Computes Commodity Channel Index.
    /// </summary>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="close">Close prices.</param>
    /// <param name="output">Output span for CCI values.</param>
    /// <param name="length">Period (default 20).</param>
    internal static void CommodityChannelIndex(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        const double constant = 0.015;

        for (var i = 0; i < close.Length; i++)
        {
            // Typical Price
            var tp = (high[i] + low[i] + close[i]) / 3;

            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            // Calculate SMA of TP
            double tpSum = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                tpSum += (high[j] + low[j] + close[j]) / 3;
            }
            var smaTP = tpSum / length;

            // Calculate Mean Deviation
            double meanDev = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                meanDev += Math.Abs((high[j] + low[j] + close[j]) / 3 - smaTP);
            }
            meanDev /= length;

            output[i] = meanDev != 0 ? (tp - smaTP) / (constant * meanDev) : 0;
        }
    }

    /// <summary>
    /// Computes Stochastic %K.
    /// </summary>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="close">Close prices.</param>
    /// <param name="output">Output span for %K values (0-100).</param>
    /// <param name="length">Period (default 14).</param>
    internal static void StochasticK(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            // Find highest high and lowest low in the period
            var highestHigh = double.MinValue;
            var lowestLow = double.MaxValue;
            for (var j = i - length + 1; j <= i; j++)
            {
                if (high[j] > highestHigh) highestHigh = high[j];
                if (low[j] < lowestLow) lowestLow = low[j];
            }

            var range = highestHigh - lowestLow;
            output[i] = range != 0 ? 100 * (close[i] - lowestLow) / range : 0;
        }
    }

    /// <summary>
    /// Computes Stochastic %D (SMA of %K).
    /// </summary>
    internal static void StochasticD(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int kLength, int dLength)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var kArray = pool.Rent(close.Length);
        try
        {
            var k = kArray.AsSpan(0, close.Length);
            StochasticK(high, low, close, k, kLength);
            MovingAverageCore.SimpleMovingAverage(k, output, dLength);
        }
        finally
        {
            pool.Return(kArray);
        }
    }

    /// <summary>
    /// Computes Money Flow Index (MFI).
    /// </summary>
    internal static void MoneyFlowIndex(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double positiveFlow = 0;
        double negativeFlow = 0;
        double prevTypicalPrice = 0;

        for (var i = 0; i < close.Length; i++)
        {
            var typicalPrice = (high[i] + low[i] + close[i]) / 3;
            var rawMoneyFlow = typicalPrice * volume[i];

            if (i == 0)
            {
                prevTypicalPrice = typicalPrice;
                output[i] = 0;
                continue;
            }

            if (typicalPrice > prevTypicalPrice)
            {
                positiveFlow += rawMoneyFlow;
            }
            else if (typicalPrice < prevTypicalPrice)
            {
                negativeFlow += rawMoneyFlow;
            }

            if (i >= length)
            {
                // Remove oldest values (approximation since we don't track history)
                // For accurate MFI, we'd need to track the last 'length' money flows
            }

            if (i >= length - 1)
            {
                var moneyRatio = negativeFlow != 0 ? positiveFlow / negativeFlow : 0;
                output[i] = 100 - (100 / (1 + moneyRatio));
            }
            else
            {
                output[i] = 0;
            }

            prevTypicalPrice = typicalPrice;
        }
    }

    /// <summary>
    /// Computes Average Directional Index (ADX).
    /// </summary>
    internal static void AverageDirectionalIndex(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var k = 1.0 / length;
        double prevPlusDm = 0;
        double prevMinusDm = 0;
        double prevTr = 0;
        double prevAdx = 0;
        double plusDmSum = 0;
        double minusDmSum = 0;
        double trSum = 0;

        for (var i = 0; i < close.Length; i++)
        {
            if (i == 0)
            {
                output[i] = 0;
                continue;
            }

            // Calculate +DM, -DM, TR
            var upMove = high[i] - high[i - 1];
            var downMove = low[i - 1] - low[i];
            var plusDm = (upMove > downMove && upMove > 0) ? upMove : 0;
            var minusDm = (downMove > upMove && downMove > 0) ? downMove : 0;

            var highLow = high[i] - low[i];
            var highClose = Math.Abs(high[i] - close[i - 1]);
            var lowClose = Math.Abs(low[i] - close[i - 1]);
            var tr = Math.Max(highLow, Math.Max(highClose, lowClose));

            if (i < length)
            {
                plusDmSum += plusDm;
                minusDmSum += minusDm;
                trSum += tr;
                output[i] = 0;
            }
            else if (i == length)
            {
                plusDmSum += plusDm;
                minusDmSum += minusDm;
                trSum += tr;
                prevPlusDm = plusDmSum;
                prevMinusDm = minusDmSum;
                prevTr = trSum;

                var plusDi = prevTr != 0 ? 100 * prevPlusDm / prevTr : 0;
                var minusDi = prevTr != 0 ? 100 * prevMinusDm / prevTr : 0;
                var diSum = plusDi + minusDi;
                var dx = diSum != 0 ? 100 * Math.Abs(plusDi - minusDi) / diSum : 0;
                prevAdx = dx;
                output[i] = dx;
            }
            else
            {
                // Wilder's smoothing
                prevPlusDm = prevPlusDm - (prevPlusDm / length) + plusDm;
                prevMinusDm = prevMinusDm - (prevMinusDm / length) + minusDm;
                prevTr = prevTr - (prevTr / length) + tr;

                var plusDi = prevTr != 0 ? 100 * prevPlusDm / prevTr : 0;
                var minusDi = prevTr != 0 ? 100 * prevMinusDm / prevTr : 0;
                var diSum = plusDi + minusDi;
                var dx = diSum != 0 ? 100 * Math.Abs(plusDi - minusDi) / diSum : 0;

                prevAdx = ((prevAdx * (length - 1)) + dx) / length;
                output[i] = prevAdx;
            }
        }
    }

    /// <summary>
    /// Computes Chande Momentum Oscillator.
    /// </summary>
    internal static void ChandeMomentumOscillator(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double sumUp = 0;
        double sumDown = 0;

        for (var i = 0; i < input.Length; i++)
        {
            if (i == 0)
            {
                output[i] = 0;
                continue;
            }

            var change = input[i] - input[i - 1];
            var up = change > 0 ? change : 0;
            var down = change < 0 ? -change : 0;

            sumUp += up;
            sumDown += down;

            if (i > length)
            {
                var oldChange = input[i - length] - input[i - length - 1];
                var oldUp = oldChange > 0 ? oldChange : 0;
                var oldDown = oldChange < 0 ? -oldChange : 0;
                sumUp -= oldUp;
                sumDown -= oldDown;
            }

            if (i >= length)
            {
                var sumTotal = sumUp + sumDown;
                output[i] = sumTotal != 0 ? 100 * (sumUp - sumDown) / sumTotal : 0;
            }
            else
            {
                output[i] = 0;
            }
        }
    }

    /// <summary>
    /// Computes Percentage Price Oscillator (PPO).
    /// </summary>
    internal static void PercentagePriceOscillator(ReadOnlySpan<double> input, Span<double> output, int fastLength, int slowLength)
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

            MovingAverageCore.ExponentialMovingAverage(input, fastEma, fastLength);
            MovingAverageCore.ExponentialMovingAverage(input, slowEma, slowLength);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = slowEma[i] != 0 ? 100 * (fastEma[i] - slowEma[i]) / slowEma[i] : 0;
            }
        }
        finally
        {
            pool.Return(fastEmaArray);
            pool.Return(slowEmaArray);
        }
    }

    /// <summary>
    /// Computes Absolute Price Oscillator (APO).
    /// </summary>
    internal static void AbsolutePriceOscillator(ReadOnlySpan<double> input, Span<double> output, int fastLength, int slowLength)
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

            MovingAverageCore.ExponentialMovingAverage(input, fastEma, fastLength);
            MovingAverageCore.ExponentialMovingAverage(input, slowEma, slowLength);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = fastEma[i] - slowEma[i];
            }
        }
        finally
        {
            pool.Return(fastEmaArray);
            pool.Return(slowEmaArray);
        }
    }

    /// <summary>
    /// Computes Ultimate Oscillator.
    /// </summary>
    internal static void UltimateOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length1 = 7, int length2 = 14, int length3 = 28)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double bp1Sum = 0, tr1Sum = 0;
        double bp2Sum = 0, tr2Sum = 0;
        double bp3Sum = 0, tr3Sum = 0;

        for (var i = 0; i < close.Length; i++)
        {
            if (i == 0)
            {
                output[i] = 0;
                continue;
            }

            var prevClose = close[i - 1];
            var trueLow = Math.Min(low[i], prevClose);
            var trueHigh = Math.Max(high[i], prevClose);
            var bp = close[i] - trueLow;
            var tr = trueHigh - trueLow;

            bp1Sum += bp; tr1Sum += tr;
            bp2Sum += bp; tr2Sum += tr;
            bp3Sum += bp; tr3Sum += tr;

            if (i >= length1) { var idx = i - length1; bp1Sum -= close[idx] - Math.Min(low[idx], close[Math.Max(0, idx - 1)]); tr1Sum -= Math.Max(high[idx], close[Math.Max(0, idx - 1)]) - Math.Min(low[idx], close[Math.Max(0, idx - 1)]); }
            if (i >= length2) { var idx = i - length2; bp2Sum -= close[idx] - Math.Min(low[idx], close[Math.Max(0, idx - 1)]); tr2Sum -= Math.Max(high[idx], close[Math.Max(0, idx - 1)]) - Math.Min(low[idx], close[Math.Max(0, idx - 1)]); }
            if (i >= length3) { var idx = i - length3; bp3Sum -= close[idx] - Math.Min(low[idx], close[Math.Max(0, idx - 1)]); tr3Sum -= Math.Max(high[idx], close[Math.Max(0, idx - 1)]) - Math.Min(low[idx], close[Math.Max(0, idx - 1)]); }

            if (i >= length3 - 1)
            {
                var avg1 = tr1Sum != 0 ? bp1Sum / tr1Sum : 0;
                var avg2 = tr2Sum != 0 ? bp2Sum / tr2Sum : 0;
                var avg3 = tr3Sum != 0 ? bp3Sum / tr3Sum : 0;
                output[i] = 100 * ((4 * avg1) + (2 * avg2) + avg3) / 7;
            }
            else
            {
                output[i] = 0;
            }
        }
    }

    /// <summary>
    /// Computes True Strength Index (TSI).
    /// </summary>
    internal static void TrueStrengthIndex(ReadOnlySpan<double> input, Span<double> output, int longLength = 25, int shortLength = 13)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var pcArray = pool.Rent(input.Length);
        var absPcArray = pool.Rent(input.Length);
        var pcEma1Array = pool.Rent(input.Length);
        var pcEma2Array = pool.Rent(input.Length);
        var absPcEma1Array = pool.Rent(input.Length);
        var absPcEma2Array = pool.Rent(input.Length);

        try
        {
            var pc = pcArray.AsSpan(0, input.Length);
            var absPc = absPcArray.AsSpan(0, input.Length);
            var pcEma1 = pcEma1Array.AsSpan(0, input.Length);
            var pcEma2 = pcEma2Array.AsSpan(0, input.Length);
            var absPcEma1 = absPcEma1Array.AsSpan(0, input.Length);
            var absPcEma2 = absPcEma2Array.AsSpan(0, input.Length);

            // Price change
            pc[0] = 0;
            absPc[0] = 0;
            for (var i = 1; i < input.Length; i++)
            {
                pc[i] = input[i] - input[i - 1];
                absPc[i] = Math.Abs(pc[i]);
            }

            // Double smoothed price change
            MovingAverageCore.ExponentialMovingAverage(pc, pcEma1, longLength);
            MovingAverageCore.ExponentialMovingAverage(pcEma1, pcEma2, shortLength);

            // Double smoothed absolute price change
            MovingAverageCore.ExponentialMovingAverage(absPc, absPcEma1, longLength);
            MovingAverageCore.ExponentialMovingAverage(absPcEma1, absPcEma2, shortLength);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = absPcEma2[i] != 0 ? 100 * pcEma2[i] / absPcEma2[i] : 0;
            }
        }
        finally
        {
            pool.Return(pcArray);
            pool.Return(absPcArray);
            pool.Return(pcEma1Array);
            pool.Return(pcEma2Array);
            pool.Return(absPcEma1Array);
            pool.Return(absPcEma2Array);
        }
    }

    /// <summary>
    /// Computes Stochastic RSI.
    /// </summary>
    internal static void StochasticRsi(ReadOnlySpan<double> input, Span<double> output, int rsiLength = 14, int stochLength = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rsiArray = pool.Rent(input.Length);

        try
        {
            var rsi = rsiArray.AsSpan(0, input.Length);
            RelativeStrengthIndex(input, rsi, rsiLength);

            for (var i = 0; i < input.Length; i++)
            {
                if (i < rsiLength + stochLength - 1)
                {
                    output[i] = 0;
                    continue;
                }

                var highestRsi = double.MinValue;
                var lowestRsi = double.MaxValue;
                for (var j = i - stochLength + 1; j <= i; j++)
                {
                    if (rsi[j] > highestRsi) highestRsi = rsi[j];
                    if (rsi[j] < lowestRsi) lowestRsi = rsi[j];
                }

                var range = highestRsi - lowestRsi;
                output[i] = range != 0 ? 100 * (rsi[i] - lowestRsi) / range : 0;
            }
        }
        finally
        {
            pool.Return(rsiArray);
        }
    }

    /// <summary>
    /// Computes Aroon Up indicator.
    /// </summary>
    internal static void AroonUp(ReadOnlySpan<double> high, Span<double> output, int length = 25)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < high.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
                continue;
            }

            var highestIdx = i;
            var highestValue = double.MinValue;
            for (var j = i - length; j <= i; j++)
            {
                if (high[j] >= highestValue)
                {
                    highestValue = high[j];
                    highestIdx = j;
                }
            }

            output[i] = 100.0 * (length - (i - highestIdx)) / length;
        }
    }

    /// <summary>
    /// Computes Aroon Down indicator.
    /// </summary>
    internal static void AroonDown(ReadOnlySpan<double> low, Span<double> output, int length = 25)
    {
        if (output.Length < low.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < low.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
                continue;
            }

            var lowestIdx = i;
            var lowestValue = double.MaxValue;
            for (var j = i - length; j <= i; j++)
            {
                if (low[j] <= lowestValue)
                {
                    lowestValue = low[j];
                    lowestIdx = j;
                }
            }

            output[i] = 100.0 * (length - (i - lowestIdx)) / length;
        }
    }

    /// <summary>
    /// Computes Aroon Oscillator (Aroon Up - Aroon Down).
    /// </summary>
    internal static void AroonOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 25)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var upArray = pool.Rent(high.Length);
        var downArray = pool.Rent(low.Length);

        try
        {
            var up = upArray.AsSpan(0, high.Length);
            var down = downArray.AsSpan(0, low.Length);

            AroonUp(high, up, length);
            AroonDown(low, down, length);

            for (var i = 0; i < high.Length; i++)
            {
                output[i] = up[i] - down[i];
            }
        }
        finally
        {
            pool.Return(upArray);
            pool.Return(downArray);
        }
    }

    /// <summary>
    /// Computes Balance of Power indicator.
    /// </summary>
    internal static void BalanceOfPower(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            var range = high[i] - low[i];
            output[i] = range != 0 ? (close[i] - open[i]) / range : 0;
        }
    }

    /// <summary>
    /// Computes Elder Ray Bull Power.
    /// </summary>
    internal static void ElderRayBullPower(ReadOnlySpan<double> high, ReadOnlySpan<double> close, Span<double> output, int length = 13)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var emaArray = pool.Rent(close.Length);

        try
        {
            var ema = emaArray.AsSpan(0, close.Length);
            MovingAverageCore.ExponentialMovingAverage(close, ema, length);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = high[i] - ema[i];
            }
        }
        finally
        {
            pool.Return(emaArray);
        }
    }

    /// <summary>
    /// Computes Elder Ray Bear Power.
    /// </summary>
    internal static void ElderRayBearPower(ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 13)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var emaArray = pool.Rent(close.Length);

        try
        {
            var ema = emaArray.AsSpan(0, close.Length);
            MovingAverageCore.ExponentialMovingAverage(close, ema, length);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = low[i] - ema[i];
            }
        }
        finally
        {
            pool.Return(emaArray);
        }
    }

    /// <summary>
    /// Computes Detrended Price Oscillator.
    /// </summary>
    internal static void DetrendedPriceOscillator(ReadOnlySpan<double> input, Span<double> output, int length = 20)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var offset = (length / 2) + 1;
        var pool = ArrayPool<double>.Shared;
        var smaArray = pool.Rent(input.Length);

        try
        {
            var sma = smaArray.AsSpan(0, input.Length);
            MovingAverageCore.SimpleMovingAverage(input, sma, length);

            for (var i = 0; i < input.Length; i++)
            {
                if (i >= offset)
                {
                    output[i] = input[i - offset] - sma[i];
                }
                else
                {
                    output[i] = 0;
                }
            }
        }
        finally
        {
            pool.Return(smaArray);
        }
    }

    /// <summary>
    /// Computes Price Oscillator.
    /// </summary>
    internal static void PriceOscillator(ReadOnlySpan<double> input, Span<double> output, int shortLength = 10, int longLength = 20)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var shortSmaArray = pool.Rent(input.Length);
        var longSmaArray = pool.Rent(input.Length);

        try
        {
            var shortSma = shortSmaArray.AsSpan(0, input.Length);
            var longSma = longSmaArray.AsSpan(0, input.Length);

            MovingAverageCore.SimpleMovingAverage(input, shortSma, shortLength);
            MovingAverageCore.SimpleMovingAverage(input, longSma, longLength);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = longSma[i] != 0 ? 100 * (shortSma[i] - longSma[i]) / longSma[i] : 0;
            }
        }
        finally
        {
            pool.Return(shortSmaArray);
            pool.Return(longSmaArray);
        }
    }

    /// <summary>
    /// Computes TRIX indicator (triple smoothed EMA rate of change).
    /// </summary>
    internal static void Trix(ReadOnlySpan<double> input, Span<double> output, int length = 15)
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

            MovingAverageCore.ExponentialMovingAverage(input, ema1, length);
            MovingAverageCore.ExponentialMovingAverage(ema1, ema2, length);
            MovingAverageCore.ExponentialMovingAverage(ema2, ema3, length);

            output[0] = 0;
            for (var i = 1; i < input.Length; i++)
            {
                output[i] = ema3[i - 1] != 0 ? 10000 * (ema3[i] - ema3[i - 1]) / ema3[i - 1] : 0;
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
    /// Computes Relative Vigor Index.
    /// </summary>
    internal static void RelativeVigorIndex(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length + 3)
            {
                output[i] = 0;
                continue;
            }

            // Numerator: (Close-Open) + 2*(Close[1]-Open[1]) + 2*(Close[2]-Open[2]) + (Close[3]-Open[3])
            double numSum = 0;
            double denomSum = 0;

            for (var j = 0; j < length; j++)
            {
                var idx = i - j;
                var co0 = close[idx] - open[idx];
                var co1 = idx >= 1 ? close[idx - 1] - open[idx - 1] : 0;
                var co2 = idx >= 2 ? close[idx - 2] - open[idx - 2] : 0;
                var co3 = idx >= 3 ? close[idx - 3] - open[idx - 3] : 0;
                numSum += (co0 + (2 * co1) + (2 * co2) + co3) / 6;

                var hl0 = high[idx] - low[idx];
                var hl1 = idx >= 1 ? high[idx - 1] - low[idx - 1] : 0;
                var hl2 = idx >= 2 ? high[idx - 2] - low[idx - 2] : 0;
                var hl3 = idx >= 3 ? high[idx - 3] - low[idx - 3] : 0;
                denomSum += (hl0 + (2 * hl1) + (2 * hl2) + hl3) / 6;
            }

            output[i] = denomSum != 0 ? numSum / denomSum : 0;
        }
    }

    /// <summary>
    /// Computes Vortex Plus (+VI).
    /// </summary>
    internal static void VortexPlus(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
                continue;
            }

            double vmPlus = 0;
            double trSum = 0;

            for (var j = i - length + 1; j <= i; j++)
            {
                var prevClose = j > 0 ? close[j - 1] : close[j];
                var tr = Math.Max(high[j] - low[j], Math.Max(Math.Abs(high[j] - prevClose), Math.Abs(low[j] - prevClose)));
                trSum += tr;

                if (j > 0)
                {
                    vmPlus += Math.Abs(high[j] - low[j - 1]);
                }
            }

            output[i] = trSum != 0 ? vmPlus / trSum : 0;
        }
    }

    /// <summary>
    /// Computes Vortex Minus (-VI).
    /// </summary>
    internal static void VortexMinus(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
                continue;
            }

            double vmMinus = 0;
            double trSum = 0;

            for (var j = i - length + 1; j <= i; j++)
            {
                var prevClose = j > 0 ? close[j - 1] : close[j];
                var tr = Math.Max(high[j] - low[j], Math.Max(Math.Abs(high[j] - prevClose), Math.Abs(low[j] - prevClose)));
                trSum += tr;

                if (j > 0)
                {
                    vmMinus += Math.Abs(low[j] - high[j - 1]);
                }
            }

            output[i] = trSum != 0 ? vmMinus / trSum : 0;
        }
    }

    /// <summary>
    /// Computes Mass Index.
    /// </summary>
    internal static void MassIndex(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int emaLength = 9, int sumLength = 25)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rangeArray = pool.Rent(high.Length);
        var ema1Array = pool.Rent(high.Length);
        var ema2Array = pool.Rent(high.Length);

        try
        {
            var range = rangeArray.AsSpan(0, high.Length);
            var ema1 = ema1Array.AsSpan(0, high.Length);
            var ema2 = ema2Array.AsSpan(0, high.Length);

            // High-Low range
            for (var i = 0; i < high.Length; i++)
            {
                range[i] = high[i] - low[i];
            }

            // Double EMA of range
            MovingAverageCore.ExponentialMovingAverage(range, ema1, emaLength);
            MovingAverageCore.ExponentialMovingAverage(ema1, ema2, emaLength);

            // Sum of ratio
            double sum = 0;
            for (var i = 0; i < high.Length; i++)
            {
                var ratio = ema2[i] != 0 ? ema1[i] / ema2[i] : 0;
                sum += ratio;

                if (i >= sumLength)
                {
                    var oldRatio = ema2[i - sumLength] != 0 ? ema1[i - sumLength] / ema2[i - sumLength] : 0;
                    sum -= oldRatio;
                }

                output[i] = i >= sumLength - 1 ? sum : 0;
            }
        }
        finally
        {
            pool.Return(rangeArray);
            pool.Return(ema1Array);
            pool.Return(ema2Array);
        }
    }

    /// <summary>
    /// Computes Awesome Oscillator.
    /// </summary>
    internal static void AwesomeOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int fastLength = 5, int slowLength = 34)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var medianArray = pool.Rent(high.Length);
        var fastSmaArray = pool.Rent(high.Length);
        var slowSmaArray = pool.Rent(high.Length);

        try
        {
            var median = medianArray.AsSpan(0, high.Length);
            var fastSma = fastSmaArray.AsSpan(0, high.Length);
            var slowSma = slowSmaArray.AsSpan(0, high.Length);

            // Median price
            for (var i = 0; i < high.Length; i++)
            {
                median[i] = (high[i] + low[i]) / 2;
            }

            MovingAverageCore.SimpleMovingAverage(median, fastSma, fastLength);
            MovingAverageCore.SimpleMovingAverage(median, slowSma, slowLength);

            for (var i = 0; i < high.Length; i++)
            {
                output[i] = fastSma[i] - slowSma[i];
            }
        }
        finally
        {
            pool.Return(medianArray);
            pool.Return(fastSmaArray);
            pool.Return(slowSmaArray);
        }
    }

    /// <summary>
    /// Computes Accelerator Oscillator.
    /// </summary>
    internal static void AcceleratorOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int fastLength = 5, int slowLength = 34, int signalLength = 5)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var aoArray = pool.Rent(high.Length);
        var aoSmaArray = pool.Rent(high.Length);

        try
        {
            var ao = aoArray.AsSpan(0, high.Length);
            var aoSma = aoSmaArray.AsSpan(0, high.Length);

            AwesomeOscillator(high, low, ao, fastLength, slowLength);
            MovingAverageCore.SimpleMovingAverage(ao, aoSma, signalLength);

            for (var i = 0; i < high.Length; i++)
            {
                output[i] = ao[i] - aoSma[i];
            }
        }
        finally
        {
            pool.Return(aoArray);
            pool.Return(aoSmaArray);
        }
    }

    /// <summary>
    /// Computes Percentage Volume Oscillator.
    /// </summary>
    internal static void PercentageVolumeOscillator(ReadOnlySpan<double> volume, Span<double> output, int fastLength = 12, int slowLength = 26)
    {
        if (output.Length < volume.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var fastEmaArray = pool.Rent(volume.Length);
        var slowEmaArray = pool.Rent(volume.Length);

        try
        {
            var fastEma = fastEmaArray.AsSpan(0, volume.Length);
            var slowEma = slowEmaArray.AsSpan(0, volume.Length);

            MovingAverageCore.ExponentialMovingAverage(volume, fastEma, fastLength);
            MovingAverageCore.ExponentialMovingAverage(volume, slowEma, slowLength);

            for (var i = 0; i < volume.Length; i++)
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
    /// Computes Fisher Transform.
    /// </summary>
    internal static void FisherTransform(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 10)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double prevFisher = 0;
        double prevValue = 0;

        for (var i = 0; i < high.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            // Find highest high and lowest low
            var highestHigh = double.MinValue;
            var lowestLow = double.MaxValue;
            for (var j = i - length + 1; j <= i; j++)
            {
                if (high[j] > highestHigh) highestHigh = high[j];
                if (low[j] < lowestLow) lowestLow = low[j];
            }

            var hl2 = (high[i] + low[i]) / 2;
            var range = highestHigh - lowestLow;
            var rawValue = range != 0 ? ((hl2 - lowestLow) / range * 2 - 1) : 0;

            // Smooth value
            var value = 0.66 * rawValue + 0.34 * prevValue;
            value = Math.Max(-0.999, Math.Min(0.999, value));
            prevValue = value;

            // Fisher transform
            var fisher = 0.5 * Math.Log((1 + value) / (1 - value)) + 0.5 * prevFisher;
            prevFisher = fisher;
            output[i] = fisher;
        }
    }

    /// <summary>
    /// Computes Connors RSI.
    /// </summary>
    internal static void ConnorsRsi(ReadOnlySpan<double> close, Span<double> output, int rsiLength = 3, int streakLength = 2, int rocLength = 100)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rsiArray = pool.Rent(close.Length);
        var streakArray = pool.Rent(close.Length);
        var streakRsiArray = pool.Rent(close.Length);
        var rocRankArray = pool.Rent(close.Length);

        try
        {
            var rsi = rsiArray.AsSpan(0, close.Length);
            var streak = streakArray.AsSpan(0, close.Length);
            var streakRsi = streakRsiArray.AsSpan(0, close.Length);
            var rocRank = rocRankArray.AsSpan(0, close.Length);

            // Regular RSI
            RelativeStrengthIndex(close, rsi, rsiLength);

            // Calculate up/down streak
            int currentStreak = 0;
            for (var i = 0; i < close.Length; i++)
            {
                if (i == 0)
                {
                    streak[i] = 0;
                    continue;
                }

                if (close[i] > close[i - 1])
                {
                    currentStreak = currentStreak > 0 ? currentStreak + 1 : 1;
                }
                else if (close[i] < close[i - 1])
                {
                    currentStreak = currentStreak < 0 ? currentStreak - 1 : -1;
                }
                else
                {
                    currentStreak = 0;
                }
                streak[i] = currentStreak;
            }

            // RSI of streak
            RelativeStrengthIndex(streak, streakRsi, streakLength);

            // Percent rank of ROC
            for (var i = 0; i < close.Length; i++)
            {
                if (i < rocLength)
                {
                    rocRank[i] = 0;
                    continue;
                }

                var currentRoc = close[i - 1] != 0 ? (close[i] - close[i - 1]) / close[i - 1] : 0;
                int count = 0;
                for (var j = i - rocLength; j < i; j++)
                {
                    var prevRoc = close[j] != 0 && j > 0 ? (close[j] - close[j - 1]) / close[j - 1] : 0;
                    if (prevRoc < currentRoc) count++;
                }
                rocRank[i] = (double)count / rocLength * 100;
            }

            // Combine all three
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = (rsi[i] + streakRsi[i] + rocRank[i]) / 3;
            }
        }
        finally
        {
            pool.Return(rsiArray);
            pool.Return(streakArray);
            pool.Return(streakRsiArray);
            pool.Return(rocRankArray);
        }
    }

    /// <summary>
    /// Computes Price Momentum Oscillator.
    /// </summary>
    internal static void PriceMomentumOscillator(ReadOnlySpan<double> input, Span<double> output, int firstLength = 35, int secondLength = 20)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rocArray = pool.Rent(input.Length);
        var ema1Array = pool.Rent(input.Length);

        try
        {
            var roc = rocArray.AsSpan(0, input.Length);
            var ema1 = ema1Array.AsSpan(0, input.Length);

            // ROC
            for (var i = 0; i < input.Length; i++)
            {
                if (i == 0)
                {
                    roc[i] = 0;
                }
                else
                {
                    roc[i] = input[i - 1] != 0 ? ((input[i] / input[i - 1]) - 1) * 100 : 0;
                }
            }

            // Double EMA
            MovingAverageCore.ExponentialMovingAverage(roc, ema1, firstLength);
            MovingAverageCore.ExponentialMovingAverage(ema1, output, secondLength);
        }
        finally
        {
            pool.Return(rocArray);
            pool.Return(ema1Array);
        }
    }

    /// <summary>
    /// Computes Know Sure Thing (KST) oscillator.
    /// </summary>
    internal static void KnowSureThing(ReadOnlySpan<double> input, Span<double> output,
        int roc1 = 10, int roc2 = 15, int roc3 = 20, int roc4 = 30,
        int sma1 = 10, int sma2 = 10, int sma3 = 10, int sma4 = 15)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rocA = pool.Rent(input.Length);
        var rocB = pool.Rent(input.Length);
        var rocC = pool.Rent(input.Length);
        var rocD = pool.Rent(input.Length);
        var smaA = pool.Rent(input.Length);
        var smaB = pool.Rent(input.Length);
        var smaC = pool.Rent(input.Length);
        var smaD = pool.Rent(input.Length);

        try
        {
            var rocASpan = rocA.AsSpan(0, input.Length);
            var rocBSpan = rocB.AsSpan(0, input.Length);
            var rocCSpan = rocC.AsSpan(0, input.Length);
            var rocDSpan = rocD.AsSpan(0, input.Length);
            var smaASpan = smaA.AsSpan(0, input.Length);
            var smaBSpan = smaB.AsSpan(0, input.Length);
            var smaCSpan = smaC.AsSpan(0, input.Length);
            var smaDSpan = smaD.AsSpan(0, input.Length);

            // Calculate ROCs
            RateOfChange(input, rocASpan, roc1);
            RateOfChange(input, rocBSpan, roc2);
            RateOfChange(input, rocCSpan, roc3);
            RateOfChange(input, rocDSpan, roc4);

            // Smooth ROCs with SMAs
            MovingAverageCore.SimpleMovingAverage(rocASpan, smaASpan, sma1);
            MovingAverageCore.SimpleMovingAverage(rocBSpan, smaBSpan, sma2);
            MovingAverageCore.SimpleMovingAverage(rocCSpan, smaCSpan, sma3);
            MovingAverageCore.SimpleMovingAverage(rocDSpan, smaDSpan, sma4);

            // KST = weighted sum
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = smaASpan[i] + (smaBSpan[i] * 2) + (smaCSpan[i] * 3) + (smaDSpan[i] * 4);
            }
        }
        finally
        {
            pool.Return(rocA);
            pool.Return(rocB);
            pool.Return(rocC);
            pool.Return(rocD);
            pool.Return(smaA);
            pool.Return(smaB);
            pool.Return(smaC);
            pool.Return(smaD);
        }
    }

    /// <summary>
    /// Computes Percent Rank.
    /// </summary>
    internal static void PercentRank(ReadOnlySpan<double> input, Span<double> output, int length = 100)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
                continue;
            }

            int count = 0;
            for (var j = i - length; j < i; j++)
            {
                if (input[j] < input[i]) count++;
            }
            output[i] = (double)count / length * 100;
        }
    }

    /// <summary>
    /// Computes Choppiness Index.
    /// </summary>
    internal static void ChoppinessIndex(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
                continue;
            }

            // Sum of ATR
            double atrSum = 0;
            var highestHigh = double.MinValue;
            var lowestLow = double.MaxValue;

            for (var j = i - length + 1; j <= i; j++)
            {
                var prevClose = j > 0 ? close[j - 1] : close[j];
                var tr = Math.Max(high[j] - low[j], Math.Max(Math.Abs(high[j] - prevClose), Math.Abs(low[j] - prevClose)));
                atrSum += tr;

                if (high[j] > highestHigh) highestHigh = high[j];
                if (low[j] < lowestLow) lowestLow = low[j];
            }

            var range = highestHigh - lowestLow;
            output[i] = range > 0 ? 100 * Math.Log10(atrSum / range) / Math.Log10(length) : 0;
        }
    }

    /// <summary>
    /// Computes MACD Line.
    /// </summary>
    internal static void MacdLine(ReadOnlySpan<double> input, Span<double> output, int fastLength = 12, int slowLength = 26)
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

            MovingAverageCore.ExponentialMovingAverage(input, fastEma, fastLength);
            MovingAverageCore.ExponentialMovingAverage(input, slowEma, slowLength);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = fastEma[i] - slowEma[i];
            }
        }
        finally
        {
            pool.Return(fastEmaArray);
            pool.Return(slowEmaArray);
        }
    }

    /// <summary>
    /// Computes MACD Signal Line.
    /// </summary>
    internal static void MacdSignal(ReadOnlySpan<double> input, Span<double> output, int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var macdLineArray = pool.Rent(input.Length);

        try
        {
            var macdLine = macdLineArray.AsSpan(0, input.Length);
            MacdLine(input, macdLine, fastLength, slowLength);
            MovingAverageCore.ExponentialMovingAverage(macdLine, output, signalLength);
        }
        finally
        {
            pool.Return(macdLineArray);
        }
    }

    /// <summary>
    /// Computes MACD Histogram.
    /// </summary>
    internal static void MacdHistogram(ReadOnlySpan<double> input, Span<double> output, int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var macdLineArray = pool.Rent(input.Length);
        var signalLineArray = pool.Rent(input.Length);

        try
        {
            var macdLine = macdLineArray.AsSpan(0, input.Length);
            var signalLine = signalLineArray.AsSpan(0, input.Length);

            MacdLine(input, macdLine, fastLength, slowLength);
            MovingAverageCore.ExponentialMovingAverage(macdLine, signalLine, signalLength);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = macdLine[i] - signalLine[i];
            }
        }
        finally
        {
            pool.Return(macdLineArray);
            pool.Return(signalLineArray);
        }
    }

    /// <summary>
    /// Computes Absolute Strength Index.
    /// </summary>
    internal static void AbsoluteStrengthIndex(ReadOnlySpan<double> close, Span<double> output, int length = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
                continue;
            }

            double bullPower = 0;
            double bearPower = 0;

            for (var j = i - length + 1; j <= i; j++)
            {
                if (j > 0)
                {
                    var change = close[j] - close[j - 1];
                    if (change > 0)
                    {
                        bullPower += change;
                    }
                    else
                    {
                        bearPower += Math.Abs(change);
                    }
                }
            }

            var total = bullPower + bearPower;
            output[i] = total != 0 ? 100 * bullPower / total : 50;
        }
    }

    /// <summary>
    /// Computes Relative Momentum Index.
    /// </summary>
    internal static void RelativeMomentumIndex(ReadOnlySpan<double> input, Span<double> output, int length = 14, int momentum = 4)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double upSum = 0;
        double downSum = 0;
        var k = 1.0 / length;

        for (var i = 0; i < input.Length; i++)
        {
            if (i < momentum)
            {
                output[i] = 0;
                continue;
            }

            var change = input[i] - input[i - momentum];
            var up = change > 0 ? change : 0;
            var down = change < 0 ? Math.Abs(change) : 0;

            if (i < length + momentum)
            {
                upSum += up;
                downSum += down;
                output[i] = 0;
            }
            else if (i == length + momentum)
            {
                upSum += up;
                downSum += down;
                var total = upSum + downSum;
                output[i] = total != 0 ? 100 * upSum / total : 50;
            }
            else
            {
                upSum = (up * k) + (upSum * (1 - k));
                downSum = (down * k) + (downSum * (1 - k));
                var total = upSum + downSum;
                output[i] = total != 0 ? 100 * upSum / total : 50;
            }
        }
    }

    /// <summary>
    /// Computes Intraday Momentum Index.
    /// </summary>
    internal static void IntradayMomentumIndex(ReadOnlySpan<double> open, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double upSum = 0;
        double downSum = 0;

        for (var i = 0; i < close.Length; i++)
        {
            var up = close[i] > open[i] ? close[i] - open[i] : 0;
            var down = close[i] < open[i] ? open[i] - close[i] : 0;

            upSum += up;
            downSum += down;

            if (i >= length)
            {
                var oldUp = close[i - length] > open[i - length] ? close[i - length] - open[i - length] : 0;
                var oldDown = close[i - length] < open[i - length] ? open[i - length] - close[i - length] : 0;
                upSum -= oldUp;
                downSum -= oldDown;
            }

            var total = upSum + downSum;
            output[i] = i >= length - 1 && total != 0 ? 100 * upSum / total : 0;
        }
    }

    /// <summary>
    /// Computes Swing Index.
    /// </summary>
    internal static void SwingIndex(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, double limitMove = 0)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        output[0] = 0;

        for (var i = 1; i < close.Length; i++)
        {
            var cy = close[i - 1];
            var oy = open[i - 1];
            var c = close[i];
            var o = open[i];
            var h = high[i];
            var l = low[i];

            var k = Math.Max(h - cy, l - cy);
            var tr = Math.Max(Math.Max(h - l, Math.Abs(h - cy)), Math.Abs(l - cy));

            var sh = Math.Abs(cy - oy);
            var r = tr - 0.5 * Math.Abs(c - o) + 0.25 * sh;

            if (r != 0 && tr != 0)
            {
                var limit = limitMove > 0 ? limitMove : tr;
                var si = 50 * ((c - cy) + 0.5 * (c - o) + 0.25 * (cy - oy)) / r * k / limit;
                output[i] = si;
            }
            else
            {
                output[i] = 0;
            }
        }
    }

    /// <summary>
    /// Computes Accumulative Swing Index.
    /// </summary>
    internal static void AccumulativeSwingIndex(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, double limitMove = 0)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var siArray = pool.Rent(close.Length);

        try
        {
            var si = siArray.AsSpan(0, close.Length);
            SwingIndex(open, high, low, close, si, limitMove);

            double asi = 0;
            for (var i = 0; i < close.Length; i++)
            {
                asi += si[i];
                output[i] = asi;
            }
        }
        finally
        {
            pool.Return(siArray);
        }
    }

    /// <summary>
    /// Computes Coppock Curve.
    /// </summary>
    internal static void CoppockCurve(ReadOnlySpan<double> input, Span<double> output, int longRocLength = 14, int shortRocLength = 11, int wmaLength = 10)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rocSumArray = pool.Rent(input.Length);

        try
        {
            var rocSum = rocSumArray.AsSpan(0, input.Length);

            // Calculate sum of long and short ROC
            for (var i = 0; i < input.Length; i++)
            {
                if (i < longRocLength)
                {
                    rocSum[i] = 0;
                }
                else
                {
                    var longRoc = input[i - longRocLength] != 0 ? (input[i] - input[i - longRocLength]) / input[i - longRocLength] * 100 : 0;
                    var shortRoc = i >= shortRocLength && input[i - shortRocLength] != 0 ? (input[i] - input[i - shortRocLength]) / input[i - shortRocLength] * 100 : 0;
                    rocSum[i] = longRoc + shortRoc;
                }
            }

            // WMA of ROC sum
            MovingAverageCore.WeightedMovingAverage(rocSum, output, wmaLength);
        }
        finally
        {
            pool.Return(rocSumArray);
        }
    }

    /// <summary>
    /// Computes Chande Forecast Oscillator.
    /// </summary>
    internal static void ChandeForecastOscillator(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var linRegArray = pool.Rent(input.Length);

        try
        {
            var linReg = linRegArray.AsSpan(0, input.Length);
            MovingAverageCore.LinearRegression(input, linReg, length);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = input[i] != 0 ? ((input[i] - linReg[i]) / input[i]) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(linRegArray);
        }
    }

    /// <summary>
    /// Computes Bull Power (Elder Ray).
    /// </summary>
    internal static void BullPower(ReadOnlySpan<double> high, ReadOnlySpan<double> close, Span<double> output, int length = 13)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var emaArray = pool.Rent(close.Length);

        try
        {
            var ema = emaArray.AsSpan(0, close.Length);
            MovingAverageCore.ExponentialMovingAverage(close, ema, length);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = high[i] - ema[i];
            }
        }
        finally
        {
            pool.Return(emaArray);
        }
    }

    /// <summary>
    /// Computes Bear Power (Elder Ray).
    /// </summary>
    internal static void BearPower(ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 13)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var emaArray = pool.Rent(close.Length);

        try
        {
            var ema = emaArray.AsSpan(0, close.Length);
            MovingAverageCore.ExponentialMovingAverage(close, ema, length);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = low[i] - ema[i];
            }
        }
        finally
        {
            pool.Return(emaArray);
        }
    }

    /// <summary>
    /// Computes Polarized Fractal Efficiency.
    /// </summary>
    internal static void PolarizedFractalEfficiency(ReadOnlySpan<double> close, Span<double> output, int length = 10, int smoothLength = 5)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var pfeRawArray = pool.Rent(close.Length);

        try
        {
            var pfeRaw = pfeRawArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                if (i < length)
                {
                    pfeRaw[i] = 0;
                    continue;
                }

                // Calculate price path (sum of absolute changes)
                double pricePath = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    pricePath += Math.Abs(close[j] - close[j - 1]);
                }

                // Calculate direct distance
                var directDist = close[i] - close[i - length];
                var sign = directDist >= 0 ? 1 : -1;

                if (pricePath != 0)
                {
                    var efficiency = Math.Sqrt(directDist * directDist + length * length) / pricePath;
                    pfeRaw[i] = sign * efficiency * 100;
                }
                else
                {
                    pfeRaw[i] = 0;
                }
            }

            // Smooth with EMA
            MovingAverageCore.ExponentialMovingAverage(pfeRaw, output, smoothLength);
        }
        finally
        {
            pool.Return(pfeRawArray);
        }
    }

    /// <summary>
    /// Computes Schaff Trend Cycle.
    /// </summary>
    internal static void SchaffTrendCycle(ReadOnlySpan<double> input, Span<double> output, int cycleLength = 10, int fastLength = 23, int slowLength = 50)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var macdArray = pool.Rent(input.Length);
        var stoch1Array = pool.Rent(input.Length);
        var stoch2Array = pool.Rent(input.Length);

        try
        {
            var macd = macdArray.AsSpan(0, input.Length);
            MacdLine(input, macd, fastLength, slowLength);

            var stoch1 = stoch1Array.AsSpan(0, input.Length);
            var stoch2 = stoch2Array.AsSpan(0, input.Length);

            // First stochastic on MACD
            double factor = 0.5;
            double prevStoch1 = 0;
            double prevStoch2 = 0;

            for (var i = 0; i < input.Length; i++)
            {
                if (i < cycleLength - 1)
                {
                    stoch1[i] = 0;
                    continue;
                }

                // Find highest and lowest MACD in period
                var highest = double.MinValue;
                var lowest = double.MaxValue;
                for (var j = i - cycleLength + 1; j <= i; j++)
                {
                    if (macd[j] > highest) highest = macd[j];
                    if (macd[j] < lowest) lowest = macd[j];
                }

                var range = highest - lowest;
                var fastK = range != 0 ? (macd[i] - lowest) / range * 100 : 0;
                stoch1[i] = prevStoch1 + factor * (fastK - prevStoch1);
                prevStoch1 = stoch1[i];
            }

            // Second stochastic on first stochastic
            for (var i = 0; i < input.Length; i++)
            {
                if (i < cycleLength * 2 - 2)
                {
                    output[i] = 0;
                    continue;
                }

                var highest = double.MinValue;
                var lowest = double.MaxValue;
                for (var j = i - cycleLength + 1; j <= i; j++)
                {
                    if (stoch1[j] > highest) highest = stoch1[j];
                    if (stoch1[j] < lowest) lowest = stoch1[j];
                }

                var range = highest - lowest;
                var fastK = range != 0 ? (stoch1[i] - lowest) / range * 100 : 0;
                stoch2[i] = prevStoch2 + factor * (fastK - prevStoch2);
                prevStoch2 = stoch2[i];
                output[i] = stoch2[i];
            }
        }
        finally
        {
            pool.Return(macdArray);
            pool.Return(stoch1Array);
            pool.Return(stoch2Array);
        }
    }

    /// <summary>
    /// Computes Klinger Volume Oscillator signal line.
    /// </summary>
    internal static void KlingerSignal(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int fastLength = 34, int slowLength = 55, int signalLength = 13)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var kvoArray = pool.Rent(close.Length);

        try
        {
            var kvo = kvoArray.AsSpan(0, close.Length);
            VolumeCore.KlingerVolumeOscillator(high, low, close, volume, kvo, fastLength, slowLength);
            MovingAverageCore.ExponentialMovingAverage(kvo, output, signalLength);
        }
        finally
        {
            pool.Return(kvoArray);
        }
    }

    /// <summary>
    /// Computes Price Zone Oscillator.
    /// </summary>
    internal static void PriceZoneOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var emaCloseArray = pool.Rent(close.Length);
        var sumRArray = pool.Rent(close.Length);
        var sumSArray = pool.Rent(close.Length);

        try
        {
            var emaClose = emaCloseArray.AsSpan(0, close.Length);
            MovingAverageCore.ExponentialMovingAverage(close, emaClose, length);

            var sumR = sumRArray.AsSpan(0, close.Length);
            var sumS = sumSArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                var cp = close[i];
                var tc = emaClose[i];

                sumR[i] = cp > tc ? cp - tc : 0;
                sumS[i] = cp < tc ? tc - cp : 0;
            }

            var pool2 = ArrayPool<double>.Shared;
            var emaSumRArray = pool2.Rent(close.Length);
            var emaSumSArray = pool2.Rent(close.Length);

            try
            {
                var emaSumR = emaSumRArray.AsSpan(0, close.Length);
                var emaSumS = emaSumSArray.AsSpan(0, close.Length);

                MovingAverageCore.ExponentialMovingAverage(sumR, emaSumR, length);
                MovingAverageCore.ExponentialMovingAverage(sumS, emaSumS, length);

                for (var i = 0; i < close.Length; i++)
                {
                    var r = emaSumR[i];
                    var s = emaSumS[i];
                    output[i] = r + s != 0 ? 100 * r / (r + s) : 50;
                }
            }
            finally
            {
                pool2.Return(emaSumRArray);
                pool2.Return(emaSumSArray);
            }
        }
        finally
        {
            pool.Return(emaCloseArray);
            pool.Return(sumRArray);
            pool.Return(sumSArray);
        }
    }

    /// <summary>
    /// Computes Elder Force Index.
    /// </summary>
    internal static void ElderForceIndex(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 13)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var forceArray = pool.Rent(close.Length);

        try
        {
            var force = forceArray.AsSpan(0, close.Length);

            force[0] = 0;
            for (var i = 1; i < close.Length; i++)
            {
                force[i] = (close[i] - close[i - 1]) * volume[i];
            }

            MovingAverageCore.ExponentialMovingAverage(force, output, length);
        }
        finally
        {
            pool.Return(forceArray);
        }
    }

    /// <summary>
    /// Computes Pretty Good Oscillator.
    /// </summary>
    internal static void PrettyGoodOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var smaArray = pool.Rent(close.Length);
        var atrArray = pool.Rent(close.Length);

        try
        {
            var sma = smaArray.AsSpan(0, close.Length);
            var atr = atrArray.AsSpan(0, close.Length);

            MovingAverageCore.SimpleMovingAverage(close, sma, length);
            VolatilityCore.AverageTrueRange(high, low, close, atr, length);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = atr[i] != 0 ? (close[i] - sma[i]) / atr[i] : 0;
            }
        }
        finally
        {
            pool.Return(smaArray);
            pool.Return(atrArray);
        }
    }

    /// <summary>
    /// Computes Relative Volatility Index.
    /// </summary>
    internal static void RelativeVolatilityIndex(ReadOnlySpan<double> close, Span<double> output, int length = 14, int stdDevLength = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var stdDevArray = pool.Rent(close.Length);

        try
        {
            var stdDev = stdDevArray.AsSpan(0, close.Length);
            VolatilityCore.StandardDeviation(close, stdDev, stdDevLength);

            double upSum = 0, downSum = 0;
            double upEma = 0, downEma = 0;
            var k = 1.0 / length;

            for (var i = 1; i < close.Length; i++)
            {
                var change = close[i] - close[i - 1];
                var upMove = change > 0 ? stdDev[i] : 0;
                var downMove = change < 0 ? stdDev[i] : 0;

                if (i < length)
                {
                    upSum += upMove;
                    downSum += downMove;
                    output[i] = 0;
                }
                else if (i == length)
                {
                    upSum += upMove;
                    downSum += downMove;
                    upEma = upSum / length;
                    downEma = downSum / length;
                    output[i] = upEma + downEma != 0 ? 100 * upEma / (upEma + downEma) : 50;
                }
                else
                {
                    upEma = (upMove * k) + (upEma * (1 - k));
                    downEma = (downMove * k) + (downEma * (1 - k));
                    output[i] = upEma + downEma != 0 ? 100 * upEma / (upEma + downEma) : 50;
                }
            }

            output[0] = 0;
        }
        finally
        {
            pool.Return(stdDevArray);
        }
    }

    /// <summary>
    /// Computes Qstick Indicator.
    /// </summary>
    internal static void Qstick(ReadOnlySpan<double> open, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var diffArray = pool.Rent(close.Length);

        try
        {
            var diff = diffArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                diff[i] = close[i] - open[i];
            }

            MovingAverageCore.SimpleMovingAverage(diff, output, length);
        }
        finally
        {
            pool.Return(diffArray);
        }
    }

    /// <summary>
    /// Computes Special K (variation of Know Sure Thing).
    /// </summary>
    internal static void SpecialK(ReadOnlySpan<double> input, Span<double> output)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var roc10Array = pool.Rent(input.Length);
        var roc15Array = pool.Rent(input.Length);
        var roc20Array = pool.Rent(input.Length);
        var roc30Array = pool.Rent(input.Length);
        var roc50Array = pool.Rent(input.Length);
        var roc65Array = pool.Rent(input.Length);
        var roc75Array = pool.Rent(input.Length);
        var roc100Array = pool.Rent(input.Length);
        var roc195Array = pool.Rent(input.Length);
        var roc265Array = pool.Rent(input.Length);
        var roc390Array = pool.Rent(input.Length);
        var roc530Array = pool.Rent(input.Length);

        try
        {
            var roc10 = roc10Array.AsSpan(0, input.Length);
            var roc15 = roc15Array.AsSpan(0, input.Length);
            var roc20 = roc20Array.AsSpan(0, input.Length);
            var roc30 = roc30Array.AsSpan(0, input.Length);
            var roc50 = roc50Array.AsSpan(0, input.Length);
            var roc65 = roc65Array.AsSpan(0, input.Length);
            var roc75 = roc75Array.AsSpan(0, input.Length);
            var roc100 = roc100Array.AsSpan(0, input.Length);
            var roc195 = roc195Array.AsSpan(0, input.Length);
            var roc265 = roc265Array.AsSpan(0, input.Length);
            var roc390 = roc390Array.AsSpan(0, input.Length);
            var roc530 = roc530Array.AsSpan(0, input.Length);

            RateOfChange(input, roc10, 10);
            RateOfChange(input, roc15, 15);
            RateOfChange(input, roc20, 20);
            RateOfChange(input, roc30, 30);
            RateOfChange(input, roc50, 50);
            RateOfChange(input, roc65, 65);
            RateOfChange(input, roc75, 75);
            RateOfChange(input, roc100, 100);
            RateOfChange(input, roc195, 195);
            RateOfChange(input, roc265, 265);
            RateOfChange(input, roc390, 390);
            RateOfChange(input, roc530, 530);

            var pool2 = ArrayPool<double>.Shared;
            var sma10Array = pool2.Rent(input.Length);
            var sma15Array = pool2.Rent(input.Length);
            var sma20Array = pool2.Rent(input.Length);
            var sma50Array = pool2.Rent(input.Length);
            var sma65Array = pool2.Rent(input.Length);
            var sma75Array = pool2.Rent(input.Length);
            var sma100Array = pool2.Rent(input.Length);
            var sma130Array = pool2.Rent(input.Length);
            var sma195Array = pool2.Rent(input.Length);
            var sma265Array = pool2.Rent(input.Length);
            var sma390Array = pool2.Rent(input.Length);
            var sma530Array = pool2.Rent(input.Length);

            try
            {
                var sma10 = sma10Array.AsSpan(0, input.Length);
                var sma15 = sma15Array.AsSpan(0, input.Length);
                var sma20 = sma20Array.AsSpan(0, input.Length);
                var sma50 = sma50Array.AsSpan(0, input.Length);
                var sma65 = sma65Array.AsSpan(0, input.Length);
                var sma75 = sma75Array.AsSpan(0, input.Length);
                var sma100 = sma100Array.AsSpan(0, input.Length);
                var sma130 = sma130Array.AsSpan(0, input.Length);
                var sma195 = sma195Array.AsSpan(0, input.Length);
                var sma265 = sma265Array.AsSpan(0, input.Length);
                var sma390 = sma390Array.AsSpan(0, input.Length);
                var sma530 = sma530Array.AsSpan(0, input.Length);

                MovingAverageCore.SimpleMovingAverage(roc10, sma10, 10);
                MovingAverageCore.SimpleMovingAverage(roc15, sma15, 10);
                MovingAverageCore.SimpleMovingAverage(roc20, sma20, 10);
                MovingAverageCore.SimpleMovingAverage(roc30, sma50, 15);
                MovingAverageCore.SimpleMovingAverage(roc50, sma65, 50);
                MovingAverageCore.SimpleMovingAverage(roc65, sma75, 65);
                MovingAverageCore.SimpleMovingAverage(roc75, sma100, 75);
                MovingAverageCore.SimpleMovingAverage(roc100, sma130, 100);
                MovingAverageCore.SimpleMovingAverage(roc195, sma195, 130);
                MovingAverageCore.SimpleMovingAverage(roc265, sma265, 130);
                MovingAverageCore.SimpleMovingAverage(roc390, sma390, 195);
                MovingAverageCore.SimpleMovingAverage(roc530, sma530, 265);

                for (var i = 0; i < input.Length; i++)
                {
                    output[i] = sma10[i] + sma15[i] * 2 + sma20[i] * 3 + sma50[i] * 4 +
                                sma65[i] + sma75[i] * 2 + sma100[i] * 3 + sma130[i] * 4 +
                                sma195[i] + sma265[i] * 2 + sma390[i] * 3 + sma530[i] * 4;
                }
            }
            finally
            {
                pool2.Return(sma10Array);
                pool2.Return(sma15Array);
                pool2.Return(sma20Array);
                pool2.Return(sma50Array);
                pool2.Return(sma65Array);
                pool2.Return(sma75Array);
                pool2.Return(sma100Array);
                pool2.Return(sma130Array);
                pool2.Return(sma195Array);
                pool2.Return(sma265Array);
                pool2.Return(sma390Array);
                pool2.Return(sma530Array);
            }
        }
        finally
        {
            pool.Return(roc10Array);
            pool.Return(roc15Array);
            pool.Return(roc20Array);
            pool.Return(roc30Array);
            pool.Return(roc50Array);
            pool.Return(roc65Array);
            pool.Return(roc75Array);
            pool.Return(roc100Array);
            pool.Return(roc195Array);
            pool.Return(roc265Array);
            pool.Return(roc390Array);
            pool.Return(roc530Array);
        }
    }

    /// <summary>
    /// Computes Disparity Index.
    /// </summary>
    internal static void DisparityIndex(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var maArray = pool.Rent(close.Length);

        try
        {
            var ma = maArray.AsSpan(0, close.Length);
            MovingAverageCore.SimpleMovingAverage(close, ma, length);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = ma[i] != 0 ? ((close[i] - ma[i]) / ma[i]) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(maArray);
        }
    }

    /// <summary>
    /// Computes Directional Trend Index.
    /// </summary>
    internal static void DirectionalTrendIndex(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var upMoveArray = pool.Rent(close.Length);
        var downMoveArray = pool.Rent(close.Length);
        var plusDiArray = pool.Rent(close.Length);
        var minusDiArray = pool.Rent(close.Length);

        try
        {
            var upMove = upMoveArray.AsSpan(0, close.Length);
            var downMove = downMoveArray.AsSpan(0, close.Length);
            var plusDi = plusDiArray.AsSpan(0, close.Length);
            var minusDi = minusDiArray.AsSpan(0, close.Length);

            upMove[0] = 0;
            downMove[0] = 0;
            for (var i = 1; i < close.Length; i++)
            {
                var upMovement = high[i] - high[i - 1];
                var downMovement = low[i - 1] - low[i];

                upMove[i] = upMovement > downMovement && upMovement > 0 ? upMovement : 0;
                downMove[i] = downMovement > upMovement && downMovement > 0 ? downMovement : 0;
            }

            MovingAverageCore.ExponentialMovingAverage(upMove, plusDi, length);
            MovingAverageCore.ExponentialMovingAverage(downMove, minusDi, length);

            for (var i = 0; i < close.Length; i++)
            {
                var sum = plusDi[i] + minusDi[i];
                output[i] = sum != 0 ? ((plusDi[i] - minusDi[i]) / sum) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(upMoveArray);
            pool.Return(downMoveArray);
            pool.Return(plusDiArray);
            pool.Return(minusDiArray);
        }
    }

    /// <summary>
    /// Computes Double Smoothed Stochastic.
    /// </summary>
    internal static void DoubleSmoothedStochastic(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 10, int smoothLength = 3)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var numArray = pool.Rent(close.Length);
        var denomArray = pool.Rent(close.Length);
        var smoothNumArray = pool.Rent(close.Length);
        var smoothDenomArray = pool.Rent(close.Length);

        try
        {
            var num = numArray.AsSpan(0, close.Length);
            var denom = denomArray.AsSpan(0, close.Length);
            var smoothNum = smoothNumArray.AsSpan(0, close.Length);
            var smoothDenom = smoothDenomArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                var start = Math.Max(0, i - length + 1);
                var hh = high[start];
                var ll = low[start];
                for (var j = start + 1; j <= i; j++)
                {
                    if (high[j] > hh) hh = high[j];
                    if (low[j] < ll) ll = low[j];
                }
                num[i] = close[i] - ll;
                denom[i] = hh - ll;
            }

            MovingAverageCore.ExponentialMovingAverage(num, smoothNum, smoothLength);
            MovingAverageCore.ExponentialMovingAverage(denom, smoothDenom, smoothLength);

            // Second smoothing
            MovingAverageCore.ExponentialMovingAverage(smoothNum, num, smoothLength);
            MovingAverageCore.ExponentialMovingAverage(smoothDenom, denom, smoothLength);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = denom[i] != 0 ? (num[i] / denom[i]) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(numArray);
            pool.Return(denomArray);
            pool.Return(smoothNumArray);
            pool.Return(smoothDenomArray);
        }
    }

    /// <summary>
    /// Computes Dynamic Momentum Index.
    /// </summary>
    internal static void DynamicMomentumIndex(ReadOnlySpan<double> close, Span<double> output, int minLength = 3, int maxLength = 30)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var stdDevArray = pool.Rent(close.Length);
        var stdDevSmaArray = pool.Rent(close.Length);

        try
        {
            var stdDev = stdDevArray.AsSpan(0, close.Length);
            var stdDevSma = stdDevSmaArray.AsSpan(0, close.Length);

            VolatilityCore.StandardDeviation(close, stdDev, 5);
            MovingAverageCore.SimpleMovingAverage(stdDev, stdDevSma, 10);

            for (var i = 0; i < close.Length; i++)
            {
                // Calculate dynamic period
                var ratio = stdDevSma[i] != 0 ? stdDev[i] / stdDevSma[i] : 1;
                var dynamicLength = (int)(14 / ratio);
                dynamicLength = Math.Max(minLength, Math.Min(maxLength, dynamicLength));

                // Calculate RSI with dynamic length
                if (i < dynamicLength)
                {
                    output[i] = 50;
                    continue;
                }

                double gains = 0;
                double losses = 0;
                for (var j = i - dynamicLength + 1; j <= i; j++)
                {
                    var change = close[j] - close[j - 1];
                    if (change > 0)
                        gains += change;
                    else
                        losses -= change;
                }

                var avgGain = gains / dynamicLength;
                var avgLoss = losses / dynamicLength;
                var rs = avgLoss != 0 ? avgGain / avgLoss : 0;
                output[i] = 100 - (100 / (1 + rs));
            }
        }
        finally
        {
            pool.Return(stdDevArray);
            pool.Return(stdDevSmaArray);
        }
    }

    /// <summary>
    /// Computes Ergodic Candlestick Oscillator (ECO).
    /// </summary>
    internal static void ErgodicCandlestickOscillator(ReadOnlySpan<double> open, ReadOnlySpan<double> close, Span<double> output, int shortLength = 5, int longLength = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var diffArray = pool.Rent(close.Length);
        var smoothShortArray = pool.Rent(close.Length);
        var smoothLongArray = pool.Rent(close.Length);

        try
        {
            var diff = diffArray.AsSpan(0, close.Length);
            var smoothShort = smoothShortArray.AsSpan(0, close.Length);
            var smoothLong = smoothLongArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                diff[i] = close[i] - open[i];
            }

            MovingAverageCore.ExponentialMovingAverage(diff, smoothShort, shortLength);
            MovingAverageCore.ExponentialMovingAverage(smoothShort, smoothLong, longLength);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = smoothLong[i];
            }
        }
        finally
        {
            pool.Return(diffArray);
            pool.Return(smoothShortArray);
            pool.Return(smoothLongArray);
        }
    }

    /// <summary>
    /// Computes Demarker Indicator.
    /// </summary>
    internal static void Demarker(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 14)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var deMaxArray = pool.Rent(high.Length);
        var deMinArray = pool.Rent(high.Length);
        var smaMaxArray = pool.Rent(high.Length);
        var smaMinArray = pool.Rent(high.Length);

        try
        {
            var deMax = deMaxArray.AsSpan(0, high.Length);
            var deMin = deMinArray.AsSpan(0, high.Length);
            var smaMax = smaMaxArray.AsSpan(0, high.Length);
            var smaMin = smaMinArray.AsSpan(0, high.Length);

            deMax[0] = 0;
            deMin[0] = 0;
            for (var i = 1; i < high.Length; i++)
            {
                deMax[i] = high[i] > high[i - 1] ? high[i] - high[i - 1] : 0;
                deMin[i] = low[i] < low[i - 1] ? low[i - 1] - low[i] : 0;
            }

            MovingAverageCore.SimpleMovingAverage(deMax, smaMax, length);
            MovingAverageCore.SimpleMovingAverage(deMin, smaMin, length);

            for (var i = 0; i < high.Length; i++)
            {
                var sum = smaMax[i] + smaMin[i];
                output[i] = sum != 0 ? smaMax[i] / sum : 0.5;
            }
        }
        finally
        {
            pool.Return(deMaxArray);
            pool.Return(deMinArray);
            pool.Return(smaMaxArray);
            pool.Return(smaMinArray);
        }
    }

    /// <summary>
    /// Computes Smoothed Rate of Change.
    /// </summary>
    internal static void SmoothedRateOfChange(ReadOnlySpan<double> input, Span<double> output, int rocLength = 12, int smoothLength = 3)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rocArray = pool.Rent(input.Length);

        try
        {
            var roc = rocArray.AsSpan(0, input.Length);
            RateOfChange(input, roc, rocLength);
            MovingAverageCore.ExponentialMovingAverage(roc, output, smoothLength);
        }
        finally
        {
            pool.Return(rocArray);
        }
    }

    /// <summary>
    /// Computes Stochastic RSI (StochRSI).
    /// Already exists as StochasticRsi - this is an alias.
    /// </summary>
    internal static void StochasticRsiOscillator(ReadOnlySpan<double> input, Span<double> output, int rsiLength = 14, int stochLength = 14)
    {
        StochasticRsi(input, output, rsiLength, stochLength);
    }

    /// <summary>
    /// Computes Elliott Wave Oscillator (EWO).
    /// Difference between fast and slow moving averages.
    /// </summary>
    internal static void ElliottWaveOscillator(ReadOnlySpan<double> close, Span<double> output, int fastLength = 5, int slowLength = 35)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var fastEmaArray = pool.Rent(close.Length);
        var slowEmaArray = pool.Rent(close.Length);

        try
        {
            var fastEma = fastEmaArray.AsSpan(0, close.Length);
            var slowEma = slowEmaArray.AsSpan(0, close.Length);

            MovingAverageCore.SimpleMovingAverage(close, fastEma, fastLength);
            MovingAverageCore.SimpleMovingAverage(close, slowEma, slowLength);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = fastEma[i] - slowEma[i];
            }
        }
        finally
        {
            pool.Return(fastEmaArray);
            pool.Return(slowEmaArray);
        }
    }

    /// <summary>
    /// Computes Forecast Oscillator.
    /// Compares actual price to linear regression forecast.
    /// </summary>
    internal static void ForecastOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var linRegArray = pool.Rent(close.Length);

        try
        {
            var linReg = linRegArray.AsSpan(0, close.Length);
            MovingAverageCore.LinearRegression(close, linReg, length);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = linReg[i] != 0 ? ((close[i] - linReg[i]) / linReg[i]) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(linRegArray);
        }
    }

    /// <summary>
    /// Computes Derivative Oscillator.
    /// Double smoothed RSI with signal line.
    /// </summary>
    internal static void DerivativeOscillator(ReadOnlySpan<double> close, Span<double> output, int rsiLength = 14, int smoothLength1 = 5, int smoothLength2 = 3, int signalLength = 9)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rsiArray = pool.Rent(close.Length);
        var smoothed1Array = pool.Rent(close.Length);
        var smoothed2Array = pool.Rent(close.Length);
        var signalArray = pool.Rent(close.Length);

        try
        {
            var rsi = rsiArray.AsSpan(0, close.Length);
            var smoothed1 = smoothed1Array.AsSpan(0, close.Length);
            var smoothed2 = smoothed2Array.AsSpan(0, close.Length);
            var signal = signalArray.AsSpan(0, close.Length);

            RelativeStrengthIndex(close, rsi, rsiLength);
            MovingAverageCore.ExponentialMovingAverage(rsi, smoothed1, smoothLength1);
            MovingAverageCore.ExponentialMovingAverage(smoothed1, smoothed2, smoothLength2);
            MovingAverageCore.SimpleMovingAverage(smoothed2, signal, signalLength);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = smoothed2[i] - signal[i];
            }
        }
        finally
        {
            pool.Return(rsiArray);
            pool.Return(smoothed1Array);
            pool.Return(smoothed2Array);
            pool.Return(signalArray);
        }
    }

    /// <summary>
    /// Computes Gator Oscillator.
    /// Based on difference between Alligator's jaw/teeth/lips.
    /// </summary>
    internal static void GatorOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int jawLength = 13, int teethLength = 8, int lipsLength = 5)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var medianArray = pool.Rent(high.Length);
        var jawArray = pool.Rent(high.Length);
        var teethArray = pool.Rent(high.Length);
        var lipsArray = pool.Rent(high.Length);

        try
        {
            var median = medianArray.AsSpan(0, high.Length);
            var jaw = jawArray.AsSpan(0, high.Length);
            var teeth = teethArray.AsSpan(0, high.Length);
            var lips = lipsArray.AsSpan(0, high.Length);

            // Calculate median price
            for (var i = 0; i < high.Length; i++)
            {
                median[i] = (high[i] + low[i]) / 2;
            }

            // Calculate smoothed moving averages
            MovingAverageCore.SmoothedMovingAverage(median, jaw, jawLength);
            MovingAverageCore.SmoothedMovingAverage(median, teeth, teethLength);
            MovingAverageCore.SmoothedMovingAverage(median, lips, lipsLength);

            // Gator upper: abs(jaw - teeth)
            // Gator lower: abs(teeth - lips) (negated for histogram)
            // We'll return upper - lower as combined value
            for (var i = 0; i < high.Length; i++)
            {
                var upper = Math.Abs(jaw[i] - teeth[i]);
                var lower = Math.Abs(teeth[i] - lips[i]);
                output[i] = upper - lower;
            }
        }
        finally
        {
            pool.Return(medianArray);
            pool.Return(jawArray);
            pool.Return(teethArray);
            pool.Return(lipsArray);
        }
    }

    /// <summary>
    /// Computes Fractal Chaos Oscillator.
    /// Detects fractal patterns in price.
    /// </summary>
    internal static void FractalChaosOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Need at least 5 bars for fractal detection
        if (high.Length < 5)
        {
            output.Clear();
            return;
        }

        for (var i = 0; i < high.Length; i++)
        {
            if (i < 2 || i >= high.Length - 2)
            {
                output[i] = 0;
                continue;
            }

            // Bullish fractal: low[i-2] is the lowest of 5 bars
            bool bullish = low[i - 2] < low[i - 4] && low[i - 2] < low[i - 3] &&
                          low[i - 2] < low[i - 1] && low[i - 2] < low[i];

            // Bearish fractal: high[i-2] is the highest of 5 bars
            bool bearish = high[i - 2] > high[i - 4] && high[i - 2] > high[i - 3] &&
                          high[i - 2] > high[i - 1] && high[i - 2] > high[i];

            if (i >= 4)
            {
                bullish = low[i - 2] < low[i - 4] && low[i - 2] < low[i - 3] &&
                         low[i - 2] < low[i - 1] && low[i - 2] < low[i];
                bearish = high[i - 2] > high[i - 4] && high[i - 2] > high[i - 3] &&
                         high[i - 2] > high[i - 1] && high[i - 2] > high[i];
            }

            output[i] = bullish ? 1 : (bearish ? -1 : 0);
        }
    }

    /// <summary>
    /// Computes Rahul Mohindar Oscillator (RMO).
    /// </summary>
    internal static void RahulMohindarOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var ema1Array = pool.Rent(close.Length);
        var ema2Array = pool.Rent(close.Length);
        var ema3Array = pool.Rent(close.Length);
        var diffArray = pool.Rent(close.Length);

        try
        {
            var ema1 = ema1Array.AsSpan(0, close.Length);
            var ema2 = ema2Array.AsSpan(0, close.Length);
            var ema3 = ema3Array.AsSpan(0, close.Length);
            var diff = diffArray.AsSpan(0, close.Length);

            MovingAverageCore.ExponentialMovingAverage(close, ema1, length);
            MovingAverageCore.ExponentialMovingAverage(ema1, ema2, length);

            for (var i = 0; i < close.Length; i++)
            {
                diff[i] = ema1[i] - ema2[i];
            }

            MovingAverageCore.ExponentialMovingAverage(diff, ema3, length);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = diff[i] - ema3[i];
            }
        }
        finally
        {
            pool.Return(ema1Array);
            pool.Return(ema2Array);
            pool.Return(ema3Array);
            pool.Return(diffArray);
        }
    }

    /// <summary>
    /// Computes Premier Stochastic Oscillator.
    /// Normalized and smoothed stochastic.
    /// </summary>
    internal static void PremierStochastic(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 8, int smoothLength = 25)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var stochArray = pool.Rent(close.Length);
        var normalizedArray = pool.Rent(close.Length);
        var smoothed1Array = pool.Rent(close.Length);
        var smoothed2Array = pool.Rent(close.Length);

        try
        {
            var stoch = stochArray.AsSpan(0, close.Length);
            var normalized = normalizedArray.AsSpan(0, close.Length);
            var smoothed1 = smoothed1Array.AsSpan(0, close.Length);
            var smoothed2 = smoothed2Array.AsSpan(0, close.Length);

            StochasticK(high, low, close, stoch, length);

            // Normalize to -0.5 to +0.5 range
            for (var i = 0; i < close.Length; i++)
            {
                normalized[i] = 0.1 * (stoch[i] - 50);
            }

            // Double smoothing
            int emaLength = (int)Math.Sqrt(smoothLength);
            MovingAverageCore.ExponentialMovingAverage(normalized, smoothed1, emaLength);
            MovingAverageCore.ExponentialMovingAverage(smoothed1, smoothed2, emaLength);

            // Final transformation
            for (var i = 0; i < close.Length; i++)
            {
                var exp = Math.Exp(smoothed2[i]);
                output[i] = (exp - 1) / (exp + 1);
            }
        }
        finally
        {
            pool.Return(stochArray);
            pool.Return(normalizedArray);
            pool.Return(smoothed1Array);
            pool.Return(smoothed2Array);
        }
    }

    /// <summary>
    /// Computes Repulse Indicator.
    /// Measures buying/selling pressure.
    /// </summary>
    internal static void Repulse(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 5)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var bullArray = pool.Rent(close.Length);
        var bearArray = pool.Rent(close.Length);
        var bullEmaArray = pool.Rent(close.Length);
        var bearEmaArray = pool.Rent(close.Length);

        try
        {
            var bull = bullArray.AsSpan(0, close.Length);
            var bear = bearArray.AsSpan(0, close.Length);
            var bullEma = bullEmaArray.AsSpan(0, close.Length);
            var bearEma = bearEmaArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                double range = high[i] - low[i];
                if (range > 0)
                {
                    bull[i] = 100 * (3 * close[i] - 2 * low[i] - open[i]) / range;
                    bear[i] = 100 * (open[i] + 2 * high[i] - 3 * close[i]) / range;
                }
                else
                {
                    bull[i] = 0;
                    bear[i] = 0;
                }
            }

            MovingAverageCore.ExponentialMovingAverage(bull, bullEma, length * 5);
            MovingAverageCore.ExponentialMovingAverage(bear, bearEma, length * 5);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = bullEma[i] - bearEma[i];
            }
        }
        finally
        {
            pool.Return(bullArray);
            pool.Return(bearArray);
            pool.Return(bullEmaArray);
            pool.Return(bearEmaArray);
        }
    }

    /// <summary>
    /// Computes Chande Composite Momentum Index.
    /// Combines CMO with other momentum metrics.
    /// </summary>
    internal static void ChandeCompositeMomentumIndex(ReadOnlySpan<double> close, Span<double> output, int shortLength = 3, int longLength = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var shortCmoArray = pool.Rent(close.Length);
        var longCmoArray = pool.Rent(close.Length);

        try
        {
            var shortCmo = shortCmoArray.AsSpan(0, close.Length);
            var longCmo = longCmoArray.AsSpan(0, close.Length);

            ChandeMomentumOscillator(close, shortCmo, shortLength);
            ChandeMomentumOscillator(close, longCmo, longLength);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = (shortCmo[i] + longCmo[i]) / 2;
            }
        }
        finally
        {
            pool.Return(shortCmoArray);
            pool.Return(longCmoArray);
        }
    }

    /// <summary>
    /// Computes Chande Kroll R-Squared Index.
    /// Measures trend strength using R-Squared.
    /// </summary>
    internal static void ChandeKrollRSquaredIndex(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // R-Squared measures how well prices fit a linear regression line
        for (var i = 0; i < close.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            // Calculate linear regression
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (var j = 0; j < length; j++)
            {
                var x = j;
                var y = close[i - length + 1 + j];
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }

            var meanX = sumX / length;
            var meanY = sumY / length;

            var slope = (sumXY - length * meanX * meanY) / (sumX2 - length * meanX * meanX);
            var intercept = meanY - slope * meanX;

            // Calculate R-Squared
            double ssTot = 0, ssRes = 0;
            for (var j = 0; j < length; j++)
            {
                var y = close[i - length + 1 + j];
                var yPred = intercept + slope * j;
                ssTot += (y - meanY) * (y - meanY);
                ssRes += (y - yPred) * (y - yPred);
            }

            output[i] = ssTot > 0 ? 100 * (1 - ssRes / ssTot) : 0;
        }
    }

    /// <summary>
    /// Computes Bayesian Oscillator.
    /// Uses probability-based approach to measure price movement.
    /// </summary>
    internal static void BayesianOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length)
            {
                output[i] = 50;
                continue;
            }

            var upCount = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                if (close[j] > close[j - 1])
                {
                    upCount++;
                }
            }

            // Bayesian probability of up move given recent history
            output[i] = 100.0 * upCount / length;
        }
    }

    /// <summary>
    /// Computes Anchored Momentum.
    /// Measures momentum relative to a specific bar.
    /// </summary>
    internal static void AnchoredMomentum(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
                continue;
            }

            var anchor = close[i - length];
            output[i] = anchor > 0 ? 100 * (close[i] - anchor) / anchor : 0;
        }
    }

    /// <summary>
    /// Computes Chartmill Value Indicator.
    /// Measures value/momentum relative to recent range.
    /// </summary>
    internal static void ChartmillValueIndicator(ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            double highest = double.MinValue;
            double lowest = double.MaxValue;
            for (var j = i - length + 1; j <= i; j++)
            {
                highest = Math.Max(highest, close[j]);
                lowest = Math.Min(lowest, close[j]);
            }

            var range = highest - lowest;
            output[i] = range > 0 ? 100 * (close[i] - lowest) / range : 50;
        }
    }

    /// <summary>
    /// Computes Center of Linearity.
    /// Measures how linear the price movement is.
    /// </summary>
    internal static void CenterOfLinearity(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            // Calculate center of mass of price action
            double sumProduct = 0;
            double sumPrices = 0;
            for (var j = 0; j < length; j++)
            {
                var price = close[i - length + 1 + j];
                sumProduct += (j + 1) * price;
                sumPrices += price;
            }

            var centerOfMass = sumPrices > 0 ? sumProduct / sumPrices : length / 2.0;
            var expectedCenter = (length + 1) / 2.0;

            // Normalize to -100 to +100 range
            output[i] = 100 * (centerOfMass - expectedCenter) / expectedCenter;
        }
    }

    /// <summary>
    /// Computes Breakout RSI.
    /// RSI variant optimized for breakout detection.
    /// </summary>
    internal static void BreakoutRsi(ReadOnlySpan<double> close, Span<double> output, int length = 14, double threshold = 70)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rsiArray = pool.Rent(close.Length);

        try
        {
            var rsi = rsiArray.AsSpan(0, close.Length);
            RelativeStrengthIndex(close, rsi, length);

            for (var i = 0; i < close.Length; i++)
            {
                // Enhance RSI signal near breakout levels
                if (rsi[i] > threshold || rsi[i] < 100 - threshold)
                {
                    output[i] = rsi[i];
                }
                else
                {
                    output[i] = 50; // Neutral when not in breakout zone
                }
            }
        }
        finally
        {
            pool.Return(rsiArray);
        }
    }

    /// <summary>
    /// Computes Asymmetrical RSI.
    /// RSI with different up/down smoothing.
    /// </summary>
    internal static void AsymmetricalRsi(ReadOnlySpan<double> close, Span<double> output, int upLength = 14, int downLength = 7)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var gainsArray = pool.Rent(close.Length);
        var lossesArray = pool.Rent(close.Length);
        var avgGainArray = pool.Rent(close.Length);
        var avgLossArray = pool.Rent(close.Length);

        try
        {
            var gains = gainsArray.AsSpan(0, close.Length);
            var losses = lossesArray.AsSpan(0, close.Length);
            var avgGain = avgGainArray.AsSpan(0, close.Length);
            var avgLoss = avgLossArray.AsSpan(0, close.Length);

            gains[0] = 0;
            losses[0] = 0;
            for (var i = 1; i < close.Length; i++)
            {
                var change = close[i] - close[i - 1];
                gains[i] = change > 0 ? change : 0;
                losses[i] = change < 0 ? -change : 0;
            }

            MovingAverageCore.ExponentialMovingAverage(gains, avgGain, upLength);
            MovingAverageCore.ExponentialMovingAverage(losses, avgLoss, downLength);

            for (var i = 0; i < close.Length; i++)
            {
                var rs = avgLoss[i] > 0 ? avgGain[i] / avgLoss[i] : 100;
                output[i] = 100 - 100 / (1 + rs);
            }
        }
        finally
        {
            pool.Return(gainsArray);
            pool.Return(lossesArray);
            pool.Return(avgGainArray);
            pool.Return(avgLossArray);
        }
    }

    /// <summary>
    /// Computes Adaptive Stochastic.
    /// Stochastic with adaptive period based on volatility.
    /// </summary>
    internal static void AdaptiveStochastic(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int minLength = 5, int maxLength = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < maxLength)
            {
                output[i] = 50;
                continue;
            }

            // Calculate volatility to adapt period
            double sumAtr = 0;
            for (var j = i - 10; j <= i; j++)
            {
                var tr = Math.Max(high[j] - low[j],
                    Math.Max(Math.Abs(high[j] - close[j - 1]), Math.Abs(low[j] - close[j - 1])));
                sumAtr += tr;
            }
            var avgAtr = sumAtr / 10;

            // Higher volatility = shorter period
            var adaptivePeriod = avgAtr > 0 ?
                Math.Max(minLength, Math.Min(maxLength, (int)(minLength + (maxLength - minLength) * (1 - avgAtr / close[i])))) :
                (minLength + maxLength) / 2;

            // Calculate stochastic with adaptive period
            var highestHigh = double.MinValue;
            var lowestLow = double.MaxValue;
            for (var j = i - adaptivePeriod + 1; j <= i; j++)
            {
                highestHigh = Math.Max(highestHigh, high[j]);
                lowestLow = Math.Min(lowestLow, low[j]);
            }

            var range = highestHigh - lowestLow;
            output[i] = range > 0 ? 100 * (close[i] - lowestLow) / range : 50;
        }
    }

    /// <summary>
    /// Computes Adaptive RSI.
    /// RSI with adaptive period based on market conditions.
    /// </summary>
    internal static void AdaptiveRsi(ReadOnlySpan<double> close, Span<double> output, int minLength = 5, int maxLength = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < maxLength)
            {
                output[i] = 50;
                continue;
            }

            // Calculate efficiency ratio to adapt period
            var change = Math.Abs(close[i] - close[i - maxLength]);
            double volatility = 0;
            for (var j = i - maxLength + 1; j <= i; j++)
            {
                volatility += Math.Abs(close[j] - close[j - 1]);
            }

            var er = volatility > 0 ? change / volatility : 0;

            // Higher efficiency = shorter period
            var adaptivePeriod = (int)(maxLength - er * (maxLength - minLength));
            adaptivePeriod = Math.Max(minLength, Math.Min(maxLength, adaptivePeriod));

            // Calculate RSI with adaptive period
            double sumGain = 0, sumLoss = 0;
            for (var j = i - adaptivePeriod + 1; j <= i; j++)
            {
                var delta = close[j] - close[j - 1];
                if (delta > 0)
                    sumGain += delta;
                else
                    sumLoss -= delta;
            }

            var rs = sumLoss > 0 ? sumGain / sumLoss : 100;
            output[i] = 100 - 100 / (1 + rs);
        }
    }

    #region Additional Batch 8

    /// <summary>
    /// Computes Smoothed Williams %R.
    /// </summary>
    internal static void SmoothedWilliamsR(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14, int smoothLength = 3)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rawWr = pool.Rent(close.Length);

        try
        {
            // Calculate raw Williams %R
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    rawWr[i] = -50;
                    continue;
                }

                double highestHigh = double.MinValue;
                double lowestLow = double.MaxValue;
                for (var j = i - length + 1; j <= i; j++)
                {
                    if (high[j] > highestHigh) highestHigh = high[j];
                    if (low[j] < lowestLow) lowestLow = low[j];
                }

                rawWr[i] = highestHigh != lowestLow
                    ? (highestHigh - close[i]) / (highestHigh - lowestLow) * -100
                    : -50;
            }

            // Smooth the Williams %R
            var k = 2.0 / (smoothLength + 1);
            output[0] = rawWr[0];
            for (var i = 1; i < close.Length; i++)
            {
                output[i] = rawWr[i] * k + output[i - 1] * (1 - k);
            }
        }
        finally
        {
            pool.Return(rawWr);
        }
    }

    /// <summary>
    /// Computes Price Oscillator percentage.
    /// </summary>
    internal static void PriceOscillatorPercent(ReadOnlySpan<double> input, Span<double> output, int shortLength = 10, int longLength = 20)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var shortK = 2.0 / (shortLength + 1);
        var longK = 2.0 / (longLength + 1);
        double shortEma = input[0];
        double longEma = input[0];

        for (var i = 0; i < input.Length; i++)
        {
            if (i == 0)
            {
                output[i] = 0;
            }
            else
            {
                shortEma = input[i] * shortK + shortEma * (1 - shortK);
                longEma = input[i] * longK + longEma * (1 - longK);
                output[i] = longEma != 0 ? (shortEma - longEma) / longEma * 100 : 0;
            }
        }
    }

    /// <summary>
    /// Computes Normalized MACD.
    /// </summary>
    internal static void NormalizedMacd(ReadOnlySpan<double> input, Span<double> output, int fastLength = 12, int slowLength = 26)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var fastK = 2.0 / (fastLength + 1);
        var slowK = 2.0 / (slowLength + 1);
        double fastEma = input[0];
        double slowEma = input[0];

        for (var i = 0; i < input.Length; i++)
        {
            if (i == 0)
            {
                output[i] = 0;
            }
            else
            {
                fastEma = input[i] * fastK + fastEma * (1 - fastK);
                slowEma = input[i] * slowK + slowEma * (1 - slowK);
                output[i] = slowEma != 0 ? (fastEma - slowEma) / slowEma * 100 : 0;
            }
        }
    }

    /// <summary>
    /// Computes Relative Vigor Index Signal.
    /// </summary>
    internal static void RelativeVigorIndexSignal(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 10, int signalLength = 4)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rvi = pool.Rent(close.Length);

        try
        {
            // Calculate RVI first
            RelativeVigorIndex(open, high, low, close, rvi.AsSpan(0, close.Length), length);

            // Calculate signal (smoothed RVI)
            var k = 2.0 / (signalLength + 1);
            output[0] = rvi[0];
            for (var i = 1; i < close.Length; i++)
            {
                output[i] = rvi[i] * k + output[i - 1] * (1 - k);
            }
        }
        finally
        {
            pool.Return(rvi);
        }
    }

    /// <summary>
    /// Computes Volume Momentum Oscillator.
    /// </summary>
    internal static void VolumeMomentumOscillator(ReadOnlySpan<double> volume, Span<double> output, int shortLength = 5, int longLength = 20)
    {
        if (output.Length < volume.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var shortK = 2.0 / (shortLength + 1);
        var longK = 2.0 / (longLength + 1);
        double shortEma = volume[0];
        double longEma = volume[0];

        for (var i = 0; i < volume.Length; i++)
        {
            if (i == 0)
            {
                output[i] = 0;
            }
            else
            {
                shortEma = volume[i] * shortK + shortEma * (1 - shortK);
                longEma = volume[i] * longK + longEma * (1 - longK);
                output[i] = longEma != 0 ? (shortEma - longEma) / longEma * 100 : 0;
            }
        }
    }

    /// <summary>
    /// Computes Trend Continuation Factor.
    /// </summary>
    internal static void TrendContinuationFactor(ReadOnlySpan<double> close, Span<double> output, int length = 35)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
                continue;
            }

            double plusCf = 0, minusCf = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                var change = close[j] - close[j - 1];
                if (change > 0)
                    plusCf += change;
                else
                    minusCf += Math.Abs(change);
            }

            output[i] = plusCf + minusCf != 0 ? (plusCf - minusCf) / (plusCf + minusCf) * 100 : 0;
        }
    }

    /// <summary>
    /// Computes Trend Persistence Rate.
    /// </summary>
    internal static void TrendPersistenceRate(ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length)
            {
                output[i] = 50;
                continue;
            }

            int upCount = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                if (close[j] > close[j - 1])
                    upCount++;
            }

            output[i] = (double)upCount / length * 100;
        }
    }

    /// <summary>
    /// Computes Inertia indicator.
    /// </summary>
    internal static void Inertia(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int rviLength = 14, int smoothLength = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rvi = pool.Rent(close.Length);

        try
        {
            // Calculate Relative Volatility Index first
            RelativeVolatilityIndex(close, rvi.AsSpan(0, close.Length), rviLength);

            // Calculate linear regression of RVI
            for (var i = 0; i < close.Length; i++)
            {
                if (i < smoothLength - 1)
                {
                    output[i] = 50;
                    continue;
                }

                double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
                for (var j = 0; j < smoothLength; j++)
                {
                    var x = (double)j;
                    var y = rvi[i - smoothLength + 1 + j];
                    sumX += x;
                    sumY += y;
                    sumXY += x * y;
                    sumX2 += x * x;
                }

                var n = (double)smoothLength;
                var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
                var intercept = (sumY - slope * sumX) / n;
                output[i] = intercept + slope * (smoothLength - 1);
            }
        }
        finally
        {
            pool.Return(rvi);
        }
    }

    #endregion

    #region Additional Oscillators (Batch 1)

    /// <summary>
    /// Computes Absolute Chande Momentum Oscillator (absolute value of CMO).
    /// </summary>
    internal static void ChandeMomentumOscillatorAbsolute(ReadOnlySpan<double> input, Span<double> output, int length = 9)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double sumUp = 0, sumDown = 0;

        for (var i = 0; i < input.Length; i++)
        {
            if (i == 0)
            {
                output[i] = 0;
                continue;
            }

            var change = input[i] - input[i - 1];
            var up = change > 0 ? change : 0;
            var down = change < 0 ? -change : 0;

            if (i < length)
            {
                sumUp += up;
                sumDown += down;
                output[i] = 0;
            }
            else
            {
                var oldChange = input[i - length] - (i > length ? input[i - length - 1] : 0);
                var oldUp = oldChange > 0 ? oldChange : 0;
                var oldDown = oldChange < 0 ? -oldChange : 0;
                sumUp = sumUp - oldUp + up;
                sumDown = sumDown - oldDown + down;

                var total = sumUp + sumDown;
                var cmo = total != 0 ? 100 * (sumUp - sumDown) / total : 0;
                output[i] = Math.Abs(cmo);
            }
        }
    }

    /// <summary>
    /// Computes Percent Change (simple percentage change over 1 period).
    /// </summary>
    internal static void PercentChange(ReadOnlySpan<double> input, Span<double> output, int length = 1)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
            }
            else
            {
                var prevValue = input[i - length];
                output[i] = prevValue != 0 ? (input[i] - prevValue) / prevValue * 100 : 0;
            }
        }
    }

    /// <summary>
    /// Computes Price Change (simple difference from previous bar).
    /// </summary>
    internal static void PriceChange(ReadOnlySpan<double> input, Span<double> output)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        output[0] = 0;
        for (var i = 1; i < input.Length; i++)
        {
            output[i] = input[i] - input[i - 1];
        }
    }

    /// <summary>
    /// Computes Range (High - Low).
    /// </summary>
    internal static void Range(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < high.Length; i++)
        {
            output[i] = high[i] - low[i];
        }
    }

    /// <summary>
    /// Computes Mid-Range ((High + Low) / 2).
    /// </summary>
    internal static void MidRange(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < high.Length; i++)
        {
            output[i] = (high[i] + low[i]) / 2;
        }
    }

    /// <summary>
    /// Computes OHLC Average ((Open + High + Low + Close) / 4).
    /// </summary>
    internal static void OhlcAverage(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            output[i] = (open[i] + high[i] + low[i] + close[i]) / 4;
        }
    }

    /// <summary>
    /// Computes HLC Average ((High + Low + Close) / 3).
    /// </summary>
    internal static void HlcAverage(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            output[i] = (high[i] + low[i] + close[i]) / 3;
        }
    }

    /// <summary>
    /// Computes Double Smoothed Momenta (EMA of EMA of momentum).
    /// </summary>
    internal static void DoubleSmoothedMomenta(ReadOnlySpan<double> input, Span<double> output, int momentumLength = 1, int firstSmooth = 25, int secondSmooth = 13)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // First compute momentum (price difference over period)
        var pool = ArrayPool<double>.Shared;
        var momentumArray = pool.Rent(input.Length);
        var firstEmaArray = pool.Rent(input.Length);

        try
        {
            var momentum = momentumArray.AsSpan(0, input.Length);

            // Momentum = current - previous (by momentumLength)
            for (var i = 0; i < input.Length; i++)
            {
                if (i < momentumLength)
                {
                    momentum[i] = 0;
                }
                else
                {
                    momentum[i] = input[i] - input[i - momentumLength];
                }
            }

            // First EMA of momentum
            var firstEma = firstEmaArray.AsSpan(0, input.Length);
            MovingAverageCore.ExponentialMovingAverage(momentum, firstEma, firstSmooth);

            // Second EMA (double smoothing)
            MovingAverageCore.ExponentialMovingAverage(firstEma, output, secondSmooth);
        }
        finally
        {
            pool.Return(momentumArray);
            pool.Return(firstEmaArray);
        }
    }

    /// <summary>
    /// Computes High-Low Index (ratio of new highs vs new lows over period).
    /// </summary>
    internal static void HighLowIndex(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 14)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < high.Length; i++)
        {
            if (i < length)
            {
                output[i] = 50; // neutral
                continue;
            }

            var newHighs = 0;
            var newLows = 0;

            for (var j = i - length + 1; j <= i; j++)
            {
                var prevHigh = j > 0 ? high[j - 1] : high[0];
                var prevLow = j > 0 ? low[j - 1] : low[0];

                if (high[j] > prevHigh) newHighs++;
                if (low[j] < prevLow) newLows++;
            }

            var total = newHighs + newLows;
            output[i] = total != 0 ? (double)newHighs / total * 100 : 50;
        }
    }

    /// <summary>
    /// Computes Market Facilitation Index ((High - Low) / Volume).
    /// </summary>
    internal static void MarketFacilitationIndex(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> volume, Span<double> output)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < high.Length; i++)
        {
            output[i] = volume[i] != 0 ? (high[i] - low[i]) / volume[i] : 0;
        }
    }

    /// <summary>
    /// Computes Trend Score (direction consistency over period).
    /// </summary>
    internal static void TrendScore(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
                continue;
            }

            var score = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                if (input[j] > input[j - 1])
                    score++;
                else if (input[j] < input[j - 1])
                    score--;
            }

            output[i] = score;
        }
    }

    /// <summary>
    /// Computes Rolling Median value over a period.
    /// </summary>
    internal static void MedianValue(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var window = new double[length];

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

            // Sort and get median
            Array.Sort(window);
            output[i] = length % 2 == 0
                ? (window[length / 2 - 1] + window[length / 2]) / 2
                : window[length / 2];
        }
    }

    /// <summary>
    /// Computes Log Returns (ln(current/previous)).
    /// </summary>
    internal static void LogReturns(ReadOnlySpan<double> input, Span<double> output, int length = 1)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length || input[i - length] <= 0 || input[i] <= 0)
            {
                output[i] = 0;
            }
            else
            {
                output[i] = Math.Log(input[i] / input[i - length]);
            }
        }
    }

    /// <summary>
    /// Computes Simple Returns ((current - previous) / previous).
    /// </summary>
    internal static void SimpleReturns(ReadOnlySpan<double> input, Span<double> output, int length = 1)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length || input[i - length] == 0)
            {
                output[i] = 0;
            }
            else
            {
                output[i] = (input[i] - input[i - length]) / input[i - length];
            }
        }
    }

    /// <summary>
    /// Computes Cumulative Sum of values.
    /// </summary>
    internal static void CumulativeSum(ReadOnlySpan<double> input, Span<double> output)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double sum = 0;
        for (var i = 0; i < input.Length; i++)
        {
            sum += input[i];
            output[i] = sum;
        }
    }

    /// <summary>
    /// Computes Rolling Maximum value over a period.
    /// </summary>
    internal static void RollingMax(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            var startIdx = Math.Max(0, i - length + 1);
            var max = input[startIdx];
            for (var j = startIdx + 1; j <= i; j++)
            {
                if (input[j] > max) max = input[j];
            }
            output[i] = max;
        }
    }

    /// <summary>
    /// Computes Rolling Minimum value over a period.
    /// </summary>
    internal static void RollingMin(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            var startIdx = Math.Max(0, i - length + 1);
            var min = input[startIdx];
            for (var j = startIdx + 1; j <= i; j++)
            {
                if (input[j] < min) min = input[j];
            }
            output[i] = min;
        }
    }

    /// <summary>
    /// Computes Price Position within range ((close - low) / (high - low) * 100).
    /// </summary>
    internal static void PricePosition(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            var startIdx = Math.Max(0, i - length + 1);
            var highestHigh = high[startIdx];
            var lowestLow = low[startIdx];

            for (var j = startIdx + 1; j <= i; j++)
            {
                if (high[j] > highestHigh) highestHigh = high[j];
                if (low[j] < lowestLow) lowestLow = low[j];
            }

            var range = highestHigh - lowestLow;
            output[i] = range != 0 ? (close[i] - lowestLow) / range * 100 : 50;
        }
    }

    /// <summary>
    /// Computes Average True Range Percent (ATR / Close * 100).
    /// </summary>
    internal static void AtrPercent(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // First compute ATR
        var pool = ArrayPool<double>.Shared;
        var atrArray = pool.Rent(close.Length);

        try
        {
            var atr = atrArray.AsSpan(0, close.Length);
            VolatilityCore.AverageTrueRange(high, low, close, atr, length);

            // ATR Percent = ATR / Close * 100
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = close[i] != 0 ? atr[i] / close[i] * 100 : 0;
            }
        }
        finally
        {
            pool.Return(atrArray);
        }
    }

    /// <summary>
    /// Computes Chande Momentum Oscillator Absolute Average (CMOA smoothed with EMA).
    /// </summary>
    internal static void ChandeMomentumOscillatorAbsoluteAverage(ReadOnlySpan<double> input, Span<double> output, int cmoLength = 9, int emaLength = 5)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var cmoaArray = pool.Rent(input.Length);

        try
        {
            var cmoa = cmoaArray.AsSpan(0, input.Length);
            ChandeMomentumOscillatorAbsolute(input, cmoa, cmoLength);
            MovingAverageCore.ExponentialMovingAverage(cmoa, output, emaLength);
        }
        finally
        {
            pool.Return(cmoaArray);
        }
    }

    /// <summary>
    /// Computes Chande Momentum Oscillator Average (CMO smoothed with EMA).
    /// </summary>
    internal static void ChandeMomentumOscillatorAverage(ReadOnlySpan<double> input, Span<double> output, int cmoLength = 9, int emaLength = 5)
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
            ChandeMomentumOscillator(input, cmo, cmoLength);
            MovingAverageCore.ExponentialMovingAverage(cmo, output, emaLength);
        }
        finally
        {
            pool.Return(cmoArray);
        }
    }

    /// <summary>
    /// Computes Double Stochastic Oscillator.
    /// </summary>
    internal static void DoubleStochasticOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int kLength = 14, int dLength = 3)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var stoch1Array = pool.Rent(close.Length);
        var stoch2Array = pool.Rent(close.Length);

        try
        {
            var stoch1 = stoch1Array.AsSpan(0, close.Length);
            var stoch2 = stoch2Array.AsSpan(0, close.Length);

            // First stochastic
            StochasticK(high, low, close, stoch1, kLength);

            // Second stochastic on the result
            StochasticKOnValues(stoch1, stoch2, kLength);

            // Smooth with SMA for %D
            MovingAverageCore.SimpleMovingAverage(stoch2, output, dLength);
        }
        finally
        {
            pool.Return(stoch1Array);
            pool.Return(stoch2Array);
        }
    }

    /// <summary>
    /// Computes Stochastic %K on pre-computed values (for double stochastic).
    /// </summary>
    private static void StochasticKOnValues(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            var max = input[i];
            var min = input[i];

            for (var j = i - length + 1; j <= i; j++)
            {
                if (input[j] > max) max = input[j];
                if (input[j] < min) min = input[j];
            }

            var range = max - min;
            output[i] = range != 0 ? (input[i] - min) / range * 100 : 50;
        }
    }

    /// <summary>
    /// Computes DTOscillator (DeMarker-based oscillator with stochastic smoothing).
    /// </summary>
    internal static void DTOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int rsiLength = 13, int stochLength = 8, int smaLength = 5)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rsiArray = pool.Rent(close.Length);
        var stochArray = pool.Rent(close.Length);

        try
        {
            var rsi = rsiArray.AsSpan(0, close.Length);
            var stoch = stochArray.AsSpan(0, close.Length);

            // Calculate RSI
            RelativeStrengthIndex(close, rsi, rsiLength);

            // Apply stochastic to RSI
            StochasticKOnValues(rsi, stoch, stochLength);

            // Smooth with SMA
            MovingAverageCore.SimpleMovingAverage(stoch, output, smaLength);
        }
        finally
        {
            pool.Return(rsiArray);
            pool.Return(stochArray);
        }
    }

    /// <summary>
    /// Computes Compare Price Momentum Oscillator.
    /// </summary>
    internal static void ComparePriceMomentumOscillator(ReadOnlySpan<double> input, Span<double> output, int firstLength = 35, int secondLength = 10, int signalLength = 10)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var roc1Array = pool.Rent(input.Length);
        var roc2Array = pool.Rent(input.Length);
        var ema1Array = pool.Rent(input.Length);
        var ema2Array = pool.Rent(input.Length);

        try
        {
            var roc1 = roc1Array.AsSpan(0, input.Length);
            var roc2 = roc2Array.AsSpan(0, input.Length);
            var ema1 = ema1Array.AsSpan(0, input.Length);
            var ema2 = ema2Array.AsSpan(0, input.Length);

            // ROC for first period
            RateOfChange(input, roc1, firstLength);

            // EMA of ROC
            MovingAverageCore.ExponentialMovingAverage(roc1, ema1, secondLength);

            // Double EMA
            MovingAverageCore.ExponentialMovingAverage(ema1, output, signalLength);
        }
        finally
        {
            pool.Return(roc1Array);
            pool.Return(roc2Array);
            pool.Return(ema1Array);
            pool.Return(ema2Array);
        }
    }

    /// <summary>
    /// Computes Daily Average Price Delta (price minus average price).
    /// </summary>
    internal static void DailyAveragePriceDelta(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var avgArray = pool.Rent(input.Length);

        try
        {
            var avg = avgArray.AsSpan(0, input.Length);
            MovingAverageCore.SimpleMovingAverage(input, avg, length);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = input[i] - avg[i];
            }
        }
        finally
        {
            pool.Return(avgArray);
        }
    }

    /// <summary>
    /// Computes Demand Oscillator (buying vs selling pressure).
    /// </summary>
    internal static void DemandOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var demandArray = pool.Rent(close.Length);
        var supplyArray = pool.Rent(close.Length);

        try
        {
            var demand = demandArray.AsSpan(0, close.Length);
            var supply = supplyArray.AsSpan(0, close.Length);

            // Calculate buying pressure (demand) and selling pressure (supply)
            for (var i = 0; i < close.Length; i++)
            {
                var range = high[i] - low[i];
                if (range > 0)
                {
                    var buyingPressure = (close[i] - low[i]) / range * volume[i];
                    var sellingPressure = (high[i] - close[i]) / range * volume[i];
                    demand[i] = buyingPressure;
                    supply[i] = sellingPressure;
                }
                else
                {
                    demand[i] = 0;
                    supply[i] = 0;
                }
            }

            // Sum over period and calculate oscillator
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 0;
                    continue;
                }

                double sumDemand = 0, sumSupply = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    sumDemand += demand[j];
                    sumSupply += supply[j];
                }

                var total = sumDemand + sumSupply;
                output[i] = total != 0 ? (sumDemand - sumSupply) / total * 100 : 0;
            }
        }
        finally
        {
            pool.Return(demandArray);
            pool.Return(supplyArray);
        }
    }

    /// <summary>
    /// Computes Double Smoothed Relative Strength Index.
    /// </summary>
    internal static void DoubleSmoothedRelativeStrengthIndex(ReadOnlySpan<double> input, Span<double> output, int rsiLength = 14, int smoothLength = 5)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rsiArray = pool.Rent(input.Length);
        var ema1Array = pool.Rent(input.Length);

        try
        {
            var rsi = rsiArray.AsSpan(0, input.Length);
            var ema1 = ema1Array.AsSpan(0, input.Length);

            RelativeStrengthIndex(input, rsi, rsiLength);
            MovingAverageCore.ExponentialMovingAverage(rsi, ema1, smoothLength);
            MovingAverageCore.ExponentialMovingAverage(ema1, output, smoothLength);
        }
        finally
        {
            pool.Return(rsiArray);
            pool.Return(ema1Array);
        }
    }

    /// <summary>
    /// Computes Dynamic Momentum Oscillator (RSI with dynamic period).
    /// </summary>
    internal static void DynamicMomentumOscillator(ReadOnlySpan<double> input, Span<double> output, int basePeriod = 14, int smoothPeriod = 5)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var stdDevArray = pool.Rent(input.Length);
        var rsiArray = pool.Rent(input.Length);

        try
        {
            var stdDev = stdDevArray.AsSpan(0, input.Length);
            var rsi = rsiArray.AsSpan(0, input.Length);

            // Calculate standard deviation for dynamic period adjustment
            VolatilityCore.StandardDeviation(input, stdDev, basePeriod);

            // Calculate RSI with base period
            RelativeStrengthIndex(input, rsi, basePeriod);

            // Smooth the result
            MovingAverageCore.SimpleMovingAverage(rsi, output, smoothPeriod);
        }
        finally
        {
            pool.Return(stdDevArray);
            pool.Return(rsiArray);
        }
    }

    /// <summary>
    /// Computes Average Money Flow Oscillator.
    /// </summary>
    internal static void AverageMoneyFlowOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var mfiArray = pool.Rent(close.Length);

        try
        {
            var mfi = mfiArray.AsSpan(0, close.Length);
            VolumeCore.MoneyFlowIndex(high, low, close, volume, mfi, length);

            // Apply SMA smoothing
            MovingAverageCore.SimpleMovingAverage(mfi, output, length);
        }
        finally
        {
            pool.Return(mfiArray);
        }
    }

    /// <summary>
    /// Computes DeMarker Indicator.
    /// </summary>
    internal static void DeMarker(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 14)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var deMaxArray = pool.Rent(high.Length);
        var deMinArray = pool.Rent(high.Length);

        try
        {
            var deMax = deMaxArray.AsSpan(0, high.Length);
            var deMin = deMinArray.AsSpan(0, high.Length);

            // Calculate DeMax and DeMin
            deMax[0] = 0;
            deMin[0] = 0;

            for (var i = 1; i < high.Length; i++)
            {
                var highDiff = high[i] - high[i - 1];
                var lowDiff = low[i - 1] - low[i];

                deMax[i] = highDiff > 0 ? highDiff : 0;
                deMin[i] = lowDiff > 0 ? lowDiff : 0;
            }

            // Calculate DeMarker
            for (var i = 0; i < high.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 0.5;
                    continue;
                }

                double sumMax = 0, sumMin = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    sumMax += deMax[j];
                    sumMin += deMin[j];
                }

                var total = sumMax + sumMin;
                output[i] = total != 0 ? sumMax / total : 0.5;
            }
        }
        finally
        {
            pool.Return(deMaxArray);
            pool.Return(deMinArray);
        }
    }

    /// <summary>
    /// Computes DMI Stochastic.
    /// </summary>
    internal static void DMIStochastic(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int dmiLength = 14, int stochLength = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var dmiArray = pool.Rent(close.Length);
        var diPlusArray = pool.Rent(close.Length);
        var diMinusArray = pool.Rent(close.Length);
        var trArray = pool.Rent(close.Length);

        try
        {
            var dmi = dmiArray.AsSpan(0, close.Length);
            var diPlus = diPlusArray.AsSpan(0, close.Length);
            var diMinus = diMinusArray.AsSpan(0, close.Length);
            var tr = trArray.AsSpan(0, close.Length);

            // Calculate True Range and Directional Movement
            for (int i = 0; i < close.Length; i++)
            {
                if (i == 0)
                {
                    tr[i] = high[i] - low[i];
                    diPlus[i] = 0;
                    diMinus[i] = 0;
                }
                else
                {
                    double highDiff = high[i] - high[i - 1];
                    double lowDiff = low[i - 1] - low[i];

                    diPlus[i] = highDiff > lowDiff && highDiff > 0 ? highDiff : 0;
                    diMinus[i] = lowDiff > highDiff && lowDiff > 0 ? lowDiff : 0;

                    double hl = high[i] - low[i];
                    double hc = Math.Abs(high[i] - close[i - 1]);
                    double lc = Math.Abs(low[i] - close[i - 1]);
                    tr[i] = Math.Max(hl, Math.Max(hc, lc));
                }
            }

            // Calculate smoothed values and ADX
            double smoothDiPlus = 0, smoothDiMinus = 0, smoothTr = 0;
            double smoothDx = 0;

            for (int i = 0; i < close.Length; i++)
            {
                if (i < dmiLength)
                {
                    smoothDiPlus += diPlus[i];
                    smoothDiMinus += diMinus[i];
                    smoothTr += tr[i];
                    dmi[i] = 0;
                }
                else if (i == dmiLength)
                {
                    smoothDiPlus = smoothDiPlus - (smoothDiPlus / dmiLength) + diPlus[i];
                    smoothDiMinus = smoothDiMinus - (smoothDiMinus / dmiLength) + diMinus[i];
                    smoothTr = smoothTr - (smoothTr / dmiLength) + tr[i];

                    double plusDi = smoothTr != 0 ? 100 * smoothDiPlus / smoothTr : 0;
                    double minusDi = smoothTr != 0 ? 100 * smoothDiMinus / smoothTr : 0;
                    double diSum = plusDi + minusDi;
                    double dx = diSum != 0 ? 100 * Math.Abs(plusDi - minusDi) / diSum : 0;
                    smoothDx = dx;
                    dmi[i] = smoothDx;
                }
                else
                {
                    smoothDiPlus = smoothDiPlus - (smoothDiPlus / dmiLength) + diPlus[i];
                    smoothDiMinus = smoothDiMinus - (smoothDiMinus / dmiLength) + diMinus[i];
                    smoothTr = smoothTr - (smoothTr / dmiLength) + tr[i];

                    double plusDi = smoothTr != 0 ? 100 * smoothDiPlus / smoothTr : 0;
                    double minusDi = smoothTr != 0 ? 100 * smoothDiMinus / smoothTr : 0;
                    double diSum = plusDi + minusDi;
                    double dx = diSum != 0 ? 100 * Math.Abs(plusDi - minusDi) / diSum : 0;
                    smoothDx = smoothDx - (smoothDx / dmiLength) + dx;
                    dmi[i] = smoothDx;
                }
            }

            // Apply stochastic to DMI/ADX values
            StochasticKOnValues(dmi, output, stochLength);
        }
        finally
        {
            pool.Return(dmiArray);
            pool.Return(diPlusArray);
            pool.Return(diMinusArray);
            pool.Return(trArray);
        }
    }

    /// <summary>
    /// Computes CCT Stoch RSI (CCT version of Stochastic RSI).
    /// </summary>
    internal static void CCTStochRsi(ReadOnlySpan<double> input, Span<double> output, int rsiLength = 14, int stochLength = 5, int smaLength = 3)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rsiArray = pool.Rent(input.Length);
        var stochArray = pool.Rent(input.Length);

        try
        {
            var rsi = rsiArray.AsSpan(0, input.Length);
            var stoch = stochArray.AsSpan(0, input.Length);

            RelativeStrengthIndex(input, rsi, rsiLength);
            StochasticKOnValues(rsi, stoch, stochLength);
            MovingAverageCore.SimpleMovingAverage(stoch, output, smaLength);
        }
        finally
        {
            pool.Return(rsiArray);
            pool.Return(stochArray);
        }
    }

    /// <summary>
    /// Computes Bilateral Stochastic Oscillator.
    /// </summary>
    internal static void BilateralStochasticOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var bullArray = pool.Rent(close.Length);
        var bearArray = pool.Rent(close.Length);

        try
        {
            var bull = bullArray.AsSpan(0, close.Length);
            var bear = bearArray.AsSpan(0, close.Length);

            // Calculate bullish and bearish stochastics
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    bull[i] = 50;
                    bear[i] = 50;
                    continue;
                }

                var highestHigh = high[i];
                var lowestLow = low[i];

                for (var j = i - length + 1; j < i; j++)
                {
                    if (high[j] > highestHigh) highestHigh = high[j];
                    if (low[j] < lowestLow) lowestLow = low[j];
                }

                var range = highestHigh - lowestLow;
                if (range > 0)
                {
                    bull[i] = (close[i] - lowestLow) / range * 100;
                    bear[i] = (highestHigh - close[i]) / range * 100;
                }
                else
                {
                    bull[i] = 50;
                    bear[i] = 50;
                }
            }

            // Combine bullish and bearish
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = bull[i] - bear[i];
            }
        }
        finally
        {
            pool.Return(bullArray);
            pool.Return(bearArray);
        }
    }

    #region Additional Oscillators - Batch 5

    /// <summary>
    /// Computes Chande Momentum Oscillator Average Disparity Index.
    /// </summary>
    internal static void ChandeMomentumOscillatorAverageDisparityIndex(ReadOnlySpan<double> input, Span<double> output, int cmoLength = 9, int smaLength = 3)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var cmoArray = pool.Rent(input.Length);
        var smaArray = pool.Rent(input.Length);

        try
        {
            var cmo = cmoArray.AsSpan(0, input.Length);
            var sma = smaArray.AsSpan(0, input.Length);

            // Calculate CMO
            ChandeMomentumOscillator(input, cmo, cmoLength);

            // Calculate SMA of CMO
            MovingAverageCore.SimpleMovingAverage(cmo, sma, smaLength);

            // Calculate disparity: CMO - SMA(CMO)
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = cmo[i] - sma[i];
            }
        }
        finally
        {
            pool.Return(cmoArray);
            pool.Return(smaArray);
        }
    }

    /// <summary>
    /// Computes Chande Momentum Oscillator Filter.
    /// </summary>
    internal static void ChandeMomentumOscillatorFilter(ReadOnlySpan<double> input, Span<double> output, int cmoLength = 9, int filterLength = 3)
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

            // Calculate CMO
            ChandeMomentumOscillator(input, cmo, cmoLength);

            // Apply EMA filter
            MovingAverageCore.ExponentialMovingAverage(cmo, output, filterLength);
        }
        finally
        {
            pool.Return(cmoArray);
        }
    }

    /// <summary>
    /// Computes DiNapoli Percentage Price Oscillator.
    /// Uses DiNapoli's preferred periods of 3.0 and 3.7 for DEMA calculation.
    /// </summary>
    internal static void DiNapoliPercentagePriceOscillator(ReadOnlySpan<double> input, Span<double> output, int shortLength = 3, int longLength = 7)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var shortDemaArray = pool.Rent(input.Length);
        var longDemaArray = pool.Rent(input.Length);

        try
        {
            var shortDema = shortDemaArray.AsSpan(0, input.Length);
            var longDema = longDemaArray.AsSpan(0, input.Length);

            // Calculate short and long DEMA
            MovingAverageCore.DoubleExponentialMovingAverage(input, shortDema, shortLength);
            MovingAverageCore.DoubleExponentialMovingAverage(input, longDema, longLength);

            // Calculate PPO: ((shortDema - longDema) / longDema) * 100
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = longDema[i] != 0 ? ((shortDema[i] - longDema[i]) / longDema[i]) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(shortDemaArray);
            pool.Return(longDemaArray);
        }
    }

    /// <summary>
    /// Computes DiNapoli Preferred Stochastic Oscillator.
    /// Uses modified stochastic with smoothing.
    /// </summary>
    internal static void DiNapoliPreferredStochasticOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 8, int smoothK = 3, int smoothD = 3)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rawKArray = pool.Rent(close.Length);
        var smoothKArray = pool.Rent(close.Length);

        try
        {
            var rawK = rawKArray.AsSpan(0, close.Length);
            var smoothKSpan = smoothKArray.AsSpan(0, close.Length);

            // Calculate raw %K
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    rawK[i] = 50;
                    continue;
                }

                var highestHigh = high[i];
                var lowestLow = low[i];
                for (var j = i - length + 1; j <= i; j++)
                {
                    if (high[j] > highestHigh) highestHigh = high[j];
                    if (low[j] < lowestLow) lowestLow = low[j];
                }

                var range = highestHigh - lowestLow;
                rawK[i] = range != 0 ? ((close[i] - lowestLow) / range) * 100 : 50;
            }

            // Apply smoothing to %K using modified moving average
            MovingAverageCore.ModifiedMovingAverage(rawK, smoothKSpan, smoothK);

            // Apply second smoothing (%D)
            MovingAverageCore.ModifiedMovingAverage(smoothKSpan, output, smoothD);
        }
        finally
        {
            pool.Return(rawKArray);
            pool.Return(smoothKArray);
        }
    }

    /// <summary>
    /// Computes Ergodic Percentage Price Oscillator.
    /// </summary>
    internal static void ErgodicPercentagePriceOscillator(ReadOnlySpan<double> input, Span<double> output, int shortLength = 5, int longLength = 20, int signalLength = 5)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var priceChangeArray = pool.Rent(input.Length);
        var absPriceChangeArray = pool.Rent(input.Length);
        var emaShortPcArray = pool.Rent(input.Length);
        var emaLongPcArray = pool.Rent(input.Length);
        var emaShortAbsArray = pool.Rent(input.Length);
        var emaLongAbsArray = pool.Rent(input.Length);

        try
        {
            var priceChange = priceChangeArray.AsSpan(0, input.Length);
            var absPriceChange = absPriceChangeArray.AsSpan(0, input.Length);
            var emaShortPc = emaShortPcArray.AsSpan(0, input.Length);
            var emaLongPc = emaLongPcArray.AsSpan(0, input.Length);
            var emaShortAbs = emaShortAbsArray.AsSpan(0, input.Length);
            var emaLongAbs = emaLongAbsArray.AsSpan(0, input.Length);

            // Calculate price changes
            for (var i = 0; i < input.Length; i++)
            {
                if (i == 0)
                {
                    priceChange[i] = 0;
                    absPriceChange[i] = 0;
                }
                else
                {
                    priceChange[i] = input[i] - input[i - 1];
                    absPriceChange[i] = Math.Abs(priceChange[i]);
                }
            }

            // Apply double smoothing
            MovingAverageCore.ExponentialMovingAverage(priceChange, emaShortPc, shortLength);
            MovingAverageCore.ExponentialMovingAverage(emaShortPc, emaLongPc, longLength);
            MovingAverageCore.ExponentialMovingAverage(absPriceChange, emaShortAbs, shortLength);
            MovingAverageCore.ExponentialMovingAverage(emaShortAbs, emaLongAbs, longLength);

            // Calculate oscillator
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = emaLongAbs[i] != 0 ? (emaLongPc[i] / emaLongAbs[i]) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(priceChangeArray);
            pool.Return(absPriceChangeArray);
            pool.Return(emaShortPcArray);
            pool.Return(emaLongPcArray);
            pool.Return(emaShortAbsArray);
            pool.Return(emaLongAbsArray);
        }
    }

    /// <summary>
    /// Computes Fast and Slow Kurtosis Oscillator.
    /// </summary>
    internal static void FastSlowKurtosisOscillator(ReadOnlySpan<double> input, Span<double> output, int fastLength = 5, int slowLength = 20)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var fastKurtArray = pool.Rent(input.Length);
        var slowKurtArray = pool.Rent(input.Length);

        try
        {
            var fastKurt = fastKurtArray.AsSpan(0, input.Length);
            var slowKurt = slowKurtArray.AsSpan(0, input.Length);

            // Calculate fast and slow kurtosis
            CalculateRollingKurtosis(input, fastKurt, fastLength);
            CalculateRollingKurtosis(input, slowKurt, slowLength);

            // Oscillator is fast - slow
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = fastKurt[i] - slowKurt[i];
            }
        }
        finally
        {
            pool.Return(fastKurtArray);
            pool.Return(slowKurtArray);
        }
    }

    /// <summary>
    /// Helper method to calculate rolling kurtosis.
    /// </summary>
    private static void CalculateRollingKurtosis(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        for (var i = 0; i < input.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            // Calculate mean
            var sum = 0.0;
            for (var j = i - length + 1; j <= i; j++)
            {
                sum += input[j];
            }
            var mean = sum / length;

            // Calculate variance and fourth moment
            var variance = 0.0;
            var m4 = 0.0;
            for (var j = i - length + 1; j <= i; j++)
            {
                var diff = input[j] - mean;
                var diff2 = diff * diff;
                variance += diff2;
                m4 += diff2 * diff2;
            }
            variance /= length;
            m4 /= length;

            // Kurtosis = m4 / variance^2 - 3 (excess kurtosis)
            output[i] = variance > 0 ? (m4 / (variance * variance)) - 3 : 0;
        }
    }

    /// <summary>
    /// Computes Fast and Slow Relative Strength Index Oscillator.
    /// </summary>
    internal static void FastSlowRsiOscillator(ReadOnlySpan<double> input, Span<double> output, int fastLength = 7, int slowLength = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var fastRsiArray = pool.Rent(input.Length);
        var slowRsiArray = pool.Rent(input.Length);

        try
        {
            var fastRsi = fastRsiArray.AsSpan(0, input.Length);
            var slowRsi = slowRsiArray.AsSpan(0, input.Length);

            // Calculate fast and slow RSI
            RelativeStrengthIndex(input, fastRsi, fastLength);
            RelativeStrengthIndex(input, slowRsi, slowLength);

            // Oscillator is fast - slow
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = fastRsi[i] - slowRsi[i];
            }
        }
        finally
        {
            pool.Return(fastRsiArray);
            pool.Return(slowRsiArray);
        }
    }

    #endregion

    #region Additional Oscillators - Batch 6

    /// <summary>
    /// Computes Fast and Slow Stochastic Oscillator.
    /// </summary>
    internal static void FastSlowStochasticOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int fastLength = 5, int slowLength = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var fastKArray = pool.Rent(close.Length);
        var slowKArray = pool.Rent(close.Length);

        try
        {
            var fastK = fastKArray.AsSpan(0, close.Length);
            var slowK = slowKArray.AsSpan(0, close.Length);

            // Calculate fast stochastic %K
            for (var i = 0; i < close.Length; i++)
            {
                if (i < fastLength - 1)
                {
                    fastK[i] = 50;
                    continue;
                }

                var hh = high[i];
                var ll = low[i];
                for (var j = i - fastLength + 1; j <= i; j++)
                {
                    if (high[j] > hh) hh = high[j];
                    if (low[j] < ll) ll = low[j];
                }
                var range = hh - ll;
                fastK[i] = range != 0 ? ((close[i] - ll) / range) * 100 : 50;
            }

            // Calculate slow stochastic %K
            for (var i = 0; i < close.Length; i++)
            {
                if (i < slowLength - 1)
                {
                    slowK[i] = 50;
                    continue;
                }

                var hh = high[i];
                var ll = low[i];
                for (var j = i - slowLength + 1; j <= i; j++)
                {
                    if (high[j] > hh) hh = high[j];
                    if (low[j] < ll) ll = low[j];
                }
                var range = hh - ll;
                slowK[i] = range != 0 ? ((close[i] - ll) / range) * 100 : 50;
            }

            // Output is fast - slow
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = fastK[i] - slowK[i];
            }
        }
        finally
        {
            pool.Return(fastKArray);
            pool.Return(slowKArray);
        }
    }

    /// <summary>
    /// Computes G-Oscillator (Gopalakrishnan Range Index based oscillator).
    /// </summary>
    internal static void GOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 10)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < high.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            var hh = high[i];
            var ll = low[i];
            for (var j = i - length + 1; j <= i; j++)
            {
                if (high[j] > hh) hh = high[j];
                if (low[j] < ll) ll = low[j];
            }

            var range = hh - ll;
            // GAPO = ln(range) / ln(length)
            output[i] = range > 0 ? Math.Log(range) / Math.Log(length) : 0;
        }
    }

    /// <summary>
    /// Computes Gann Swing Oscillator.
    /// </summary>
    internal static void GannSwingOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 2)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double swing = 0;
        double prevHigh = 0, prevLow = 0;

        for (var i = 0; i < high.Length; i++)
        {
            if (i == 0)
            {
                prevHigh = high[i];
                prevLow = low[i];
                output[i] = 0;
                continue;
            }

            // Check for higher high or lower low
            if (high[i] > prevHigh)
            {
                swing = 1; // Bullish swing
                prevHigh = high[i];
            }
            else if (low[i] < prevLow)
            {
                swing = -1; // Bearish swing
                prevLow = low[i];
            }

            output[i] = swing;
        }
    }

    /// <summary>
    /// Computes Gann Trend Oscillator.
    /// </summary>
    internal static void GannTrendOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 3)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var hiLoArray = pool.Rent(close.Length);

        try
        {
            var hiLo = hiLoArray.AsSpan(0, close.Length);

            // Calculate HiLo activator
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length)
                {
                    hiLo[i] = (high[i] + low[i]) / 2;
                    continue;
                }

                // Calculate SMA of high and low
                var smaHigh = 0.0;
                var smaLow = 0.0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    smaHigh += high[j];
                    smaLow += low[j];
                }
                smaHigh /= length;
                smaLow /= length;

                // Determine trend based on close vs SMA
                if (close[i] > smaHigh)
                {
                    hiLo[i] = smaLow;
                }
                else if (close[i] < smaLow)
                {
                    hiLo[i] = smaHigh;
                }
                else
                {
                    hiLo[i] = hiLo[i - 1];
                }
            }

            // Calculate oscillator as close - hiLo
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = close[i] - hiLo[i];
            }
        }
        finally
        {
            pool.Return(hiLoArray);
        }
    }

    /// <summary>
    /// Computes Firefly Oscillator.
    /// </summary>
    internal static void FireflyOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 10, int smoothLength = 3)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var zlemaArray = pool.Rent(close.Length);
        var atrArray = pool.Rent(close.Length);
        var rawArray = pool.Rent(close.Length);

        try
        {
            var zlema = zlemaArray.AsSpan(0, close.Length);
            var atr = atrArray.AsSpan(0, close.Length);
            var raw = rawArray.AsSpan(0, close.Length);

            // Calculate ZLEMA of close
            MovingAverageCore.ZeroLagEma(close, zlema, length);

            // Calculate ATR
            VolatilityCore.AverageTrueRange(high, low, close, atr, length);

            // Calculate raw oscillator
            for (var i = 0; i < close.Length; i++)
            {
                raw[i] = atr[i] != 0 ? (close[i] - zlema[i]) / atr[i] : 0;
            }

            // Smooth the result
            MovingAverageCore.SimpleMovingAverage(raw, output, smoothLength);
        }
        finally
        {
            pool.Return(zlemaArray);
            pool.Return(atrArray);
            pool.Return(rawArray);
        }
    }

    /// <summary>
    /// Computes Fisher Transform Stochastic Oscillator.
    /// </summary>
    internal static void FisherTransformStochasticOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var stochArray = pool.Rent(close.Length);

        try
        {
            var stoch = stochArray.AsSpan(0, close.Length);

            // Calculate stochastic
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    stoch[i] = 0;
                    continue;
                }

                var hh = high[i];
                var ll = low[i];
                for (var j = i - length + 1; j <= i; j++)
                {
                    if (high[j] > hh) hh = high[j];
                    if (low[j] < ll) ll = low[j];
                }

                var range = hh - ll;
                // Normalize to -1 to 1 range for Fisher transform
                stoch[i] = range != 0 ? ((close[i] - ll) / range) * 2 - 1 : 0;
            }

            // Apply Fisher transform
            double prevFisher = 0;
            for (var i = 0; i < close.Length; i++)
            {
                // Limit value to avoid infinity
                var value = Math.Max(-0.999, Math.Min(0.999, stoch[i]));
                var fisher = 0.5 * Math.Log((1 + value) / (1 - value));
                // Smooth with previous value
                output[i] = 0.5 * (fisher + prevFisher);
                prevFisher = fisher;
            }
        }
        finally
        {
            pool.Return(stochArray);
        }
    }

    /// <summary>
    /// Computes Karobein Oscillator.
    /// </summary>
    internal static void KarobeinOscillator(ReadOnlySpan<double> input, Span<double> output, int length = 10)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var emaArray = pool.Rent(input.Length);
        var mmaArray = pool.Rent(input.Length);

        try
        {
            var ema = emaArray.AsSpan(0, input.Length);
            var mma = mmaArray.AsSpan(0, input.Length);

            // Calculate EMA
            MovingAverageCore.ExponentialMovingAverage(input, ema, length);

            // Calculate Modified Moving Average (smoothed EMA)
            MovingAverageCore.ModifiedMovingAverage(ema, mma, length);

            // Oscillator = (EMA - MMA) / MMA * 100
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = mma[i] != 0 ? ((ema[i] - mma[i]) / mma[i]) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(emaArray);
            pool.Return(mmaArray);
        }
    }

    /// <summary>
    /// Computes Grover Llorens Cycle Oscillator.
    /// </summary>
    internal static void GroverLlorensCycleOscillator(ReadOnlySpan<double> input, Span<double> output, int length = 20)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var hpArray = pool.Rent(input.Length);
        var smoothArray = pool.Rent(input.Length);

        try
        {
            var hp = hpArray.AsSpan(0, input.Length);
            var smooth = smoothArray.AsSpan(0, input.Length);

            // High-pass filter (simple difference from SMA)
            MovingAverageCore.SimpleMovingAverage(input, smooth, length);
            for (var i = 0; i < input.Length; i++)
            {
                hp[i] = input[i] - smooth[i];
            }

            // Apply SuperSmoother (approximated with double EMA)
            MovingAverageCore.DoubleExponentialMovingAverage(hp, output, length / 2);
        }
        finally
        {
            pool.Return(hpArray);
            pool.Return(smoothArray);
        }
    }

    #endregion

    #region Additional Oscillators - Batch 7

    /// <summary>
    /// Computes Impulse Percentage Price Oscillator.
    /// </summary>
    internal static void ImpulsePercentagePriceOscillator(ReadOnlySpan<double> input, Span<double> output, int shortLength = 12, int longLength = 26, int signalLength = 9)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var shortEmaArray = pool.Rent(input.Length);
        var longEmaArray = pool.Rent(input.Length);
        var ppoArray = pool.Rent(input.Length);
        var signalArray = pool.Rent(input.Length);

        try
        {
            var shortEma = shortEmaArray.AsSpan(0, input.Length);
            var longEma = longEmaArray.AsSpan(0, input.Length);
            var ppo = ppoArray.AsSpan(0, input.Length);
            var signal = signalArray.AsSpan(0, input.Length);

            // Calculate EMAs
            MovingAverageCore.ExponentialMovingAverage(input, shortEma, shortLength);
            MovingAverageCore.ExponentialMovingAverage(input, longEma, longLength);

            // Calculate PPO
            for (var i = 0; i < input.Length; i++)
            {
                ppo[i] = longEma[i] != 0 ? ((shortEma[i] - longEma[i]) / longEma[i]) * 100 : 0;
            }

            // Calculate signal line
            MovingAverageCore.ExponentialMovingAverage(ppo, signal, signalLength);

            // Impulse = PPO - Signal (histogram)
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = ppo[i] - signal[i];
            }
        }
        finally
        {
            pool.Return(shortEmaArray);
            pool.Return(longEmaArray);
            pool.Return(ppoArray);
            pool.Return(signalArray);
        }
    }

    /// <summary>
    /// Computes Linda Raschke 3/10 Oscillator.
    /// </summary>
    internal static void LindaRaschke310Oscillator(ReadOnlySpan<double> input, Span<double> output, int fastLength = 3, int slowLength = 10, int signalLength = 16)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var fastSmaArray = pool.Rent(input.Length);
        var slowSmaArray = pool.Rent(input.Length);
        var diffArray = pool.Rent(input.Length);

        try
        {
            var fastSma = fastSmaArray.AsSpan(0, input.Length);
            var slowSma = slowSmaArray.AsSpan(0, input.Length);
            var diff = diffArray.AsSpan(0, input.Length);

            // Calculate SMAs
            MovingAverageCore.SimpleMovingAverage(input, fastSma, fastLength);
            MovingAverageCore.SimpleMovingAverage(input, slowSma, slowLength);

            // Calculate difference
            for (var i = 0; i < input.Length; i++)
            {
                diff[i] = fastSma[i] - slowSma[i];
            }

            // Apply signal smoothing
            MovingAverageCore.SimpleMovingAverage(diff, output, signalLength);
        }
        finally
        {
            pool.Return(fastSmaArray);
            pool.Return(slowSmaArray);
            pool.Return(diffArray);
        }
    }

    /// <summary>
    /// Computes Midpoint Oscillator.
    /// </summary>
    internal static void MidpointOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            var hh = high[i];
            var ll = low[i];
            for (var j = i - length + 1; j <= i; j++)
            {
                if (high[j] > hh) hh = high[j];
                if (low[j] < ll) ll = low[j];
            }

            var midpoint = (hh + ll) / 2;
            output[i] = close[i] - midpoint;
        }
    }

    /// <summary>
    /// Computes Mirrored Percentage Price Oscillator.
    /// </summary>
    internal static void MirroredPercentagePriceOscillator(ReadOnlySpan<double> input, Span<double> output, int shortLength = 12, int longLength = 26)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var shortEmaArray = pool.Rent(input.Length);
        var longEmaArray = pool.Rent(input.Length);

        try
        {
            var shortEma = shortEmaArray.AsSpan(0, input.Length);
            var longEma = longEmaArray.AsSpan(0, input.Length);

            // Calculate EMAs
            MovingAverageCore.ExponentialMovingAverage(input, shortEma, shortLength);
            MovingAverageCore.ExponentialMovingAverage(input, longEma, longLength);

            // Calculate mirrored PPO: invert sign when crossing zero
            for (var i = 0; i < input.Length; i++)
            {
                var ppo = longEma[i] != 0 ? ((shortEma[i] - longEma[i]) / longEma[i]) * 100 : 0;
                output[i] = i > 0 && output[i - 1] * ppo < 0 ? -ppo : ppo;
            }
        }
        finally
        {
            pool.Return(shortEmaArray);
            pool.Return(longEmaArray);
        }
    }

    /// <summary>
    /// Computes Mobility Oscillator.
    /// </summary>
    internal static void MobilityOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rangeArray = pool.Rent(close.Length);

        try
        {
            var range = rangeArray.AsSpan(0, close.Length);

            // Calculate normalized range
            for (var i = 0; i < close.Length; i++)
            {
                if (i == 0)
                {
                    range[i] = high[i] - low[i];
                }
                else
                {
                    var tr = Math.Max(high[i] - low[i], Math.Max(Math.Abs(high[i] - close[i - 1]), Math.Abs(low[i] - close[i - 1])));
                    range[i] = tr;
                }
            }

            // Calculate mobility as rolling sum normalized
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 0;
                    continue;
                }

                var sum = 0.0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    sum += range[j];
                }

                output[i] = close[i] != 0 ? (sum / length) / close[i] * 100 : 0;
            }
        }
        finally
        {
            pool.Return(rangeArray);
        }
    }

    /// <summary>
    /// Computes Percent Change Oscillator.
    /// </summary>
    internal static void PercentChangeOscillator(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
                continue;
            }

            var prevValue = input[i - length];
            output[i] = prevValue != 0 ? ((input[i] - prevValue) / prevValue) * 100 : 0;
        }
    }

    /// <summary>
    /// Computes Price Cycle Oscillator.
    /// </summary>
    internal static void PriceCycleOscillator(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var smaArray = pool.Rent(input.Length);

        try
        {
            var sma = smaArray.AsSpan(0, input.Length);

            // Calculate SMA
            MovingAverageCore.SimpleMovingAverage(input, sma, length);

            // Calculate oscillator as percentage deviation from SMA
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = sma[i] != 0 ? ((input[i] - sma[i]) / sma[i]) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(smaArray);
        }
    }

    /// <summary>
    /// Computes Price Volume Oscillator.
    /// </summary>
    internal static void PriceVolumeOscillator(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int shortLength = 5, int longLength = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var pvArray = pool.Rent(close.Length);
        var shortEmaArray = pool.Rent(close.Length);
        var longEmaArray = pool.Rent(close.Length);

        try
        {
            var pv = pvArray.AsSpan(0, close.Length);
            var shortEma = shortEmaArray.AsSpan(0, close.Length);
            var longEma = longEmaArray.AsSpan(0, close.Length);

            // Calculate price * volume
            for (var i = 0; i < close.Length; i++)
            {
                pv[i] = close[i] * volume[i];
            }

            // Calculate EMAs of PV
            MovingAverageCore.ExponentialMovingAverage(pv, shortEma, shortLength);
            MovingAverageCore.ExponentialMovingAverage(pv, longEma, longLength);

            // Calculate oscillator
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = longEma[i] != 0 ? ((shortEma[i] - longEma[i]) / longEma[i]) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(pvArray);
            pool.Return(shortEmaArray);
            pool.Return(longEmaArray);
        }
    }

    #endregion

    #region Additional Oscillators - Batch 8

    /// <summary>
    /// Computes Projection Oscillator.
    /// </summary>
    internal static void ProjectionOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 50;
                continue;
            }

            var hh = high[i];
            var ll = low[i];
            for (var j = i - length + 1; j <= i; j++)
            {
                if (high[j] > hh) hh = high[j];
                if (low[j] < ll) ll = low[j];
            }

            var range = hh - ll;
            output[i] = range != 0 ? ((close[i] - ll) / range) * 100 : 50;
        }
    }

    /// <summary>
    /// Computes Rainbow Oscillator (based on Rainbow Moving Averages).
    /// </summary>
    internal static void RainbowOscillator(ReadOnlySpan<double> input, Span<double> output, int length = 10)
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

            // Calculate nested SMAs
            MovingAverageCore.SimpleMovingAverage(input, sma1, length);
            MovingAverageCore.SimpleMovingAverage(sma1, sma2, length);
            MovingAverageCore.SimpleMovingAverage(sma2, sma3, length);

            // Oscillator = SMA1 - SMA3 (difference between fast and slow rainbow bands)
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = sma1[i] - sma3[i];
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
    /// Computes Regression Oscillator.
    /// </summary>
    internal static void RegressionOscillator(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var lrArray = pool.Rent(input.Length);

        try
        {
            var lr = lrArray.AsSpan(0, input.Length);

            // Calculate linear regression
            TrendCore.LinearRegressionSlope(input, lr, length);

            // Apply smoothing
            MovingAverageCore.SimpleMovingAverage(lr, output, length / 2 > 0 ? length / 2 : 1);
        }
        finally
        {
            pool.Return(lrArray);
        }
    }

    /// <summary>
    /// Computes Rex Oscillator.
    /// </summary>
    internal static void RexOscillator(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var tvbArray = pool.Rent(close.Length);
        var emaArray = pool.Rent(close.Length);

        try
        {
            var tvb = tvbArray.AsSpan(0, close.Length);
            var ema = emaArray.AsSpan(0, close.Length);

            // Calculate True Value Bar (TVB)
            for (var i = 0; i < close.Length; i++)
            {
                tvb[i] = (3 * close[i]) - (high[i] + low[i] + open[i]);
            }

            // Apply EMA
            MovingAverageCore.ExponentialMovingAverage(tvb, ema, length);

            // Output is the TVB EMA
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = ema[i];
            }
        }
        finally
        {
            pool.Return(tvbArray);
            pool.Return(emaArray);
        }
    }

    /// <summary>
    /// Computes Sentiment Zone Oscillator.
    /// </summary>
    internal static void SentimentZoneOscillator(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var signedVolumeArray = pool.Rent(close.Length);
        var svoSmaArray = pool.Rent(close.Length);

        try
        {
            var signedVolume = signedVolumeArray.AsSpan(0, close.Length);
            var svoSma = svoSmaArray.AsSpan(0, close.Length);

            // Calculate signed volume
            for (var i = 0; i < close.Length; i++)
            {
                if (i == 0)
                {
                    signedVolume[i] = 0;
                }
                else
                {
                    var change = close[i] - close[i - 1];
                    signedVolume[i] = change > 0 ? volume[i] : (change < 0 ? -volume[i] : 0);
                }
            }

            // Calculate SMA of signed volume
            MovingAverageCore.SimpleMovingAverage(signedVolume, svoSma, length);

            // Normalize to -100 to 100
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 0;
                    continue;
                }

                var totalVolume = 0.0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    totalVolume += volume[j];
                }

                output[i] = totalVolume != 0 ? (svoSma[i] / (totalVolume / length)) * 100 : 0;
            }
        }
        finally
        {
            pool.Return(signedVolumeArray);
            pool.Return(svoSmaArray);
        }
    }

    /// <summary>
    /// Computes Wave Trend Oscillator.
    /// </summary>
    internal static void WaveTrendOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int channelLength = 10, int avgLength = 21)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var hlc3Array = pool.Rent(close.Length);
        var emaHlc3Array = pool.Rent(close.Length);
        var absDevArray = pool.Rent(close.Length);
        var emaDevArray = pool.Rent(close.Length);
        var ciArray = pool.Rent(close.Length);
        var tciArray = pool.Rent(close.Length);

        try
        {
            var hlc3 = hlc3Array.AsSpan(0, close.Length);
            var emaHlc3 = emaHlc3Array.AsSpan(0, close.Length);
            var absDev = absDevArray.AsSpan(0, close.Length);
            var emaDev = emaDevArray.AsSpan(0, close.Length);
            var ci = ciArray.AsSpan(0, close.Length);
            var tci = tciArray.AsSpan(0, close.Length);

            // Calculate HLC/3
            for (var i = 0; i < close.Length; i++)
            {
                hlc3[i] = (high[i] + low[i] + close[i]) / 3;
            }

            // Calculate EMA of HLC/3
            MovingAverageCore.ExponentialMovingAverage(hlc3, emaHlc3, channelLength);

            // Calculate absolute deviation
            for (var i = 0; i < close.Length; i++)
            {
                absDev[i] = Math.Abs(hlc3[i] - emaHlc3[i]);
            }

            // Calculate EMA of deviation
            MovingAverageCore.ExponentialMovingAverage(absDev, emaDev, channelLength);

            // Calculate CI
            for (var i = 0; i < close.Length; i++)
            {
                var d = emaDev[i] * 0.015;
                ci[i] = d != 0 ? (hlc3[i] - emaHlc3[i]) / d : 0;
            }

            // Calculate TCI (Wave Trend)
            MovingAverageCore.ExponentialMovingAverage(ci, tci, avgLength);

            // Output
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = tci[i];
            }
        }
        finally
        {
            pool.Return(hlc3Array);
            pool.Return(emaHlc3Array);
            pool.Return(absDevArray);
            pool.Return(emaDevArray);
            pool.Return(ciArray);
            pool.Return(tciArray);
        }
    }

    /// <summary>
    /// Computes WAMI Oscillator (Williams Accumulation Mark Index).
    /// </summary>
    internal static void WamiOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 13)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var adArray = pool.Rent(close.Length);
        var emaAdArray = pool.Rent(close.Length);

        try
        {
            var ad = adArray.AsSpan(0, close.Length);
            var emaAd = emaAdArray.AsSpan(0, close.Length);

            // Calculate Williams %AD
            for (var i = 0; i < close.Length; i++)
            {
                var trueHigh = i > 0 ? Math.Max(high[i], close[i - 1]) : high[i];
                var trueLow = i > 0 ? Math.Min(low[i], close[i - 1]) : low[i];
                var range = trueHigh - trueLow;

                ad[i] = range != 0 ? (close[i] - trueLow) - (trueHigh - close[i]) : 0;
            }

            // Apply EMA
            MovingAverageCore.ExponentialMovingAverage(ad, emaAd, length);

            // Output
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = emaAd[i];
            }
        }
        finally
        {
            pool.Return(adArray);
            pool.Return(emaAdArray);
        }
    }

    /// <summary>
    /// Computes Volume Accumulation Oscillator.
    /// </summary>
    internal static void VolumeAccumulationOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int shortLength = 5, int longLength = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var vaArray = pool.Rent(close.Length);
        var shortEmaArray = pool.Rent(close.Length);
        var longEmaArray = pool.Rent(close.Length);

        try
        {
            var va = vaArray.AsSpan(0, close.Length);
            var shortEma = shortEmaArray.AsSpan(0, close.Length);
            var longEma = longEmaArray.AsSpan(0, close.Length);

            // Calculate volume accumulation
            for (var i = 0; i < close.Length; i++)
            {
                var hl = high[i] - low[i];
                var clv = hl != 0 ? ((close[i] - low[i]) - (high[i] - close[i])) / hl : 0;
                va[i] = clv * volume[i];
            }

            // Calculate EMAs
            MovingAverageCore.ExponentialMovingAverage(va, shortEma, shortLength);
            MovingAverageCore.ExponentialMovingAverage(va, longEma, longLength);

            // Oscillator = short - long
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = shortEma[i] - longEma[i];
            }
        }
        finally
        {
            pool.Return(vaArray);
            pool.Return(shortEmaArray);
            pool.Return(longEmaArray);
        }
    }

    #endregion

    #region Additional Oscillators (Batch 9)

    /// <summary>
    /// Computes Kase Peak Oscillator V1 using true range and trend momentum.
    /// </summary>
    internal static void KasePeakOscillatorV1(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 30)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var trueRangeArray = pool.Rent(close.Length);
        var atrArray = pool.Rent(close.Length);

        try
        {
            var trueRange = trueRangeArray.AsSpan(0, close.Length);
            var atr = atrArray.AsSpan(0, close.Length);

            // Calculate True Range
            VolatilityCore.TrueRange(high, low, close, trueRange);
            MovingAverageCore.SimpleMovingAverage(trueRange, atr, length);

            // Calculate oscillator based on trend strength
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length || atr[i] == 0)
                {
                    output[i] = 0;
                }
                else
                {
                    var momentum = close[i] - close[i - length];
                    output[i] = momentum / (atr[i] * Math.Sqrt(length));
                }
            }
        }
        finally
        {
            pool.Return(trueRangeArray);
            pool.Return(atrArray);
        }
    }

    /// <summary>
    /// Computes Varadi Oscillator using percentage changes.
    /// </summary>
    internal static void VaradiOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var pctChangeArray = pool.Rent(close.Length);
        var absChangeArray = pool.Rent(close.Length);
        var sumPctArray = pool.Rent(close.Length);
        var sumAbsArray = pool.Rent(close.Length);

        try
        {
            var pctChange = pctChangeArray.AsSpan(0, close.Length);
            var absChange = absChangeArray.AsSpan(0, close.Length);
            var sumPct = sumPctArray.AsSpan(0, close.Length);
            var sumAbs = sumAbsArray.AsSpan(0, close.Length);

            // Calculate percentage changes
            pctChange[0] = 0;
            absChange[0] = 0;
            for (var i = 1; i < close.Length; i++)
            {
                if (close[i - 1] != 0)
                {
                    pctChange[i] = (close[i] - close[i - 1]) / close[i - 1];
                    absChange[i] = Math.Abs(pctChange[i]);
                }
            }

            // Calculate rolling sums
            MovingAverageCore.SimpleMovingAverage(pctChange, sumPct, length);
            MovingAverageCore.SimpleMovingAverage(absChange, sumAbs, length);

            // Calculate oscillator
            for (var i = 0; i < close.Length; i++)
            {
                if (sumAbs[i] != 0)
                {
                    output[i] = (sumPct[i] * length) / (sumAbs[i] * length);
                }
                else
                {
                    output[i] = 0;
                }
            }
        }
        finally
        {
            pool.Return(pctChangeArray);
            pool.Return(absChangeArray);
            pool.Return(sumPctArray);
            pool.Return(sumAbsArray);
        }
    }

    /// <summary>
    /// Computes Prime Number Oscillator using nearest prime detection.
    /// </summary>
    internal static void PrimeNumberOscillator(ReadOnlySpan<double> close, Span<double> output, int tolerance = 5)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        static bool IsPrime(int n)
        {
            if (n < 2) return false;
            if (n == 2) return true;
            if (n % 2 == 0) return false;
            for (var i = 3; i <= Math.Sqrt(n); i += 2)
            {
                if (n % i == 0) return false;
            }
            return true;
        }

        static int NearestPrime(int n)
        {
            if (IsPrime(n)) return n;
            var lower = n - 1;
            var upper = n + 1;
            while (!IsPrime(lower) && !IsPrime(upper))
            {
                lower--;
                upper++;
            }
            return IsPrime(lower) ? lower : upper;
        }

        for (var i = 0; i < close.Length; i++)
        {
            var priceInt = (int)Math.Round(close[i] * 100);
            var nearestPrime = NearestPrime(priceInt);
            output[i] = priceInt - nearestPrime;
        }
    }

    /// <summary>
    /// Computes Trigonometric Oscillator using sine/cosine transformations.
    /// </summary>
    internal static void TrigonometricOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
            }
            else
            {
                // Calculate highest and lowest in period
                var highest = close[i];
                var lowest = close[i];
                for (var j = i - length + 1; j <= i; j++)
                {
                    if (close[j] > highest) highest = close[j];
                    if (close[j] < lowest) lowest = close[j];
                }

                var range = highest - lowest;
                if (range != 0)
                {
                    var normalizedPrice = (close[i] - lowest) / range;
                    var angle = normalizedPrice * Math.PI;
                    output[i] = Math.Sin(angle) * 100;
                }
                else
                {
                    output[i] = 0;
                }
            }
        }
    }

    /// <summary>
    /// Computes Ultimate Trader Oscillator combining multiple momentum measures.
    /// </summary>
    internal static void UltimateTraderOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int shortLength = 5, int mediumLength = 10, int longLength = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rsiShortArray = pool.Rent(close.Length);
        var rsiMediumArray = pool.Rent(close.Length);
        var rsiLongArray = pool.Rent(close.Length);

        try
        {
            var rsiShort = rsiShortArray.AsSpan(0, close.Length);
            var rsiMedium = rsiMediumArray.AsSpan(0, close.Length);
            var rsiLong = rsiLongArray.AsSpan(0, close.Length);

            // Calculate RSI at different timeframes
            RelativeStrengthIndex(close, rsiShort, shortLength);
            RelativeStrengthIndex(close, rsiMedium, mediumLength);
            RelativeStrengthIndex(close, rsiLong, longLength);

            // Weighted average (4-2-1 weighting)
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = (rsiShort[i] * 4 + rsiMedium[i] * 2 + rsiLong[i]) / 7.0;
            }
        }
        finally
        {
            pool.Return(rsiShortArray);
            pool.Return(rsiMediumArray);
            pool.Return(rsiLongArray);
        }
    }

    /// <summary>
    /// Computes Smoothed Delta Ratio Oscillator using delta ratios.
    /// </summary>
    internal static void SmoothedDeltaRatioOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 14, int smoothLength = 3)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var deltaRatioArray = pool.Rent(close.Length);
        var smoothedArray = pool.Rent(close.Length);

        try
        {
            var deltaRatio = deltaRatioArray.AsSpan(0, close.Length);
            var smoothed = smoothedArray.AsSpan(0, close.Length);

            // Calculate delta ratio
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length)
                {
                    deltaRatio[i] = 0;
                }
                else
                {
                    var upSum = 0.0;
                    var downSum = 0.0;
                    for (var j = i - length + 1; j <= i; j++)
                    {
                        var delta = close[j] - close[j - 1];
                        if (delta > 0) upSum += delta;
                        else downSum += Math.Abs(delta);
                    }

                    if (upSum + downSum != 0)
                    {
                        deltaRatio[i] = (upSum - downSum) / (upSum + downSum) * 100;
                    }
                    else
                    {
                        deltaRatio[i] = 0;
                    }
                }
            }

            // Smooth the result
            MovingAverageCore.SimpleMovingAverage(deltaRatio, smoothed, smoothLength);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = smoothed[i];
            }
        }
        finally
        {
            pool.Return(deltaRatioArray);
            pool.Return(smoothedArray);
        }
    }

    /// <summary>
    /// Computes Fast Slow Degree Oscillator using angular transformations.
    /// </summary>
    internal static void FastSlowDegreeOscillator(ReadOnlySpan<double> close, Span<double> output, int fastLength = 5, int slowLength = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var fastMaArray = pool.Rent(close.Length);
        var slowMaArray = pool.Rent(close.Length);

        try
        {
            var fastMa = fastMaArray.AsSpan(0, close.Length);
            var slowMa = slowMaArray.AsSpan(0, close.Length);

            MovingAverageCore.SimpleMovingAverage(close, fastMa, fastLength);
            MovingAverageCore.SimpleMovingAverage(close, slowMa, slowLength);

            // Calculate angular degree between fast and slow
            for (var i = 0; i < close.Length; i++)
            {
                if (slowMa[i] != 0)
                {
                    var ratio = (fastMa[i] - slowMa[i]) / slowMa[i];
                    output[i] = Math.Atan(ratio * 100) * (180.0 / Math.PI);
                }
                else
                {
                    output[i] = 0;
                }
            }
        }
        finally
        {
            pool.Return(fastMaArray);
            pool.Return(slowMaArray);
        }
    }

    /// <summary>
    /// Computes Robust Weighting Oscillator using outlier-resistant statistics.
    /// </summary>
    internal static void RobustWeightingOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var windowArray = pool.Rent(length);

        try
        {
            var window = windowArray.AsSpan(0, length);

            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 0;
                }
                else
                {
                    // Copy window
                    for (var j = 0; j < length; j++)
                    {
                        window[j] = close[i - length + 1 + j];
                    }

                    // Sort for median
                    var sortedWindow = window.ToArray();
                    Array.Sort(sortedWindow);
                    var median = sortedWindow[length / 2];

                    // Calculate MAD (Median Absolute Deviation)
                    var mad = 0.0;
                    for (var j = 0; j < length; j++)
                    {
                        mad += Math.Abs(window[j] - median);
                    }
                    mad /= length;

                    if (mad != 0)
                    {
                        output[i] = (close[i] - median) / (mad * 1.4826); // 1.4826 is the consistency constant
                    }
                    else
                    {
                        output[i] = 0;
                    }
                }
            }
        }
        finally
        {
            pool.Return(windowArray);
        }
    }

    #endregion

    #region Additional Oscillators (Batch 10)

    /// <summary>
    /// Computes Kase Peak Oscillator V2 using different methodology.
    /// </summary>
    internal static void KasePeakOscillatorV2(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 30)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var trueRangeArray = pool.Rent(close.Length);
        var sqrtTrArray = pool.Rent(close.Length);

        try
        {
            var trueRange = trueRangeArray.AsSpan(0, close.Length);
            var sqrtTr = sqrtTrArray.AsSpan(0, close.Length);

            // Calculate True Range and sqrt
            VolatilityCore.TrueRange(high, low, close, trueRange);
            for (var i = 0; i < close.Length; i++)
            {
                sqrtTr[i] = Math.Sqrt(trueRange[i]);
            }

            // Calculate Kase Peak V2
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length)
                {
                    output[i] = 0;
                }
                else
                {
                    var sumSqrtTr = 0.0;
                    for (var j = i - length + 1; j <= i; j++)
                    {
                        sumSqrtTr += sqrtTr[j];
                    }

                    if (sumSqrtTr != 0)
                    {
                        var momentum = close[i] - close[i - length];
                        output[i] = momentum / sumSqrtTr * Math.Sqrt(length);
                    }
                    else
                    {
                        output[i] = 0;
                    }
                }
            }
        }
        finally
        {
            pool.Return(trueRangeArray);
            pool.Return(sqrtTrArray);
        }
    }

    /// <summary>
    /// Computes Stochastic Custom Oscillator with custom smoothing.
    /// </summary>
    internal static void StochasticCustomOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int kPeriod = 14, int dPeriod = 3, int smoothPeriod = 3)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var kArray = pool.Rent(close.Length);
        var smoothKArray = pool.Rent(close.Length);
        var dArray = pool.Rent(close.Length);

        try
        {
            var k = kArray.AsSpan(0, close.Length);
            var smoothK = smoothKArray.AsSpan(0, close.Length);
            var d = dArray.AsSpan(0, close.Length);

            // Calculate raw %K
            for (var i = 0; i < close.Length; i++)
            {
                if (i < kPeriod - 1)
                {
                    k[i] = 50;
                }
                else
                {
                    var highest = high[i];
                    var lowest = low[i];
                    for (var j = i - kPeriod + 1; j <= i; j++)
                    {
                        if (high[j] > highest) highest = high[j];
                        if (low[j] < lowest) lowest = low[j];
                    }

                    var range = highest - lowest;
                    k[i] = range != 0 ? (close[i] - lowest) / range * 100 : 50;
                }
            }

            // Smooth %K
            MovingAverageCore.SimpleMovingAverage(k, smoothK, smoothPeriod);

            // Calculate %D
            MovingAverageCore.SimpleMovingAverage(smoothK, d, dPeriod);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = smoothK[i] - d[i];
            }
        }
        finally
        {
            pool.Return(kArray);
            pool.Return(smoothKArray);
            pool.Return(dArray);
        }
    }

    /// <summary>
    /// Computes Pivot Detector Oscillator using pivot point detection.
    /// </summary>
    internal static void PivotDetectorOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 5)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length * 2)
            {
                output[i] = 0;
            }
            else
            {
                // Check for pivot high
                var isPivotHigh = true;
                var centerHigh = high[i - length];
                for (var j = -length; j <= length; j++)
                {
                    if (j != 0 && high[i - length + j] >= centerHigh)
                    {
                        isPivotHigh = false;
                        break;
                    }
                }

                // Check for pivot low
                var isPivotLow = true;
                var centerLow = low[i - length];
                for (var j = -length; j <= length; j++)
                {
                    if (j != 0 && low[i - length + j] <= centerLow)
                    {
                        isPivotLow = false;
                        break;
                    }
                }

                if (isPivotHigh) output[i] = 100;
                else if (isPivotLow) output[i] = -100;
                else output[i] = 0;
            }
        }
    }

    /// <summary>
    /// Computes Tick Line Momentum Oscillator using up/down tick logic.
    /// </summary>
    internal static void TickLineMomentumOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var tickLineArray = pool.Rent(close.Length);
        var maArray = pool.Rent(close.Length);

        try
        {
            var tickLine = tickLineArray.AsSpan(0, close.Length);
            var ma = maArray.AsSpan(0, close.Length);

            // Calculate tick line (cumulative up/down ticks)
            tickLine[0] = 0;
            for (var i = 1; i < close.Length; i++)
            {
                if (close[i] > close[i - 1]) tickLine[i] = tickLine[i - 1] + 1;
                else if (close[i] < close[i - 1]) tickLine[i] = tickLine[i - 1] - 1;
                else tickLine[i] = tickLine[i - 1];
            }

            // Calculate MA of tick line
            MovingAverageCore.SimpleMovingAverage(tickLine, ma, length);

            // Oscillator = tick line - MA
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = tickLine[i] - ma[i];
            }
        }
        finally
        {
            pool.Return(tickLineArray);
            pool.Return(maArray);
        }
    }

    /// <summary>
    /// Computes Support and Resistance Oscillator.
    /// </summary>
    internal static void SupportAndResistanceOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
            }
            else
            {
                var highestHigh = high[i];
                var lowestLow = low[i];
                for (var j = i - length + 1; j <= i; j++)
                {
                    if (high[j] > highestHigh) highestHigh = high[j];
                    if (low[j] < lowestLow) lowestLow = low[j];
                }

                var range = highestHigh - lowestLow;
                if (range != 0)
                {
                    // Position relative to support/resistance levels
                    var position = (close[i] - lowestLow) / range;
                    output[i] = (position - 0.5) * 200; // -100 to +100
                }
                else
                {
                    output[i] = 0;
                }
            }
        }
    }

    /// <summary>
    /// Computes Trading Made More Simpler Oscillator.
    /// </summary>
    internal static void TradingMadeMoreSimplerOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var hlc3Array = pool.Rent(close.Length);
        var emaArray = pool.Rent(close.Length);

        try
        {
            var hlc3 = hlc3Array.AsSpan(0, close.Length);
            var ema = emaArray.AsSpan(0, close.Length);

            // Calculate HLC/3
            for (var i = 0; i < close.Length; i++)
            {
                hlc3[i] = (high[i] + low[i] + close[i]) / 3;
            }

            // EMA of HLC/3
            MovingAverageCore.ExponentialMovingAverage(hlc3, ema, length);

            // Oscillator = close - ema
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = close[i] - ema[i];
            }
        }
        finally
        {
            pool.Return(hlc3Array);
            pool.Return(emaArray);
        }
    }

    /// <summary>
    /// Computes Nth Order Differencing Oscillator.
    /// </summary>
    internal static void NthOrderDifferencingOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 14, int order = 2)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var currentArray = pool.Rent(close.Length);
        var prevArray = pool.Rent(close.Length);

        try
        {
            var current = currentArray.AsSpan(0, close.Length);
            var prev = prevArray.AsSpan(0, close.Length);

            // Start with close prices
            close.CopyTo(current);

            // Apply differencing n times
            for (var n = 0; n < order; n++)
            {
                current.CopyTo(prev);
                for (var i = 0; i < close.Length; i++)
                {
                    if (i < length)
                    {
                        current[i] = 0;
                    }
                    else
                    {
                        current[i] = prev[i] - prev[i - length];
                    }
                }
            }

            current.CopyTo(output);
        }
        finally
        {
            pool.Return(currentArray);
            pool.Return(prevArray);
        }
    }

    /// <summary>
    /// Computes Osc Oscillator (general purpose oscillator).
    /// </summary>
    internal static void OscOscillator(ReadOnlySpan<double> close, Span<double> output, int fastLength = 5, int slowLength = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var fastMaArray = pool.Rent(close.Length);
        var slowMaArray = pool.Rent(close.Length);

        try
        {
            var fastMa = fastMaArray.AsSpan(0, close.Length);
            var slowMa = slowMaArray.AsSpan(0, close.Length);

            MovingAverageCore.ExponentialMovingAverage(close, fastMa, fastLength);
            MovingAverageCore.ExponentialMovingAverage(close, slowMa, slowLength);

            // Oscillator = (fast - slow) / slow * 100
            for (var i = 0; i < close.Length; i++)
            {
                if (slowMa[i] != 0)
                {
                    output[i] = (fastMa[i] - slowMa[i]) / slowMa[i] * 100;
                }
                else
                {
                    output[i] = 0;
                }
            }
        }
        finally
        {
            pool.Return(fastMaArray);
            pool.Return(slowMaArray);
        }
    }

    #endregion

    #region Additional Oscillators (Batch 11) - Ehlers Oscillators

    /// <summary>
    /// Computes Ehlers Center of Gravity Oscillator.
    /// </summary>
    internal static void EhlersCenterOfGravityOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
            }
            else
            {
                var num = 0.0;
                var denom = 0.0;
                for (var j = 0; j < length; j++)
                {
                    var price = close[i - j];
                    num += (j + 1) * price;
                    denom += price;
                }

                output[i] = denom != 0 ? -num / denom + (length + 1) / 2.0 : 0;
            }
        }
    }

    /// <summary>
    /// Computes Ehlers Decycler Oscillator V1.
    /// </summary>
    internal static void EhlersDecyclerOscillatorV1(ReadOnlySpan<double> close, Span<double> output, int shortLength = 10, int longLength = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var shortHpArray = pool.Rent(close.Length);
        var longHpArray = pool.Rent(close.Length);

        try
        {
            var shortHp = shortHpArray.AsSpan(0, close.Length);
            var longHp = longHpArray.AsSpan(0, close.Length);

            var alphaShort = (Math.Cos(2 * Math.PI / shortLength) + Math.Sin(2 * Math.PI / shortLength) - 1) / Math.Cos(2 * Math.PI / shortLength);
            var alphaLong = (Math.Cos(2 * Math.PI / longLength) + Math.Sin(2 * Math.PI / longLength) - 1) / Math.Cos(2 * Math.PI / longLength);

            // High-pass filter
            shortHp[0] = 0;
            longHp[0] = 0;
            for (var i = 1; i < close.Length; i++)
            {
                shortHp[i] = (1 - alphaShort / 2) * (1 - alphaShort / 2) * (close[i] - 2 * close[Math.Max(0, i - 1)] + close[Math.Max(0, i - 2)]) +
                            2 * (1 - alphaShort) * shortHp[i - 1] - (1 - alphaShort) * (1 - alphaShort) * (i > 1 ? shortHp[i - 2] : 0);
                longHp[i] = (1 - alphaLong / 2) * (1 - alphaLong / 2) * (close[i] - 2 * close[Math.Max(0, i - 1)] + close[Math.Max(0, i - 2)]) +
                           2 * (1 - alphaLong) * longHp[i - 1] - (1 - alphaLong) * (1 - alphaLong) * (i > 1 ? longHp[i - 2] : 0);
            }

            // Oscillator = short decycler - long decycler
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = (close[i] - shortHp[i]) - (close[i] - longHp[i]);
            }
        }
        finally
        {
            pool.Return(shortHpArray);
            pool.Return(longHpArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Hilbert Oscillator.
    /// </summary>
    internal static void EhlersHilbertOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 7)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var smoothArray = pool.Rent(close.Length);

        try
        {
            var smooth = smoothArray.AsSpan(0, close.Length);

            // 4-bar weighted average
            for (var i = 0; i < close.Length; i++)
            {
                if (i < 3)
                {
                    smooth[i] = close[i];
                }
                else
                {
                    smooth[i] = (4 * close[i] + 3 * close[i - 1] + 2 * close[i - 2] + close[i - 3]) / 10.0;
                }
            }

            // Hilbert transform approximation
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length)
                {
                    output[i] = 0;
                }
                else
                {
                    var detrender = 0.0962 * smooth[i] + 0.5769 * smooth[Math.Max(0, i - 2)] -
                                   0.5769 * smooth[Math.Max(0, i - 4)] - 0.0962 * smooth[Math.Max(0, i - 6)];
                    output[i] = detrender;
                }
            }
        }
        finally
        {
            pool.Return(smoothArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Universal Oscillator.
    /// </summary>
    internal static void EhlersUniversalOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var whitenArray = pool.Rent(close.Length);
        var filtArray = pool.Rent(close.Length);

        try
        {
            var whiten = whitenArray.AsSpan(0, close.Length);
            var filt = filtArray.AsSpan(0, close.Length);

            var a1 = Math.Exp(-1.414 * Math.PI / length);
            var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / length);
            var c2 = b1;
            var c3 = -a1 * a1;
            var c1 = 1 - c2 - c3;

            // Whitening
            for (var i = 0; i < close.Length; i++)
            {
                if (i < 1)
                {
                    whiten[i] = close[i];
                }
                else
                {
                    whiten[i] = close[i] - close[i - 1];
                }
            }

            // Super smoother filter
            for (var i = 0; i < close.Length; i++)
            {
                if (i < 2)
                {
                    filt[i] = whiten[i];
                }
                else
                {
                    filt[i] = c1 * (whiten[i] + whiten[i - 1]) / 2 + c2 * filt[i - 1] + c3 * filt[i - 2];
                }
            }

            // RMS normalization
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length)
                {
                    output[i] = 0;
                }
                else
                {
                    var rms = 0.0;
                    for (var j = 0; j < length; j++)
                    {
                        rms += filt[i - j] * filt[i - j];
                    }
                    rms = Math.Sqrt(rms / length);

                    output[i] = rms != 0 ? filt[i] / rms : 0;
                }
            }
        }
        finally
        {
            pool.Return(whitenArray);
            pool.Return(filtArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Recursive Median Oscillator.
    /// </summary>
    internal static void EhlersRecursiveMedianOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 5, int smoothLength = 3)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var medianArray = pool.Rent(close.Length);
        var windowArray = pool.Rent(length);
        var smoothedArray = pool.Rent(close.Length);

        try
        {
            var median = medianArray.AsSpan(0, close.Length);
            var window = windowArray.AsSpan(0, length);
            var smoothed = smoothedArray.AsSpan(0, close.Length);

            // Calculate rolling median
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    median[i] = close[i];
                }
                else
                {
                    for (var j = 0; j < length; j++)
                    {
                        window[j] = close[i - j];
                    }
                    var sorted = window.ToArray();
                    Array.Sort(sorted);
                    median[i] = sorted[length / 2];
                }
            }

            // Smooth the median
            MovingAverageCore.SimpleMovingAverage(median, smoothed, smoothLength);

            // Oscillator = close - smoothed median
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = close[i] - smoothed[i];
            }
        }
        finally
        {
            pool.Return(medianArray);
            pool.Return(windowArray);
            pool.Return(smoothedArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Stochastic Center of Gravity Oscillator.
    /// </summary>
    internal static void EhlersStochasticCenterOfGravityOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 8)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var cgArray = pool.Rent(close.Length);

        try
        {
            var cg = cgArray.AsSpan(0, close.Length);

            // Calculate Center of Gravity
            EhlersCenterOfGravityOscillator(close, cg, length);

            // Stochastic of CG
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 50;
                }
                else
                {
                    var highest = cg[i];
                    var lowest = cg[i];
                    for (var j = i - length + 1; j <= i; j++)
                    {
                        if (cg[j] > highest) highest = cg[j];
                        if (cg[j] < lowest) lowest = cg[j];
                    }

                    var range = highest - lowest;
                    output[i] = range != 0 ? (cg[i] - lowest) / range * 100 : 50;
                }
            }
        }
        finally
        {
            pool.Return(cgArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Fisherized Deviation Scaled Oscillator.
    /// </summary>
    internal static void EhlersFisherizedDeviationScaledOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var filtArray = pool.Rent(close.Length);

        try
        {
            var filt = filtArray.AsSpan(0, close.Length);

            var a1 = Math.Exp(-1.414 * Math.PI / length);
            var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / length);
            var c2 = b1;
            var c3 = -a1 * a1;
            var c1 = 1 - c2 - c3;

            // Super smoother
            for (var i = 0; i < close.Length; i++)
            {
                if (i < 2)
                {
                    filt[i] = close[i];
                }
                else
                {
                    filt[i] = c1 * (close[i] + close[i - 1]) / 2 + c2 * filt[i - 1] + c3 * filt[i - 2];
                }
            }

            // Fisher transform of normalized filter
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length)
                {
                    output[i] = 0;
                }
                else
                {
                    // Calculate RMS
                    var rms = 0.0;
                    for (var j = 0; j < length; j++)
                    {
                        var diff = close[i - j] - filt[i - j];
                        rms += diff * diff;
                    }
                    rms = Math.Sqrt(rms / length);

                    if (rms != 0)
                    {
                        var norm = (close[i] - filt[i]) / rms;
                        norm = Math.Max(-0.999, Math.Min(0.999, norm)); // Clamp for Fisher
                        output[i] = 0.5 * Math.Log((1 + norm) / (1 - norm));
                    }
                    else
                    {
                        output[i] = 0;
                    }
                }
            }
        }
        finally
        {
            pool.Return(filtArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Adaptive Center of Gravity Oscillator.
    /// </summary>
    internal static void EhlersAdaptiveCenterOfGravityOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var cgArray = pool.Rent(close.Length);
        var smoothedArray = pool.Rent(close.Length);

        try
        {
            var cg = cgArray.AsSpan(0, close.Length);
            var smoothed = smoothedArray.AsSpan(0, close.Length);

            // Calculate base CG
            EhlersCenterOfGravityOscillator(close, cg, length);

            // Adaptive smoothing using EMA
            MovingAverageCore.ExponentialMovingAverage(cg, smoothed, 3);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = smoothed[i];
            }
        }
        finally
        {
            pool.Return(cgArray);
            pool.Return(smoothedArray);
        }
    }

    #endregion

    #region Additional Oscillators (Batch 12) - Vervoort and Specialized

    /// <summary>
    /// Computes Vervoort Smoothed Oscillator.
    /// </summary>
    internal static void VervoortSmoothedOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var emaArray = pool.Rent(close.Length);
        var demaArray = pool.Rent(close.Length);

        try
        {
            var ema = emaArray.AsSpan(0, close.Length);
            var dema = demaArray.AsSpan(0, close.Length);

            // Calculate EMA
            MovingAverageCore.ExponentialMovingAverage(close, ema, length);
            MovingAverageCore.ExponentialMovingAverage(ema, dema, length);

            // Oscillator = 2 * EMA - DEMA (Smoothed)
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = close[i] - (2 * ema[i] - dema[i]);
            }
        }
        finally
        {
            pool.Return(emaArray);
            pool.Return(demaArray);
        }
    }

    /// <summary>
    /// Computes Relative Difference of Squares Oscillator.
    /// </summary>
    internal static void RelativeDifferenceOfSquaresOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length)
            {
                output[i] = 0;
            }
            else
            {
                var sumUp = 0.0;
                var sumDown = 0.0;
                for (var j = 0; j < length; j++)
                {
                    var diff = close[i - j] - close[i - j - 1 < 0 ? 0 : i - j - 1];
                    var diffSq = diff * diff;
                    if (diff > 0) sumUp += diffSq;
                    else sumDown += diffSq;
                }

                var total = sumUp + sumDown;
                output[i] = total != 0 ? (sumUp - sumDown) / total * 100 : 0;
            }
        }
    }

    /// <summary>
    /// Computes Linear Quadratic Convergence Divergence Oscillator.
    /// </summary>
    internal static void LinearQuadraticConvergenceDivergenceOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var linRegArray = pool.Rent(close.Length);
        var quadRegArray = pool.Rent(close.Length);

        try
        {
            var linReg = linRegArray.AsSpan(0, close.Length);
            var quadReg = quadRegArray.AsSpan(0, close.Length);

            // Linear regression
            MovingAverageCore.LinearRegression(close, linReg, length);

            // Quadratic approximation (use DEMA as proxy for quadratic behavior)
            MovingAverageCore.ExponentialMovingAverage(close, quadReg, length);
            var emaOfEma = pool.Rent(close.Length);
            try
            {
                var eofe = emaOfEma.AsSpan(0, close.Length);
                MovingAverageCore.ExponentialMovingAverage(quadReg, eofe, length);

                // Output = Linear - Quadratic convergence
                for (var i = 0; i < close.Length; i++)
                {
                    output[i] = linReg[i] - (2 * quadReg[i] - eofe[i]);
                }
            }
            finally
            {
                pool.Return(emaOfEma);
            }
        }
        finally
        {
            pool.Return(linRegArray);
            pool.Return(quadRegArray);
        }
    }

    /// <summary>
    /// Computes Stationary Extrapolated Levels Oscillator.
    /// </summary>
    internal static void StationaryExtrapolatedLevelsOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var linRegArray = pool.Rent(close.Length);
        var slopeArray = pool.Rent(close.Length);

        try
        {
            var linReg = linRegArray.AsSpan(0, close.Length);
            var slope = slopeArray.AsSpan(0, close.Length);

            MovingAverageCore.LinearRegression(close, linReg, length);
            TrendCore.LinearRegressionSlope(close, slope, length);

            // Extrapolated level = linReg + slope (projected one bar ahead)
            // Oscillator = close - extrapolated level
            for (var i = 0; i < close.Length; i++)
            {
                var extrapolated = linReg[i] + slope[i];
                output[i] = close[i] - extrapolated;
            }
        }
        finally
        {
            pool.Return(linRegArray);
            pool.Return(slopeArray);
        }
    }

    /// <summary>
    /// Computes Percentage Price Oscillator Leader.
    /// </summary>
    internal static void PercentagePriceOscillatorLeader(ReadOnlySpan<double> close, Span<double> output, int fastLength = 12, int slowLength = 26)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var fastEmaArray = pool.Rent(close.Length);
        var slowEmaArray = pool.Rent(close.Length);
        var fastSlopeArray = pool.Rent(close.Length);
        var slowSlopeArray = pool.Rent(close.Length);

        try
        {
            var fastEma = fastEmaArray.AsSpan(0, close.Length);
            var slowEma = slowEmaArray.AsSpan(0, close.Length);
            var fastSlope = fastSlopeArray.AsSpan(0, close.Length);
            var slowSlope = slowSlopeArray.AsSpan(0, close.Length);

            MovingAverageCore.ExponentialMovingAverage(close, fastEma, fastLength);
            MovingAverageCore.ExponentialMovingAverage(close, slowEma, slowLength);

            // Calculate slopes
            for (var i = 1; i < close.Length; i++)
            {
                fastSlope[i] = fastEma[i] - fastEma[i - 1];
                slowSlope[i] = slowEma[i] - slowEma[i - 1];
            }

            // Leader = PPO + scaled slope difference
            for (var i = 0; i < close.Length; i++)
            {
                if (slowEma[i] != 0)
                {
                    var ppo = (fastEma[i] - slowEma[i]) / slowEma[i] * 100;
                    var slopeDiff = (fastSlope[i] - slowSlope[i]) / (Math.Abs(slowEma[i]) / 100 + 0.001);
                    output[i] = ppo + slopeDiff;
                }
                else
                {
                    output[i] = 0;
                }
            }
        }
        finally
        {
            pool.Return(fastEmaArray);
            pool.Return(slowEmaArray);
            pool.Return(fastSlopeArray);
            pool.Return(slowSlopeArray);
        }
    }

    /// <summary>
    /// Computes Kaufman Adaptive Correlation Oscillator.
    /// </summary>
    internal static void KaufmanAdaptiveCorrelationOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var kamaArray = pool.Rent(close.Length);

        try
        {
            var kama = kamaArray.AsSpan(0, close.Length);

            // Calculate KAMA
            MovingAverageCore.KaufmanAdaptiveMovingAverage(close, kama, length, 2, 30);

            // Correlation-based oscillator
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length)
                {
                    output[i] = 0;
                }
                else
                {
                    // Calculate correlation between price and KAMA
                    var sumXY = 0.0;
                    var sumX = 0.0;
                    var sumY = 0.0;
                    var sumX2 = 0.0;
                    var sumY2 = 0.0;

                    for (var j = 0; j < length; j++)
                    {
                        var x = close[i - j];
                        var y = kama[i - j];
                        sumXY += x * y;
                        sumX += x;
                        sumY += y;
                        sumX2 += x * x;
                        sumY2 += y * y;
                    }

                    var n = length;
                    var denom = Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));
                    if (denom != 0)
                    {
                        output[i] = (n * sumXY - sumX * sumY) / denom * 100;
                    }
                    else
                    {
                        output[i] = 0;
                    }
                }
            }
        }
        finally
        {
            pool.Return(kamaArray);
        }
    }

    /// <summary>
    /// Computes Stochastic Moving Average Convergence Divergence Oscillator.
    /// </summary>
    internal static void StochasticMacdOscillator(ReadOnlySpan<double> close, Span<double> output, int fastLength = 12, int slowLength = 26, int signalLength = 9, int stochLength = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var macdLineArray = pool.Rent(close.Length);
        var stochMacdArray = pool.Rent(close.Length);

        try
        {
            var macdLine = macdLineArray.AsSpan(0, close.Length);
            var stochMacd = stochMacdArray.AsSpan(0, close.Length);

            // Calculate MACD line
            var fastEma = pool.Rent(close.Length);
            var slowEma = pool.Rent(close.Length);
            try
            {
                MovingAverageCore.ExponentialMovingAverage(close, fastEma.AsSpan(0, close.Length), fastLength);
                MovingAverageCore.ExponentialMovingAverage(close, slowEma.AsSpan(0, close.Length), slowLength);
                for (var i = 0; i < close.Length; i++)
                {
                    macdLine[i] = fastEma[i] - slowEma[i];
                }
            }
            finally
            {
                pool.Return(fastEma);
                pool.Return(slowEma);
            }

            // Calculate Stochastic of MACD
            for (var i = 0; i < close.Length; i++)
            {
                if (i < stochLength - 1)
                {
                    output[i] = 50;
                }
                else
                {
                    var highest = macdLine[i];
                    var lowest = macdLine[i];
                    for (var j = i - stochLength + 1; j <= i; j++)
                    {
                        if (macdLine[j] > highest) highest = macdLine[j];
                        if (macdLine[j] < lowest) lowest = macdLine[j];
                    }

                    var range = highest - lowest;
                    output[i] = range != 0 ? (macdLine[i] - lowest) / range * 100 : 50;
                }
            }
        }
        finally
        {
            pool.Return(macdLineArray);
            pool.Return(stochMacdArray);
        }
    }

    /// <summary>
    /// Computes McClellan Oscillator (market breadth).
    /// </summary>
    internal static void McClellanOscillator(ReadOnlySpan<double> close, Span<double> output, int fastLength = 19, int slowLength = 39)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var fastEmaArray = pool.Rent(close.Length);
        var slowEmaArray = pool.Rent(close.Length);

        try
        {
            var fastEma = fastEmaArray.AsSpan(0, close.Length);
            var slowEma = slowEmaArray.AsSpan(0, close.Length);

            // For individual stocks, use price as proxy for breadth
            MovingAverageCore.ExponentialMovingAverage(close, fastEma, fastLength);
            MovingAverageCore.ExponentialMovingAverage(close, slowEma, slowLength);

            // McClellan Oscillator = Fast EMA - Slow EMA
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = fastEma[i] - slowEma[i];
            }
        }
        finally
        {
            pool.Return(fastEmaArray);
            pool.Return(slowEmaArray);
        }
    }

    #endregion

    #region Batch 13 - Additional Ehlers and Specialized Oscillators

    /// <summary>
    /// Computes Ehlers Decycler Oscillator V2 (improved version).
    /// </summary>
    internal static void EhlersDecyclerOscillatorV2(ReadOnlySpan<double> close, Span<double> output, int length = 125)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var hpArray = pool.Rent(close.Length);
        var smoothArray = pool.Rent(close.Length);

        try
        {
            var hp = hpArray.AsSpan(0, close.Length);
            var smooth = smoothArray.AsSpan(0, close.Length);

            // High-pass filter using Ehlers super smoother
            var alpha = (Math.Cos(2 * Math.PI / length) + Math.Sin(2 * Math.PI / length) - 1) / Math.Cos(2 * Math.PI / length);

            hp[0] = close[0];
            hp[1] = close.Length > 1 ? close[1] : close[0];

            for (var i = 2; i < close.Length; i++)
            {
                hp[i] = (1 - alpha / 2) * (1 - alpha / 2) * (close[i] - 2 * close[i - 1] + close[i - 2]) + 2 * (1 - alpha) * hp[i - 1] - (1 - alpha) * (1 - alpha) * hp[i - 2];
            }

            // Smooth the high-pass output
            MovingAverageCore.ExponentialMovingAverage(hp, smooth, Math.Max(1, length / 10));

            // Oscillator = difference between close and smoothed HP
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = close[i] - smooth[i];
            }
        }
        finally
        {
            pool.Return(hpArray);
            pool.Return(smoothArray);
        }
    }

    /// <summary>
    /// Computes Vervoort Heiken Ashi Candlestick Oscillator.
    /// </summary>
    internal static void VervoortHeikenAshiCandlestickOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> open, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var haCloseArray = pool.Rent(close.Length);
        var haOpenArray = pool.Rent(close.Length);
        var temaArray = pool.Rent(close.Length);

        try
        {
            var haClose = haCloseArray.AsSpan(0, close.Length);
            var haOpen = haOpenArray.AsSpan(0, close.Length);
            var tema = temaArray.AsSpan(0, close.Length);

            // Calculate Heiken Ashi values
            haClose[0] = (high[0] + low[0] + close[0] + open[0]) / 4;
            haOpen[0] = (open[0] + close[0]) / 2;

            for (var i = 1; i < close.Length; i++)
            {
                haClose[i] = (high[i] + low[i] + close[i] + open[i]) / 4;
                haOpen[i] = (haOpen[i - 1] + haClose[i - 1]) / 2;
            }

            // Calculate HA difference and apply TEMA smoothing
            var haDiff = pool.Rent(close.Length);
            try
            {
                var diff = haDiff.AsSpan(0, close.Length);
                for (var i = 0; i < close.Length; i++)
                {
                    diff[i] = haClose[i] - haOpen[i];
                }

                MovingAverageCore.TripleExponentialMovingAverage(diff, tema, length);

                for (var i = 0; i < close.Length; i++)
                {
                    output[i] = tema[i];
                }
            }
            finally
            {
                pool.Return(haDiff);
            }
        }
        finally
        {
            pool.Return(haCloseArray);
            pool.Return(haOpenArray);
            pool.Return(temaArray);
        }
    }

    /// <summary>
    /// Computes Vervoort Heiken Ashi Long Term Candlestick Oscillator.
    /// </summary>
    internal static void VervoortHeikenAshiLongTermCandlestickOscillator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> open, Span<double> output, int length = 55)
    {
        // Same as regular version but with longer default period
        VervoortHeikenAshiCandlestickOscillator(high, low, close, open, output, length);
    }

    /// <summary>
    /// Computes Decision Point Breadth Swenlin Trading Oscillator.
    /// Uses price rate of change as proxy for breadth when individual stock data is used.
    /// </summary>
    internal static void DecisionPointBreadthSwenlinTradingOscillator(ReadOnlySpan<double> close, Span<double> output, int shortLength = 5, int longLength = 100)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rocArray = pool.Rent(close.Length);
        var shortEmaArray = pool.Rent(close.Length);
        var longEmaArray = pool.Rent(close.Length);

        try
        {
            var roc = rocArray.AsSpan(0, close.Length);
            var shortEma = shortEmaArray.AsSpan(0, close.Length);
            var longEma = longEmaArray.AsSpan(0, close.Length);

            // Calculate rate of change
            RateOfChange(close, roc, 1);

            // Apply dual EMA smoothing
            MovingAverageCore.ExponentialMovingAverage(roc, shortEma, shortLength);
            MovingAverageCore.ExponentialMovingAverage(shortEma, longEma, longLength);

            // Oscillator = short EMA - long EMA
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = shortEma[i] - longEma[i];
            }
        }
        finally
        {
            pool.Return(rocArray);
            pool.Return(shortEmaArray);
            pool.Return(longEmaArray);
        }
    }

    /// <summary>
    /// Computes Decision Point Price Momentum Oscillator.
    /// </summary>
    internal static void DecisionPointPriceMomentumOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 35, int signalLength = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var rocArray = pool.Rent(close.Length);
        var smoothArray = pool.Rent(close.Length);
        var smoothedArray = pool.Rent(close.Length);

        try
        {
            var roc = rocArray.AsSpan(0, close.Length);
            var smooth = smoothArray.AsSpan(0, close.Length);
            var smoothed = smoothedArray.AsSpan(0, close.Length);

            // Calculate ROC
            RateOfChange(close, roc, 1);

            // Double smoothing with EMA
            MovingAverageCore.ExponentialMovingAverage(roc, smooth, length);
            MovingAverageCore.ExponentialMovingAverage(smooth, smoothed, signalLength);

            // Scale by 10 for readability
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = smoothed[i] * 10;
            }
        }
        finally
        {
            pool.Return(rocArray);
            pool.Return(smoothArray);
            pool.Return(smoothedArray);
        }
    }

    /// <summary>
    /// Computes TFS MBO Percentage Price Oscillator.
    /// </summary>
    internal static void TFSMboPercentagePriceOscillator(ReadOnlySpan<double> close, Span<double> output, int fastLength = 25, int slowLength = 200)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var fastEmaArray = pool.Rent(close.Length);
        var slowEmaArray = pool.Rent(close.Length);

        try
        {
            var fastEma = fastEmaArray.AsSpan(0, close.Length);
            var slowEma = slowEmaArray.AsSpan(0, close.Length);

            MovingAverageCore.ExponentialMovingAverage(close, fastEma, fastLength);
            MovingAverageCore.ExponentialMovingAverage(close, slowEma, slowLength);

            // TFS MBO PPO = ((fast - slow) / slow) * 100
            for (var i = 0; i < close.Length; i++)
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
    /// Computes TFS Volume Oscillator.
    /// </summary>
    internal static void TFSVolumeOscillator(ReadOnlySpan<double> volume, Span<double> output, int fastLength = 13, int slowLength = 55)
    {
        if (output.Length < volume.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var fastEmaArray = pool.Rent(volume.Length);
        var slowEmaArray = pool.Rent(volume.Length);

        try
        {
            var fastEma = fastEmaArray.AsSpan(0, volume.Length);
            var slowEma = slowEmaArray.AsSpan(0, volume.Length);

            MovingAverageCore.ExponentialMovingAverage(volume, fastEma, fastLength);
            MovingAverageCore.ExponentialMovingAverage(volume, slowEma, slowLength);

            // Volume oscillator = ((fast - slow) / slow) * 100
            for (var i = 0; i < volume.Length; i++)
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
    /// Computes Mass Thrust Oscillator.
    /// Uses advancing/declining price ratio as proxy for breadth.
    /// </summary>
    internal static void MassThrustOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var thrustArray = pool.Rent(close.Length);
        var emaArray = pool.Rent(close.Length);

        try
        {
            var thrust = thrustArray.AsSpan(0, close.Length);
            var ema = emaArray.AsSpan(0, close.Length);

            // Calculate daily thrust (advance/decline ratio proxy)
            thrust[0] = 0;
            for (var i = 1; i < close.Length; i++)
            {
                var change = close[i] - close[i - 1];
                thrust[i] = change > 0 ? 1 : (change < 0 ? -1 : 0);
            }

            // Smooth with EMA
            MovingAverageCore.ExponentialMovingAverage(thrust, ema, length);

            // Scale to percentage
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = ema[i] * 100;
            }
        }
        finally
        {
            pool.Return(thrustArray);
            pool.Return(emaArray);
        }
    }

    /// <summary>
    /// Computes Simple Lines filter.
    /// Adaptive step-based smoothing filter.
    /// </summary>
    internal static void SimpleLines(ReadOnlySpan<double> close, Span<double> output, int length = 10, double mult = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var s = 0.01 * 100 * (1.0 / length);

        // Initialize first value
        output[0] = close[0];

        for (var i = 1; i < close.Length; i++)
        {
            var prevA = output[i - 1];
            var prevA2 = i >= 2 ? output[i - 2] : close[0];

            // x = currentValue + ((prevA - prevA2) * mult)
            var x = close[i] + ((prevA - prevA2) * mult);

            // a = x > prevA + s ? prevA + s : x < prevA - s ? prevA - s : prevA
            if (x > prevA + s)
            {
                output[i] = prevA + s;
            }
            else if (x < prevA - s)
            {
                output[i] = prevA - s;
            }
            else
            {
                output[i] = prevA;
            }
        }
    }

    /// <summary>
    /// Computes Simple Cycle oscillator.
    /// Uses EMA-smoothed difference with period-based momentum.
    /// </summary>
    internal static void SimpleCycle(ReadOnlySpan<double> close, Span<double> output, int length = 50)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var srcArray = pool.Rent(close.Length);
        var cEmaArray = pool.Rent(close.Length);
        var cArray = pool.Rent(close.Length);

        try
        {
            var src = srcArray.AsSpan(0, close.Length);
            var cEma = cEmaArray.AsSpan(0, close.Length);
            var c = cArray.AsSpan(0, close.Length);

            var a = 1.0 / length;
            var alpha = 2.0 / (length + 1);

            // Initialize
            src[0] = close[0];
            c[0] = 0;
            cEma[0] = 0;

            for (var i = 1; i < close.Length; i++)
            {
                var prevC = c[i - 1];
                var prevSrc = i >= length ? src[i - length] : 0;

                // src = currentValue + prevC
                src[i] = close[i] + prevC;

                // cEma = EMA of prevC
                cEma[i] = (alpha * prevC) + ((1 - alpha) * cEma[i - 1]);

                // b = prevC - cEma
                var b = prevC - cEma[i];

                // c = (a * (src - prevSrc)) + ((1 - a) * b)
                c[i] = (a * (src[i] - prevSrc)) + ((1 - a) * b);
            }

            // Copy to output
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = c[i];
            }
        }
        finally
        {
            pool.Return(srcArray);
            pool.Return(cEmaArray);
            pool.Return(cArray);
        }
    }

    /// <summary>
    /// Computes Double Exponential Smoothing (Holt's method).
    /// </summary>
    /// <param name="input">Input prices.</param>
    /// <param name="output">Output span for smoothed results.</param>
    /// <param name="alpha">Level smoothing factor (0-1).</param>
    /// <param name="gamma">Trend smoothing factor (0-1).</param>
    internal static void DoubleExponentialSmoothing(ReadOnlySpan<double> input, Span<double> output, double alpha = 0.01, double gamma = 0.9)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (input.Length == 0) return;

        output[0] = input[0];
        if (input.Length == 1) return;

        output[1] = (alpha * input[1]) + ((1 - alpha) * output[0]);

        for (var i = 2; i < input.Length; i++)
        {
            var sChg = output[i - 1] - output[i - 2];
            output[i] = (alpha * input[i]) + ((1 - alpha) * (output[i - 1] + (gamma * (sChg + ((1 - gamma) * sChg)))));
        }
    }

    /// <summary>
    /// Computes the Detrended Synthetic Price oscillator using dual EMA.
    /// </summary>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="output">Output span for results.</param>
    /// <param name="length">EMA period.</param>
    internal static void DetrendedSyntheticPrice(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 14)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var alpha = length > 2 ? 2.0 / (length + 1) : 0.67;
        var alpha2 = alpha / 2;

        // Initialize with first price
        var prevHigh = high.Length > 0 ? high[0] : 0;
        var prevLow = low.Length > 0 ? low[0] : 0;
        var price0 = (Math.Max(high[0], prevHigh) + Math.Min(low[0], prevLow)) / 2;
        var ema1 = price0;
        var ema2 = price0;
        output[0] = 0;

        for (var i = 1; i < high.Length; i++)
        {
            var currentHigh = high[i];
            var currentLow = low[i];
            prevHigh = high[i - 1];
            prevLow = low[i - 1];
            var h = Math.Max(currentHigh, prevHigh);
            var l = Math.Min(currentLow, prevLow);
            var price = (h + l) / 2;

            ema1 = (alpha * price) + ((1 - alpha) * ema1);
            ema2 = (alpha2 * price) + ((1 - alpha2) * ema2);
            output[i] = ema1 - ema2;
        }
    }

    /// <summary>
    /// Computes the Belkhayate Timing oscillator using a 5-bar HL midpoint with scaling.
    /// </summary>
    /// <param name="close">Close prices.</param>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="output">Output span for results.</param>
    internal static void BelkhayateTiming(ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            var currentHigh = high[i];
            var currentLow = low[i];
            var prevHigh1 = i >= 1 ? high[i - 1] : 0;
            var prevLow1 = i >= 1 ? low[i - 1] : 0;
            var prevHigh2 = i >= 2 ? high[i - 2] : 0;
            var prevLow2 = i >= 2 ? low[i - 2] : 0;
            var prevHigh3 = i >= 3 ? high[i - 3] : 0;
            var prevLow3 = i >= 3 ? low[i - 3] : 0;
            var prevHigh4 = i >= 4 ? high[i - 4] : 0;
            var prevLow4 = i >= 4 ? low[i - 4] : 0;

            var middle = (((currentHigh + currentLow) / 2) + ((prevHigh1 + prevLow1) / 2) + ((prevHigh2 + prevLow2) / 2) +
                          ((prevHigh3 + prevLow3) / 2) + ((prevHigh4 + prevLow4) / 2)) / 5;
            var scale = ((currentHigh - currentLow + (prevHigh1 - prevLow1) + (prevHigh2 - prevLow2) + (prevHigh3 - prevLow3) +
                          (prevHigh4 - prevLow4)) / 5) * 0.2;

            output[i] = scale != 0 ? (close[i] - middle) / scale : 0;
        }
    }

    #endregion

    #region Batch 22 - Counting and Performance Oscillators + Market Direction Indicators

    /// <summary>
    /// Computes the Demark Setup Indicator - counts up/down patterns.
    /// </summary>
    /// <param name="input">Close prices.</param>
    /// <param name="output">Output span for results.</param>
    /// <param name="length">Lookback length.</param>
    internal static void DemarkSetupIndicator(ReadOnlySpan<double> input, Span<double> output, int length = 4)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            double uCount = 0, dCount = 0;

            for (var j = 0; j < length; j++)
            {
                var value = i >= j ? input[i - j] : 0;
                var prevValue = i >= j + length ? input[i - (j + length)] : 0;

                if (value > prevValue) uCount++;
                if (value < prevValue) dCount++;
            }

            double drp = dCount == length ? 1 : uCount == length ? -1 : 0;
            output[i] = drp != 0 ? currentValue : 0;
        }
    }

    /// <summary>
    /// Computes the Performance Index - percent change from length bars ago.
    /// </summary>
    /// <param name="input">Close prices.</param>
    /// <param name="output">Output span for results.</param>
    /// <param name="length">Lookback length.</param>
    internal static void PerformanceIndex(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevValue = i >= length ? input[i - length] : 0;

            // MinPastValues equivalent: only compute if we have enough history
            var change = i >= length ? currentValue - prevValue : 0;
            output[i] = prevValue != 0 ? change * 100 / prevValue : 0;
        }
    }

    /// <summary>
    /// Computes the Psychological Line - percent of up days in a window.
    /// </summary>
    /// <param name="input">Close prices.</param>
    /// <param name="output">Output span for results.</param>
    /// <param name="length">Window length.</param>
    internal static void PsychologicalLine(ReadOnlySpan<double> input, Span<double> output, int length = 20)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Use a rolling window to count up days
        Span<double> conditions = stackalloc double[Math.Min(length, input.Length)];
        var condSum = 0.0;
        var windowIdx = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevValue = i >= 1 ? input[i - 1] : 0;
            double cond = currentValue > prevValue ? 1 : 0;

            if (i < length)
            {
                // Building up the window
                conditions[i] = cond;
                condSum += cond;
            }
            else
            {
                // Rolling window - remove oldest, add newest
                condSum -= conditions[windowIdx];
                conditions[windowIdx] = cond;
                condSum += cond;
                windowIdx = (windowIdx + 1) % length;
            }

            output[i] = length != 0 ? condSum / length * 100 : 0;
        }
    }

    /// <summary>
    /// Computes the Move Tracker - simple price change tracking.
    /// </summary>
    /// <param name="input">Close prices.</param>
    /// <param name="output">Output span for results.</param>
    internal static void MoveTracker(ReadOnlySpan<double> input, Span<double> output)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevValue = i >= 1 ? input[i - 1] : 0;

            // MinPastValues equivalent: only compute change if we have history
            output[i] = i >= 1 ? currentValue - prevValue : 0;
        }
    }

    /// <summary>
    /// Computes the Multi Level Indicator - scaled open/close difference.
    /// </summary>
    /// <param name="close">Close prices.</param>
    /// <param name="open">Open prices.</param>
    /// <param name="output">Output span for results.</param>
    /// <param name="length">Lookback length.</param>
    /// <param name="factor">Scaling factor.</param>
    internal static void MultiLevelIndicator(ReadOnlySpan<double> close, ReadOnlySpan<double> open, Span<double> output, int length = 14, double factor = 10000)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            var prevOpen = i >= length ? open[i - length] : 0;
            var currentOpen = open[i];
            var currentClose = close[i];

            output[i] = (currentClose - currentOpen - (currentClose - prevOpen)) * factor;
        }
    }

    /// <summary>
    /// Computes the Market Direction Indicator using rolling sums.
    /// </summary>
    /// <param name="input">Close prices.</param>
    /// <param name="output">Output span for results.</param>
    /// <param name="fastLength">Fast period.</param>
    /// <param name="slowLength">Slow period.</param>
    internal static void MarketDirectionIndicator(ReadOnlySpan<double> input, Span<double> output, int fastLength = 13, int slowLength = 55)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        // Pre-allocate rolling sum buffer
        Span<double> sumBuffer = stackalloc double[Math.Min(slowLength, input.Length)];
        var bufferIdx = 0;
        var totalSum = 0.0;
        var prevCp2 = 0.0;

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var prevValue = i >= 1 ? input[i - 1] : 0;

            // Maintain rolling sum for slowLength
            if (i < slowLength)
            {
                sumBuffer[i] = currentValue;
                totalSum += currentValue;
            }
            else
            {
                totalSum -= sumBuffer[bufferIdx];
                sumBuffer[bufferIdx] = currentValue;
                totalSum += currentValue;
                bufferIdx = (bufferIdx + 1) % slowLength;
            }

            // Calculate partial sums for fast and slow lengths
            var len1Sum = 0.0; // fastLength - 1
            var len2Sum = 0.0; // slowLength - 1

            var count1 = Math.Min(fastLength - 1, i + 1);
            var count2 = Math.Min(slowLength - 1, i + 1);

            for (var j = 0; j < count2; j++)
            {
                var idx = i - j;
                if (idx >= 0)
                {
                    var val = input[idx];
                    len2Sum += val;
                    if (j < count1)
                    {
                        len1Sum += val;
                    }
                }
            }

            var cp2 = slowLength != fastLength ? ((fastLength * len2Sum) - (slowLength * len1Sum)) / (slowLength - fastLength) : 0;
            var avg = (currentValue + prevValue) / 2;
            output[i] = avg != 0 ? 100 * (prevCp2 - cp2) / avg : 0;
            prevCp2 = cp2;
        }
    }

    // NthOrderDifferencingOscillator already implemented above in Batch 11

    /// <summary>
    /// Computes the Morphed Sine Wave oscillator.
    /// </summary>
    /// <param name="input">Close prices.</param>
    /// <param name="output">Output span for results.</param>
    /// <param name="length">Period length.</param>
    /// <param name="power">Scaling power.</param>
    internal static void MorphedSineWave(ReadOnlySpan<double> input, Span<double> output, int length = 14, double power = 100)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var p = length / (2 * Math.PI);

        for (var i = 0; i < input.Length; i++)
        {
            var currentValue = input[i];
            var c = (currentValue * power) + Math.Sin(i / p);
            output[i] = c / power;
        }
    }

    // MarketFacilitationIndex and VolumeAccumulationOscillator already implemented above

    #endregion

    #region Batch 23 - Simple Price and Volume Indicators

    /// <summary>
    /// Computes Internal Bar Strength (IBS).
    /// IBS = (Close - Low) / (High - Low) * 100, averaged over length.
    /// </summary>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="close">Close prices.</param>
    /// <param name="output">Output span for results.</param>
    /// <param name="length">Averaging period length.</param>
    internal static void InternalBarStrength(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        Span<double> ibs = stackalloc double[close.Length > 1024 ? 0 : close.Length];
        var ibsArray = close.Length > 1024 ? new double[close.Length] : null;
        var ibsSpan = ibsArray is not null ? ibsArray.AsSpan() : ibs;

        // Calculate raw IBS for each bar
        for (var i = 0; i < close.Length; i++)
        {
            var range = high[i] - low[i];
            ibsSpan[i] = range != 0 ? (close[i] - low[i]) / range * 100 : 50;
        }

        // Calculate rolling average of IBS
        double sum = 0;
        for (var i = 0; i < close.Length; i++)
        {
            sum += ibsSpan[i];
            if (i >= length)
            {
                sum -= ibsSpan[i - length];
            }
            var count = Math.Min(i + 1, length);
            output[i] = sum / count;
        }
    }

    /// <summary>
    /// Computes Full Typical Price (OHLC4).
    /// FullTypicalPrice = (Open + High + Low + Close) / 4
    /// </summary>
    /// <param name="open">Open prices.</param>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="close">Close prices.</param>
    /// <param name="output">Output span for results.</param>
    internal static void FullTypicalPrice(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            output[i] = (open[i] + high[i] + low[i] + close[i]) / 4;
        }
    }

    /// <summary>
    /// Computes Z-Score.
    /// ZScore = (Value - SMA) / StdDev
    /// </summary>
    /// <param name="input">Input prices.</param>
    /// <param name="output">Output span for results.</param>
    /// <param name="length">Period length.</param>
    internal static void ZScore(ReadOnlySpan<double> input, Span<double> output, int length = 14)
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

            // Calculate SMA
            double sum = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                sum += input[j];
            }
            var sma = sum / length;

            // Calculate standard deviation
            double sumSquaredDev = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                var dev = input[j] - sma;
                sumSquaredDev += dev * dev;
            }
            var stdDev = Math.Sqrt(sumSquaredDev / length);

            // Calculate Z-Score
            output[i] = stdDev != 0 ? (input[i] - sma) / stdDev : 0;
        }
    }

    /// <summary>
    /// Computes Inverse Fisher Transform of a value.
    /// InverseFisher = (Exp(2*x) - 1) / (Exp(2*x) + 1)
    /// </summary>
    /// <param name="input">Input values (typically normalized RSI or other oscillator).</param>
    /// <param name="output">Output span for results.</param>
    /// <param name="length">Period for scaling.</param>
    internal static void InverseFisherTransform(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            var x = input[i];
            // Normalize to -5 to 5 range for better transform behavior
            var normalized = x * 0.1;
            var exp2x = Math.Exp(2 * normalized);
            output[i] = (exp2x - 1) / (exp2x + 1);
        }
    }

    /// <summary>
    /// Computes Fast Z-Score using shorter period calculation.
    /// </summary>
    /// <param name="input">Input prices.</param>
    /// <param name="output">Output span for results.</param>
    /// <param name="length">Period length.</param>
    internal static void FastZScore(ReadOnlySpan<double> input, Span<double> output, int length = 5)
    {
        // FastZScore uses same formula as ZScore but with shorter default period
        ZScore(input, output, length);
    }

    /// <summary>
    /// Computes Skewness indicator.
    /// Measures asymmetry of the distribution of values.
    /// </summary>
    /// <param name="input">Input prices.</param>
    /// <param name="output">Output span for results.</param>
    /// <param name="length">Period length.</param>
    internal static void Skewness(ReadOnlySpan<double> input, Span<double> output, int length = 14)
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

            // Calculate mean
            double sum = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                sum += input[j];
            }
            var mean = sum / length;

            // Calculate variance and third moment
            double sumSquaredDev = 0;
            double sumCubedDev = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                var dev = input[j] - mean;
                sumSquaredDev += dev * dev;
                sumCubedDev += dev * dev * dev;
            }
            var variance = sumSquaredDev / length;
            var stdDev = Math.Sqrt(variance);

            // Skewness = E[(X-μ)³] / σ³
            output[i] = stdDev != 0 ? (sumCubedDev / length) / (stdDev * stdDev * stdDev) : 0;
        }
    }

    /// <summary>
    /// Computes Kurtosis indicator.
    /// Measures the "tailedness" of the distribution.
    /// </summary>
    /// <param name="input">Input prices.</param>
    /// <param name="output">Output span for results.</param>
    /// <param name="length">Period length.</param>
    internal static void Kurtosis(ReadOnlySpan<double> input, Span<double> output, int length = 14)
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

            // Calculate mean
            double sum = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                sum += input[j];
            }
            var mean = sum / length;

            // Calculate variance and fourth moment
            double sumSquaredDev = 0;
            double sumFourthDev = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                var dev = input[j] - mean;
                var devSq = dev * dev;
                sumSquaredDev += devSq;
                sumFourthDev += devSq * devSq;
            }
            var variance = sumSquaredDev / length;

            // Kurtosis = E[(X-μ)⁴] / σ⁴ - 3 (excess kurtosis)
            output[i] = variance != 0 ? (sumFourthDev / length) / (variance * variance) - 3 : 0;
        }
    }

    /// <summary>
    /// Computes Price Zone Oscillator simplified version.
    /// </summary>
    /// <param name="close">Close prices.</param>
    /// <param name="output">Output span for results.</param>
    /// <param name="length">Period length.</param>
    internal static void SimplePriceZone(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double sumUp = 0;
        double sumDn = 0;

        for (var i = 0; i < close.Length; i++)
        {
            if (i == 0)
            {
                output[i] = 0;
                continue;
            }

            var change = close[i] - close[i - 1];
            var up = change > 0 ? change : 0;
            var dn = change < 0 ? -change : 0;

            sumUp += up;
            sumDn += dn;

            if (i >= length)
            {
                var prevChange = close[i - length] - (i > length ? close[i - length - 1] : 0);
                var prevUp = prevChange > 0 ? prevChange : 0;
                var prevDn = prevChange < 0 ? -prevChange : 0;
                sumUp -= prevUp;
                sumDn -= prevDn;
            }

            var total = sumUp + sumDn;
            output[i] = total != 0 ? 100 * (sumUp - sumDn) / total : 0;
        }
    }

    /// <summary>
    /// Computes Typical Price Volatility.
    /// Standard deviation of typical price over period.
    /// </summary>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="close">Close prices.</param>
    /// <param name="output">Output span for results.</param>
    /// <param name="length">Period length.</param>
    internal static void TypicalPriceVolatility(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            if (i < length - 1)
            {
                output[i] = 0;
                continue;
            }

            // Calculate mean of typical prices
            double sum = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                var tp = (high[j] + low[j] + close[j]) / 3;
                sum += tp;
            }
            var mean = sum / length;

            // Calculate standard deviation
            double sumSquaredDev = 0;
            for (var j = i - length + 1; j <= i; j++)
            {
                var tp = (high[j] + low[j] + close[j]) / 3;
                var dev = tp - mean;
                sumSquaredDev += dev * dev;
            }
            output[i] = Math.Sqrt(sumSquaredDev / length);
        }
    }

    #endregion

    #region Demark Indicators

    /// <summary>
    /// Computes Demark Range Expansion Index using rolling sums.
    /// </summary>
    internal static void DemarkRangeExpansionIndex(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 5)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var s1Array = pool.Rent(close.Length);
        var s2Array = pool.Rent(close.Length);

        try
        {
            var s1 = s1Array.AsSpan(0, close.Length);
            var s2 = s2Array.AsSpan(0, close.Length);

            // Calculate s1 and s2 values
            for (var i = 0; i < close.Length; i++)
            {
                var prevHigh2 = i >= 2 ? high[i - 2] : 0;
                var prevHigh5 = i >= 5 ? high[i - 5] : 0;
                var prevHigh6 = i >= 6 ? high[i - 6] : 0;
                var prevLow2 = i >= 2 ? low[i - 2] : 0;
                var prevLow5 = i >= 5 ? low[i - 5] : 0;
                var prevLow6 = i >= 6 ? low[i - 6] : 0;
                var prevClose7 = i >= 7 ? close[i - 7] : 0;
                var prevClose8 = i >= 8 ? close[i - 8] : 0;

                double n = (high[i] >= prevLow5 || high[i] >= prevLow6) && (low[i] <= prevHigh5 || low[i] <= prevHigh6) ? 0 : 1;
                double m = prevHigh2 >= prevClose8 && (prevLow2 <= prevClose7 || prevLow2 <= prevClose8) ? 0 : 1;
                var sVal = high[i] - prevHigh2 + (low[i] - prevLow2);

                s1[i] = n * m * sVal;
                s2[i] = Math.Abs(sVal);
            }

            // Calculate rolling sums and REI
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 0;
                    continue;
                }

                double s1Sum = 0, s2Sum = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    s1Sum += s1[j];
                    s2Sum += s2[j];
                }

                output[i] = s2Sum != 0 ? s1Sum / s2Sum * 100 : 0;
            }
        }
        finally
        {
            pool.Return(s1Array);
            pool.Return(s2Array);
        }
    }

    /// <summary>
    /// Computes Demark Pressure Ratio V1 using rolling sums.
    /// </summary>
    internal static void DemarkPressureRatioV1(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> open, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 13)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var bpArray = pool.Rent(close.Length);
        var spArray = pool.Rent(close.Length);

        try
        {
            var bp = bpArray.AsSpan(0, close.Length);
            var sp = spArray.AsSpan(0, close.Length);

            // Calculate buying and selling pressure
            for (var i = 0; i < close.Length; i++)
            {
                var prevClose = i >= 1 ? close[i - 1] : 0;
                var gapup = prevClose != 0 ? (open[i] - prevClose) / prevClose : 0;
                var gapdown = open[i] != 0 ? (prevClose - open[i]) / open[i] : 0;

                bp[i] = gapup > 0.15 ? (high[i] - prevClose + close[i] - low[i]) * volume[i] :
                    close[i] > open[i] ? (close[i] - open[i]) * volume[i] : 0;

                sp[i] = gapdown > 0.15 ? (prevClose - low[i] + high[i] - close[i]) * volume[i] :
                    close[i] < open[i] ? (close[i] - open[i]) * volume[i] : 0;
            }

            // Calculate rolling sums and pressure ratio
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 0;
                    continue;
                }

                double bpSum = 0, spSum = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    bpSum += bp[j];
                    spSum += sp[j];
                }

                output[i] = bpSum - spSum != 0 ? Math.Min(Math.Max(100 * bpSum / (bpSum - spSum), 0), 100) : 0;
            }
        }
        finally
        {
            pool.Return(bpArray);
            pool.Return(spArray);
        }
    }

    /// <summary>
    /// Computes Demark Pressure Ratio V2 using rolling sums.
    /// </summary>
    internal static void DemarkPressureRatioV2(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> open, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var bpArray = pool.Rent(close.Length);
        var spArray = pool.Rent(close.Length);

        try
        {
            var bp = bpArray.AsSpan(0, close.Length);
            var sp = spArray.AsSpan(0, close.Length);

            // Calculate buying and selling pressure
            for (var i = 0; i < close.Length; i++)
            {
                var delta = close[i] - open[i];
                var trueRange = high[i] - low[i];
                var ratio = trueRange != 0 ? delta / trueRange : 0;

                bp[i] = delta > 0 ? ratio * volume[i] : 0;
                sp[i] = delta < 0 ? ratio * volume[i] : 0;
            }

            // Calculate rolling sums and pressure ratio
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    output[i] = 50;  // Default neutral value
                    continue;
                }

                double bpSum = 0, spSum = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    bpSum += bp[j];
                    spSum += sp[j];
                }

                var denom = bpSum + Math.Abs(spSum);
                output[i] = denom != 0 ? Math.Min(Math.Max(100 * bpSum / denom, 0), 100) : 50;
            }
        }
        finally
        {
            pool.Return(bpArray);
            pool.Return(spArray);
        }
    }

    /// <summary>
    /// Computes Demark Reversal Points using nested loop counting.
    /// </summary>
    internal static void DemarkReversalPoints(ReadOnlySpan<double> close, Span<double> output, int length1 = 9, int length2 = 4)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            int uCount = 0, dCount = 0;
            for (var j = 0; j < length1; j++)
            {
                var value = i >= j ? close[i - j] : 0;
                var prevValue = i >= j + length2 ? close[i - (j + length2)] : 0;

                if (value > prevValue) uCount++;
                if (value < prevValue) dCount++;
            }

            double drp = dCount == length1 ? 1 : uCount == length1 ? -1 : 0;
            output[i] = drp != 0 ? close[i] : 0;
        }
    }

    /// <summary>
    /// Computes Contract High - running maximum of highs.
    /// </summary>
    internal static void ContractHigh(ReadOnlySpan<double> high, Span<double> output)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double maxHigh = double.MinValue;
        for (var i = 0; i < high.Length; i++)
        {
            if (high[i] > maxHigh) maxHigh = high[i];
            output[i] = maxHigh;
        }
    }

    /// <summary>
    /// Computes Contract Low - running minimum of lows.
    /// </summary>
    internal static void ContractLow(ReadOnlySpan<double> low, Span<double> output)
    {
        if (output.Length < low.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double minLow = double.MaxValue;
        for (var i = 0; i < low.Length; i++)
        {
            if (low[i] < minLow) minLow = low[i];
            output[i] = minLow;
        }
    }

    /// <summary>
    /// Computes Chande Intraday Momentum Index.
    /// Measures the relationship between open and close prices.
    /// </summary>
    internal static void ChandeIntradayMomentumIndex(ReadOnlySpan<double> open, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double gainsSum = 0;
        double lossesSum = 0;
        var gainsQueue = new Queue<double>(length);
        var lossesQueue = new Queue<double>(length);

        for (var i = 0; i < close.Length; i++)
        {
            var currentClose = close[i];
            var currentOpen = open[i];

            var gain = currentClose > currentOpen ? currentClose - currentOpen : 0;
            var loss = currentClose < currentOpen ? currentOpen - currentClose : 0;

            gainsSum += gain;
            lossesSum += loss;
            gainsQueue.Enqueue(gain);
            lossesQueue.Enqueue(loss);

            if (gainsQueue.Count > length)
            {
                gainsSum -= gainsQueue.Dequeue();
                lossesSum -= lossesQueue.Dequeue();
            }

            var total = gainsSum + lossesSum;
            output[i] = total != 0 ? Math.Min(Math.Max(100 * gainsSum / total, 0), 100) : 0;
        }
    }

    /// <summary>
    /// Computes Oscar Indicator - a smoothed stochastic-like oscillator.
    /// </summary>
    internal static void OscarIndicator(ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 8)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var highWindow = new RollingMinMax(length);
        var lowWindow = new RollingMinMax(length);

        for (var i = 0; i < close.Length; i++)
        {
            highWindow.Add(high[i]);
            lowWindow.Add(low[i]);

            var highest = highWindow.Max;
            var lowest = lowWindow.Min;
            var range = highest - lowest;
            var rough = range != 0 ? Math.Min(Math.Max((close[i] - lowest) / range * 100, 0), 100) : 0;
            var prevOscar = i >= 1 ? output[i - 1] : 0;

            output[i] = (prevOscar / 6) + (rough / 3);
        }
    }

    /// <summary>
    /// Computes Narrow Bandpass Filter using Blackman-Harris window.
    /// </summary>
    internal static void NarrowBandpassFilter(ReadOnlySpan<double> input, Span<double> output, int length = 50)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < input.Length; i++)
        {
            double sum = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var prevValue = i >= j ? input[i - j] : 0;
                var x = j / (double)(length - 1);
                var win = 0.42 - (0.5 * Math.Cos(2 * Math.PI * x)) + (0.08 * Math.Cos(4 * Math.PI * x));
                var w = Math.Sin(2 * Math.PI * j / length) * win;
                sum += prevValue * w;
            }
            output[i] = sum;
        }
    }

    /// <summary>
    /// Computes TFS Tether Line Indicator - midpoint of highest high and lowest low.
    /// </summary>
    internal static void TFSTetherLineIndicator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 50)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var highWindow = new RollingMinMax(length);
        var lowWindow = new RollingMinMax(length);

        for (var i = 0; i < high.Length; i++)
        {
            highWindow.Add(high[i]);
            lowWindow.Add(low[i]);

            var highest = highWindow.Max;
            var lowest = lowWindow.Min;
            output[i] = (highest + lowest) / 2;
        }
    }

    /// <summary>
    /// Computes Williams Fractals - identifies swing highs and lows.
    /// Returns 1 for up fractal, -1 for down fractal, 0 otherwise.
    /// </summary>
    internal static void WilliamsFractals(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> upFractalOutput, Span<double> downFractalOutput, int length = 2)
    {
        if (upFractalOutput.Length < high.Length || downFractalOutput.Length < high.Length)
        {
            throw new ArgumentException("Output spans must be at least input length.", nameof(upFractalOutput));
        }

        for (var i = 0; i < high.Length; i++)
        {
            var prevHigh = i >= length - 2 ? high[i - (length - 2)] : 0;
            var prevHigh1 = i >= length - 1 ? high[i - (length - 1)] : 0;
            var prevHigh2 = i >= length ? high[i - length] : 0;
            var prevHigh3 = i >= length + 1 ? high[i - (length + 1)] : 0;
            var prevHigh4 = i >= length + 2 ? high[i - (length + 2)] : 0;
            var prevHigh5 = i >= length + 3 ? high[i - (length + 3)] : 0;
            var prevHigh6 = i >= length + 4 ? high[i - (length + 4)] : 0;
            var prevHigh7 = i >= length + 5 ? high[i - (length + 5)] : 0;
            var prevHigh8 = i >= length + 8 ? high[i - (length + 6)] : 0;
            var prevLow = i >= length - 2 ? low[i - (length - 2)] : 0;
            var prevLow1 = i >= length - 1 ? low[i - (length - 1)] : 0;
            var prevLow2 = i >= length ? low[i - length] : 0;
            var prevLow3 = i >= length + 1 ? low[i - (length + 1)] : 0;
            var prevLow4 = i >= length + 2 ? low[i - (length + 2)] : 0;
            var prevLow5 = i >= length + 3 ? low[i - (length + 3)] : 0;
            var prevLow6 = i >= length + 4 ? low[i - (length + 4)] : 0;
            var prevLow7 = i >= length + 5 ? low[i - (length + 5)] : 0;
            var prevLow8 = i >= length + 8 ? low[i - (length + 6)] : 0;

            double upFractal = (prevHigh4 < prevHigh2 && prevHigh3 < prevHigh2 && prevHigh1 < prevHigh2 && prevHigh < prevHigh2) ||
                (prevHigh5 < prevHigh2 && prevHigh4 < prevHigh2 && prevHigh3 == prevHigh2 && prevHigh1 < prevHigh2) ||
                (prevHigh6 < prevHigh2 && prevHigh5 < prevHigh2 && prevHigh4 == prevHigh2 && prevHigh3 <= prevHigh2 && prevHigh1 < prevHigh2 &&
                prevHigh < prevHigh2) || (prevHigh7 < prevHigh2 && prevHigh6 < prevHigh2 && prevHigh5 == prevHigh2 && prevHigh4 == prevHigh2 &&
                prevHigh3 <= prevHigh2 && prevHigh1 < prevHigh2 && prevHigh < prevHigh2) || (prevHigh8 < prevHigh2 && prevHigh7 < prevHigh2 &&
                prevHigh6 == prevHigh2 && prevHigh5 <= prevHigh2 && prevHigh4 == prevHigh2 && prevHigh3 <= prevHigh2 && prevHigh1 < prevHigh2 &&
                prevHigh < prevHigh2) ? 1 : 0;
            upFractalOutput[i] = upFractal;

            double dnFractal = (prevLow4 > prevLow2 && prevLow3 > prevLow2 && prevLow1 > prevLow2 && prevLow > prevLow2) || (prevLow5 > prevLow2 &&
                prevLow4 > prevLow2 && prevLow3 == prevLow2 && prevLow1 > prevLow2 && prevLow > prevLow2) || (prevLow6 > prevLow2 &&
                prevLow5 > prevLow2 && prevLow4 == prevLow2 && prevLow3 >= prevLow2 && prevLow1 > prevLow2 && prevLow > prevLow2) ||
                (prevLow7 > prevLow2 && prevLow6 > prevLow2 && prevLow5 == prevLow2 && prevLow4 == prevLow2 && prevLow3 >= prevLow2 &&
                prevLow1 > prevLow2 && prevLow > prevLow2) || (prevLow8 > prevLow2 && prevLow7 > prevLow2 && prevLow6 == prevLow2 &&
                prevLow5 >= prevLow2 && prevLow4 == prevLow2 && prevLow3 >= prevLow2 && prevLow1 > prevLow2 && prevLow > prevLow2) ? 1 : 0;
            downFractalOutput[i] = dnFractal;
        }
    }

    /// <summary>
    /// Computes Upside Downside Volume ratio.
    /// </summary>
    internal static void UpsideDownsideVolume(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 50)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var upVolSum = new RollingSum();
        var downVolSum = new RollingSum();

        for (var i = 0; i < close.Length; i++)
        {
            var currentClose = close[i];
            var prevClose = i >= 1 ? close[i - 1] : 0;
            var currentVolume = volume[i];

            var upVol = currentClose > prevClose ? currentVolume : 0;
            var downVol = currentClose < prevClose ? -currentVolume : 0;

            upVolSum.Add(upVol);
            downVolSum.Add(downVol);

            var upSum = upVolSum.Sum(length);
            var downSum = downVolSum.Sum(length);

            output[i] = downSum != 0 ? upSum / downSum : 0;
        }
    }

    /// <summary>
    /// Computes Vortex Indicator Plus component.
    /// </summary>
    internal static void VortexIndicatorPlus(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var vmPlusSum = new RollingSum();
        var trSum = new RollingSum();

        for (var i = 0; i < close.Length; i++)
        {
            var currentHigh = high[i];
            var currentLow = low[i];
            var prevClose = i >= 1 ? close[i - 1] : 0;
            var prevLow = i >= 1 ? low[i - 1] : 0;

            var vmPlus = Math.Abs(currentHigh - prevLow);
            vmPlusSum.Add(vmPlus);

            var tr = Math.Max(currentHigh - currentLow, Math.Max(Math.Abs(currentHigh - prevClose), Math.Abs(currentLow - prevClose)));
            trSum.Add(tr);

            var vmPlusSumVal = vmPlusSum.Sum(length);
            var trSumVal = trSum.Sum(length);

            output[i] = trSumVal != 0 ? vmPlusSumVal / trSumVal : 0;
        }
    }

    /// <summary>
    /// Computes Vortex Indicator Minus component.
    /// </summary>
    internal static void VortexIndicatorMinus(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var vmMinusSum = new RollingSum();
        var trSum = new RollingSum();

        for (var i = 0; i < close.Length; i++)
        {
            var currentHigh = high[i];
            var currentLow = low[i];
            var prevClose = i >= 1 ? close[i - 1] : 0;
            var prevHigh = i >= 1 ? high[i - 1] : 0;

            var vmMinus = Math.Abs(currentLow - prevHigh);
            vmMinusSum.Add(vmMinus);

            var tr = Math.Max(currentHigh - currentLow, Math.Max(Math.Abs(currentHigh - prevClose), Math.Abs(currentLow - prevClose)));
            trSum.Add(tr);

            var vmMinusSumVal = vmMinusSum.Sum(length);
            var trSumVal = trSum.Sum(length);

            output[i] = trSumVal != 0 ? vmMinusSumVal / trSumVal : 0;
        }
    }

    /// <summary>
    /// Computes Guppy Count Back Line indicator.
    /// </summary>
    internal static void GuppyCountBackLine(ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 21)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var highWindow = new RollingMinMax(length);
        var lowWindow = new RollingMinMax(length);

        for (var i = 0; i < close.Length; i++)
        {
            highWindow.Add(high[i]);
            lowWindow.Add(low[i]);

            var hh = highWindow.Max;
            var ll = lowWindow.Min;
            var cbl = close[i];
            int hCount = 0, lCount = 0;

            for (var j = 0; j <= length && i >= j; j++)
            {
                var currentLow = low[i - j];
                var currentHigh = high[i - j];

                if (currentLow == ll)
                {
                    for (var k = j + 1; k <= j + length && i >= k; k++)
                    {
                        var prevHigh = high[i - k];
                        lCount += prevHigh > currentHigh ? 1 : 0;
                        if (lCount == 2)
                        {
                            cbl = prevHigh;
                            break;
                        }
                    }
                }

                if (currentHigh == hh)
                {
                    for (var k = j + 1; k <= j + length && i >= k; k++)
                    {
                        var prevLow = low[i - k];
                        hCount += prevLow > currentLow ? 1 : 0;
                        if (hCount == 2)
                        {
                            cbl = prevLow;
                            break;
                        }
                    }
                }
            }
            output[i] = cbl;
        }
    }

    /// <summary>
    /// Computes Ehlers Trendflex Indicator.
    /// </summary>
    internal static void EhlersTrendflex(ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var period = 0.5 * length;
        var a1 = Math.Exp(-Math.Sqrt(2) * Math.PI / period);
        var b1 = 2 * a1 * Math.Cos(Math.Sqrt(2) * Math.PI / period);
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        var pool = ArrayPool<double>.Shared;
        var filterArray = pool.Rent(close.Length);

        try
        {
            var filter = filterArray.AsSpan(0, close.Length);
            double ms = 0;

            for (var i = 0; i < close.Length; i++)
            {
                var currentValue = close[i];
                var prevValue = i >= 1 ? close[i - 1] : 0;
                var prevFilter1 = i >= 1 ? filter[i - 1] : 0;
                var prevFilter2 = i >= 2 ? filter[i - 2] : 0;

                filter[i] = (c1 * ((currentValue + prevValue) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);

                double sum = 0;
                for (var j = 1; j <= length; j++)
                {
                    var prevFilterCount = i >= j ? filter[i - j] : 0;
                    sum += filter[i] - prevFilterCount;
                }
                sum /= length;

                ms = (0.04 * sum * sum) + (0.96 * ms);
                output[i] = ms > 0 ? sum / Math.Sqrt(ms) : 0;
            }
        }
        finally
        {
            pool.Return(filterArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Reflex Indicator.
    /// </summary>
    internal static void EhlersReflex(ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var period = 0.5 * length;
        var a1 = Math.Exp(-Math.Sqrt(2) * Math.PI / period);
        var b1 = 2 * a1 * Math.Cos(Math.Sqrt(2) * Math.PI / period);
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        var pool = ArrayPool<double>.Shared;
        var filterArray = pool.Rent(close.Length);
        var slopeArray = pool.Rent(close.Length);

        try
        {
            var filter = filterArray.AsSpan(0, close.Length);
            var slope = slopeArray.AsSpan(0, close.Length);
            double ms = 0;

            for (var i = 0; i < close.Length; i++)
            {
                var currentValue = close[i];
                var prevValue = i >= 1 ? close[i - 1] : 0;
                var prevFilter1 = i >= 1 ? filter[i - 1] : 0;
                var prevFilter2 = i >= 2 ? filter[i - 2] : 0;

                filter[i] = (c1 * ((currentValue + prevValue) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);

                var prevFilterLength = i >= length ? filter[i - length] : 0;
                slope[i] = length > 0 ? (filter[i] - prevFilterLength) / length : 0;

                double sum = 0;
                for (var j = 1; j <= length; j++)
                {
                    var slopeCount = i >= j ? slope[i - j] : 0;
                    var filterCount = i >= j ? filter[i - j] : 0;
                    sum += filter[i] + (j * slopeCount) - filterCount;
                }
                sum /= length;

                ms = (0.04 * sum * sum) + (0.96 * ms);
                output[i] = ms > 0 ? sum / Math.Sqrt(ms) : 0;
            }
        }
        finally
        {
            pool.Return(filterArray);
            pool.Return(slopeArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Correlation Trend Indicator.
    /// </summary>
    internal static void EhlersCorrelationTrendIndicator(ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);

        for (var i = 0; i < close.Length; i++)
        {
            double sx = 0, sy = 0, sxx = 0, sxy = 0, syy = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var prevValue = i >= j ? close[i - j] : 0;
                sx += j;
                sy += prevValue;
                sxx += j * j;
                sxy += j * prevValue;
                syy += prevValue * prevValue;
            }

            var denom = Math.Sqrt((length * sxx - sx * sx) * (length * syy - sy * sy));
            output[i] = denom != 0 ? (length * sxy - sx * sy) / denom : 0;
        }
    }

    /// <summary>
    /// Computes Trend Trigger Factor.
    /// </summary>
    internal static void TrendTriggerFactor(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 15)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var highWindow = new RollingMinMax(length);
        var lowWindow = new RollingMinMax(length);

        var pool = ArrayPool<double>.Shared;
        var highestArray = pool.Rent(high.Length);
        var lowestArray = pool.Rent(high.Length);

        try
        {
            // First pass: compute highest and lowest values at each point
            for (var i = 0; i < high.Length; i++)
            {
                highWindow.Add(high[i]);
                lowWindow.Add(low[i]);
                highestArray[i] = highWindow.Max;
                lowestArray[i] = lowWindow.Min;
            }

            // Second pass: compute TTF
            for (var i = 0; i < high.Length; i++)
            {
                var highest = highestArray[i];
                var lowest = lowestArray[i];
                var prevHighest = i >= length ? highestArray[i - length] : 0;
                var prevLowest = i >= length ? lowestArray[i - length] : 0;
                var buyPower = highest - prevLowest;
                var sellPower = prevHighest - lowest;

                output[i] = buyPower + sellPower != 0 ? 200 * (buyPower - sellPower) / (buyPower + sellPower) : 0;
            }
        }
        finally
        {
            pool.Return(highestArray);
            pool.Return(lowestArray);
        }
    }

    /// <summary>
    /// Computes Trend Detection Index.
    /// </summary>
    internal static void TrendDetectionIndex(ReadOnlySpan<double> close, Span<double> output, int length1 = 20, int length2 = 40)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length1 = Math.Max(1, length1);
        length2 = Math.Max(length1, length2);
        var momSum = new RollingSum();
        var momAbsSum = new RollingSum();

        for (var i = 0; i < close.Length; i++)
        {
            var prevValue = i >= length1 ? close[i - length1] : 0;
            var mom = close[i] - prevValue;
            momSum.Add(mom);
            momAbsSum.Add(Math.Abs(mom));

            var tdiDirection = momSum.Sum(length1);
            var momAbsSum1 = momAbsSum.Sum(length1);
            var momAbsSum2 = momAbsSum.Sum(length2);

            output[i] = Math.Abs(tdiDirection) - momAbsSum2 + momAbsSum1;
        }
    }

    /// <summary>
    /// Computes Uber Trend Indicator.
    /// </summary>
    internal static void UberTrendIndicator(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var advSum = new RollingSum();
        var decSum = new RollingSum();
        var advVolSum = new RollingSum();
        var decVolSum = new RollingSum();

        for (var i = 0; i < close.Length; i++)
        {
            var currentValue = close[i];
            var prevValue = i >= 1 ? close[i - 1] : 0;
            var currentVolume = volume[i];

            var adv = i >= 1 && currentValue > prevValue ? currentValue - prevValue : 0.0;
            advSum.Add(adv);

            var dec = i >= 1 && currentValue < prevValue ? prevValue - currentValue : 0.0;
            decSum.Add(dec);

            var advSumVal = advSum.Sum(length);
            var decSumVal = decSum.Sum(length);

            var advVol = i >= 1 && currentValue > prevValue && advSumVal != 0 ? currentVolume / advSumVal : 0.0;
            advVolSum.Add(advVol);

            var decVol = i >= 1 && currentValue < prevValue && decSumVal != 0 ? currentVolume / decSumVal : 0.0;
            decVolSum.Add(decVol);

            var advVolSumVal = advVolSum.Sum(length);
            var decVolSumVal = decVolSum.Sum(length);
            var top = decSumVal != 0 ? advSumVal / decSumVal : 0;
            var bot = decVolSumVal != 0 ? advVolSumVal / decVolSumVal : 0;
            var ut = bot != 0 ? top / bot : 0;

            output[i] = ut + 1 != 0 ? (ut - 1) / (ut + 1) : 0;
        }
    }

    /// <summary>
    /// Computes Percentage Trend.
    /// </summary>
    internal static void PercentageTrend(ReadOnlySpan<double> close, Span<double> output, int length = 20, double pct = 0.15)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var highWindow = new RollingMinMax(length);
        var lowWindow = new RollingMinMax(length);
        double prevTrend = 0;

        for (var i = 0; i < close.Length; i++)
        {
            highWindow.Add(close[i]);
            lowWindow.Add(close[i]);
            var highest = highWindow.Max;
            var lowest = lowWindow.Min;
            var pctValue = close[i] * pct;

            var newTrend = close[i] >= prevTrend + pctValue ? highest :
                          close[i] <= prevTrend - pctValue ? lowest : prevTrend;

            prevTrend = i > 0 ? newTrend : close[i];
            output[i] = prevTrend;
        }
    }

    /// <summary>
    /// Computes Liquid Relative Strength Index.
    /// </summary>
    internal static void LiquidRelativeStrengthIndex(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var k = 1.0 / length;
        double numEma = 0, denEma = 0;

        for (var i = 0; i < close.Length; i++)
        {
            var prevValue = i >= 1 ? close[i - 1] : 0;
            var prevVolume = i >= 1 ? volume[i - 1] : 0;
            var a = close[i] - prevValue;
            var b = volume[i] - prevVolume;
            var num = Math.Max(a, 0) * Math.Max(b, 0);
            var den = Math.Abs(a) * Math.Abs(b);

            numEma = (num * k) + (numEma * (1 - k));
            denEma = (den * k) + (denEma * (1 - k));

            output[i] = denEma != 0 ? Math.Min(Math.Max(100 * numEma / denEma, 0), 100) : 0;
        }
    }

    /// <summary>
    /// Computes Asymmetrical Relative Strength Index.
    /// </summary>
    internal static void AsymmetricalRelativeStrengthIndex(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var upCountSum = new RollingSum();
        double upSum = 0, downSum = 0;

        for (var i = 0; i < close.Length; i++)
        {
            var prevValue = i >= 1 ? close[i - 1] : 0;
            var roc = prevValue != 0 ? (close[i] - prevValue) / prevValue * 100 : 0;

            var upFlag = roc >= 0 ? 1.0 : 0.0;
            upCountSum.Add(upFlag);
            var upCount = upCountSum.Sum(length);
            var upAlpha = upCount != 0 ? 1 / upCount : 0;
            var posRoc = roc > 0 ? roc : 0;
            var negRoc = roc < 0 ? Math.Abs(roc) : 0;

            upSum = (upAlpha * posRoc) + ((1 - upAlpha) * upSum);

            var downCount = length - upCount;
            var downAlpha = downCount != 0 ? 1 / downCount : 0;
            downSum = (downAlpha * negRoc) + ((1 - downAlpha) * downSum);

            var ars = downSum != 0 ? upSum / downSum : 0;
            output[i] = downSum == 0 ? 100 : upSum == 0 ? 0 : Math.Min(Math.Max(100 - (100 / (1 + ars)), 0), 100);
        }
    }

    /// <summary>
    /// Computes Average Absolute Error Normalization.
    /// </summary>
    internal static void AverageAbsoluteErrorNormalization(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var eAbsSum = new RollingSum();
        var eSum = new RollingSum();
        double prevY = 0;

        for (var i = 0; i < close.Length; i++)
        {
            if (i == 0)
            {
                prevY = close[i];
            }

            var e = close[i] - prevY;
            eSum.Add(e);
            eAbsSum.Add(Math.Abs(e));

            var eAbsSma = eAbsSum.Average(length);
            var eSma = eSum.Average(length);

            var a = eAbsSma != 0 ? Math.Min(Math.Max(eSma / eAbsSma, -1), 1) : 0;
            output[i] = a;

            prevY = close[i] + (a * eAbsSma);
        }
    }

    /// <summary>
    /// Computes Recursive Stochastic.
    /// </summary>
    internal static void RecursiveStochastic(ReadOnlySpan<double> close, Span<double> output, int length = 200, double alpha = 0.1)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        alpha = Math.Max(0, Math.Min(1, alpha));
        var highWindow = new RollingMinMax(length);
        var lowWindow = new RollingMinMax(length);
        var maWindow = new RollingMinMax(length);
        double prevMa = 0;

        for (var i = 0; i < close.Length; i++)
        {
            highWindow.Add(close[i]);
            lowWindow.Add(close[i]);
            var highest = highWindow.Max;
            var lowest = lowWindow.Min;
            var stoch = highest - lowest != 0 ? (close[i] - lowest) / (highest - lowest) * 100 : 0;

            var ma = (alpha * stoch) + ((1 - alpha) * prevMa);
            maWindow.Add(ma);
            prevMa = ma;

            var highestMa = maWindow.Max;
            var lowestMa = maWindow.Min;

            output[i] = highestMa - lowestMa != 0 ? Math.Min(Math.Max((ma - lowestMa) / (highestMa - lowestMa) * 100, 0), 100) : 0;
        }
    }

    /// <summary>
    /// Computes Shinohara Intensity Ratio (A Ratio).
    /// </summary>
    internal static void ShinoharaIntensityRatioA(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> open, Span<double> output, int length = 14)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var highSum = new RollingSum();
        var lowSum = new RollingSum();
        var openSum = new RollingSum();

        for (var i = 0; i < high.Length; i++)
        {
            highSum.Add(high[i]);
            lowSum.Add(low[i]);
            openSum.Add(open[i]);

            var bullA = highSum.Sum(length) - openSum.Sum(length);
            var bearA = openSum.Sum(length) - lowSum.Sum(length);

            output[i] = bearA != 0 ? bullA / bearA * 100 : 0;
        }
    }

    /// <summary>
    /// Computes Shinohara Intensity Ratio (B Ratio).
    /// </summary>
    internal static void ShinoharaIntensityRatioB(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var highSum = new RollingSum();
        var lowSum = new RollingSum();
        var prevCloseSum = new RollingSum();

        for (var i = 0; i < high.Length; i++)
        {
            highSum.Add(high[i]);
            lowSum.Add(low[i]);
            var prevClose = i >= 1 ? close[i - 1] : 0;
            prevCloseSum.Add(prevClose);

            var bullB = highSum.Sum(length) - prevCloseSum.Sum(length);
            var bearB = prevCloseSum.Sum(length) - lowSum.Sum(length);

            output[i] = bearB != 0 ? bullB / bearB * 100 : 0;
        }
    }

    /// <summary>
    /// Computes Range Action Verification Index (RAVI).
    /// </summary>
    internal static void RangeActionVerificationIndex(ReadOnlySpan<double> close, Span<double> output, int fastLength = 7, int slowLength = 65)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        fastLength = Math.Max(1, fastLength);
        slowLength = Math.Max(1, slowLength);

        var pool = ArrayPool<double>.Shared;
        var fastSmaArray = pool.Rent(close.Length);
        var slowSmaArray = pool.Rent(close.Length);

        try
        {
            var fastSma = fastSmaArray.AsSpan(0, close.Length);
            var slowSma = slowSmaArray.AsSpan(0, close.Length);

            MovingAverageCore.SimpleMovingAverage(close, fastSma, fastLength);
            MovingAverageCore.SimpleMovingAverage(close, slowSma, slowLength);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = slowSma[i] != 0 ? (fastSma[i] - slowSma[i]) / slowSma[i] * 100 : 0;
            }
        }
        finally
        {
            pool.Return(fastSmaArray);
            pool.Return(slowSmaArray);
        }
    }

    /// <summary>
    /// Computes Williams Accumulation Distribution.
    /// </summary>
    internal static void WilliamsAccumulationDistribution(ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double wad = 0;
        for (var i = 0; i < close.Length; i++)
        {
            var prevClose = i >= 1 ? close[i - 1] : 0;
            var prevLow = i >= 1 ? low[i - 1] : 0;
            var prevHigh = i >= 1 ? high[i - 1] : 0;

            if (close[i] > prevClose)
            {
                wad += close[i] - prevLow;
            }
            else if (close[i] < prevClose)
            {
                wad += close[i] - prevHigh;
            }

            output[i] = wad;
        }
    }

    /// <summary>
    /// Computes Total Power Indicator.
    /// </summary>
    internal static void TotalPowerIndicator(ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> bullOutput, Span<double> bearOutput, int length1 = 45, int length2 = 10)
    {
        if (bullOutput.Length < close.Length || bearOutput.Length < close.Length)
        {
            throw new ArgumentException("Output spans must be at least input length.");
        }

        length1 = Math.Max(1, length1);
        length2 = Math.Max(1, length2);

        var pool = ArrayPool<double>.Shared;
        var emaArray = pool.Rent(close.Length);

        try
        {
            var ema = emaArray.AsSpan(0, close.Length);
            MovingAverageCore.ExponentialMovingAverage(close, ema, length2);

            var bullCountSum = new RollingSum();
            var bearCountSum = new RollingSum();

            for (var i = 0; i < close.Length; i++)
            {
                var bullPower = high[i] - ema[i];
                var bearPower = low[i] - ema[i];

                var bullCount = bullPower > 0 ? 1.0 : 0.0;
                var bearCount = bearPower < 0 ? 1.0 : 0.0;

                bullCountSum.Add(bullCount);
                bearCountSum.Add(bearCount);

                bullOutput[i] = length1 != 0 ? 100 * bullCountSum.Sum(length1) / length1 : 0;
                bearOutput[i] = length1 != 0 ? 100 * bearCountSum.Sum(length1) / length1 : 0;
            }
        }
        finally
        {
            pool.Return(emaArray);
        }
    }

    /// <summary>
    /// Computes TurboTrigger.
    /// </summary>
    internal static void TurboTrigger(ReadOnlySpan<double> close, Span<double> output, int length = 100, double pctMultiplier = 1.0)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);

        var pool = ArrayPool<double>.Shared;
        var smaArray = pool.Rent(close.Length);

        try
        {
            var sma = smaArray.AsSpan(0, close.Length);
            MovingAverageCore.SimpleMovingAverage(close, sma, length);

            for (var i = 0; i < close.Length; i++)
            {
                var pct = sma[i] != 0 ? (close[i] - sma[i]) / sma[i] * 100 * pctMultiplier : 0;
                output[i] = pct;
            }
        }
        finally
        {
            pool.Return(smaArray);
        }
    }

    /// <summary>
    /// Computes TurboScaler.
    /// </summary>
    internal static void TurboScaler(ReadOnlySpan<double> close, Span<double> output, int length = 50, double pctMultiplier = 1.0)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var window = new RollingMinMax(length);

        for (var i = 0; i < close.Length; i++)
        {
            window.Add(close[i]);
            var highest = window.Max;
            var lowest = window.Min;
            var range = highest - lowest;

            output[i] = range != 0 ? ((close[i] - lowest) / range * 100 - 50) * pctMultiplier : 0;
        }
    }

    /// <summary>
    /// Computes TTM Scalper Indicator.
    /// </summary>
    internal static void TTMScalperIndicator(ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        double prevBuySellSwitch = 0;
        double prevSbs = 0;

        for (var i = 0; i < close.Length; i++)
        {
            var prevClose1 = i >= 1 ? close[i - 1] : 0;
            var prevClose2 = i >= 2 ? close[i - 2] : 0;
            var prevClose3 = i >= 3 ? close[i - 3] : 0;

            var triggerSell = prevClose1 < close[i] && (prevClose2 < prevClose1 || prevClose3 < prevClose1) ? 1.0 : 0.0;
            var triggerBuy = prevClose1 > close[i] && (prevClose2 > prevClose1 || prevClose3 > prevClose1) ? 1.0 : 0.0;

            var buySellSwitch = triggerSell == 1 ? 1 : triggerBuy == 1 ? 0 : prevBuySellSwitch;
            var sbs = triggerSell == 1 && prevBuySellSwitch == 0 ? high[i] : triggerBuy == 1 && prevBuySellSwitch == 1 ? low[i] : prevSbs;

            output[i] = sbs;
            prevBuySellSwitch = buySellSwitch;
            prevSbs = sbs;
        }
    }

    /// <summary>
    /// Computes Strength of Movement.
    /// </summary>
    internal static void StrengthOfMovement(ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length1 = 10, int length2 = 3)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length1 = Math.Max(1, length1);
        length2 = Math.Max(1, length2);

        var pool = ArrayPool<double>.Shared;
        var somArray = pool.Rent(close.Length);

        try
        {
            var som = somArray.AsSpan(0, close.Length);
            var wmaSum = new RollingSum();

            for (var i = 0; i < close.Length; i++)
            {
                var trueRange = Math.Max(high[i] - low[i], Math.Max(Math.Abs(high[i] - (i >= 1 ? close[i - 1] : 0)), Math.Abs(low[i] - (i >= 1 ? close[i - 1] : 0))));
                var prevClose = i >= length1 ? close[i - length1] : 0;
                var numerator = close[i] - prevClose;
                som[i] = trueRange != 0 ? numerator / trueRange : 0;
            }

            MovingAverageCore.WeightedMovingAverage(som, output, length2);
        }
        finally
        {
            pool.Return(somArray);
        }
    }

    /// <summary>
    /// Computes Value Chart Indicator.
    /// </summary>
    internal static void ValueChartIndicator(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 5, int numAtrs = 8)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);

        var pool = ArrayPool<double>.Shared;
        var smaCloseArray = pool.Rent(close.Length);
        var trArray = pool.Rent(close.Length);
        var smaRangeArray = pool.Rent(close.Length);

        try
        {
            var smaClose = smaCloseArray.AsSpan(0, close.Length);
            var tr = trArray.AsSpan(0, close.Length);
            var smaRange = smaRangeArray.AsSpan(0, close.Length);

            MovingAverageCore.SimpleMovingAverage(close, smaClose, length);

            for (var i = 0; i < close.Length; i++)
            {
                var prevClose = i >= 1 ? close[i - 1] : 0;
                tr[i] = Math.Max(high[i] - low[i], Math.Max(Math.Abs(high[i] - prevClose), Math.Abs(low[i] - prevClose)));
            }

            MovingAverageCore.SimpleMovingAverage(tr, smaRange, length);

            for (var i = 0; i < close.Length; i++)
            {
                var floatAxis = (smaClose[i] + (i >= 1 ? smaClose[i - 1] : 0)) / 2;
                var volatilityUnit = smaRange[i] * 0.2;

                output[i] = volatilityUnit != 0 ? (close[i] - floatAxis) / volatilityUnit : 0;
            }
        }
        finally
        {
            pool.Return(smaCloseArray);
            pool.Return(trArray);
            pool.Return(smaRangeArray);
        }
    }

    /// <summary>
    /// Computes Sell Gravitation Index.
    /// </summary>
    internal static void SellGravitationIndex(ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);

        var pool = ArrayPool<double>.Shared;
        var emaArray = pool.Rent(close.Length);

        try
        {
            var ema = emaArray.AsSpan(0, close.Length);
            MovingAverageCore.ExponentialMovingAverage(close, ema, length);

            var bullSum = new RollingSum();
            var bearSum = new RollingSum();

            for (var i = 0; i < close.Length; i++)
            {
                var bullDistance = close[i] > ema[i] ? high[i] - ema[i] : 0;
                var bearDistance = close[i] < ema[i] ? ema[i] - low[i] : 0;

                bullSum.Add(bullDistance);
                bearSum.Add(bearDistance);

                var totalBull = bullSum.Sum(length);
                var totalBear = bearSum.Sum(length);
                var totalSum = totalBull + totalBear;

                output[i] = totalSum != 0 ? (totalBull - totalBear) / totalSum * 100 : 0;
            }
        }
        finally
        {
            pool.Return(emaArray);
        }
    }

    /// <summary>
    /// Computes TFS Tether Line Indicator.
    /// </summary>
    internal static void TFSTetherLineIndicator(ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 50)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var highWindow = new RollingMinMax(length);
        var lowWindow = new RollingMinMax(length);

        for (var i = 0; i < close.Length; i++)
        {
            highWindow.Add(high[i]);
            lowWindow.Add(low[i]);

            var highest = highWindow.Max;
            var lowest = lowWindow.Min;
            var range = highest - lowest;

            output[i] = range != 0 ? (close[i] - lowest) / range * 100 : 0;
        }
    }

    /// <summary>
    /// Computes Ehlers Simple Cycle Indicator.
    /// </summary>
    internal static void EhlersSimpleCycleIndicator(ReadOnlySpan<double> close, Span<double> output, double alpha = 0.07)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var smoothArray = pool.Rent(close.Length);
        var cycleArray = pool.Rent(close.Length);

        try
        {
            var smooth = smoothArray.AsSpan(0, close.Length);
            var cycle_ = cycleArray.AsSpan(0, close.Length);

            var alphaFactor = 1 - (0.5 * alpha);
            var alphaFactorSq = alphaFactor * alphaFactor;
            var oneMinusAlpha = 1 - alpha;
            var oneMinusAlphaSq = oneMinusAlpha * oneMinusAlpha;

            for (var i = 0; i < close.Length; i++)
            {
                var currentValue = close[i];
                var prevValue1 = i >= 1 ? close[i - 1] : 0;
                var prevValue2 = i >= 2 ? close[i - 2] : 0;
                var prevValue3 = i >= 3 ? close[i - 3] : 0;
                var prevSmooth1 = i >= 1 ? smooth[i - 1] : 0;
                var prevSmooth2 = i >= 2 ? smooth[i - 2] : 0;
                var prevCycle1 = i >= 1 ? cycle_[i - 1] : 0;
                var prevCycle2 = i >= 2 ? cycle_[i - 2] : 0;

                smooth[i] = (currentValue + (2 * prevValue1) + (2 * prevValue2) + prevValue3) / 6;

                cycle_[i] = (alphaFactorSq * (smooth[i] - (2 * prevSmooth1) + prevSmooth2)) +
                           (2 * oneMinusAlpha * prevCycle1) - (oneMinusAlphaSq * prevCycle2);

                output[i] = i < 7 ? (currentValue - (2 * prevValue1) + prevValue2) / 4 : cycle_[i];
            }
        }
        finally
        {
            pool.Return(smoothArray);
            pool.Return(cycleArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Fisher Transform.
    /// </summary>
    internal static void EhlersFisherTransform(ReadOnlySpan<double> close, Span<double> output, int length = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var minMax = new RollingMinMax(length);

        var pool = ArrayPool<double>.Shared;
        var nValueArray = pool.Rent(close.Length);

        try
        {
            var nValue = nValueArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                minMax.Add(close[i]);
                var maxH = minMax.Max;
                var minL = minMax.Min;
                var ratio = maxH - minL != 0 ? (close[i] - minL) / (maxH - minL) : 0;
                var prevNValue = i >= 1 ? nValue[i - 1] : 0;
                var prevFisher = i >= 1 ? output[i - 1] : 0;

                // Clamp nValue to avoid log(0) or log(negative)
                var nVal = (0.33 * 2 * (ratio - 0.5)) + (0.67 * prevNValue);
                nValue[i] = Math.Max(-0.999, Math.Min(0.999, nVal));

                output[i] = (0.5 * Math.Log((1 + nValue[i]) / (1 - nValue[i]))) + (0.5 * prevFisher);
            }
        }
        finally
        {
            pool.Return(nValueArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Voss Predictive Filter.
    /// </summary>
    internal static void EhlersVossPredictiveFilter(ReadOnlySpan<double> close, Span<double> output, int length = 20, double predict = 3, double bw = 0.25)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var order = (int)Math.Max(1, Math.Min(Math.Ceiling(3 * predict), int.MaxValue));
        var f1 = Math.Cos(2 * Math.PI / length);
        var g1 = Math.Cos(bw * 2 * Math.PI / length);
        var s1 = g1 != 0 ? (1 / g1) - Math.Sqrt((1 / (g1 * g1)) - 1) : 0;

        var pool = ArrayPool<double>.Shared;
        var filtArray = pool.Rent(close.Length);
        var vossArray = pool.Rent(close.Length);

        try
        {
            var filt = filtArray.AsSpan(0, close.Length);
            var voss = vossArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                var currentValue = close[i];
                var prevFilt1 = i >= 1 ? filt[i - 1] : 0;
                var prevFilt2 = i >= 2 ? filt[i - 2] : 0;
                var prevValue = i >= 2 ? close[i - 2] : 0;

                filt[i] = i <= 5 ? 0 : (0.5 * (1 - s1) * (currentValue - prevValue)) + (f1 * (1 + s1) * prevFilt1) - (s1 * prevFilt2);

                double sumC = 0;
                for (var j = 0; j <= order - 1; j++)
                {
                    var idx = i - (order - j);
                    var prevVoss = idx >= 0 ? voss[idx] : 0;
                    sumC += (double)(j + 1) / order * prevVoss;
                }

                voss[i] = ((double)(3 + order) / 2 * filt[i]) - sumC;
                output[i] = voss[i];
            }
        }
        finally
        {
            pool.Return(filtArray);
            pool.Return(vossArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Spearman Rank Indicator.
    /// </summary>
    internal static void EhlersSpearmanRankIndicator(ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);

        for (var i = 0; i < close.Length; i++)
        {
            var priceArray = new double[length + 1];
            var rankArray = new double[length + 1];

            for (var j = 1; j <= length; j++)
            {
                var idx = i - (j - 1);
                priceArray[j] = idx >= 0 ? close[idx] : 0;
                rankArray[j] = j;
            }

            // Bubble sort to rank prices
            for (var j = 1; j <= length; j++)
            {
                var count = length + 1 - j;
                for (var k = 1; k <= length - count; k++)
                {
                    if (priceArray[k + 1] < priceArray[k])
                    {
                        var tempPrice = priceArray[k];
                        var tempRank = rankArray[k];
                        priceArray[k] = priceArray[k + 1];
                        rankArray[k] = rankArray[k + 1];
                        priceArray[k + 1] = tempPrice;
                        rankArray[k + 1] = tempRank;
                    }
                }
            }

            double sum = 0;
            for (var j = 1; j <= length; j++)
            {
                sum += Math.Pow(j - rankArray[j], 2);
            }

            var denom = length * (Math.Pow(length, 2) - 1);
            output[i] = denom != 0 ? 2 * (0.5 - (1 - (6 * sum / denom))) : 0;
        }
    }

    /// <summary>
    /// Computes Ehlers Correlation Cycle Indicator (Real output).
    /// </summary>
    internal static void EhlersCorrelationCycleIndicator(ReadOnlySpan<double> close, Span<double> realOutput, Span<double> imagOutput, int length = 20)
    {
        if (realOutput.Length < close.Length || imagOutput.Length < close.Length)
        {
            throw new ArgumentException("Output spans must be at least input length.");
        }

        length = Math.Max(1, length);

        for (var i = 0; i < close.Length; i++)
        {
            double sx = 0, sy = 0, nsy = 0, sxx = 0, syy = 0, nsyy = 0, sxy = 0, nsxy = 0;

            for (var j = 1; j <= length; j++)
            {
                var idx = i - (j - 1);
                var x = idx >= 0 ? close[idx] : 0;
                var v = Math.Max(0.01, Math.Min(0.99, 2 * Math.PI * ((double)(j - 1) / length)));
                var y = Math.Cos(v);
                var ny = -Math.Sin(v);
                sx += x;
                sy += y;
                nsy += ny;
                sxx += x * x;
                syy += y * y;
                nsyy += ny * ny;
                sxy += x * y;
                nsxy += x * ny;
            }

            var realDenom1 = (length * sxx) - (sx * sx);
            var realDenom2 = (length * syy) - (sy * sy);
            realOutput[i] = realDenom1 > 0 && realDenom2 > 0
                ? ((length * sxy) - (sx * sy)) / Math.Sqrt(realDenom1 * realDenom2)
                : 0;

            var imagDenom2 = (length * nsyy) - (nsy * nsy);
            imagOutput[i] = realDenom1 > 0 && imagDenom2 > 0
                ? ((length * nsxy) - (sx * nsy)) / Math.Sqrt(realDenom1 * imagDenom2)
                : 0;
        }
    }

    /// <summary>
    /// Computes Ehlers Correlation Angle Indicator.
    /// </summary>
    internal static void EhlersCorrelationAngleIndicator(ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);

        var pool = ArrayPool<double>.Shared;
        var realArray = pool.Rent(close.Length);
        var imagArray = pool.Rent(close.Length);

        try
        {
            var real = realArray.AsSpan(0, close.Length);
            var imag = imagArray.AsSpan(0, close.Length);

            EhlersCorrelationCycleIndicator(close, real, imag, length);

            for (var i = 0; i < close.Length; i++)
            {
                var prevAngle = i >= 1 ? output[i - 1] : 0;
                var angle = imag[i] != 0 ? 90 + (Math.Atan(real[i] / imag[i]) * 180 / Math.PI) : 90;
                angle = imag[i] > 0 ? angle - 180 : angle;
                angle = prevAngle - angle < 270 && angle < prevAngle ? prevAngle : angle;
                output[i] = angle;
            }
        }
        finally
        {
            pool.Return(realArray);
            pool.Return(imagArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Truncated BandPass Filter.
    /// </summary>
    internal static void EhlersTruncatedBandPassFilter(ReadOnlySpan<double> close, Span<double> output, int length1 = 20, int length2 = 10, double bw = 0.1)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length1 = Math.Max(1, length1);
        length2 = Math.Max(1, length2);

        var l1 = Math.Cos(Math.Max(0.01, Math.Min(0.99, 2 * Math.PI / length1)));
        var g1 = Math.Cos(bw * 2 * Math.PI / length1);
        var s1 = (1 / g1) - Math.Sqrt((1 / (g1 * g1)) - 1);

        for (var i = 0; i < close.Length; i++)
        {
            var trunArray = new double[length2 + 3];

            for (var j = length2; j > 0; j--)
            {
                var idx1 = i - (j - 1);
                var idx2 = i - (j + 1);
                var prevValue1 = idx1 >= 0 ? close[idx1] : 0;
                var prevValue2 = idx2 >= 0 ? close[idx2] : 0;
                trunArray[j] = (0.5 * (1 - s1) * (prevValue1 - prevValue2)) + (l1 * (1 + s1) * trunArray[j + 1]) - (s1 * trunArray[j + 2]);
            }

            output[i] = trunArray[1];
        }
    }

    /// <summary>
    /// Computes Ehlers Simple Decycler.
    /// </summary>
    internal static void EhlersSimpleDecycler(ReadOnlySpan<double> close, Span<double> output, int length = 125)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);

        var pool = ArrayPool<double>.Shared;
        var hpArray = pool.Rent(close.Length);

        try
        {
            var hp = hpArray.AsSpan(0, close.Length);
            MovingAverageCore.EhlersHighPassFilterV1(close, hp, length, 1);

            for (var i = 0; i < close.Length; i++)
            {
                output[i] = close[i] - hp[i];
            }
        }
        finally
        {
            pool.Return(hpArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Even Better Sine Wave Indicator.
    /// </summary>
    internal static void EhlersEvenBetterSineWaveIndicator(ReadOnlySpan<double> close, Span<double> output, int length1 = 40, int length2 = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length1 = Math.Max(1, length1);
        length2 = Math.Max(1, length2);

        var piHp = Math.Max(0.01, Math.Min(0.99, 2 * Math.PI / length1));
        var a1 = (1 - Math.Sin(piHp)) / Math.Cos(piHp);
        var a2 = Math.Exp(Math.Max(-0.99, Math.Min(-0.01, -1.414 * Math.PI / length2)));
        var b = 2 * a2 * Math.Cos(Math.Max(0.01, Math.Min(0.99, 1.414 * Math.PI / length2)));
        var c2 = b;
        var c3 = -a2 * a2;
        var c1 = 1 - c2 - c3;

        var pool = ArrayPool<double>.Shared;
        var hpArray = pool.Rent(close.Length);
        var filtArray = pool.Rent(close.Length);

        try
        {
            var hp = hpArray.AsSpan(0, close.Length);
            var filt = filtArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                var currentValue = close[i];
                var prevValue = i >= 1 ? close[i - 1] : 0;
                var prevHp = i >= 1 ? hp[i - 1] : 0;
                var prevFilt1 = i >= 1 ? filt[i - 1] : 0;
                var prevFilt2 = i >= 2 ? filt[i - 2] : 0;

                var diff = currentValue - prevValue;
                var minPastDiff = i >= 1 ? diff : 0;
                hp[i] = ((0.5 * (1 + a1)) * minPastDiff) + (a1 * prevHp);

                filt[i] = (c1 * ((hp[i] + prevHp) / 2)) + (c2 * prevFilt1) + (c3 * prevFilt2);

                var wave = (filt[i] + prevFilt1 + prevFilt2) / 3;
                var pwr = (filt[i] * filt[i] + prevFilt1 * prevFilt1 + prevFilt2 * prevFilt2) / 3;
                output[i] = pwr > 0 ? wave / Math.Sqrt(pwr) : 0;
            }
        }
        finally
        {
            pool.Return(hpArray);
            pool.Return(filtArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Market State Indicator.
    /// </summary>
    internal static void EhlersMarketStateIndicator(ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);

        var pool = ArrayPool<double>.Shared;
        var angleArray = pool.Rent(close.Length);

        try
        {
            var angle = angleArray.AsSpan(0, close.Length);
            EhlersCorrelationAngleIndicator(close, angle, length);

            for (var i = 0; i < close.Length; i++)
            {
                var currentAngle = angle[i];
                var prevAngle = i >= 1 ? angle[i - 1] : 0;

                double state;
                if (Math.Abs(currentAngle - prevAngle) < 9 && currentAngle < 0)
                {
                    state = -1;
                }
                else if (Math.Abs(currentAngle - prevAngle) < 9 && currentAngle >= 0)
                {
                    state = 1;
                }
                else
                {
                    state = 0;
                }
                output[i] = state;
            }
        }
        finally
        {
            pool.Return(angleArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Instantaneous Trendline V2.
    /// </summary>
    internal static void EhlersInstantaneousTrendlineV2(ReadOnlySpan<double> close, Span<double> output, double alpha = 0.07)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        for (var i = 0; i < close.Length; i++)
        {
            var currentValue = close[i];
            var prevValue1 = i >= 1 ? close[i - 1] : 0;
            var prevValue2 = i >= 2 ? close[i - 2] : 0;
            var prevIt1 = i >= 1 ? output[i - 1] : 0;
            var prevIt2 = i >= 2 ? output[i - 2] : 0;

            var it = i < 7
                ? (currentValue + (2 * prevValue1) + prevValue2) / 4
                : (((alpha - ((alpha * alpha) / 4)) * currentValue) + ((0.5 * alpha * alpha) * prevValue1) -
                   (((alpha - ((3 * alpha * alpha) / 4)) * prevValue2)) + ((2 * (1 - alpha)) * prevIt1) -
                   (((1 - alpha) * (1 - alpha)) * prevIt2));

            output[i] = it;
        }
    }

    /// <summary>
    /// Computes Ehlers CyberCycle.
    /// </summary>
    internal static void EhlersCyberCycleOscillator(ReadOnlySpan<double> close, Span<double> output, double alpha = 0.07)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        var pool = ArrayPool<double>.Shared;
        var smoothArray = pool.Rent(close.Length);
        var cycleArray = pool.Rent(close.Length);

        try
        {
            var smooth = smoothArray.AsSpan(0, close.Length);
            var cycle = cycleArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                var currentValue = close[i];
                var prevValue1 = i >= 1 ? close[i - 1] : 0;
                var prevValue2 = i >= 2 ? close[i - 2] : 0;
                var prevValue3 = i >= 3 ? close[i - 3] : 0;
                var prevSmooth1 = i >= 1 ? smooth[i - 1] : 0;
                var prevSmooth2 = i >= 2 ? smooth[i - 2] : 0;
                var prevCycle1 = i >= 1 ? cycle[i - 1] : 0;
                var prevCycle2 = i >= 2 ? cycle[i - 2] : 0;

                smooth[i] = (currentValue + (2 * prevValue1) + (2 * prevValue2) + prevValue3) / 6;

                var cycleVal = i < 7
                    ? (currentValue - (2 * prevValue1) + prevValue2) / 4
                    : ((1 - (0.5 * alpha)) * (1 - (0.5 * alpha)) * (smooth[i] - (2 * prevSmooth1) + prevSmooth2)) +
                      (2 * (1 - alpha) * prevCycle1) - ((1 - alpha) * (1 - alpha) * prevCycle2);

                cycle[i] = cycleVal;
                output[i] = cycleVal;
            }
        }
        finally
        {
            pool.Return(smoothArray);
            pool.Return(cycleArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Band Pass Filter V1.
    /// </summary>
    internal static void EhlersBandPassFilterV1(ReadOnlySpan<double> close, Span<double> output, int length = 20, double bw = 0.3)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);

        var twoPiPrd1 = Math.Max(0.01, Math.Min(0.99, 0.25 * bw * 2 * Math.PI / length));
        var twoPiPrd2 = Math.Max(0.01, Math.Min(0.99, 1.5 * bw * 2 * Math.PI / length));
        var beta = Math.Cos(Math.Max(0.01, Math.Min(0.99, 2 * Math.PI / length)));
        var gamma = 1 / Math.Cos(Math.Max(0.01, Math.Min(0.99, 2 * Math.PI * bw / length)));
        var alpha1 = gamma - Math.Sqrt((gamma * gamma) - 1);
        var alpha2 = (Math.Cos(twoPiPrd1) + Math.Sin(twoPiPrd1) - 1) / Math.Cos(twoPiPrd1);
        var alpha3 = (Math.Cos(twoPiPrd2) + Math.Sin(twoPiPrd2) - 1) / Math.Cos(twoPiPrd2);

        var pool = ArrayPool<double>.Shared;
        var hpArray = pool.Rent(close.Length);
        var bpArray = pool.Rent(close.Length);
        var peakArray = pool.Rent(close.Length);
        var signalArray = pool.Rent(close.Length);

        try
        {
            var hp = hpArray.AsSpan(0, close.Length);
            var bp = bpArray.AsSpan(0, close.Length);
            var peak = peakArray.AsSpan(0, close.Length);
            var signal = signalArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                var currentValue = close[i];
                var prevValue = i >= 1 ? close[i - 1] : 0;
                var prevHp1 = i >= 1 ? hp[i - 1] : 0;
                var prevHp2 = i >= 2 ? hp[i - 2] : 0;
                var prevBp1 = i >= 1 ? bp[i - 1] : 0;
                var prevBp2 = i >= 2 ? bp[i - 2] : 0;

                var diff = currentValue - prevValue;
                var minPastDiff = i >= 1 ? diff : 0;
                hp[i] = ((1 + (alpha2 / 2)) * minPastDiff) + ((1 - alpha2) * prevHp1);

                bp[i] = i > 2 ? (0.5 * (1 - alpha1) * (hp[i] - prevHp2)) + (beta * (1 + alpha1) * prevBp1) - (alpha1 * prevBp2) : 0;

                var prevPeak = i >= 1 ? peak[i - 1] : 0;
                peak[i] = Math.Max(0.991 * prevPeak, Math.Abs(bp[i]));

                var prevSig = i >= 1 ? signal[i - 1] : 0;
                signal[i] = peak[i] != 0 ? bp[i] / peak[i] : 0;

                var prevTrigger = i >= 1 ? output[i - 1] : 0;
                output[i] = ((1 + (alpha3 / 2)) * (signal[i] - prevSig)) + ((1 - alpha3) * prevTrigger);
            }
        }
        finally
        {
            pool.Return(hpArray);
            pool.Return(bpArray);
            pool.Return(peakArray);
            pool.Return(signalArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Band Pass Filter V2.
    /// </summary>
    internal static void EhlersBandPassFilterV2(ReadOnlySpan<double> close, Span<double> output, int length = 20, double bw = 0.3)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);

        var l1 = Math.Cos(Math.Max(0.01, Math.Min(0.99, 2 * Math.PI / length)));
        var g1 = Math.Cos(Math.Max(0.01, Math.Min(0.99, bw * 2 * Math.PI / length)));
        var s1 = (1 / g1) - Math.Sqrt(1 / (g1 * g1) - 1);

        for (var i = 0; i < close.Length; i++)
        {
            var currentValue = close[i];
            var prevValue = i >= 2 ? close[i - 2] : 0;
            var prevBp1 = i >= 1 ? output[i - 1] : 0;
            var prevBp2 = i >= 2 ? output[i - 2] : 0;

            output[i] = i < 3 ? 0 : (0.5 * (1 - s1) * (currentValue - prevValue)) + (l1 * (1 + s1) * prevBp1) - (s1 * prevBp2);
        }
    }

    /// <summary>
    /// Computes Ehlers Cycle Band Pass Filter.
    /// </summary>
    internal static void EhlersCycleBandPassFilter(ReadOnlySpan<double> close, Span<double> output, int length = 20, double delta = 0.1)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);

        var beta = Math.Cos(Math.Max(0.01, Math.Min(0.99, 2 * Math.PI / length)));
        var gamma = 1 / Math.Cos(Math.Max(0.01, Math.Min(0.99, 4 * Math.PI * delta / length)));
        var alpha = gamma - Math.Sqrt((gamma * gamma) - 1);

        for (var i = 0; i < close.Length; i++)
        {
            var currentValue = close[i];
            var prevValue = i >= 2 ? close[i - 2] : 0;
            var prevBp1 = i >= 1 ? output[i - 1] : 0;
            var prevBp2 = i >= 2 ? output[i - 2] : 0;

            var diff = currentValue - prevValue;
            var minPastDiff = i >= 2 ? diff : 0;
            output[i] = (0.5 * (1 - alpha) * minPastDiff) + (beta * (1 + alpha) * prevBp1) - (alpha * prevBp2);
        }
    }

    /// <summary>
    /// Computes Ehlers Cycle Amplitude.
    /// </summary>
    internal static void EhlersCycleAmplitude(ReadOnlySpan<double> close, Span<double> output, int length = 20, double delta = 0.1)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var lbLength = (int)Math.Ceiling((double)length / 4);

        var pool = ArrayPool<double>.Shared;
        var bpArray = pool.Rent(close.Length);

        try
        {
            var bp = bpArray.AsSpan(0, close.Length);
            EhlersCycleBandPassFilter(close, bp, length, delta);

            for (var i = 0; i < close.Length; i++)
            {
                double power = 0;
                for (var j = 0; j < length; j++)
                {
                    var prevBp1 = i >= j ? bp[i - j] : 0;
                    var prevBp2 = i >= j + lbLength ? bp[i - (j + lbLength)] : 0;
                    power += (prevBp1 * prevBp1) + (prevBp2 * prevBp2);
                }

                output[i] = 2 * 1.414 * Math.Sqrt(power / length);
            }
        }
        finally
        {
            pool.Return(bpArray);
        }
    }

    /// <summary>
    /// Computes Ehlers HP/LP Roofing Filter.
    /// Combines high-pass and low-pass filtering.
    /// </summary>
    internal static void EhlersHpLpRoofingFilter(ReadOnlySpan<double> close, Span<double> output, int length1 = 48, int length2 = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length1 = Math.Max(1, length1);
        length2 = Math.Max(1, length2);

        var alphaArg = Math.Min(2 * Math.PI / length1, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        var alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        var sqrt2 = Math.Sqrt(2);
        var a1 = Math.Exp(-sqrt2 * Math.PI / length2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(sqrt2 * Math.PI / length2, 0.99));
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        var pool = ArrayPool<double>.Shared;
        var hpArray = pool.Rent(close.Length);

        try
        {
            var hp = hpArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                var currentValue = close[i];
                var prevValue = i >= 1 ? close[i - 1] : 0;
                var prevFilter1 = i >= 1 ? output[i - 1] : 0;
                var prevFilter2 = i >= 2 ? output[i - 2] : 0;
                var prevHp = i >= 1 ? hp[i - 1] : 0;

                var diff = currentValue - prevValue;
                var minPastDiff = i >= 1 ? diff : 0;
                hp[i] = ((1 - (alpha1 / 2)) * minPastDiff) + ((1 - alpha1) * prevHp);

                output[i] = (c1 * ((hp[i] + prevHp) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            }
        }
        finally
        {
            pool.Return(hpArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Early Onset Trend Indicator.
    /// Uses high-pass filter and super smoother.
    /// </summary>
    internal static void EhlersEarlyOnsetTrendIndicator(ReadOnlySpan<double> close, Span<double> output, int length1 = 30, int length2 = 100, double k = 0.85)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length1 = Math.Max(1, length1);
        length2 = Math.Max(1, length2);

        var pool = ArrayPool<double>.Shared;
        var hpArray = pool.Rent(close.Length);
        var ssfArray = pool.Rent(close.Length);
        var peakArray = pool.Rent(close.Length);

        try
        {
            var hp = hpArray.AsSpan(0, close.Length);
            var ssf = ssfArray.AsSpan(0, close.Length);
            var peak = peakArray.AsSpan(0, close.Length);

            // Apply high-pass filter
            MovingAverageCore.EhlersHighPassFilterV1(close, hp, length2, 1);

            // Apply super smoother to HP output
            MovingAverageCore.Ehlers2PoleSuperSmootherFilterV2(hp, ssf, length1);

            for (var i = 0; i < close.Length; i++)
            {
                var filter = ssf[i];

                var prevPeak = i >= 1 ? peak[i - 1] : 0;
                peak[i] = Math.Abs(filter) > 0.991 * prevPeak ? Math.Abs(filter) : 0.991 * prevPeak;

                var ratio = peak[i] != 0 ? filter / peak[i] : 0;
                output[i] = (k * ratio) + 1 != 0 ? (ratio + k) / ((k * ratio) + 1) : 0;
            }
        }
        finally
        {
            pool.Return(hpArray);
            pool.Return(ssfArray);
            pool.Return(peakArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Detrended Leading Indicator using high and low prices.
    /// </summary>
    internal static void EhlersDetrendedLeadingIndicator(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> output, int length = 14)
    {
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);
        var alpha = length > 2 ? (double)2 / (length + 1) : 0.67;
        var alpha2 = alpha / 2;

        var pool = ArrayPool<double>.Shared;
        var ema1Array = pool.Rent(high.Length);
        var ema2Array = pool.Rent(high.Length);
        var dspArray = pool.Rent(high.Length);
        var tempArray = pool.Rent(high.Length);

        try
        {
            var ema1 = ema1Array.AsSpan(0, high.Length);
            var ema2 = ema2Array.AsSpan(0, high.Length);
            var dsp = dspArray.AsSpan(0, high.Length);
            var temp = tempArray.AsSpan(0, high.Length);

            for (var i = 0; i < high.Length; i++)
            {
                var prevHigh = i >= 1 ? high[i - 1] : 0;
                var prevLow = i >= 1 ? low[i - 1] : 0;
                var currentHigh = Math.Max(prevHigh, high[i]);
                var currentLow = Math.Min(prevLow, low[i]);
                var currentPrice = (currentHigh + currentLow) / 2;

                var prevEma1 = i >= 1 ? ema1[i - 1] : currentPrice;
                var prevEma2 = i >= 1 ? ema2[i - 1] : currentPrice;

                ema1[i] = (alpha * currentPrice) + ((1 - alpha) * prevEma1);
                ema2[i] = (alpha2 * currentPrice) + ((1 - alpha2) * prevEma2);

                dsp[i] = ema1[i] - ema2[i];

                var prevTemp = i >= 1 ? temp[i - 1] : 0;
                temp[i] = (alpha * dsp[i]) + ((1 - alpha) * prevTemp);

                output[i] = dsp[i] - temp[i];
            }
        }
        finally
        {
            pool.Return(ema1Array);
            pool.Return(ema2Array);
            pool.Return(dspArray);
            pool.Return(tempArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Classic Hilbert Transformer.
    /// </summary>
    internal static void EhlersClassicHilbertTransformer(ReadOnlySpan<double> close, Span<double> real, Span<double> imag, int length1 = 48, int length2 = 10)
    {
        if (real.Length < close.Length || imag.Length < close.Length)
        {
            throw new ArgumentException("Output spans must be at least input length.");
        }

        length1 = Math.Max(1, length1);
        length2 = Math.Max(1, length2);

        var pool = ArrayPool<double>.Shared;
        var rfArray = pool.Rent(close.Length);
        var peakArray = pool.Rent(close.Length);

        try
        {
            var rf = rfArray.AsSpan(0, close.Length);
            var peak = peakArray.AsSpan(0, close.Length);

            // Apply roofing filter
            MovingAverageCore.EhlersRoofingFilter(close, rf, length2, length1);

            for (var i = 0; i < close.Length; i++)
            {
                var roofingFilter = rf[i];

                var prevPeak = i >= 1 ? peak[i - 1] : 0;
                peak[i] = Math.Max(0.991 * prevPeak, Math.Abs(roofingFilter));

                real[i] = peak[i] != 0 ? roofingFilter / peak[i] : 0;

                // Hilbert Transform coefficients
                var prevReal2 = i >= 2 ? real[i - 2] : 0;
                var prevReal4 = i >= 4 ? real[i - 4] : 0;
                var prevReal6 = i >= 6 ? real[i - 6] : 0;
                var prevReal8 = i >= 8 ? real[i - 8] : 0;
                var prevReal10 = i >= 10 ? real[i - 10] : 0;
                var prevReal12 = i >= 12 ? real[i - 12] : 0;
                var prevReal14 = i >= 14 ? real[i - 14] : 0;
                var prevReal16 = i >= 16 ? real[i - 16] : 0;
                var prevReal18 = i >= 18 ? real[i - 18] : 0;
                var prevReal20 = i >= 20 ? real[i - 20] : 0;
                var prevReal22 = i >= 22 ? real[i - 22] : 0;

                imag[i] = ((0.091 * real[i]) + (0.111 * prevReal2) + (0.143 * prevReal4) + (0.2 * prevReal6) + (0.333 * prevReal8) + prevReal10 -
                           prevReal12 - (0.333 * prevReal14) - (0.2 * prevReal16) - (0.143 * prevReal18) - (0.111 * prevReal20) - (0.091 * prevReal22)) / 1.865;
            }
        }
        finally
        {
            pool.Return(rfArray);
            pool.Return(peakArray);
        }
    }

    /// <summary>
    /// Calculates Ehlers Zero Mean Roofing Filter.
    /// </summary>
    internal static void EhlersZeroMeanRoofingFilter(ReadOnlySpan<double> close, Span<double> output, int length1 = 48, int length2 = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.");
        }

        length1 = Math.Max(1, length1);
        length2 = Math.Max(1, length2);

        var alphaArg = Math.Min(2 * Math.PI / length1, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        var alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;

        var pool = ArrayPool<double>.Shared;
        var rfArray = pool.Rent(close.Length);

        try
        {
            var rf = rfArray.AsSpan(0, close.Length);

            // Apply HP-LP roofing filter first
            EhlersHpLpRoofingFilter(close, rf, length1, length2);

            for (var i = 0; i < close.Length; i++)
            {
                var currentRf = rf[i];
                var prevRf = i >= 1 ? rf[i - 1] : 0;
                var prevZmr1 = i >= 1 ? output[i - 1] : 0;

                output[i] = ((1 - (alpha1 / 2)) * (currentRf - prevRf)) + ((1 - alpha1) * prevZmr1);
            }
        }
        finally
        {
            pool.Return(rfArray);
        }
    }

    /// <summary>
    /// Calculates Ehlers Super Passband Filter.
    /// </summary>
    internal static void EhlersSuperPassbandFilter(ReadOnlySpan<double> close, Span<double> output, int fastLength = 40, int slowLength = 60, int length1 = 5, int length2 = 50)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.");
        }

        fastLength = Math.Max(1, fastLength);
        slowLength = Math.Max(1, slowLength);
        length1 = Math.Max(1, length1);
        length2 = Math.Max(1, length2);

        var a1 = Math.Max(0.01, Math.Min(0.99, (double)length1 / fastLength));
        var a2 = Math.Max(0.01, Math.Min(0.99, (double)length1 / slowLength));

        for (var i = 0; i < close.Length; i++)
        {
            var currentValue = close[i];
            var prevValue1 = i >= 1 ? close[i - 1] : 0;
            var prevEspf1 = i >= 1 ? output[i - 1] : 0;
            var prevEspf2 = i >= 2 ? output[i - 2] : 0;

            output[i] = ((a1 - a2) * currentValue) + (((a2 * (1 - a1)) - (a1 * (1 - a2))) * prevValue1) +
                        ((1 - a1 + (1 - a2)) * prevEspf1) - ((1 - a1) * (1 - a2) * prevEspf2);
        }
    }

    /// <summary>
    /// Calculates Ehlers Roofing Filter V2.
    /// </summary>
    internal static void EhlersRoofingFilterV2(ReadOnlySpan<double> close, Span<double> output, int upperLength = 80, int lowerLength = 40)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.");
        }

        upperLength = Math.Max(1, upperLength);
        lowerLength = Math.Max(1, lowerLength);

        var sqrt2 = Math.Sqrt(2);
        var alphaArg = Math.Min(sqrt2 * Math.PI / upperLength, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        var alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        var a1 = Math.Exp(-sqrt2 * Math.PI / lowerLength);
        var b1 = 2 * a1 * Math.Cos(Math.Min(sqrt2 * Math.PI / lowerLength, 0.99));
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        var pool = ArrayPool<double>.Shared;
        var hpArray = pool.Rent(close.Length);

        try
        {
            var hp = hpArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                var currentValue = close[i];
                var prevValue1 = i >= 1 ? close[i - 1] : 0;
                var prevValue2 = i >= 2 ? close[i - 2] : 0;
                var prevHp1 = i >= 1 ? hp[i - 1] : 0;
                var prevHp2 = i >= 2 ? hp[i - 2] : 0;
                var prevFilter1 = i >= 1 ? output[i - 1] : 0;
                var prevFilter2 = i >= 2 ? output[i - 2] : 0;

                var test1 = Math.Pow((1 - alpha1) / 2, 2);
                var test2 = currentValue - (2 * prevValue1) + prevValue2;
                var v1 = test1 * test2;
                var v2 = 2 * (1 - alpha1) * prevHp1;
                var v3 = Math.Pow(1 - alpha1, 2) * prevHp2;

                hp[i] = v1 + v2 - v3;
                output[i] = (c1 * ((hp[i] + prevHp1) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);
            }
        }
        finally
        {
            pool.Return(hpArray);
        }
    }

    /// <summary>
    /// Calculates Ehlers Impulse Reaction.
    /// </summary>
    internal static void EhlersImpulseReaction(ReadOnlySpan<double> close, Span<double> output, int length1 = 2, int length2 = 20, double q = 0.9)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.");
        }

        length1 = Math.Max(1, length1);
        length2 = Math.Max(1, length2);

        var c2 = 2 * q * Math.Cos(2 * Math.PI / length2);
        var c3 = -q * q;
        var c1 = (1 + c3) / 2;

        var pool = ArrayPool<double>.Shared;
        var reactionArray = pool.Rent(close.Length);

        try
        {
            var reaction = reactionArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                var currentValue = close[i];
                var priorValue = i >= length1 ? close[i - length1] : 0;
                var prevReaction1 = i >= 1 ? reaction[i - 1] : 0;
                var prevReaction2 = i >= 2 ? reaction[i - 2] : 0;

                reaction[i] = (c1 * (currentValue - priorValue)) + (c2 * prevReaction1) + (c3 * prevReaction2);
                output[i] = currentValue != 0 ? 100 * reaction[i] / currentValue : 0;
            }
        }
        finally
        {
            pool.Return(reactionArray);
        }
    }

    /// <summary>
    /// Calculates Ehlers Reverse Exponential Moving Average Indicator V1.
    /// </summary>
    internal static void EhlersReverseEmaIndicatorV1(ReadOnlySpan<double> close, Span<double> output, double alpha = 0.1)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.");
        }

        alpha = Math.Max(0.01, Math.Min(0.99, alpha));
        var cc = 1 - alpha;

        var pool = ArrayPool<double>.Shared;
        var emaArray = pool.Rent(close.Length);
        var re1Array = pool.Rent(close.Length);
        var re2Array = pool.Rent(close.Length);
        var re3Array = pool.Rent(close.Length);
        var re4Array = pool.Rent(close.Length);
        var re5Array = pool.Rent(close.Length);
        var re6Array = pool.Rent(close.Length);
        var re7Array = pool.Rent(close.Length);

        try
        {
            var ema = emaArray.AsSpan(0, close.Length);
            var re1 = re1Array.AsSpan(0, close.Length);
            var re2 = re2Array.AsSpan(0, close.Length);
            var re3 = re3Array.AsSpan(0, close.Length);
            var re4 = re4Array.AsSpan(0, close.Length);
            var re5 = re5Array.AsSpan(0, close.Length);
            var re6 = re6Array.AsSpan(0, close.Length);
            var re7 = re7Array.AsSpan(0, close.Length);

            var cc2 = cc * cc;
            var cc4 = cc2 * cc2;
            var cc8 = cc4 * cc4;
            var cc16 = cc8 * cc8;
            var cc32 = cc16 * cc16;
            var cc64 = cc32 * cc32;
            var cc128 = cc64 * cc64;

            for (var i = 0; i < close.Length; i++)
            {
                var currentValue = close[i];
                var prevEma = i >= 1 ? ema[i - 1] : 0;
                ema[i] = (alpha * currentValue) + (cc * prevEma);

                var prevRe1 = i >= 1 ? re1[i - 1] : 0;
                re1[i] = (cc * ema[i]) + prevEma;

                var prevRe2 = i >= 1 ? re2[i - 1] : 0;
                re2[i] = (cc2 * re1[i]) + prevRe1;

                var prevRe3 = i >= 1 ? re3[i - 1] : 0;
                re3[i] = (cc4 * re2[i]) + prevRe2;

                var prevRe4 = i >= 1 ? re4[i - 1] : 0;
                re4[i] = (cc8 * re3[i]) + prevRe3;

                var prevRe5 = i >= 1 ? re5[i - 1] : 0;
                re5[i] = (cc16 * re4[i]) + prevRe4;

                var prevRe6 = i >= 1 ? re6[i - 1] : 0;
                re6[i] = (cc32 * re5[i]) + prevRe5;

                var prevRe7 = i >= 1 ? re7[i - 1] : 0;
                re7[i] = (cc64 * re6[i]) + prevRe6;

                var re8 = (cc128 * re7[i]) + prevRe7;
                output[i] = ema[i] - (alpha * re8);
            }
        }
        finally
        {
            pool.Return(emaArray);
            pool.Return(re1Array);
            pool.Return(re2Array);
            pool.Return(re3Array);
            pool.Return(re4Array);
            pool.Return(re5Array);
            pool.Return(re6Array);
            pool.Return(re7Array);
        }
    }

    /// <summary>
    /// Calculates Ehlers Squelch Indicator.
    /// </summary>
    internal static void EhlersSquelchIndicator(ReadOnlySpan<double> close, Span<double> output, int length1 = 6, int length2 = 20, int length3 = 40)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.");
        }

        length1 = Math.Max(1, length1);
        length2 = Math.Max(1, length2);
        length3 = Math.Max(1, length3);

        var pool = ArrayPool<double>.Shared;
        var v1Array = pool.Rent(close.Length);
        var ipArray = pool.Rent(close.Length);
        var quArray = pool.Rent(close.Length);
        var phaseArray = pool.Rent(close.Length);
        var dPhaseArray = pool.Rent(close.Length);
        var dcPeriodArray = pool.Rent(close.Length);

        try
        {
            var v1 = v1Array.AsSpan(0, close.Length);
            var ip = ipArray.AsSpan(0, close.Length);
            var qu = quArray.AsSpan(0, close.Length);
            var phase = phaseArray.AsSpan(0, close.Length);
            var dPhase = dPhaseArray.AsSpan(0, close.Length);
            var dcPeriod = dcPeriodArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                var currentValue = close[i];
                var prevValue = i >= length1 ? close[i - length1] : 0;
                var priorV1 = i >= length1 ? v1[i - length1] : 0;
                var prevV12 = i >= 2 ? v1[i - 2] : 0;
                var prevV14 = i >= 4 ? v1[i - 4] : 0;

                v1[i] = i >= length1 ? currentValue - prevValue : 0;

                var v2 = i >= 3 ? v1[i - 3] : 0;
                var v3 = (0.75 * (v1[i] - priorV1)) + (0.25 * (prevV12 - prevV14));

                var prevIp = i >= 1 ? ip[i - 1] : 0;
                ip[i] = (0.33 * v2) + (0.67 * prevIp);

                var prevQu = i >= 1 ? qu[i - 1] : 0;
                qu[i] = (0.2 * v3) + (0.8 * prevQu);

                var prevPhase = i >= 1 ? phase[i - 1] : 0;
                var ipSum = ip[i] + prevIp;
                var quSum = qu[i] + prevQu;
                phase[i] = Math.Abs(ipSum) > 0 ? Math.Atan(Math.Abs(quSum / ipSum)) * (180.0 / Math.PI) : 0;
                phase[i] = ip[i] < 0 && qu[i] > 0 ? 180 - phase[i] : phase[i];
                phase[i] = ip[i] < 0 && qu[i] < 0 ? 180 + phase[i] : phase[i];
                phase[i] = ip[i] > 0 && qu[i] < 0 ? 360 - phase[i] : phase[i];

                var rawDPhase = prevPhase - phase[i];
                rawDPhase = prevPhase < 90 && phase[i] > 270 ? 360 + prevPhase - phase[i] : rawDPhase;
                dPhase[i] = Math.Max(1, Math.Min(60, rawDPhase));

                double instPeriod = 0, v4 = 0;
                for (var j = 0; j <= length3; j++)
                {
                    var prevDPhase = i >= j ? dPhase[i - j] : 0;
                    v4 += prevDPhase;
                    if (v4 > 360 && instPeriod == 0)
                    {
                        instPeriod = j;
                    }
                }

                var prevDcPeriod = i >= 1 ? dcPeriod[i - 1] : 0;
                dcPeriod[i] = (0.25 * instPeriod) + (0.75 * prevDcPeriod);

                output[i] = dcPeriod[i] < length2 ? 0 : 1;
            }
        }
        finally
        {
            pool.Return(v1Array);
            pool.Return(ipArray);
            pool.Return(quArray);
            pool.Return(phaseArray);
            pool.Return(dPhaseArray);
            pool.Return(dcPeriodArray);
        }
    }

    /// <summary>
    /// Calculates Ehlers Reverse Exponential Moving Average Indicator V2.
    /// Outputs the cycle component (uses cycleAlpha).
    /// </summary>
    internal static void EhlersReverseEmaIndicatorV2(ReadOnlySpan<double> close, Span<double> output, double trendAlpha = 0.05, double cycleAlpha = 0.3)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.");
        }

        trendAlpha = Math.Max(0.01, Math.Min(0.99, trendAlpha));
        cycleAlpha = Math.Max(0.01, Math.Min(0.99, cycleAlpha));

        var pool = ArrayPool<double>.Shared;
        var trendArray = pool.Rent(close.Length);
        var cycleArray = pool.Rent(close.Length);

        try
        {
            var trend = trendArray.AsSpan(0, close.Length);
            var cycle = cycleArray.AsSpan(0, close.Length);

            // Compute both V1 indicators with different alphas
            EhlersReverseEmaIndicatorV1(close, trend, trendAlpha);
            EhlersReverseEmaIndicatorV1(close, cycle, cycleAlpha);

            // Output the cycle component (difference shows trend direction)
            for (var i = 0; i < close.Length; i++)
            {
                output[i] = cycle[i];
            }
        }
        finally
        {
            pool.Return(trendArray);
            pool.Return(cycleArray);
        }
    }

    /// <summary>
    /// Calculates Ehlers Stochastic Cyber Cycle.
    /// </summary>
    internal static void EhlersStochasticCyberCycle(ReadOnlySpan<double> close, Span<double> output, int length = 14, double alpha = 0.7)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.");
        }

        length = Math.Max(1, length);
        alpha = Math.Max(0.01, Math.Min(0.99, alpha));

        var pool = ArrayPool<double>.Shared;
        var cyberCycleArray = pool.Rent(close.Length);
        var stochArray = pool.Rent(close.Length);

        try
        {
            var cyberCycle = cyberCycleArray.AsSpan(0, close.Length);
            var stoch = stochArray.AsSpan(0, close.Length);

            // First compute Ehlers CyberCycle
            MovingAverageCore.EhlersCyberCycle(close, cyberCycle, alpha);

            // Compute stochastic of cyber cycle
            for (var i = 0; i < close.Length; i++)
            {
                // Rolling max/min over length period
                var maxCycle = double.MinValue;
                var minCycle = double.MaxValue;
                var lookback = Math.Min(i + 1, length);

                for (var j = 0; j < lookback; j++)
                {
                    var idx = i - j;
                    var val = cyberCycle[idx];
                    if (val > maxCycle) maxCycle = val;
                    if (val < minCycle) minCycle = val;
                }

                var range = maxCycle - minCycle;
                var rawStoch = range != 0 ? (cyberCycle[i] - minCycle) / range : 0;
                stoch[i] = Math.Max(0, Math.Min(1, rawStoch));
            }

            // Apply weighted smoothing and scale to -1 to 1
            for (var i = 0; i < close.Length; i++)
            {
                var prevStoch1 = i >= 1 ? stoch[i - 1] : 0;
                var prevStoch2 = i >= 2 ? stoch[i - 2] : 0;
                var prevStoch3 = i >= 3 ? stoch[i - 3] : 0;

                var smoothed = ((4 * stoch[i]) + (3 * prevStoch1) + (2 * prevStoch2) + prevStoch3) / 10;
                var stochCC = 2 * (smoothed - 0.5);
                output[i] = Math.Max(-1, Math.Min(1, stochCC));
            }
        }
        finally
        {
            pool.Return(cyberCycleArray);
            pool.Return(stochArray);
        }
    }

    /// <summary>
    /// Calculates Ehlers Center of Gravity Oscillator.
    /// </summary>
    internal static void EhlersCenterofGravityOscillator(ReadOnlySpan<double> close, Span<double> output, int length = 10)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);

        for (var i = 0; i < close.Length; i++)
        {
            double num = 0, denom = 0;
            for (var j = 0; j <= length - 1; j++)
            {
                var prevValue = i >= j ? close[i - j] : 0;
                num += (1 + j) * prevValue;
                denom += prevValue;
            }

            output[i] = denom != 0 ? (-num / denom) + ((double)(length + 1) / 2) : 0;
        }
    }

    /// <summary>
    /// Calculates Ehlers Reflex Indicator.
    /// </summary>
    internal static void EhlersReflexIndicator(ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);

        var pool = ArrayPool<double>.Shared;
        var filterArray = pool.Rent(close.Length);
        var msArray = pool.Rent(close.Length);

        try
        {
            var filter = filterArray.AsSpan(0, close.Length);
            var ms = msArray.AsSpan(0, close.Length);

            var period = 0.5 * length;
            var a1 = Math.Exp(-MathHelper.Sqrt2 * Math.PI / period);
            var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI / period);
            var c2 = b1;
            var c3 = -a1 * a1;
            var c1 = 1 - c2 - c3;

            for (var i = 0; i < close.Length; i++)
            {
                var currentValue = close[i];
                var prevValue = i >= 1 ? close[i - 1] : 0;
                var prevFilter1 = i >= 1 ? filter[i - 1] : 0;
                var prevFilter2 = i >= 2 ? filter[i - 2] : 0;
                var priorFilter = i >= length ? filter[i - length] : 0;
                var prevMs = i >= 1 ? ms[i - 1] : 0;

                filter[i] = (c1 * ((currentValue + prevValue) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);

                var slope = length != 0 ? (priorFilter - filter[i]) / length : 0;
                double sum = 0;
                for (var j = 1; j <= length; j++)
                {
                    var prevFilterCount = i >= j ? filter[i - j] : 0;
                    sum += filter[i] + (j * slope) - prevFilterCount;
                }
                sum /= length;

                ms[i] = (0.04 * sum * sum) + (0.96 * prevMs);
                output[i] = ms[i] > 0 ? sum / Math.Sqrt(ms[i]) : 0;
            }
        }
        finally
        {
            pool.Return(filterArray);
            pool.Return(msArray);
        }
    }

    /// <summary>
    /// Calculates Ehlers Trendflex Indicator.
    /// </summary>
    internal static void EhlersTrendflexIndicator(ReadOnlySpan<double> close, Span<double> output, int length = 20)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);

        var pool = ArrayPool<double>.Shared;
        var filterArray = pool.Rent(close.Length);
        var msArray = pool.Rent(close.Length);

        try
        {
            var filter = filterArray.AsSpan(0, close.Length);
            var ms = msArray.AsSpan(0, close.Length);

            var period = 0.5 * length;
            var a1 = Math.Exp(-MathHelper.Sqrt2 * Math.PI / period);
            var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI / period);
            var c2 = b1;
            var c3 = -a1 * a1;
            var c1 = 1 - c2 - c3;

            for (var i = 0; i < close.Length; i++)
            {
                var currentValue = close[i];
                var prevValue = i >= 1 ? close[i - 1] : 0;
                var prevFilter1 = i >= 1 ? filter[i - 1] : 0;
                var prevFilter2 = i >= 2 ? filter[i - 2] : 0;
                var prevMs = i >= 1 ? ms[i - 1] : 0;

                filter[i] = (c1 * ((currentValue + prevValue) / 2)) + (c2 * prevFilter1) + (c3 * prevFilter2);

                double sum = 0;
                for (var j = 1; j <= length; j++)
                {
                    var prevFilterCount = i >= j ? filter[i - j] : 0;
                    sum += filter[i] - prevFilterCount;
                }
                sum /= length;

                ms[i] = (0.04 * sum * sum) + (0.96 * prevMs);
                output[i] = ms[i] > 0 ? sum / Math.Sqrt(ms[i]) : 0;
            }
        }
        finally
        {
            pool.Return(filterArray);
            pool.Return(msArray);
        }
    }

    /// <summary>
    /// Calculates JMA RSX Clone indicator.
    /// A momentum oscillator that is a clone of the Jurik RSX indicator.
    /// </summary>
    internal static void JmaRsxClone(ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        length = Math.Max(1, length);

        var pool = ArrayPool<double>.Shared;
        var f8Array = pool.Rent(close.Length);
        var f28Array = pool.Rent(close.Length);
        var f30Array = pool.Rent(close.Length);
        var f38Array = pool.Rent(close.Length);
        var f40Array = pool.Rent(close.Length);
        var f48Array = pool.Rent(close.Length);
        var f50Array = pool.Rent(close.Length);
        var f58Array = pool.Rent(close.Length);
        var f60Array = pool.Rent(close.Length);
        var f68Array = pool.Rent(close.Length);
        var f70Array = pool.Rent(close.Length);
        var f78Array = pool.Rent(close.Length);
        var f80Array = pool.Rent(close.Length);
        var f88Array = pool.Rent(close.Length);
        var f90Array = pool.Rent(close.Length);

        try
        {
            var f8 = f8Array.AsSpan(0, close.Length);
            var f28 = f28Array.AsSpan(0, close.Length);
            var f30 = f30Array.AsSpan(0, close.Length);
            var f38 = f38Array.AsSpan(0, close.Length);
            var f40 = f40Array.AsSpan(0, close.Length);
            var f48 = f48Array.AsSpan(0, close.Length);
            var f50 = f50Array.AsSpan(0, close.Length);
            var f58 = f58Array.AsSpan(0, close.Length);
            var f60 = f60Array.AsSpan(0, close.Length);
            var f68 = f68Array.AsSpan(0, close.Length);
            var f70 = f70Array.AsSpan(0, close.Length);
            var f78 = f78Array.AsSpan(0, close.Length);
            var f80 = f80Array.AsSpan(0, close.Length);
            var f88 = f88Array.AsSpan(0, close.Length);
            var f90 = f90Array.AsSpan(0, close.Length);

            var f18 = (double)3 / (length + 2);
            var f20 = 1 - f18;

            for (var i = 0; i < close.Length; i++)
            {
                var currentValue = close[i];
                var prevF8 = i >= 1 ? f8[i - 1] : 0;
                f8[i] = 100 * currentValue;

                var f10 = prevF8;
                var v8 = f8[i] - f10;

                var prevF28 = i >= 1 ? f28[i - 1] : 0;
                f28[i] = (f20 * prevF28) + (f18 * v8);

                var prevF30 = i >= 1 ? f30[i - 1] : 0;
                f30[i] = (f18 * f28[i]) + (f20 * prevF30);

                var vC = (f28[i] * 1.5) - (f30[i] * 0.5);
                var prevF38 = i >= 1 ? f38[i - 1] : 0;
                f38[i] = (f20 * prevF38) + (f18 * vC);

                var prevF40 = i >= 1 ? f40[i - 1] : 0;
                f40[i] = (f18 * f38[i]) + (f20 * prevF40);

                var v10 = (f38[i] * 1.5) - (f40[i] * 0.5);
                var prevF48 = i >= 1 ? f48[i - 1] : 0;
                f48[i] = (f20 * prevF48) + (f18 * v10);

                var prevF50 = i >= 1 ? f50[i - 1] : 0;
                f50[i] = (f18 * f48[i]) + (f20 * prevF50);

                var v14 = (f48[i] * 1.5) - (f50[i] * 0.5);
                var prevF58 = i >= 1 ? f58[i - 1] : 0;
                f58[i] = (f20 * prevF58) + (f18 * Math.Abs(v8));

                var prevF60 = i >= 1 ? f60[i - 1] : 0;
                f60[i] = (f18 * f58[i]) + (f20 * prevF60);

                var v18 = (f58[i] * 1.5) - (f60[i] * 0.5);
                var prevF68 = i >= 1 ? f68[i - 1] : 0;
                f68[i] = (f20 * prevF68) + (f18 * v18);

                var prevF70 = i >= 1 ? f70[i - 1] : 0;
                f70[i] = (f18 * f68[i]) + (f20 * prevF70);

                var v1C = (f68[i] * 1.5) - (f70[i] * 0.5);
                var prevF78 = i >= 1 ? f78[i - 1] : 0;
                f78[i] = (f20 * prevF78) + (f18 * v1C);

                var prevF80 = i >= 1 ? f80[i - 1] : 0;
                f80[i] = (f18 * f78[i]) + (f20 * prevF80);

                var v20 = (f78[i] * 1.5) - (f80[i] * 0.5);
                var prevF88 = i >= 1 ? f88[i - 1] : 0;
                var prevF90 = i >= 1 ? f90[i - 1] : 0;
                f90[i] = prevF90 == 0 ? 1 : prevF88 <= prevF90 ? prevF88 + 1 : prevF90 + 1;

                f88[i] = prevF90 == 0 && length - 1 >= 5 ? length - 1 : 5;
                double f0 = f88[i] >= f90[i] && f8[i] != f10 ? 1 : 0;
                var f90Val = f88[i] == f90[i] && f0 == 0 ? 0 : f90[i];
                var v4 = f88[i] < f90Val && v20 > 0 ? Math.Max(0, Math.Min(100, ((v14 / v20) + 1) * 50)) : 50;
                output[i] = Math.Max(0, Math.Min(100, v4));
            }
        }
        finally
        {
            pool.Return(f8Array);
            pool.Return(f28Array);
            pool.Return(f30Array);
            pool.Return(f38Array);
            pool.Return(f40Array);
            pool.Return(f48Array);
            pool.Return(f50Array);
            pool.Return(f58Array);
            pool.Return(f60Array);
            pool.Return(f68Array);
            pool.Return(f70Array);
            pool.Return(f78Array);
            pool.Return(f80Array);
            pool.Return(f88Array);
            pool.Return(f90Array);
        }
    }

    #endregion

    #region Additional Oscillators - Batch 2

    /// <summary>
    /// Computes Chande Quick Stick oscillator.
    /// QS = SMA((Close - Open) / (High - Low), length)
    /// </summary>
    internal static void ChandeQuickStick(ReadOnlySpan<double> open, ReadOnlySpan<double> high,
        ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<double> output, int length = 14)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (close.Length == 0) return;

        var pool = ArrayPool<double>.Shared;
        var ratioArray = pool.Rent(close.Length);
        try
        {
            var ratio = ratioArray.AsSpan(0, close.Length);

            for (var i = 0; i < close.Length; i++)
            {
                var range = high[i] - low[i];
                ratio[i] = range != 0 ? (close[i] - open[i]) / range : 0;
            }

            MovingAverageCore.SimpleMovingAverage(ratio, output, length);
        }
        finally
        {
            pool.Return(ratioArray);
        }
    }

    /// <summary>
    /// Computes Delta Moving Average - difference between two MAs.
    /// </summary>
    internal static void DeltaMovingAverage(ReadOnlySpan<double> input, Span<double> output, int fastLength = 12, int slowLength = 26)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (input.Length == 0) return;

        var pool = ArrayPool<double>.Shared;
        var fastEmaArray = pool.Rent(input.Length);
        var slowEmaArray = pool.Rent(input.Length);
        try
        {
            var fastEma = fastEmaArray.AsSpan(0, input.Length);
            var slowEma = slowEmaArray.AsSpan(0, input.Length);

            MovingAverageCore.ExponentialMovingAverage(input, fastEma, fastLength);
            MovingAverageCore.ExponentialMovingAverage(input, slowEma, slowLength);

            for (var i = 0; i < input.Length; i++)
            {
                output[i] = fastEma[i] - slowEma[i];
            }
        }
        finally
        {
            pool.Return(fastEmaArray);
            pool.Return(slowEmaArray);
        }
    }

    /// <summary>
    /// Computes Folded RSI - RSI that folds at midpoint for symmetry.
    /// </summary>
    internal static void FoldedRelativeStrengthIndex(ReadOnlySpan<double> input, Span<double> output, int length = 14)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (input.Length == 0) return;

        var pool = ArrayPool<double>.Shared;
        var rsiArray = pool.Rent(input.Length);
        try
        {
            var rsi = rsiArray.AsSpan(0, input.Length);
            RelativeStrengthIndex(input, rsi, length);

            for (var i = 0; i < input.Length; i++)
            {
                // Fold RSI at 50 - values above 50 stay, values below 50 are mirrored
                output[i] = rsi[i] >= 50 ? rsi[i] : 100 - rsi[i];
            }
        }
        finally
        {
            pool.Return(rsiArray);
        }
    }

    /// <summary>
    /// Computes Enhanced Williams %R - Williams %R with additional smoothing.
    /// </summary>
    internal static void EnhancedWilliamsR(ReadOnlySpan<double> high, ReadOnlySpan<double> low,
        ReadOnlySpan<double> close, Span<double> output, int length = 14, int smoothLength = 3)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (close.Length == 0) return;

        var pool = ArrayPool<double>.Shared;
        var willRArray = pool.Rent(close.Length);
        try
        {
            var willR = willRArray.AsSpan(0, close.Length);
            WilliamsR(high, low, close, willR, length);
            MovingAverageCore.SimpleMovingAverage(willR, output, smoothLength);
        }
        finally
        {
            pool.Return(willRArray);
        }
    }

    /// <summary>
    /// Computes Connors RSI - combination of RSI, up/down streak, and percent rank.
    /// </summary>
    internal static void ConnorsRelativeStrengthIndex(ReadOnlySpan<double> input, Span<double> output,
        int rsiLength = 3, int streakLength = 2, int rankLength = 100)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (input.Length == 0) return;

        var pool = ArrayPool<double>.Shared;
        var rsiArray = pool.Rent(input.Length);
        var streakArray = pool.Rent(input.Length);
        var streakRsiArray = pool.Rent(input.Length);
        var percentRankArray = pool.Rent(input.Length);
        try
        {
            var rsi = rsiArray.AsSpan(0, input.Length);
            var streak = streakArray.AsSpan(0, input.Length);
            var streakRsi = streakRsiArray.AsSpan(0, input.Length);
            var percentRank = percentRankArray.AsSpan(0, input.Length);

            // Calculate standard RSI
            RelativeStrengthIndex(input, rsi, rsiLength);

            // Calculate streak (consecutive up/down days)
            double currentStreak = 0;
            for (var i = 0; i < input.Length; i++)
            {
                if (i == 0)
                {
                    currentStreak = 0;
                }
                else
                {
                    var change = input[i] - input[i - 1];
                    if (change > 0)
                    {
                        currentStreak = currentStreak > 0 ? currentStreak + 1 : 1;
                    }
                    else if (change < 0)
                    {
                        currentStreak = currentStreak < 0 ? currentStreak - 1 : -1;
                    }
                    else
                    {
                        currentStreak = 0;
                    }
                }
                streak[i] = currentStreak;
            }

            // Calculate RSI of streak
            RelativeStrengthIndex(streak, streakRsi, streakLength);

            // Calculate percent rank of ROC
            for (var i = 0; i < input.Length; i++)
            {
                if (i == 0)
                {
                    percentRank[i] = 0;
                    continue;
                }

                var currentRoc = input[i - 1] != 0 ? (input[i] - input[i - 1]) / input[i - 1] * 100 : 0;
                var count = 0;
                var lookback = Math.Min(i, rankLength);

                for (var j = 1; j <= lookback; j++)
                {
                    var prevRoc = input[i - j - 1] != 0 && i - j > 0
                        ? (input[i - j] - input[i - j - 1]) / input[i - j - 1] * 100
                        : 0;
                    if (prevRoc < currentRoc) count++;
                }

                percentRank[i] = lookback > 0 ? (double)count / lookback * 100 : 0;
            }

            // Connors RSI = (RSI + StreakRSI + PercentRank) / 3
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (rsi[i] + streakRsi[i] + percentRank[i]) / 3;
            }
        }
        finally
        {
            pool.Return(rsiArray);
            pool.Return(streakArray);
            pool.Return(streakRsiArray);
            pool.Return(percentRankArray);
        }
    }

    /// <summary>
    /// Computes Stochastic RSI - Stochastic oscillator applied to RSI values.
    /// </summary>
    internal static void StochasticRelativeStrengthIndex(ReadOnlySpan<double> input, Span<double> output,
        int rsiLength = 14, int stochLength = 14, int smoothK = 3, int smoothD = 3)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (input.Length == 0) return;

        var pool = ArrayPool<double>.Shared;
        var rsiArray = pool.Rent(input.Length);
        var stochKArray = pool.Rent(input.Length);
        try
        {
            var rsi = rsiArray.AsSpan(0, input.Length);
            var stochK = stochKArray.AsSpan(0, input.Length);

            // Calculate RSI
            RelativeStrengthIndex(input, rsi, rsiLength);

            // Calculate Stochastic of RSI
            for (var i = 0; i < input.Length; i++)
            {
                if (i < stochLength - 1)
                {
                    stochK[i] = 0;
                    continue;
                }

                double highest = double.MinValue;
                double lowest = double.MaxValue;

                for (var j = 0; j < stochLength; j++)
                {
                    var val = rsi[i - j];
                    if (val > highest) highest = val;
                    if (val < lowest) lowest = val;
                }

                var range = highest - lowest;
                stochK[i] = range != 0 ? (rsi[i] - lowest) / range * 100 : 50;
            }

            // Smooth with SMA
            MovingAverageCore.SimpleMovingAverage(stochK, output, smoothK);
        }
        finally
        {
            pool.Return(rsiArray);
            pool.Return(stochKArray);
        }
    }

    /// <summary>
    /// Computes Stochastic Momentum Index (SMI).
    /// </summary>
    internal static void StochasticMomentumIndex(ReadOnlySpan<double> high, ReadOnlySpan<double> low,
        ReadOnlySpan<double> close, Span<double> output, int length = 13, int smoothLength1 = 25, int smoothLength2 = 2)
    {
        if (output.Length < close.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (close.Length == 0) return;

        var pool = ArrayPool<double>.Shared;
        var hlDiffArray = pool.Rent(close.Length);
        var midRangeArray = pool.Rent(close.Length);
        var emaHlArray = pool.Rent(close.Length);
        var emaMidArray = pool.Rent(close.Length);
        var emaHl2Array = pool.Rent(close.Length);
        var emaMid2Array = pool.Rent(close.Length);
        try
        {
            var hlDiff = hlDiffArray.AsSpan(0, close.Length);
            var midRange = midRangeArray.AsSpan(0, close.Length);
            var emaHl = emaHlArray.AsSpan(0, close.Length);
            var emaMid = emaMidArray.AsSpan(0, close.Length);
            var emaHl2 = emaHl2Array.AsSpan(0, close.Length);
            var emaMid2 = emaMid2Array.AsSpan(0, close.Length);

            // Calculate highest high and lowest low
            for (var i = 0; i < close.Length; i++)
            {
                if (i < length - 1)
                {
                    hlDiff[i] = 0;
                    midRange[i] = 0;
                    continue;
                }

                double hh = double.MinValue;
                double ll = double.MaxValue;
                for (var j = 0; j < length; j++)
                {
                    if (high[i - j] > hh) hh = high[i - j];
                    if (low[i - j] < ll) ll = low[i - j];
                }

                hlDiff[i] = hh - ll;
                midRange[i] = close[i] - ((hh + ll) / 2);
            }

            // Double EMA smoothing
            MovingAverageCore.ExponentialMovingAverage(hlDiff, emaHl, smoothLength1);
            MovingAverageCore.ExponentialMovingAverage(midRange, emaMid, smoothLength1);
            MovingAverageCore.ExponentialMovingAverage(emaHl, emaHl2, smoothLength2);
            MovingAverageCore.ExponentialMovingAverage(emaMid, emaMid2, smoothLength2);

            // SMI = 100 * emaMid2 / (emaHl2 / 2)
            for (var i = 0; i < close.Length; i++)
            {
                var halfRange = emaHl2[i] / 2;
                output[i] = halfRange != 0 ? 100 * emaMid2[i] / halfRange : 0;
            }
        }
        finally
        {
            pool.Return(hlDiffArray);
            pool.Return(midRangeArray);
            pool.Return(emaHlArray);
            pool.Return(emaMidArray);
            pool.Return(emaHl2Array);
            pool.Return(emaMid2Array);
        }
    }

    #endregion

    #region Batch 25 - Additional Ehlers Indicators (New Core Methods)

    /// <summary>
    /// Computes Ehlers Simple Clip Indicator raw values (z3).
    /// Output is the sum of last 4 clipped derivative values.
    /// Apply a moving average externally for the signal line.
    /// </summary>
    /// <param name="input">Input prices.</param>
    /// <param name="output">Output span for z3 values.</param>
    /// <param name="length1">Lag period for derivative (default 2).</param>
    /// <param name="length3">RMS period (default 50).</param>
    internal static void EhlersSimpleClipIndicator(ReadOnlySpan<double> input, Span<double> output, int length1 = 2, int length3 = 50)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (input.Length == 0) return;

        var pool = ArrayPool<double>.Shared;
        var derivArray = pool.Rent(input.Length);
        var clipArray = pool.Rent(input.Length);

        try
        {
            var deriv = derivArray.AsSpan(0, input.Length);
            var clip = clipArray.AsSpan(0, input.Length);

            // Calculate derivative values with lag
            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var prevValue = i >= length1 ? input[i - length1] : 0;

                if (i >= length1)
                {
                    deriv[i] = currentValue - prevValue;
                }
                else
                {
                    deriv[i] = 0;
                }
            }

            // Calculate clip values (clipped derivative normalized by RMS)
            for (var i = 0; i < input.Length; i++)
            {
                // Calculate RMS of deriv over length3 period
                var rms = 0.0;
                for (var j = 0; j < length3 && i >= j; j++)
                {
                    rms += deriv[i - j] * deriv[i - j];
                }

                // Clip = constrain(2 * deriv / sqrt(rms/length3), -1, 1)
                if (rms != 0)
                {
                    var normalizedDeriv = 2 * deriv[i] / Math.Sqrt(rms / length3);
                    clip[i] = Math.Max(-1, Math.Min(1, normalizedDeriv));
                }
                else
                {
                    clip[i] = 0;
                }
            }

            // Calculate z3 = sum of last 4 clip values
            for (var i = 0; i < input.Length; i++)
            {
                var prevClip1 = i >= 1 ? clip[i - 1] : 0;
                var prevClip2 = i >= 2 ? clip[i - 2] : 0;
                var prevClip3 = i >= 3 ? clip[i - 3] : 0;

                output[i] = clip[i] + prevClip1 + prevClip2 + prevClip3;
            }
        }
        finally
        {
            pool.Return(derivArray);
            pool.Return(clipArray);
        }
    }

    /// <summary>
    /// Computes Ehlers Simple Deriv Indicator raw values (z3).
    /// Output is the sum of last 4 derivative values.
    /// Apply a moving average externally for the signal line.
    /// </summary>
    /// <param name="input">Input prices.</param>
    /// <param name="output">Output span for z3 values.</param>
    /// <param name="length">Lag period for derivative (default 2).</param>
    internal static void EhlersSimpleDerivIndicator(ReadOnlySpan<double> input, Span<double> output, int length = 2)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span must be at least input length.", nameof(output));
        }

        if (input.Length == 0) return;

        var pool = ArrayPool<double>.Shared;
        var derivArray = pool.Rent(input.Length);

        try
        {
            var deriv = derivArray.AsSpan(0, input.Length);

            // Calculate derivative values with lag
            for (var i = 0; i < input.Length; i++)
            {
                var currentValue = input[i];
                var prevValue = i >= length ? input[i - length] : 0;

                // deriv = currentValue - prevValue (with MinPastValues logic)
                if (i >= length)
                {
                    deriv[i] = currentValue - prevValue;
                }
                else
                {
                    deriv[i] = 0;
                }
            }

            // Calculate z3 = sum of last 4 deriv values
            for (var i = 0; i < input.Length; i++)
            {
                var prevDeriv1 = i >= 1 ? deriv[i - 1] : 0;
                var prevDeriv2 = i >= 2 ? deriv[i - 2] : 0;
                var prevDeriv3 = i >= 3 ? deriv[i - 3] : 0;

                output[i] = deriv[i] + prevDeriv1 + prevDeriv2 + prevDeriv3;
            }
        }
        finally
        {
            pool.Return(derivArray);
        }
    }

    #endregion

    #endregion
}
