using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class MorphedSineWindow
{
    private readonly int _length;
    private readonly Number _power;
    private int _phase;
    private Number _previousLine, _previousSlope;
    internal MorphedSineWindow(int length, double power)
    {
        StreamingInputValidation.Finite(power, nameof(power));
        if (power == 0) throw new ArgumentOutOfRangeException(nameof(power), "Power must be nonzero."); // NOSONAR: S1244 - Only exact zero makes the scaling formula undefined.
        _length = Math.Max(1, length); _power = Number.Of(power);
    }
    internal (double Value, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        var sine = FastSlowDegreeWindow.SinePhase(2L * _phase, _length);
        var line = Number.Of(price) + Number.Of(sine).Divide(_power);
        var slope = line - _previousLine; var change = slope - _previousSlope;
        var trade = slope.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : slope.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _phase = _phase == _length - 1 ? 0 : _phase + 1; _previousLine = line; _previousSlope = slope; }
        return (line.Publish(), trade);
    }
    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length, double power)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        var window = new MorphedSineWindow(length, power);
        for (var i = 0; i < input.Length; i++) StreamingInputValidation.Finite(input[i], nameof(input));
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true).Value;
    }
    internal void Reset() { _phase = 0; _previousLine = _previousSlope = default; }
}
