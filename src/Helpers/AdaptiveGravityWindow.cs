namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AdaptiveGravityWindow
{
    private readonly AdaptiveCyberWindow _period;
    private readonly List<double> _prices = new();
    internal AdaptiveGravityWindow(int length) => _period = new(length, .07);
    internal double Next(double price, bool commit)
    {
        var period = _period.Next(price, commit).Period; var window = (int)Math.Ceiling(period / 2); var center = (window + 1) / 2;
        var numerator = new ExactMeanAccumulator(); var denominator = new ExactMeanAccumulator();
        for (var lag = 0; lag < window && lag <= _prices.Count; lag++)
        {
            var value = lag == 0 ? price : _prices[_prices.Count - lag]; denominator.Add(value); numerator.Add(value, center - lag - 1);
        }
        var result = denominator.IsExactlyZero ? 0 : numerator.Ratio(denominator);
        // Clamped phase is at least .1, so dominant <=63.3318 and both positive smoothing stages stay below64.
        // The requested half-period therefore never exceeds32; keep that much past input for future growth.
        if (commit) { if (_prices.Count == 32) _prices.RemoveAt(0); _prices.Add(price); }
        return result;
    }
    internal void Reset() { _period.Reset(); _prices.Clear(); }
}
