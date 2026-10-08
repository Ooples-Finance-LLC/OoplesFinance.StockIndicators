using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SwissArmyWindow : IDisposable
{
    internal static readonly string[] Keys = { "EmaFilter", "SmaFilter", "GaussFilter", "ButterFilter", "SmoothFilter", "HpFilter", "PhpFilter", "BpFilter", "BsFilter" };
    private static readonly BigInteger Unit = BigInteger.One << 1074, Square = BigInteger.One << 2148;
    private readonly int _length;
    private readonly BigInteger _ema, _gauss, _band, _cosine;
    private readonly Queue<BigInteger> _prices = new();
    private BigInteger[] _previous = new BigInteger[9], _older = new BigInteger[9];
    private BigInteger _input1, _input2;
    private long _count;
    internal SwissArmyWindow(int length, double delta)
    {
        if (double.IsNaN(delta) || double.IsInfinity(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        _length = Math.Max(1, length); var angle = Math.Max(.01, Math.Min(.99, 2 * Math.PI / _length)); var bandwidth = Math.Max(.01, Math.Min(.99, 4 * Math.PI * (delta / _length)));
        var cosine = Math.Cos(angle); var beta = 2.415 * (1 - cosine);
        _ema = ExactVarianceWindow.Units(1 - cosine / (1 + Math.Sin(angle))); _gauss = ExactVarianceWindow.Units(2 * beta / (Math.Sqrt(beta * beta + 2 * beta) + beta));
        _band = ExactVarianceWindow.Units(Math.Cos(bandwidth) / (1 + Math.Sin(bandwidth))); _cosine = ExactVarianceWindow.Units(cosine);
    }
    internal (double[] Values, Signal Signal) Next(double price, bool commit)
    {
        if (double.IsNaN(price) || double.IsInfinity(price)) throw new ArgumentOutOfRangeException(nameof(price));
        var x = ExactVarianceWindow.Units(price); var lag = _prices.Count == _length ? _prices.Peek() : BigInteger.Zero; var values = new BigInteger[9];
        values[4] = RocBankValue.RoundUnits(x + 2 * _input1 + _input2, 4);
        if (_count <= _length)
        {
            values[0] = values[1] = values[2] = values[3] = values[7] = values[8] = x;
        }
        else
        {
            var pole = Unit - _gauss; var gaussianFeedback = 2 * pole * Unit; var gaussianDecay = -pole * pole;
            values[0] = RocBankValue.RoundUnits(_ema * x + (Unit - _ema) * _previous[0], Unit);
            values[1] = RocBankValue.RoundUnits(_previous[1] * _length + x - lag, _length);
            values[2] = RocBankValue.RoundUnits(_gauss * _gauss * x + gaussianFeedback * _previous[2] + gaussianDecay * _older[2], Square);
            values[3] = RocBankValue.RoundUnits(_gauss * _gauss * (x + 2 * _input1 + _input2) + 4 * gaussianFeedback * _previous[3] + 4 * gaussianDecay * _older[3], 4 * Square);
            values[5] = RocBankValue.RoundUnits((2 * Unit - _ema) * (x - _input1) + 2 * (Unit - _ema) * _previous[5], 2 * Unit);
            values[6] = RocBankValue.RoundUnits((2 * Unit - _gauss) * (2 * Unit - _gauss) * (x - 2 * _input1 + _input2) + 4 * gaussianFeedback * _previous[6] + 4 * gaussianDecay * _older[6], 4 * Square);
            values[7] = RocBankValue.RoundUnits((Unit - _band) * Unit * (x - _input2) + 2 * _cosine * (Unit + _band) * _previous[7] - 2 * _band * Unit * _older[7], 2 * Square);
            values[8] = RocBankValue.RoundUnits((Unit + _band) * (Unit * (x + _input2) - 2 * _cosine * _input1) + 2 * _cosine * (Unit + _band) * _previous[8] - 2 * _band * Unit * _older[8], 2 * Square);
        }
        var difference = values[1] - _previous[1]; var before = _previous[1] - _older[1];
        var signal = difference.Sign > 0 && difference > before ? Signal.StrongBuy : difference.Sign < 0 && difference < before ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(x); _input2 = _input1; _input1 = x; _older = _previous; _previous = values; if (_count <= _length) _count++; }
        return (values.Select(v => ExactMeanAccumulator.UnitRatio(v, BigInteger.One)).ToArray(), signal);
    }
    internal void Reset() { _prices.Clear(); Array.Clear(_previous, 0, 9); Array.Clear(_older, 0, 9); _input1 = _input2 = default; _count = 0; }
    public void Dispose() => Reset();
}
