using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ClampedBandPassWindow
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly BigInteger _alpha, _beta, _hpAlpha, _triggerAlpha;
    private readonly int _variant;
    private BigInteger _input1, _input2, _hp1, _hp2, _band1, _band2, _peak, _signal, _trigger;
    private int _count;
    internal ClampedBandPassWindow(int length, double bandwidth, int variant)
    {
        HighLowBandsWindow.ValidateShift(bandwidth); length = Math.Max(1, length); _variant = variant;
        double Clamp(double value) => Math.Max(.01, Math.Min(.99, value));
        var beta = Math.Cos(Clamp(2 * Math.PI / length)); double alpha;
        if (variant == 1) { var cosine = Math.Cos(Clamp(bandwidth * 2 * Math.PI / length)); alpha = 1 / cosine - Math.Sqrt(1 / (cosine * cosine) - 1); }
        else { var gamma = 1 / Math.Cos(Clamp((variant == 2 ? 4 : 2) * Math.PI * bandwidth / length)); alpha = gamma - Math.Sqrt(gamma * gamma - 1); }
        _alpha = ExactVarianceWindow.Units(alpha); _beta = ExactVarianceWindow.Units(beta);
        if (variant == 0)
        {
            var hpAngle = Clamp(.25 * bandwidth * 2 * Math.PI / length); var triggerAngle = Clamp(1.5 * bandwidth * 2 * Math.PI / length);
            _hpAlpha = ExactVarianceWindow.Units((Math.Cos(hpAngle) + Math.Sin(hpAngle) - 1) / Math.Cos(hpAngle)); _triggerAlpha = ExactVarianceWindow.Units((Math.Cos(triggerAngle) + Math.Sin(triggerAngle) - 1) / Math.Cos(triggerAngle));
        }
    }
    private static BigInteger HighPass(BigInteger change, BigInteger previous, BigInteger alpha) => RocBankValue.RoundUnits((2 * Unit + alpha) * change + 2 * (Unit - alpha) * previous, 2 * Unit);
    internal (double Value, double Signal) Next(double close, bool commit)
    {
        var price = ExactVarianceWindow.Units(close); var hp = _variant == 0 ? HighPass(_count == 0 ? BigInteger.Zero : price - _input1, _hp1, _hpAlpha) : price;
        var difference = _variant == 0 ? hp - _hp2 : price - _input2; var start = _variant == 2 ? 2 : 3;
        var band = _count < start ? BigInteger.Zero : RocBankValue.RoundUnits((Unit - _alpha) * Unit * difference + 2 * _beta * (Unit + _alpha) * _band1 - 2 * _alpha * Unit * _band2, BigInteger.One << 2149);
        var peak = _variant == 0 ? BigInteger.Max(RocBankValue.RoundUnits(_peak * ExactVarianceWindow.Units(.991), Unit), BigInteger.Abs(band)) : BigInteger.Zero;
        var signal = _variant == 0 ? peak.IsZero ? 0 : ExactMeanAccumulator.UnitRatio(band << 1074, peak) : ExactMeanAccumulator.UnitRatio(band, BigInteger.One);
        var signalUnits = _variant == 0 ? ExactVarianceWindow.Units(signal) : BigInteger.Zero; var trigger = _variant == 0 ? HighPass(signalUnits - _signal, _trigger, _triggerAlpha) : BigInteger.Zero;
        if (commit) { _input2 = _input1; _input1 = price; _hp2 = _hp1; _hp1 = hp; _band2 = _band1; _band1 = band; _peak = peak; _signal = signalUnits; _trigger = trigger; if (_count < 3) _count++; }
        return (signal, ExactMeanAccumulator.UnitRatio(trigger, BigInteger.One));
    }
    internal void Reset() { _input1 = _input2 = _hp1 = _hp2 = _band1 = _band2 = _peak = _signal = _trigger = default; _count = 0; }
}
