using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class GrandForecastWindow
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private static readonly BigInteger Carry = ExactVarianceWindow.Units(.9), Input = ExactVarianceWindow.Units(.1);
    private readonly int _length, _horizon;
    private readonly BigInteger _multiplier;
    private readonly History _trend, _change, _forecast, _errors;
    private BigInteger _trendSum, _errorSum, _previousBull, _previousBear;
    internal GrandForecastWindow(int length, int forecastLength, double mult)
    {
        if (double.IsNaN(mult) || double.IsInfinity(mult) || mult < 0) throw new ArgumentOutOfRangeException(nameof(mult));
        _length = Math.Max(1, length); _horizon = Math.Max(1, forecastLength); _multiplier = ExactVarianceWindow.Units(mult);
        _trend = new(Math.Max(_length, _horizon)); _change = new(_length); _forecast = new(_horizon); _errors = new(_horizon);
    }
    internal (double Trend, double Upper, double Middle, double Lower, Signal Trade) Next(double price, bool final)
    {
        var current = ExactVarianceWindow.Units(price);
        var previousTrend = _trend.Count >= _length ? _trend.Lag(_length) : current;
        var previousChange = _change.Count >= _length ? _change.Lag(_length) : current;
        var priorTrend = _trend.Count >= _horizon ? _trend.Lag(_horizon) : BigInteger.Zero;
        var previousForecast = _forecast.Count >= _horizon ? _forecast.Lag(_horizon) : BigInteger.Zero;
        var change = RocBankValue.RoundUnits(Carry * previousTrend, Unit);
        var trend = RocBankValue.RoundUnits((2 * change - previousChange) * Unit + Input * current, Unit);
        var trendSum = _trendSum + trend - (_trend.Count >= _length ? _trend.Lag(_length) : BigInteger.Zero);
        var mean = RocBankValue.RoundUnits(trendSum, new BigInteger(Math.Min(_length, _trend.Count + 1L)));
        var forecast = RocBankValue.RoundUnits(2 * trend - priorTrend, BigInteger.One);
        var error = BigInteger.Abs(RocBankValue.RoundUnits(current - previousForecast, BigInteger.One));
        var errorSum = _errorSum + error - (_errors.Count >= _horizon ? _errors.Lag(_horizon) : BigInteger.Zero);
        var errorMean = RocBankValue.RoundUnits(errorSum, new BigInteger(Math.Min(_horizon, _errors.Count + 1L)));
        var upper = RocBankValue.RoundUnits(forecast * Unit + errorMean * _multiplier, Unit);
        var lower = RocBankValue.RoundUnits(forecast * Unit - errorMean * _multiplier, Unit);
        var bull = current - BigInteger.Max(forecast, BigInteger.Max(trend, mean));
        var bear = current - BigInteger.Min(forecast, BigInteger.Min(trend, mean));
        var trade = bull.Sign > 0 && bull > _previousBull ? Signal.StrongBuy : bear.Sign < 0 && bear < _previousBear ? Signal.StrongSell
            : bull.Sign > 0 ? Signal.Buy : bear.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        { _trend.Add(trend); _change.Add(change); _forecast.Add(forecast); _errors.Add(error); _trendSum = trendSum; _errorSum = errorSum; _previousBull = bull; _previousBear = bear; }
        double Publish(BigInteger value) => ExactMeanAccumulator.UnitRatio(value, BigInteger.One);
        return (Publish(mean), Publish(upper), Publish(forecast), Publish(lower), trade);
    }
    internal void Reset() { _trend.Reset(); _change.Reset(); _forecast.Reset(); _errors.Reset(); _trendSum = _errorSum = _previousBull = _previousBear = default; }
    // Indexed lags retain only observed data. Prefix compaction is amortized.
    private sealed class History
    {
        private readonly int _capacity; private readonly List<BigInteger> _values = new(); private int _start;
        internal History(int capacity) => _capacity = capacity;
        internal int Count => _values.Count - _start;
        internal BigInteger Lag(int period) => _values[_values.Count - period];
        internal void Add(BigInteger value)
        {
            if (Count == _capacity) _start++; _values.Add(value);
            if (_start >= 1024 && _start >= _values.Count / 2) { _values.RemoveRange(0, _start); _start = 0; }
        }
        internal void Reset() { _values.Clear(); _start = 0; }
    }
}
