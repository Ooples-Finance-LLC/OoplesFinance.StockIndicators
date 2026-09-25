using OoplesFinance.StockIndicators.Streaming;
using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class TraderPressureWindow : IDisposable
{
    private readonly RollingWindowMax _high;
    private readonly RollingWindowMin _low;
    private readonly StrengthAverage[]? _exact;
    private readonly IMovingAverageSmoother[]? _fallback;
    private double _previousHigh, _previousLow;
    internal TraderPressureWindow(MovingAvgType kind, int length, int rangeLength, int smoothLength)
    {
        _high = new RollingWindowMax(Math.Max(1, rangeLength)); _low = new RollingWindowMin(Math.Max(1, rangeLength));
        var periods = new[] { length, length, smoothLength };
        if (StrengthWindow.Supports(kind)) _exact = periods.Select(p => new StrengthAverage(kind, p)).ToArray();
        else _fallback = periods.Select(p => MovingAverageSmootherFactory.Create(kind, Math.Max(1, p))).ToArray();
    }
    internal static double Pressure(double high, double low, double previousHigh, double previousLow, double upper, double lower, bool bullish)
    {
        var range = ExactVarianceWindow.Units(upper) - ExactVarianceWindow.Units(lower);
        if (range.IsZero) return 0;
        var highChange = ExactVarianceWindow.Units(high) - ExactVarianceWindow.Units(previousHigh);
        var lowChange = ExactVarianceWindow.Units(low) - ExactVarianceWindow.Units(previousLow);
        var magnitude = bullish ? BigInteger.Max(highChange, BigInteger.Zero) + BigInteger.Max(lowChange, BigInteger.Zero)
            : -BigInteger.Min(highChange, BigInteger.Zero) - BigInteger.Min(lowChange, BigInteger.Zero);
        var numerator = new ExactMeanAccumulator(); numerator.Add(1, magnitude);
        var denominator = new ExactMeanAccumulator(); denominator.Add(1, range);
        return 100 * Math.Min(1, numerator.Ratio(denominator));
    }
    internal (double Net, double Bulls, double Bears) Next(double high, double low, bool commit)
    {
        var upper = commit ? _high.Add(high, out _) : _high.Preview(high, out _);
        var lower = commit ? _low.Add(low, out _) : _low.Preview(low, out _);
        var up = Pressure(high, low, _previousHigh, _previousLow, upper, lower, true);
        var down = Pressure(high, low, _previousHigh, _previousLow, upper, lower, false);
        double Average(int stage, double value) => _exact is null ? _fallback![stage].Next(value, commit)
            : _exact[stage].Next(new StrengthValue(value), commit).Mantissa;
        var bulls = Average(0, up); var bears = Average(1, down); var net = Average(2, bulls - bears);
        if (commit) { _previousHigh = high; _previousLow = low; }
        return (net, bulls, bears);
    }
    internal void Reset() { _high.Reset(); _low.Reset(); _previousHigh = _previousLow = 0; if (_exact is not null) foreach (var stage in _exact) stage.Reset(); if (_fallback is not null) foreach (var stage in _fallback) stage.Reset(); }
    public void Dispose() { _high.Dispose(); _low.Dispose(); if (_exact is not null) foreach (var stage in _exact) stage.Dispose(); if (_fallback is not null) foreach (var stage in _fallback) stage.Dispose(); }
}
