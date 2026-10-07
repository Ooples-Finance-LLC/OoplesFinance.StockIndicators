using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ImpulseResponseWindow : IDisposable
{
    private readonly double _drive, _feedback, _decay;
    private readonly double[]? _weights;
    private readonly ExactMeanAccumulator _mass;
    private readonly List<RocBankValue> _history = new();
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    private RocBankValue _first, _second;
    private double _previous, _older;
    private int _startup;
    internal RocBankValue ExtendedOutput { get; private set; }
    internal static bool Supports(MovingAvgType kind) => HannIndicatorWindow.Supports(kind);
    internal ImpulseResponseWindow(MovingAvgType kind, int length, double bandwidth)
    {
        HighLowBandsWindow.ValidateShift(bandwidth); length = Math.Max(1, length);
        var period = (int)Math.Max(2, Math.Min(530, Math.Ceiling(length / 1.4)));
        var cosine = Math.Cos(Math.Max(.01, Math.Min(.99, bandwidth * 2 * Math.PI / length)));
        _decay = 1 / cosine - Math.Sqrt(1 / (cosine * cosine) - 1); _drive = .5 * (1 - _decay);
        _feedback = Math.Cos(Math.Max(.01, Math.Min(.99, 2 * Math.PI / length))) * (1 + _decay);
        if (kind == MovingAvgType.EhlersHannMovingAverage)
        {
            _weights = new double[period]; var mass = new ExactMeanAccumulator();
            for (var lag = 0; lag < period; lag++) { _weights[lag] = 1 - Math.Cos(2 * Math.PI * ((lag + 1d) / (period + 1d))); mass.Add(_weights[lag]); }
            _mass = mass;
        }
        else if (StrengthWindow.Supports(kind)) _average = new(kind, period, period);
        else _fallback = MovingAverageSmootherFactory.Create(kind, period);
    }
    internal double Next(double value, bool commit, bool captureExtended = false)
    {
        RocBankValue band = default;
        if (_startup >= 3)
        {
            var delta = new ExactMeanAccumulator(); delta.Add(value); delta.Add(_older, -1);
            var sum = new ExactMeanAccumulator(); RocBankValue.Round(delta).Multiply(_drive).AddTo(ref sum); _first.Multiply(_feedback).AddTo(ref sum);
            var difference = new ExactMeanAccumulator(); RocBankValue.Round(sum).AddTo(ref difference); _second.Multiply(_decay).AddTo(ref difference, -1);
            band = RocBankValue.Round(difference);
        }
        double output;
        if (_weights is not null)
        {
            var sum = new ExactMeanAccumulator();
            for (var lag = 0; lag < _weights.Length && lag <= _history.Count; lag++)
            {
                var v = lag == 0 ? band : _history[_history.Count - lag];
                var term = new ExactMeanAccumulator(); term.AddProduct(v.Mantissa, _weights[lag], -1); term.ScaleByPowerOfTwo(v.UpperShift); sum.Subtract(term);
            }
            output = sum.Ratio(_mass);
            if (captureExtended)
            {
                for (var shift = 0; ; shift += 1024)
                {
                    var denominator = _mass; denominator.ScaleByPowerOfTwo(shift);
                    var rounded = sum.Ratio(denominator);
                    if (!double.IsInfinity(rounded)) { ExtendedOutput = new(rounded, shift); break; }
                }
            }
            if (commit) { if (_history.Count == _weights.Length) _history.RemoveAt(0); _history.Add(band); }
        }
        else if (_average is not null)
        {
            var average = _average.Next(band, commit); output = average.Publish();
            if (captureExtended) ExtendedOutput = average;
        }
        else
        {
            output = _fallback!.Next(band.Publish(), commit);
            if (captureExtended) ExtendedOutput = new(output);
        }
        if (commit) { _second = _first; _first = band; _older = _previous; _previous = value; _startup = Math.Min(3, _startup + 1); }
        return output;
    }
    internal void Reset() { ExtendedOutput = default; _first = _second = default; _previous = _older = 0; _startup = 0; _history.Clear(); _average?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _average?.Dispose(); _fallback?.Dispose(); _history.Clear(); }
}
