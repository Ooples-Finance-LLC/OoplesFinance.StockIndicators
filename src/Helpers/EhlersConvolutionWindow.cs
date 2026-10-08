using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class EhlersConvolutionWindow
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _length;
    private readonly BigInteger _highDrive, _highFeedback, _highDecay, _gain, _feedback, _decay;
    private BigInteger _high, _olderHigh, _roof, _olderRoof, _expiredPredecessor;
    private BigInteger _sx, _sy, _xx, _yy, _xy;
    private double _price, _olderPrice;
    private readonly List<BigInteger> _history = new(); private int _start;
    internal EhlersConvolutionWindow(int highLength, int lowLength, int correlationLength)
    {
        _length = Math.Max(1, correlationLength);
        var angle = Math.Min(.99, Math.Sqrt(2) * Math.PI / Math.Max(1, highLength));
        var pole = U(Math.Cos(angle) / (1 + Math.Sin(angle)));
        _highDrive = (Unit + pole) * (Unit + pole); _highFeedback = 2 * pole * Unit; _highDecay = -pole * pole;
        var lowAngle = Math.Sqrt(2) * Math.PI / Math.Max(1, lowLength);
        var radius = U(Math.Exp(-lowAngle)); var cosine = U(Math.Cos(lowAngle));
        _feedback = 2 * radius * cosine; _decay = -radius * radius; _gain = Unit * Unit - _feedback - _decay;
    }
    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);
    internal static double Slope(BigInteger current, BigInteger previous)
    {
        var scale = BigInteger.Max(Unit, BigInteger.Max(BigInteger.Abs(current), BigInteger.Abs(previous)));
        return ((current - previous) << 1074) > U(1e-12) * scale ? -1 : 1;
    }
    internal (double Value, double Slope, Signal Trade, double Correlation) Next(double price, bool final)
    {
        var change = U(price) - 2 * U(_price) + U(_olderPrice);
        var high = RocBankValue.RoundUnits(_highDrive * change + 4 * _highFeedback * _high + 4 * _highDecay * _olderHigh, BigInteger.One << 2150);
        var roof = RocBankValue.RoundUnits(_gain * (high + _high) + 2 * _feedback * _roof + 2 * _decay * _olderRoof, BigInteger.One << 2149);
        var retained = _history.Count - _start; var full = retained == _length;
        var n = (int)Math.Min(_length, retained + 1L);
        var xOld = full ? _history[_start] : BigInteger.Zero; var yOld = full ? _expiredPredecessor : BigInteger.Zero;
        var sx = _sx + roof - xOld; var sy = _sy + _roof - yOld;
        var xx = _xx + roof * roof - xOld * xOld; var yy = _yy + _roof * _roof - yOld * yOld; var xy = _xy + roof * _roof - xOld * yOld;
        var vx = n * xx - sx * sx; var vy = n * yy - sy * sy; var covariance = n * xy - sx * sy;
        var correlation = n < 2 || vx.IsZero || vy.IsZero ? 0 : covariance.Sign * ExactPopulationDeviation.RootRatio((covariance * covariance) << 2148, vx * vy);
        var exponential = Math.Exp(3 * correlation); var value = exponential / (exponential + 1) / 2;
        var lag = n / 2 + n % 2; var previous = lag <= retained ? _history[_history.Count - lag] : BigInteger.Zero;
        var slope = Slope(roof, previous); var changeNow = roof - _roof; var changeBefore = _roof - _olderRoof;
        var trade = changeNow.Sign > 0 && changeNow > changeBefore ? Signal.StrongBuy : changeNow.Sign < 0 && changeNow < changeBefore ? Signal.StrongSell
            : changeNow.Sign > 0 ? Signal.Buy : changeNow.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            _sx = sx; _sy = sy; _xx = xx; _yy = yy; _xy = xy;
            _olderPrice = _price; _price = price; _olderHigh = _high; _high = high; _olderRoof = _roof; _roof = roof;
            _history.Add(roof);
            if (_history.Count - _start > _length) _expiredPredecessor = _history[_start++];
            if (_start >= 1024 && _start >= _history.Count / 2) { _history.RemoveRange(0, _start); _start = 0; }
        }
        return (value, slope, trade, correlation);
    }
    internal void Reset()
    { _high = _olderHigh = _roof = _olderRoof = _expiredPredecessor = _sx = _sy = _xx = _yy = _xy = default; _price = _olderPrice = 0; _history.Clear(); _start = 0; }
}
