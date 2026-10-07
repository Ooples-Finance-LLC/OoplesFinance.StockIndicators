using OoplesFinance.StockIndicators.Streaming;
using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class PeriodicChannelWindow : IDisposable
{
    internal readonly struct Number
    {
        internal BigInteger Numerator { get; }
        private readonly BigInteger _denominator;
        internal BigInteger Denominator => _denominator.IsZero ? BigInteger.One : _denominator;
        internal int Sign => Numerator.Sign;
        private Number(BigInteger numerator, BigInteger denominator)
        { Numerator = numerator; _denominator = denominator; }
        internal static Number Integer(BigInteger value) => new(value, BigInteger.One);
        internal static Number Of(double value)
        {
            var units = ExactVarianceWindow.Units(value); var denominator = BigInteger.One << 1074;
            var common = BigInteger.GreatestCommonDivisor(BigInteger.Abs(units), denominator);
            return new(units / common, denominator / common);
        }
        public static Number operator +(Number a, Number b)
        {
            if (a.Sign == 0) return b;
            if (b.Sign == 0) return a;
            var common = BigInteger.GreatestCommonDivisor(a.Denominator, b.Denominator);
            var left = a.Denominator / common; var right = b.Denominator / common;
            var numerator = a.Numerator * right + b.Numerator * left;
            // For reduced operands, any remaining common factor divides the
            // old denominator gcd. Never run gcd on the full growing product.
            var reduction = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), common);
            return new(numerator / reduction, left * (b.Denominator / reduction));
        }
        public static Number operator -(Number a, Number b) => a + new Number(-b.Numerator, b.Denominator);
        public static Number operator *(Number a, Number b)
        {
            var left = BigInteger.GreatestCommonDivisor(BigInteger.Abs(a.Numerator), b.Denominator);
            var right = BigInteger.GreatestCommonDivisor(BigInteger.Abs(b.Numerator), a.Denominator);
            return new((a.Numerator / left) * (b.Numerator / right), (a.Denominator / right) * (b.Denominator / left));
        }
        internal Number Divide(Number value)
        {
            if (value.Sign == 0) throw new DivideByZeroException();
            return this * new Number(value.Sign * value.Denominator, BigInteger.Abs(value.Numerator));
        }
        internal Number Times(long value) => this * Integer(value);
        internal Number Divide(long value) => Divide(Integer(value));
        private static double PublishRatio(BigInteger top, BigInteger bottom)
        {
            var numerator = new ExactMeanAccumulator(); numerator.Add(1, top);
            var denominator = new ExactMeanAccumulator(); denominator.Add(1, bottom);
            return numerator.Ratio(denominator);
        }
        internal double Publish() => PublishRatio(Numerator, Denominator);
        internal static double[] PublishBands(Number line, Number width)
        {
            // These fractions are only published, never stored in arithmetic state.
            // A single common denominator avoids six large, unnecessary reductions.
            var basis = line.Numerator * width.Denominator;
            var step = width.Numerator * line.Denominator;
            var denominator = line.Denominator * width.Denominator;
            return new[] { line.Publish(), width.Publish(), PublishRatio(basis + step, denominator),
                PublishRatio(basis + step * 2, denominator), PublishRatio(basis + step * 3, denominator),
                PublishRatio(basis - step, denominator), PublishRatio(basis - step * 2, denominator),
                PublishRatio(basis - step * 3, denominator) };
        }
    }

    private readonly int _period;
    private readonly PeriodicCorrelationSign _direction;
    private long _count;
    private Number _prices, _sines, _sineErrors, _priceErrors, _lineErrors, _previousMargin;
    internal static readonly string[] Keys = { "K", "Os", "Ap", "Bp", "Cp", "Al", "Bl", "Cl" };
    internal PeriodicChannelWindow(int period, int lookback)
    { _period = Math.Max(1, period); _direction = new(lookback); }
    private static Number Abs(Number value) => value.Sign < 0 ? default(Number) - value : value;

    internal (double[] Values, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        var direction = _direction.Next(price, final);
        var current = Number.Of(price);
        // Math.Sin supplies the binary64 transcendental sample; all subsequent
        // sums and ratios, including the signal comparison, retain exact values.
        var sine = Number.Of(Math.Sin((double)_count * direction / _period));
        var prices = _prices + current; var sines = _sines + sine;
        var mean = _count == 0 ? default : prices.Divide(_count);
        var sineOffset = _count == 0 ? default : sine - sines.Divide(_count);
        var sineErrors = _sineErrors + Abs(sineOffset);
        var sineZ = sineErrors.Sign == 0 ? default : sineOffset.Times(_count).Divide(sineErrors);
        var priceErrors = _priceErrors + Abs(current - mean);
        var line = _count == 0 ? default : mean + (Number.Integer(_count > 1 ? 2 : 0) + sineZ) * priceErrors.Divide(_count);
        var margin = current - line; var lineErrors = _lineErrors + Abs(margin);
        var width = _count == 0 ? default : lineErrors.Divide(_count);
        var change = margin - _previousMargin;
        var trade = margin.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy
            : margin.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
        var values = Number.PublishBands(line, width);
        if (final)
        {
            _prices = prices; _sines = sines; _sineErrors = sineErrors; _priceErrors = priceErrors;
            _lineErrors = lineErrors; _previousMargin = margin; _count++;
        }
        return (values, trade);
    }
    internal void Reset()
    {
        _direction.Reset(); _count = 0;
        _prices = _sines = _sineErrors = _priceErrors = _lineErrors = _previousMargin = default;
    }
    public void Dispose() => Reset();
}
