using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Burg autoregression with temporal Hann smoothing of each lag coefficient.</summary>
internal sealed class MesaPredictionKernel : IDisposable
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _window, _order, _horizon, _smooth;
    private readonly BigInteger _h1, _h2, _h3, _l1, _l2, _l3;
    private readonly List<BigInteger> _samples = new();
    private readonly Queue<BigInteger[]> _coefficients = new();
    private BigInteger? _mass;
    private BigInteger _price1, _price2, _hp1, _hp2, _ssf1, _ssf2, _previousPrediction, _previousDifference;
    private int _startup;
    private static BigInteger R(BigInteger numerator) => RocBankValue.RoundUnits(numerator, BigInteger.One);
    private static BigInteger R(BigInteger numerator, BigInteger denominator) => RocBankValue.RoundUnits(numerator, denominator);
    private static int Trailing(BigInteger value)
    {
        if(value.IsZero) return int.MaxValue;
        var bytes=BigInteger.Abs(value).ToByteArray();var i=0;while(bytes[i]==0)i++;
        var result=8*i;var last=bytes[i];while((last&1)==0){result++;last>>=1;}return result;
    }
    private static BigInteger Product(BigInteger a, BigInteger b)
    {
        if(a.IsZero||b.IsZero)return BigInteger.Zero;
        var shiftA=Trailing(a);var shiftB=Trailing(b);var product=(a>>shiftA)*(b>>shiftB);var shift=shiftA+shiftB-1074;
        return shift>=0?R(product<<shift):R(product,BigInteger.One<<-shift);
    }
    private static BigInteger ExactProduct(BigInteger a, BigInteger b)
    {
        if(a.IsZero||b.IsZero)return BigInteger.Zero;
        var shiftA=Trailing(a);var shiftB=Trailing(b);return ((a>>shiftA)*(b>>shiftB))<<(shiftA+shiftB);
    }
    private static BigInteger At(IReadOnlyList<BigInteger> values, int index) => index < 0 || index >= values.Count ? BigInteger.Zero : values[index];
    private BigInteger Weight(int lag) => ExactVarianceWindow.Units(1 - Math.Cos(2 * Math.PI * (lag + 1d) / (_smooth + 1d)));
    internal MesaPredictionKernel(int horizon, int order, int smooth, int window)
    {
        _window = Math.Max(2, window); _order = Math.Min(Math.Max(1, order), _window - 1);
        _horizon = Math.Max(1, horizon); _smooth = Math.Max(1, smooth);
        var highAngle = Math.Sqrt(2) * Math.PI / _window; var highPole = Math.Exp(-highAngle);
        var h2 = 2 * highPole * Math.Cos(highAngle); var h3 = -highPole * highPole;
        _h1 = ExactVarianceWindow.Units((1 + h2 - h3) / 4); _h2 = ExactVarianceWindow.Units(h2); _h3 = ExactVarianceWindow.Units(h3);
        var lowAngle = Math.Sqrt(2) * Math.PI / _smooth; var lowPole = Math.Exp(-lowAngle);
        var l2 = 2 * lowPole * Math.Cos(lowAngle); var l3 = -lowPole * lowPole;
        _l1 = ExactVarianceWindow.Units(1 - l2 - l3); _l2 = ExactVarianceWindow.Units(l2); _l3 = ExactVarianceWindow.Units(l3);
    }
    private BigInteger[] Fit(IReadOnlyList<BigInteger> sample, BigInteger scale)
    {
        // Leading unavailable samples are zero. Residual support moves one position
        // left per Burg order, so retain only its observed, non-leading-zero tail.
        var prefix = _window - sample.Count; var start = Math.Max(0, prefix - 1);
        var size = _window - 1 - start; var forward = new BigInteger[size]; var backward = new BigInteger[size];
        for (var j = 0; j < size; j++)
        {
            forward[j] = R(At(sample, start + j + 1 - prefix) * Unit, scale);
            backward[j] = R(At(sample, start + j - prefix) * Unit, scale);
        }
        var ar = new List<BigInteger> { BigInteger.Zero };
        for (var order = 1; order <= _order; order++)
        {
            if (forward.All(v => v.IsZero) || backward.All(v => v.IsZero)) break;
            BigInteger cross = 0, energy = 0;
            var commonShift = forward.Concat(backward).Min(Trailing);
            for (var j = 0; j < forward.Length; j++)
            {
                var f=forward[j]>>commonShift;var b=backward[j]>>commonShift;
                cross += f*b; energy += f*f+b*b;
            }
            var reflection = energy.IsZero ? BigInteger.Zero : R(2 * cross * Unit, energy);
            reflection = BigInteger.Max(-Unit, BigInteger.Min(Unit, reflection));
            var prior = ar.ToArray();
            for (var k = 1; k < order; k++) ar[k] = R(prior[k] - Product(reflection, prior[order-k]));
            ar.Add(reflection);
            var nextStart = Math.Max(0, start - 1); var nextSize = _window - order - 1 - nextStart;
            var nextForward = new BigInteger[nextSize]; var nextBackward = new BigInteger[nextSize];
            for (var j = 0; j < nextSize; j++)
            {
                var index = nextStart + j - start;
                nextForward[j] = R(At(forward, index + 1) - Product(reflection, At(backward, index + 1)));
                nextBackward[j] = R(At(backward, index) - Product(reflection, At(forward, index)));
            }
            forward = nextForward; backward = nextBackward; start = nextStart;
        }
        return ar.Skip(1).ToArray();
    }
    private BigInteger[] Smooth(BigInteger[] current)
    {
        var history = _coefficients.Reverse().Take(_smooth - 1).ToArray();
        var count = Math.Max(current.Length, history.Length == 0 ? 0 : history.Max(v => v.Length));
        var result = new BigInteger[count];
        if (count == 0) return result;
        if (_mass is null)
        {
            BigInteger mass = 0; for (var lag = 0; lag < _smooth; lag++) mass += Weight(lag);
            _mass = mass;
        }
        for (var k = 0; k < count; k++)
        {
            var sum = ExactProduct(At(current, k), Weight(0));
            for (var lag = 0; lag < history.Length; lag++) sum += ExactProduct(At(history[lag], k), Weight(lag + 1));
            result[k] = R(sum, _mass.Value);
        }
        return result;
    }
    internal (double Ssf, double Predict, double PrePredict, Signal Signal) Next(double value, bool final)
    {
        StreamingInputValidation.Finite(value, nameof(value));
        var price = ExactVarianceWindow.Units(value);
        var delta = R(R(price - _price1) - R(_price1 - _price2));
        var hp = _startup < 4 ? BigInteger.Zero : R(R(Product(_h1, delta) + Product(_h2, _hp1)) + Product(_h3, _hp2));
        var ssf = R(R(R(Product(_l1, R(hp + _hp1)), 2) + Product(_l2, _ssf1)) + Product(_l3, _ssf2));
        var sample = _samples.Skip(_samples.Count == _window ? 1 : 0).Concat(new[] { ssf }).ToArray();
        var scale = sample.Max(BigInteger.Abs);
        // 64*epsilon = 2^-46; compare without underflowing the existing cutoff.
        var ar = (scale << 46) > BigInteger.Max(BigInteger.Abs(price), BigInteger.Abs(_price1)) ? Fit(sample, scale) : Array.Empty<BigInteger>();
        var smoothed = Smooth(ar); BigInteger prePredict = 0;
        if (smoothed.Any(v => !v.IsZero))
        {
            var path = sample.Skip(Math.Max(0, sample.Length - smoothed.Length)).ToList();
            for (var step = 0; step < _horizon; step++)
            {
                BigInteger sum = 0;
                for (var k = 0; k < smoothed.Length; k++) sum += ExactProduct(smoothed[k], At(path, path.Count - 1 - k));
                prePredict = R(sum, Unit);
                if (path.Count == smoothed.Length) path.RemoveAt(0); path.Add(prePredict);
                if (path.All(v => v.IsZero)) break;
            }
        }
        var predict = R(prePredict + _previousPrediction, 2); var difference = ssf - predict;
        var signal = difference.Sign > 0 ? difference > _previousDifference ? Signal.StrongBuy : Signal.Buy
            : difference.Sign < 0 ? difference < _previousDifference ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (final)
        {
            if (_samples.Count == _window) _samples.RemoveAt(0); _samples.Add(ssf);
            if (_coefficients.Count == _smooth) _coefficients.Dequeue(); _coefficients.Enqueue(ar);
            _price2 = _price1; _price1 = price; _hp2 = _hp1; _hp1 = hp;
            _ssf2 = _ssf1; _ssf1 = ssf; _previousPrediction = prePredict; _previousDifference = difference; _startup = Math.Min(4, _startup + 1);
        }
        return (ExactMeanAccumulator.UnitRatio(ssf, BigInteger.One), ExactMeanAccumulator.UnitRatio(predict, BigInteger.One), ExactMeanAccumulator.UnitRatio(prePredict, BigInteger.One), signal);
    }
    internal void Reset()
    {
        _samples.Clear(); _coefficients.Clear();
        _price1 = _price2 = _hp1 = _hp2 = _ssf1 = _ssf2 = _previousPrediction = _previousDifference = BigInteger.Zero; _startup = 0;
    }
    public void Dispose() { _samples.Clear(); _coefficients.Clear(); }
}
