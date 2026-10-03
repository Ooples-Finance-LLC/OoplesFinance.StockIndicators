using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AdaptiveBandPassWindow : IDisposable
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _upper, _firstLag;
    private readonly double _width;
    private readonly AutocorrelationSpectrumWindow _spectrum;
    private readonly EhlersRoofingFilterV2Kernel _roof;
    private BigInteger _roof1, _roof2, _band1, _band2, _peak;
    private double _signal, _trigger;
    private int _count;
    internal AdaptiveBandPassWindow(int upper, int lower, int firstLag, double bw)
    {
        if (double.IsNaN(bw) || double.IsInfinity(bw) || bw < 0) throw new ArgumentOutOfRangeException(nameof(bw));
        _upper = Math.Max(1, upper); _firstLag = Math.Max(1, firstLag); _width = bw; _spectrum = new(_upper, Math.Max(1, lower), _firstLag); _roof = new(_upper, Math.Max(1, lower));
    }
    internal (double Value, double Trigger, Signal Signal) Next(double price, bool commit)
    {
        var cycle = MathHelper.MinOrMax(_spectrum.Next(price, commit).Value, _upper, _firstLag); var period = .9 * cycle; var angle = 2 * Math.PI * (Math.IEEERemainder(_width, period) / period); var cosine = Math.Cos(angle); var alpha = cosine <= 0 ? .01 : Math.Max(.01, Math.Min(.99, cosine / (1 + Math.Abs(Math.Sin(angle)))));
        var decay = ExactVarianceWindow.Units(alpha); var beta = ExactVarianceWindow.Units(Math.Cos(2 * Math.PI / period)); _roof.Next(price, commit, true); var exact = _roof.ExactOutput; var roof = ExactVarianceWindow.Units(exact.Mantissa) << exact.UpperShift;
        var band = _count < 3 ? BigInteger.Zero : RocBankValue.RoundUnits((Unit - decay) * Unit * (roof - _roof2) + 2 * beta * (Unit + decay) * _band1 - 2 * decay * Unit * _band2, BigInteger.One << 2149);
        var peak = BigInteger.Max(RocBankValue.RoundUnits(_peak * ExactVarianceWindow.Units(.991), Unit), BigInteger.Abs(band)); var value = peak.IsZero ? 0 : ExactMeanAccumulator.UnitRatio(band << 1074, peak); var trigger = .9 * _signal;
        var signal = SignalHelper.GetRsiSignal(value - trigger, _signal - _trigger, value, _signal, MathHelper.InverseSqrt2, -MathHelper.InverseSqrt2);
        if (commit) { _roof2 = _roof1; _roof1 = roof; _band2 = _band1; _band1 = band; _peak = peak; _signal = value; _trigger = trigger; if (_count < 3) _count++; } return (value, trigger, signal);
    }
    internal void Reset() { _spectrum.Reset(); _roof.Reset(); _roof1 = _roof2 = _band1 = _band2 = _peak = default; _signal = _trigger = 0; _count = 0; }
    public void Dispose() => Reset();
}
