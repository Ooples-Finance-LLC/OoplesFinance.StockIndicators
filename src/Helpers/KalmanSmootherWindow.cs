using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class KalmanSmootherWindow
{
    internal readonly struct Value
    {
        internal readonly F A, B;
        internal Value(F a, F b) { A = a; B = b; }
        public static Value operator +(Value x, Value y) => new(x.A + y.A, x.B + y.B);
        public static Value operator -(Value x, Value y) => new(x.A - y.A, x.B - y.B);
        internal Value Scale(F q) => new(A * q, B * q);
        internal Value RootTimes(F r) => new(B * r, A);
        internal int Sign(F r)
        {
            if (A.Sign == 0) return B.Sign;
            if (B.Sign == 0 || A.Sign == B.Sign) return A.Sign;
            return A.Sign * (A * A).CompareTo(B * B * r);
        }
        internal double Publish(F r)
        {
            if (B.Sign == 0) return A.Publish();
            if (Sign(r) == 0) return 0;
            for (var bits = 64; ; bits = checked(bits * 2))
            {
                var root = UltimatePowerWeights.Root(r, bits);
                var lo = A + B * (B.Sign > 0 ? root.Lower : root.Upper);
                var hi = A + B * (B.Sign > 0 ? root.Upper : root.Lower);
                var left = lo.Publish(); var right = hi.Publish();
#pragma warning disable S1244 // Exact equality certifies that both interval endpoints round to the same binary64 value; an epsilon cannot certify this.
                if (left == right) return left;
#pragma warning restore S1244
            }
        }
    }
    private readonly F _q, _r;
    private readonly F? _rationalRoot;
    private readonly BigInteger _denominatorStep, _gainStep, _rootStep;
    private BigInteger _denominator = BigInteger.One, _levelUnits, _velocityUnits, _marginUnits;
    private Value _level, _velocity, _margin;
    private bool _started;
    internal KalmanSmootherWindow(int length, bool allowRationalRoot = true)
    {
        _q = (F)Math.Max(1, length) / 10000; _r = 2 * _q;
        if (allowRationalRoot)
        {
            var numerator = ExactPopulationDeviation.IntegerRoot(_r.Numerator);
            var denominator = ExactPopulationDeviation.IntegerRoot(_r.Denominator);
            if (numerator * numerator == _r.Numerator && denominator * denominator == _r.Denominator)
            {
                _rationalRoot = new F(numerator, denominator);
                _denominatorStep = _q.Denominator / BigInteger.GreatestCommonDivisor(_q.Denominator, denominator) * denominator;
                _gainStep = _q.Numerator * (_denominatorStep / _q.Denominator);
                _rootStep = numerator * (_denominatorStep / denominator);
            }
        }
    }
    internal (double Line, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        if (_rationalRoot.HasValue) return NextRational(price, final);
        var x = new Value(F.Of(price), 0); var prior = _started ? _level : x;
        var delta = x - prior; var velocity = _velocity + delta.Scale(_q);
        var level = prior + delta.RootTimes(_r) + velocity;
        var margin = x - level; var sign = margin.Sign(_r); var change = (margin - _margin).Sign(_r);
        var trade = sign > 0 && change > 0 ? Signal.StrongBuy : sign < 0 && change < 0 ? Signal.StrongSell
            : sign > 0 ? Signal.Buy : sign < 0 ? Signal.Sell : Signal.None;
        var output = level.Publish(_r);
        if (final) { _level = level; _velocity = velocity; _margin = margin; _started = true; }
        return (output, trade);
    }
    private (double Line, Signal Trade) NextRational(double price, bool final)
    {
        // All three states share D * 2^1074. Move them to D * step together;
        // no fraction reduction is needed to preserve the exact recurrence.
        var input = ExactVarianceWindow.Units(price) * _denominator;
        var prior = _started ? _levelUnits : input;
        var delta = input - prior;
        var velocity = _velocityUnits * _denominatorStep + delta * _gainStep;
        var level = prior * _denominatorStep + delta * _rootStep + velocity;
        var denominator = _denominator * _denominatorStep;
        var margin = input * _denominatorStep - level;
        var sign = margin.Sign;
        var change = (margin - _marginUnits * _denominatorStep).Sign;
        var trade = sign > 0 && change > 0 ? Signal.StrongBuy : sign < 0 && change < 0 ? Signal.StrongSell
            : sign > 0 ? Signal.Buy : sign < 0 ? Signal.Sell : Signal.None;
        var output = ExactMeanAccumulator.UnitRatio(level, denominator);
        if (final)
        {
            _denominator = denominator; _levelUnits = level; _velocityUnits = velocity;
            _marginUnits = margin; _started = true;
        }
        return (output, trade);
    }
    internal void Reset()
    {
        _level = _velocity = _margin = default; _started = false;
        _denominator = BigInteger.One; _levelUnits = _velocityUnits = _marginUnits = default;
    }
    internal static (double[] Values, Signal[] Signals) Calculate(StockData data, int length)
    {
        foreach (var series in new[] { data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes, data.ChainedValues, data.InputValues })
            foreach (var value in series) StreamingInputValidation.Finite(value, nameof(data));
        var input = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var window = new KalmanSmootherWindow(length); var values = new double[input.Count]; var signals = new Signal[input.Count];
        for (var i = 0; i < input.Count; i++) (values[i], signals[i]) = window.Next(input[i], true);
        return (values, signals);
    }
}
