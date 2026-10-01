using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class MultiDepthWindow
{
    private readonly Pole[] _poles;
    private Number _previousMargin;
    internal MultiDepthWindow(int length)
    {
        length = Math.Max(1, length);
        var one = Number.Of(1).Round(1);
        var gain1 = Number.Of(2).Round(1).Divide(length + 1L);
        var angle2 = Math.Sqrt(2) * Math.PI / length;
        var angle3 = Math.PI / length;
        static Number Gap(double angle) => Number.Of(2 * Math.Exp(-angle / 2) * Math.Sinh(angle / 2)).Round(53);
        var gap2 = Gap(angle2); var radius2 = one - gap2;
        var gap3 = Gap(angle3); var radius3 = one - gap3;
        var sine2 = Number.Of(Math.Sin(angle2 / 2)).Round(53);
        var sine3 = Number.Of(Math.Sin(Math.Sqrt(3) * Math.PI / length / 2)).Round(53);
        var b2 = radius2.Times(2) * (one - (sine2 * sine2).Times(2));
        var b3 = radius3.Times(2) * (one - (sine3 * sine3).Times(2));
        var c = radius3 * radius3;
        // Exact algebra after the binary64 primitives preserves unity gain and
        // the tiny feed terms at extreme periods. c shares the complex radius.
        var gain2 = gap2 * gap2 + (radius2 * sine2 * sine2).Times(4);
        var gain3 = (gap3 * gap3 + (radius3 * sine3 * sine3).Times(4)) * (one - c);
        _poles = new[]
        {
            new Pole(gain1, new[] { one - gain1 }),
            new Pole(gain2, new[] { b2, default(Number) - radius2 * radius2 }),
            new Pole(gain3, new[] { b3 + c, default(Number) - c - b3 * c, c * c })
        };
    }
    internal (double One, double Two, double Three, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        var current = Number.Of(price).Round(53);
        var first = _poles[0].Next(current, final);
        var second = _poles[1].Next(current, final);
        var third = _poles[2].Next(current, final);
        var margin = current - second; var change = margin - _previousMargin;
        var trade = margin.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : margin.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) _previousMargin = margin;
        return (first.Publish(), second.Publish(), third.Publish(), trade);
    }
    internal void Reset() { foreach (var pole in _poles) pole.Reset(); _previousMargin = default; }
    private sealed class Pole
    {
        private readonly Number _gain;
        private readonly Number[] _feedback, _anchors, _alpha, _beta;
        private int _count;
        internal Pole(Number gain, Number[] feedback)
        {
            _gain = gain; _feedback = feedback;
            _anchors = new Number[feedback.Length]; _alpha = new Number[feedback.Length]; _beta = new Number[feedback.Length];
        }
        private static Number Abs(Number value) => value.Sign < 0 ? default(Number) - value : value;
        internal Number Next(Number current, bool final)
        {
            // Missing alpha lags equal the current price, so their centered
            // differences vanish. Beta lags are zero until observed.
            Number residual = default;
            for (var lag = 0; lag < _count; lag++) residual += _feedback[lag] * ((_anchors[lag] - current) + _alpha[lag]);
            var alpha = current + residual;
            var beta = _gain * (default(Number) - residual);
            for (var lag = 0; lag < _count; lag++) beta += _feedback[lag] * _beta[lag];
            var result = alpha + beta.Divide(_feedback.Length);
            if (final)
            {
                for (var lag = _feedback.Length - 1; lag > 0; lag--)
                { _anchors[lag] = _anchors[lag - 1]; _alpha[lag] = _alpha[lag - 1]; _beta[lag] = _beta[lag - 1]; }
                var relative = (Abs(alpha) - Abs(residual)).Sign >= 0;
                _anchors[0] = relative ? current : default;
                // Fixed coefficients allow exact recursive state. Rounding the two
                // stages separately destroys exact impulse cancellations.
                _alpha[0] = relative ? residual : alpha; _beta[0] = beta;
                if (_count < _feedback.Length) _count++;
            }
            return result;
        }
        internal void Reset()
        { Array.Clear(_anchors, 0, _anchors.Length); Array.Clear(_alpha, 0, _alpha.Length); Array.Clear(_beta, 0, _beta.Length); _count = 0; }
    }
}
