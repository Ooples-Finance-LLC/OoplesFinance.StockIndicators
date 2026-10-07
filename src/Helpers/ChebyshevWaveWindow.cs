namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ChebyshevWaveWindow
{
    private static readonly double[,] Coefficients =
    {
        { EhlersChebyshevGains.Minus2, 1.907, .293, .063, .513, .451, .481 },
        { EhlersChebyshevGains.Minus1, 1.777, .731, .166, .977, 1.008, .561 },
        { EhlersChebyshevGains.Zero, 1.572, 1.026, .282, .356, 1.329, .644 },
        { EhlersChebyshevGains.One, 1.192, 1.281, .426, -.384, 1.565, .729 },
        { EhlersChebyshevGains.Two, .681, 1.46, .543, -.966, 1.703, .793 },
        { EhlersChebyshevGains.Three, .012, 1.606, .65, -1.408, 1.801, .848 },
        { EhlersChebyshevGains.Four, -.669, 1.716, .74, -1.685, 1.866, .89 },
        { EhlersChebyshevGains.Five, -1.226, 1.8, .811, -1.842, 1.91, .922 },
        { EhlersChebyshevGains.Six, -1.659, 1.873, .878, -1.957, 1.946, .951 }
    };
    private readonly int _wave;
    private RocBankValue _section1, _section2, _output1, _output2;
    private double _previous, _older;
    internal ChebyshevWaveWindow(int wave) => _wave = wave;
    private static RocBankValue Add(RocBankValue left, RocBankValue right, int sign = 1)
    { var sum = new ExactMeanAccumulator(); left.AddTo(ref sum); right.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    internal double Next(double value, bool commit)
    {
        var input = Add(Add(new(value), new RocBankValue(_previous).Multiply(Coefficients[_wave, 1])), new(_older));
        var section = Add(Add(input.Multiply(Coefficients[_wave, 0]), _section1.Multiply(Coefficients[_wave, 2])), _section2.Multiply(Coefficients[_wave, 3]), -1);
        var output = Add(Add(Add(Add(section, _section1.Multiply(Coefficients[_wave, 4])), _section2), _output1.Multiply(Coefficients[_wave, 5])), _output2.Multiply(Coefficients[_wave, 6]), -1);
        if (commit) { _older = _previous; _previous = value; _section2 = _section1; _section1 = section; _output2 = _output1; _output1 = output; }
        return output.Publish();
    }
    internal void Reset() { _previous = _older = 0; _section1 = _section2 = _output1 = _output2 = default; }
}
