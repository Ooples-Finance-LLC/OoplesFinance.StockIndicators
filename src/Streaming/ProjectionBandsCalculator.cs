using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

internal readonly struct ProjectionBandsSnapshot
{
    internal BigInteger UpperUnits { get; }
    internal BigInteger LowerUnits { get; }
    internal BigInteger MiddleUnits { get; }
    internal ProjectionBandsSnapshot(BigInteger upper, BigInteger lower)
    { UpperUnits = upper; LowerUnits = lower; MiddleUnits = Round(upper + lower, 2); }
    internal static BigInteger Round(BigInteger numerator, BigInteger denominator) => denominator.IsZero ? BigInteger.Zero
        : RocBankValue.RoundUnits(denominator.Sign < 0 ? -numerator : numerator, BigInteger.Abs(denominator));
    internal static double Publish(BigInteger value) => ExactMeanAccumulator.UnitRatio(value, BigInteger.One);
    public double Upper => Publish(UpperUnits);
    public double Middle => Publish(MiddleUnits);
    public double Lower => Publish(LowerUnits);
    internal BigInteger Oscillator(double value) => Round((100 * (ExactVarianceWindow.Units(value) - LowerUnits)) << 1074, UpperUnits - LowerUnits);
    internal BigInteger Bandwidth => Round((200 * (UpperUnits - LowerUnits)) << 1074, UpperUnits + LowerUnits);
}

internal sealed class ProjectionBandsCalculator : IDisposable
{
    private readonly int _length;
    private readonly Queue<(BigInteger High, BigInteger Low, BigInteger HighSlope, BigInteger LowSlope)> _history = new();
    private BigInteger _highSum, _lowSum, _highWeighted, _lowWeighted;
    internal ProjectionBandsCalculator(int length) => _length = Math.Max(1, length);
    private static (BigInteger Sum, BigInteger Weighted, BigInteger Slope) Fit(BigInteger value, BigInteger sum,
        BigInteger weighted, BigInteger expired, int count, bool full)
    {
        var nextSum = sum + value - expired;
        var nextWeighted = full ? weighted - sum + expired + (count - 1) * value : weighted + (count - 1) * value;
        var n = new BigInteger(count);
        var slope = count == 1 ? BigInteger.Zero : ProjectionBandsSnapshot.Round(6 * (2 * nextWeighted - (n - 1) * nextSum), n * (n * n - 1));
        return (nextSum, nextWeighted, slope);
    }
    internal ProjectionBandsSnapshot Update(double high, double low, bool final)
    {
        StreamingInputValidation.Finite(high, nameof(high)); StreamingInputValidation.Finite(low, nameof(low));
        var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low);
        var history = _history.ToArray(); var upper = h; var lower = l;
        // Preserve the original lag pairing: price at i-j+1, slope at i-j.
        var lastLag = Math.Min((long)_length, history.Length + 1L);
        for (var lag = 1L; lag <= lastLag; lag++)
        {
            var priceIndex = history.Length - (int)lag + 1;
            var slopeIndex = history.Length - (int)lag;
            var oldHigh = lag == 1 ? h : history[priceIndex].High;
            var oldLow = lag == 1 ? l : history[priceIndex].Low;
            var highSlope = slopeIndex < 0 ? BigInteger.Zero : history[slopeIndex].HighSlope;
            var lowSlope = slopeIndex < 0 ? BigInteger.Zero : history[slopeIndex].LowSlope;
            var projectedHigh = ProjectionBandsSnapshot.Round(oldHigh + ProjectionBandsSnapshot.Round(highSlope * lag, BigInteger.One), BigInteger.One);
            var projectedLow = ProjectionBandsSnapshot.Round(oldLow + ProjectionBandsSnapshot.Round(lowSlope * lag, BigInteger.One), BigInteger.One);
            upper = BigInteger.Max(upper, projectedHigh); lower = BigInteger.Min(lower, projectedLow);
        }
        if (_length > history.Length + 1L) { upper = BigInteger.Max(upper, BigInteger.Zero); lower = BigInteger.Min(lower, BigInteger.Zero); }
        var result = new ProjectionBandsSnapshot(upper, lower);
        if (final)
        {
            var full = _history.Count == _length; var expired = full ? _history.Peek() : default;
            var count = full ? _length : _history.Count + 1;
            var highFit = Fit(h, _highSum, _highWeighted, expired.High, count, full);
            var lowFit = Fit(l, _lowSum, _lowWeighted, expired.Low, count, full);
            if (full) _history.Dequeue();
            _history.Enqueue((h,l,highFit.Slope,lowFit.Slope));
            _highSum = highFit.Sum; _highWeighted = highFit.Weighted;
            _lowSum = lowFit.Sum; _lowWeighted = lowFit.Weighted;
        }
        return result;
    }
    internal void Reset() { _history.Clear(); _highSum = _lowSum = _highWeighted = _lowWeighted = default; }
    public void Dispose() => _history.Clear();
}
