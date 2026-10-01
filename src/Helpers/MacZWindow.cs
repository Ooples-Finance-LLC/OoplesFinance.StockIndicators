using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;

// Keep unpublished means and standardized values beyond both binary64 exponent
// limits. Only the three public output series are projected to binary64.
internal sealed class MacZWindow : IDisposable
{
    private readonly int _length;
    private readonly double _mult;
    private readonly Average _fast, _slow, _wilder, _signal;
    private readonly Queue<double> _prices = new();
    private BigInteger _sum, _squares;
    private Number _previousHistogram;
    internal MacZWindow(MovingAvgType kind, int fast, int slow, int signal, int length, double mult)
    {
        StreamingInputValidation.Finite(mult, nameof(mult));
        _length = Math.Max(1, length); _mult = mult;
        _fast = new(kind, fast); _slow = new(kind, slow); _signal = new(kind, signal);
        _wilder = new(MovingAvgType.WildersSmoothingMethod, length);
    }
    internal readonly struct Number
    {
        private readonly BigInteger _coefficient;
        private readonly int _exponent;
        private readonly BigInteger _denominator;
        private BigInteger Denominator => _denominator.IsZero ? BigInteger.One : _denominator;
        private Number(BigInteger coefficient, int exponent, BigInteger denominator = default, bool reduced = false)
        {
            if (denominator.IsZero) denominator = BigInteger.One;
            var common = reduced ? BigInteger.One : BigInteger.GreatestCommonDivisor(BigInteger.Abs(coefficient), denominator);
            _coefficient = coefficient / common; _denominator = denominator / common; _exponent = exponent;
        }
        internal int Sign => _coefficient.Sign;
        internal static Number Of(double value) => new(ExactVarianceWindow.Units(value), -1074);
        private static int Bits(BigInteger value)
        { var bytes = BigInteger.Abs(value).ToByteArray(); var last = bytes.Length - 1; while (last > 0 && bytes[last] == 0) last--; var bits = last * 8; for (var b = bytes[last]; b != 0; b >>= 1) bits++; return bits; }
        public static Number operator +(Number a, Number b)
        {
            if (a.Sign == 0) return b; if (b.Sign == 0) return a;
            var power = Math.Min(a._exponent, b._exponent);
            var common = BigInteger.GreatestCommonDivisor(a.Denominator, b.Denominator);
            var aScale = b.Denominator / common; var bScale = a.Denominator / common;
            // Exponent alignment can create further factors, so fully reduce the sum.
            return new((a._coefficient << (a._exponent - power)) * aScale + (b._coefficient << (b._exponent - power)) * bScale, power, a.Denominator * aScale);
        }
        public static Number operator -(Number a, Number b) => a + new Number(-b._coefficient, b._exponent, b.Denominator, reduced: true);
        public static Number operator *(Number a, Number b)
        {
            var crossA = BigInteger.GreatestCommonDivisor(BigInteger.Abs(a._coefficient), b.Denominator);
            var crossB = BigInteger.GreatestCommonDivisor(BigInteger.Abs(b._coefficient), a.Denominator);
            return new((a._coefficient / crossA) * (b._coefficient / crossB), checked(a._exponent + b._exponent),
                (a.Denominator / crossB) * (b.Denominator / crossA), reduced: true);
        }
        internal Number Divide(Number divisor)
        {
            if (divisor.Sign == 0) throw new DivideByZeroException();
            var numeratorCommon = BigInteger.GreatestCommonDivisor(BigInteger.Abs(_coefficient), BigInteger.Abs(divisor._coefficient));
            var denominatorCommon = BigInteger.GreatestCommonDivisor(Denominator, divisor.Denominator);
            return new((_coefficient / numeratorCommon) * (divisor.Denominator / denominatorCommon) * divisor.Sign,
                checked(_exponent - divisor._exponent), (Denominator / denominatorCommon) * (BigInteger.Abs(divisor._coefficient) / numeratorCommon), reduced: true);
        }
        internal Number OverRoot(Number square)
        {
            if (square.Sign < 0) throw new ArgumentOutOfRangeException(nameof(square));
            if (Sign == 0 || square.Sign == 0) return default;
            var ratio = (this * this).Divide(square);
            var numerator = ratio._coefficient; var denominator = ratio.Denominator; var binaryPower = ratio._exponent;
            if ((binaryPower & 1) != 0) { numerator <<= 1; binaryPower--; }
            var numeratorRoot = ExactPopulationDeviation.IntegerRoot(numerator);
            var denominatorRoot = ExactPopulationDeviation.IntegerRoot(denominator);
            if (numeratorRoot * numeratorRoot == numerator && denominatorRoot * denominatorRoot == denominator)
                return new(Sign * numeratorRoot, binaryPower / 2, denominatorRoot);
            var normalPower = Bits(numerator) - Bits(denominator);
            if (normalPower >= 0 ? numerator < (denominator << normalPower) : (numerator << -normalPower) < denominator) normalPower--;
            var grid = (normalPower >= 0 ? normalPower / 2 : (normalPower - 1) / 2) - 105;
            if (grid >= 0) denominator <<= 2 * grid; else numerator <<= -2 * grid;
            var significand = ExactPopulationDeviation.IntegerRoot(numerator / denominator);
            var boundary = 2 * significand + 1; var side = (4 * numerator).CompareTo(denominator * boundary * boundary);
            if (side > 0 || side == 0 && !significand.IsEven) significand++;
            return new(Sign * significand, checked(binaryPower / 2 + grid));
        }
        internal Number Times(long value) => new(_coefficient * value, _exponent, Denominator);
        internal Number Divide(long divisor) => new(_coefficient, _exponent, Denominator * divisor);
        // Recursive states are bounded to 106 significant bits. Finite-window
        // means retain their exact divisor, including binary64 midpoint ties.
        internal Number Round()
        {
            if (Sign == 0) return default;
            var numerator = BigInteger.Abs(_coefficient); var denominator = Denominator;
            var exponent = Bits(numerator) - Bits(denominator);
            if (exponent >= 0 ? numerator < (denominator << exponent) : (numerator << -exponent) < denominator) exponent--;
            var shift = exponent - 105;
            if (shift >= 0) denominator <<= shift; else numerator <<= -shift;
            var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
            var comparison = (2 * remainder).CompareTo(denominator);
            if (comparison > 0 || comparison == 0 && !quotient.IsEven) quotient++;
            return new(Sign * quotient, checked(_exponent + shift));
        }
        internal Number Round(int precision)
        {
            if (Sign == 0) return default;
            var magnitude = BigInteger.Abs(_coefficient); var divisor = Denominator;
            var power = Bits(magnitude) - Bits(divisor);
            if (power >= 0 ? magnitude < (divisor << power) : (magnitude << -power) < divisor) power--;
            var grid = power - (precision - 1);
            if (grid >= 0) divisor <<= grid; else magnitude <<= -grid;
            var rounded = BigInteger.DivRem(magnitude, divisor, out var tail);
            var direction = (2 * tail).CompareTo(divisor);
            if (direction > 0 || direction == 0 && !rounded.IsEven) rounded++;
            return new(Sign * rounded, checked(_exponent + grid));
        }
        internal Number OverDeviation(BigInteger radicand, int length)
        {
            if (Sign == 0 || radicand.IsZero) return default;
            var exactRoot = ExactPopulationDeviation.IntegerRoot(radicand);
            if (exactRoot * exactRoot == radicand)
                return new(_coefficient * length, checked(_exponent + 1074), Denominator * exactRoot);
            var numerator = _coefficient * _coefficient * length * (BigInteger)length;
            var denominator = radicand * Denominator * Denominator;
            var exponent = Bits(numerator) - Bits(denominator);
            if (exponent >= 0 ? numerator < (denominator << exponent) : (numerator << -exponent) < denominator) exponent--;
            var rootExponent = exponent >= 0 ? exponent / 2 : (exponent - 1) / 2;
            var shift = rootExponent - 105;
            if (shift >= 0) denominator <<= 2 * shift; else numerator <<= -2 * shift;
            var root = ExactPopulationDeviation.IntegerRoot(numerator / denominator);
            var midpoint = 2 * root + 1;
            var comparison = (4 * numerator).CompareTo(denominator * midpoint * midpoint);
            if (comparison > 0 || comparison == 0 && !root.IsEven) root++;
            return new(Sign * root, checked(_exponent + 1074 + shift));
        }
        internal double Publish()
        { var sum = new ExactMeanAccumulator(); sum.Add(1, _coefficient); sum.ScaleByPowerOfTwo(_exponent); var denominator = new ExactMeanAccumulator(); denominator.Add(1, Denominator); return sum.Ratio(denominator); }
    }
    internal sealed class Average : IDisposable
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<Number> _history = new();
        private readonly IMovingAverageSmoother? _fallback;
        private Number _sum, _weighted, _previous; private long _count;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = Math.Max(1, length); if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, _length); }
        internal Number Next(Number value, bool final)
        {
            if (_fallback is not null) return Number.Of(_fallback.Next(value.Publish(), final));
            if (_length == 1) return value;
            var sum = _sum; var weighted = _weighted; Number result;
            var window = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (window)
            {
                weighted = weighted - sum + value.Times(_length);
                if (_history.Count == _length) sum -= _history.Peek(); sum += value;
                result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Divide((long)_length * (_length + 1L) / 2)
                    : _count + 1 < _length ? default : sum.Divide(_length);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
            { sum += value; result = sum.Divide(_count + 1); }
            else
            { var ema = _kind == MovingAvgType.ExponentialMovingAverage; result = (_previous.Times(_length - 1L) + value.Times(ema ? 2 : 1)).Divide(ema ? _length + 1L : _length).Round(); }
            if (final) { if (window) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } _sum = sum; _weighted = weighted; _previous = result; _count++; }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; _fallback?.Reset(); }
        public void Dispose() => _fallback?.Dispose();
    }
    private Number Standardize(double price, Number fast, Number slow, Number wilder, bool final)
    {
        var current = ExactVarianceWindow.Units(price); var sum = _sum + current; var squares = _squares + current * current;
        if (_prices.Count == _length) { var old = ExactVarianceWindow.Units(_prices.Peek()); sum -= old; squares -= old * old; }
        var radicand = _prices.Count < _length - 1 ? BigInteger.Zero : _length * squares - sum * sum;
        var numerator = (Number.Of(price) - wilder + fast - slow) * Number.Of(_mult);
        var line = numerator.OverDeviation(radicand, _length);
        if (final) { if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(price); _sum = sum; _squares = squares; }
        return line;
    }
    private (double Line, double SignalLine, double Histogram, Signal Trade) Finish(Number line, Number signal, bool final)
    {
        var histogram = line - signal; var change = histogram - _previousHistogram;
        var trade = histogram.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : histogram.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : histogram.Sign > 0 ? Signal.Buy : histogram.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) _previousHistogram = histogram;
        return (line.Publish(), signal.Publish(), histogram.Publish(), trade);
    }
    internal (double Line, double SignalLine, double Histogram, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var value = Number.Of(price);
        var line = Standardize(price, _fast.Next(value, final), _slow.Next(value, final), _wilder.Next(value, final), final);
        return Finish(line, _signal.Next(line, final), final);
    }
    internal static (double[] Line, double[] SignalLine, double[] Histogram, Signal[] Trades) Calculate(StockData data, List<double> prices,
        MovingAvgType kind, int fast, int slow, int signal, int length, double mult, bool callbacks)
    {
        StreamingInputValidation.Finite(mult, nameof(mult)); foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        var caller = data.CaptureInputSeries();
        Number[] Mean(Number[] source, MovingAvgType meanKind, int period)
        {
            period = Math.Max(1, period); var published = callbacks || !StrengthWindow.Supports(meanKind) ? source.Select(v => v.Publish()).ToArray() : null;
            var custom = callbacks ? ComponentAverage.Take(published!, period) : null;
            if (custom is not null) return custom.Select(Number.Of).ToArray();
            if (!StrengthWindow.Supports(meanKind)) return CalculationsHelper.GetMovingAverageList(data, meanKind, period, published!.ToList()).Select(Number.Of).ToArray();
            using var average = new Average(meanKind, period); return source.Select(v => average.Next(v, true)).ToArray();
        }
        try
        {
            var source = prices.Select(Number.Of).ToArray(); var fastValues = Mean(source, kind, fast); var slowValues = Mean(source, kind, slow);
            var wilder = Mean(source, MovingAvgType.WildersSmoothingMethod, length);
            using var window = new MacZWindow(kind, fast, slow, signal, length, mult);
            var line = prices.Select((p, i) => window.Standardize(p, fastValues[i], slowValues[i], wilder[i], true)).ToArray();
            var signals = Mean(line, kind, signal); var result = line.Select((v, i) => window.Finish(v, signals[i], true)).ToArray();
            return (result.Select(v => v.Line).ToArray(), result.Select(v => v.SignalLine).ToArray(), result.Select(v => v.Histogram).ToArray(), result.Select(v => v.Trade).ToArray());
        }
        finally { data.RestoreInputSeries(caller); }
    }
    internal void Reset() { _fast.Reset(); _slow.Reset(); _wilder.Reset(); _signal.Reset(); _prices.Clear(); _sum = _squares = default; _previousHistogram = default; }
    public void Dispose() { _fast.Dispose(); _slow.Dispose(); _wilder.Dispose(); _signal.Dispose(); }
}
