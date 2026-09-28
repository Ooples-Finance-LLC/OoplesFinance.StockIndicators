namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class GeneralFilterWindow
{
    private readonly int _period; private readonly double _gamma, _zeta;
    private readonly Queue<(RocBankValue B, RocBankValue D)> _history = new();
    private RocBankValue _b, _d; private bool _seeded;
    internal GeneralFilterWindow(int length, double beta, double gamma, double zeta)
    {
        var period = Math.Ceiling(Math.Max(1, length) / beta);
        if (double.IsNaN(beta) || double.IsInfinity(beta) || beta <= 0 || period > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(beta));
        if (double.IsNaN(gamma) || double.IsInfinity(gamma)) throw new ArgumentOutOfRangeException(nameof(gamma));
        if (double.IsNaN(zeta) || double.IsInfinity(zeta)) throw new ArgumentOutOfRangeException(nameof(zeta));
        _period = Math.Max(1, (int)period); _gamma = gamma; _zeta = zeta;
    }
    private static RocBankValue Add(RocBankValue a, RocBankValue b, int sign = 1)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    private RocBankValue Step(RocBankValue value)
    { var sum = new ExactMeanAccumulator(); value.AddTo(ref sum); return RocBankValue.Round(sum, count: _period).Multiply(_gamma); }
    internal double Next(double price, bool commit)
    {
        var current = new RocBankValue(price); var prior = _history.Count == _period ? _history.Peek() : (current, current);
        var a = Add(current, prior.Item1, -1);
        var b = Add(_seeded ? _b : current, Step(a));
        var c = Add(b, _history.Count == _period ? prior.Item2 : b, -1);
        var d = Add(_seeded ? _d : current, Step(Add(a.Multiply(_zeta), c.Multiply(1 - _zeta))));
        if (commit) { if (_history.Count == _period) _history.Dequeue(); _history.Enqueue((b, d)); _b = b; _d = d; _seeded = true; }
        return d.Publish();
    }
    internal void Reset() { _history.Clear(); _b = _d = default; _seeded = false; }
}
