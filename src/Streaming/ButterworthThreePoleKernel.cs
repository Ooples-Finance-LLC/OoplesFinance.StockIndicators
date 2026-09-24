namespace OoplesFinance.StockIndicators.Streaming;

// Retain coefficient and feedback low parts when the three poles approach one.
// Otherwise coefficient cancellation is amplified throughout the startup transient.
internal sealed class ButterworthThreePoleKernel
{
    private readonly bool _smoothInput;
    private readonly SpreadNumber _gain, _second, _third, _fourth;
    private SpreadNumber _previous, _older, _oldest;
    private double _input1, _input2, _input3;
    private int _count;

    internal ButterworthThreePoleKernel(int length, bool smoothInput = true)
    {
        length = Math.Max(2, length);
        _smoothInput = smoothInput;
        var radius = new SpreadNumber(Math.Exp(-Math.PI / length));
        var cosineTerm = radius.Times(Math.Cos(1.738 * Math.PI / length)).Times(2);
        var square = radius.Times(radius);
        _second = SpreadNumber.Add(cosineTerm, square);
        _third = SpreadNumber.Add(square, cosineTerm.Times(square)).Times(-1);
        _fourth = square.Times(square);
        _gain = SpreadNumber.Add(SpreadNumber.Subtract(new(1), cosineTerm), square)
            .Times(SpreadNumber.Subtract(new(1), square)).DividedBy(smoothInput ? 8 : 1);
    }

    internal double Next(double value, bool final)
    {
        var drive = SpreadNumber.Add(new(value), new SpreadNumber(_input1).Times(3));
        drive = SpreadNumber.Add(drive, new SpreadNumber(_input2).Times(3));
        drive = SpreadNumber.Add(drive, new(_input3));
        if (!_smoothInput) drive = new(value);
        var output = _count < 4 ? new SpreadNumber(value) : SpreadNumber.Add(
            SpreadNumber.Add(_gain.Times(drive), _second.Times(_previous)),
            SpreadNumber.Add(_third.Times(_older), _fourth.Times(_oldest)));
        if (final)
        {
            _oldest = _older; _older = _previous; _previous = output;
            _input3 = _input2; _input2 = _input1; _input1 = value;
            if (_count < 4) _count++;
        }
        return output.Value;
    }

    internal void Reset()
    {
        _previous = _older = _oldest = default;
        _input1 = _input2 = _input3 = 0;
        _count = 0;
    }
}
