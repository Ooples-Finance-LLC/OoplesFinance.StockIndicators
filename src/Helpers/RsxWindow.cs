using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Repository RSX clone: three paired filters of signed changes and three of
// absolute changes. Each complete unpublished stage keeps an extended exponent.
internal sealed class RsxWindow
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly BigInteger _gain, _memory;
    private readonly Pair[] _signed = { new(), new(), new() }, _absolute = { new(), new(), new() };
    private BigInteger _previousScaled;
    private int _warmup;
    private double _previous, _before;
    internal RsxWindow(int length)
    {
        var gain = 3d / (Math.Max(1, length) + 2d);
        _gain = ExactVarianceWindow.Units(gain); _memory = ExactVarianceWindow.Units(1 - gain);
    }
    internal (double Value, Signal Trade) Next(double price, bool final)
    {
        var scaled = RocBankValue.RoundUnits(100 * ExactVarianceWindow.Units(price), BigInteger.One);
        var change = RocBankValue.RoundUnits(scaled - _previousScaled, BigInteger.One);
        var signed = change; var absolute = BigInteger.Abs(change);
        for (var stage = 0; stage < 3; stage++)
        {
            signed = _signed[stage].Next(signed, _gain, _memory, final);
            absolute = _absolute[stage].Next(absolute, _gain, _memory, final);
        }
        // The legacy f88/f90 counter reduces to exactly five neutral observations:
        // f90 advances 1..6 then stays at6; f88 is5 after its first observation.
        var value = _warmup < 5 || absolute.Sign <= 0 ? 50
            : signed >= absolute ? 100 : signed <= -absolute ? 0
            : ExactMeanAccumulator.UnitRatio(50 * (signed + absolute) * Unit, absolute);
        var trade = SignalHelper.GetRsiSignal(value - _previous, _previous - _before, value, _previous, 70, 30);
        if (final)
        {
            _previousScaled = scaled; _warmup = Math.Min(5, _warmup + 1);
            _before = _previous; _previous = value;
        }
        return (value, trade);
    }
    internal void Reset()
    {
        foreach (var pair in _signed) pair.Reset(); foreach (var pair in _absolute) pair.Reset();
        _previousScaled = default; _warmup = 0; _previous = _before = 0;
    }
    private sealed class Pair
    {
        private BigInteger _first, _second;
        internal BigInteger Next(BigInteger input, BigInteger gain, BigInteger memory, bool final)
        {
            var first = RocBankValue.RoundUnits(memory * _first + gain * input, Unit);
            var second = RocBankValue.RoundUnits(gain * first + memory * _second, Unit);
            var result = RocBankValue.RoundUnits(3 * first - second, new BigInteger(2));
            if (final) { _first = first; _second = second; }
            return result;
        }
        internal void Reset() => _first = _second = default;
    }
}
