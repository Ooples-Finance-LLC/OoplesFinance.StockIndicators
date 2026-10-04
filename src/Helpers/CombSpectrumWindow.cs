using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
namespace OoplesFinance.StockIndicators.Helpers;

// Each period keeps its present filter and an identical delayed filter. Their
// squared outputs update rolling power without a period-by-period history matrix.
internal sealed class CombSpectrumWindow
{
    private sealed class Band
    {
        internal readonly int Period;
        private readonly double _drive, _feedback, _decay;
        private RocBankValue _first, _second, _delayedFirst, _delayedSecond;
        private BigInteger _squares;
        internal Band(int period, double bandwidth)
        {
            Period = period;
            var angle = 2 * Math.PI * bandwidth / period;
            if (double.IsInfinity(angle))
            {
                var turns = F.Of(bandwidth) / period;
                angle = (turns - new F(turns.Floor(), BigInteger.One)).Publish() * (2 * Math.PI);
            }
            var cosine = Math.Cos(angle);
            var decay = cosine <= 0 ? .01 : cosine / (1 + Math.Sqrt((1 - cosine) * (1 + cosine)));
            _decay = Math.Max(.01, Math.Min(.99, decay));
            _feedback = Math.Cos(2 * Math.PI / period) * (1 + _decay); _drive = .5 * (1 - _decay);
        }
        private static void Product(ref ExactMeanAccumulator sum, RocBankValue value, double factor)
        {
            var product = new ExactMeanAccumulator(); product.AddProduct(value.Mantissa, factor);
            product.ScaleByPowerOfTwo(value.UpperShift); sum.Subtract(Negate(product));
        }
        private static ExactMeanAccumulator Negate(ExactMeanAccumulator value)
        { var zero = new ExactMeanAccumulator(); zero.Subtract(value); return zero; }
        private RocBankValue Filter(RocBankValue current, RocBankValue older, RocBankValue first, RocBankValue second)
        {
            var sum = new ExactMeanAccumulator(); Product(ref sum, current, _drive); Product(ref sum, older, -_drive);
            Product(ref sum, first, _feedback); Product(ref sum, second, -_decay); return RocBankValue.Round(sum);
        }
        private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
        internal F Next(RocBankValue current, RocBankValue older, RocBankValue delayed, RocBankValue delayedOlder, bool advanceDelayed, bool final)
        {
            var added = Units(_first); var removed = Units(_delayedFirst);
            var squares = _squares + added * added - removed * removed;
            if (final)
            {
                var next = Filter(current, older, _first, _second);
                var lagged = advanceDelayed ? Filter(delayed, delayedOlder, _delayedFirst, _delayedSecond) : default;
                _second = _first; _first = next; _delayedSecond = _delayedFirst; _delayedFirst = lagged; _squares = squares;
            }
            return new F(squares, (BigInteger)Period * Period);
        }
        internal void Reset() { _first = _second = _delayedFirst = _delayedSecond = default; _squares = default; }
    }
    private readonly int _upper, _lower;
    private readonly double _bandwidth;
    private readonly EhlersRoofingFilterV2Kernel _roof;
    private readonly Dictionary<long, RocBankValue> _roofHistory = new();
    private List<Band>? _bands;
    private long _index;
    internal CombSpectrumWindow(int upper, int lower, double bandwidth)
    {
        StreamingInputValidation.Finite(bandwidth, nameof(bandwidth));
        if (bandwidth < 0) throw new ArgumentOutOfRangeException(nameof(bandwidth));
        _upper = Math.Max(1, upper); _lower = Math.Max(1, lower); _bandwidth = bandwidth;
        _roof = new(_upper, _lower);
    }
    private RocBankValue At(long index) => _roofHistory.TryGetValue(index, out var value) ? value : default;
    private static F Exact(RocBankValue value) => F.Of(value.Mantissa) * new F(BigInteger.One << value.UpperShift, BigInteger.One);
    internal (double Value, Signal Signal) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        _roof.Next(price, false, true); var current = _roof.ExactOutput;
        if (_bands is null)
        {
            _bands = new();
            for (long period = _lower; period <= _upper; period++) _bands.Add(new((int)period, _bandwidth));
        }
        var powers = _bands.Select(b => b.Next(current, At(_index - 2), At(_index - b.Period), At(_index - b.Period - 2), _index >= b.Period, false)).ToArray();
        var value = Summarize(_bands.Select((band, i) => (band.Period, Power: powers[i])).ToArray());
        var slope = Exact(current) - Exact(At(_index - 1));
        var change = slope - (Exact(At(_index - 1)) - Exact(At(_index - 2)));
        var signal = slope.Sign > 0 ? change.Sign > 0 ? Signal.StrongBuy : Signal.Buy
            : slope.Sign < 0 ? change.Sign < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (final)
        {
            foreach (var band in _bands) band.Next(current, At(_index - 2), At(_index - band.Period), At(_index - band.Period - 2), _index >= band.Period, true);
            _roof.Next(price, true); _roofHistory[_index] = current;
            _roofHistory.Remove(_index - (long)_upper - 2); _index++;
        }
        return (value, signal);
    }
    internal void Reset() { _roof.Reset(); _roofHistory.Clear(); if (_bands is not null) foreach (var band in _bands) band.Reset(); _index = 0; }
    internal static double Summarize(IReadOnlyList<(int Period, F Power)> powers)
    {
        F maximum = 0;
        foreach (var item in powers) if (item.Power > maximum) maximum = item.Power;
        F weighted = 0, total = 0;
        foreach (var item in powers)
            if (maximum.Sign > 0 && 2 * item.Power >= maximum) { weighted += item.Period * item.Power; total += item.Power; }
        return total.Sign == 0 ? 0 : (weighted / total).Publish();
    }
}
