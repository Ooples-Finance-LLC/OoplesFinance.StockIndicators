using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;

// The library's public JMA approximation: three rounded recursive stages,
// followed by a rounded accumulated output. Unpublished stages may exceed double.
internal sealed class JmaWindow
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly BigInteger _alpha, _beta, _phase, _oneMinusAlpha, _oneMinusBeta, _drive, _decay;
    private BigInteger _e0, _e1, _e2, _jma, _margin;
    internal JmaWindow(int length, double phase, double power)
    {
        if (double.IsNaN(phase) || double.IsInfinity(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        if (double.IsNaN(power) || double.IsInfinity(power)) throw new ArgumentOutOfRangeException(nameof(power));
        length = Math.Max(1, length);
        var ratio = .45 * (length - 1L); var beta = ratio / (ratio + 2); var alpha = MathHelper.Pow(beta, power);
        if (double.IsNaN(alpha) || double.IsInfinity(alpha)) throw new ArgumentOutOfRangeException(nameof(power), "Power must produce a finite filter pole.");
        _alpha = U(alpha); _beta = U(beta); _phase = U(phase < -100 ? .5 : phase > 100 ? 2.5 : phase / 100 + 1.5);
        _oneMinusAlpha = U(1 - alpha); _oneMinusBeta = U(1 - beta);
        _drive = RocBankValue.RoundUnits(_oneMinusAlpha * _oneMinusAlpha, Unit);
        _decay = RocBankValue.RoundUnits(_alpha * _alpha, Unit);
    }
    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);
    internal (double Value, Signal Trade) Next(double price, bool final)
    {
        var value = U(price);
        var e0 = RocBankValue.RoundUnits(_oneMinusAlpha * value + _alpha * _e0, Unit);
        var e1 = RocBankValue.RoundUnits((value - e0) * _oneMinusBeta + _beta * _e1, Unit);
        var target = (e0 - _jma) * Unit + _phase * e1;
        var e2 = RocBankValue.RoundUnits(target * _drive + _decay * _e2 * Unit, Unit * Unit);
        // At a zero pole the transfer function is exactly the identity.
        // Avoid losing a small current price in a rounded large correction.
        var jma = _alpha.IsZero ? value : RocBankValue.RoundUnits(e2 + _jma, BigInteger.One);
        var margin = value - jma;
        var trade = margin.Sign > 0 && margin > _margin ? Signal.StrongBuy : margin.Sign < 0 && margin < _margin ? Signal.StrongSell
            : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _e0 = e0; _e1 = e1; _e2 = e2; _jma = jma; _margin = margin; }
        return (ExactMeanAccumulator.UnitRatio(jma, BigInteger.One), trade);
    }
    internal void Reset() => _e0 = _e1 = _e2 = _jma = _margin = default;
}
