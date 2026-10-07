using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class TimeMoneyWindow : IDisposable
{
    internal static readonly string[] Keys = { "Ch+1", "Ch-1", "Ch+2", "Ch-2", "Ch+3", "Ch-3", "Median" };
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _lag, _length;
    private readonly bool _simple;
    private readonly RocBankAverage _basis, _mean, _moment, _width;
    private readonly Queue<BigInteger> _bases = new(), _variances = new(), _returns = new();
    private BigInteger _sum, _squares, _previousSlope;
    internal TimeMoneyWindow(MovingAvgType kind, int length1, int length2)
    {
        length1 = Math.Max(1, length1); _length = Math.Max(1, length2);
        _lag = (int)Math.Max(2, Math.Min(530, (length1 + 1L) / 2));
        _simple = kind == MovingAvgType.SimpleMovingAverage;
        _basis = new(kind, length1, 1, true); _mean = new(kind, _length, 1, true);
        _moment = new(kind, _length, 1, true); _width = new(kind, length1, 1, true);
    }
    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);
    private static BigInteger Round(BigInteger numerator, BigInteger denominator) => RocBankValue.RoundUnits(
        denominator.Sign < 0 ? -numerator : numerator, BigInteger.Abs(denominator));
    private static BigInteger Root(BigInteger numerator, BigInteger denominator)
    {
        if (numerator.Sign <= 0) return BigInteger.Zero;
        for (var shift = 0; ; shift += 32)
        {
            var value = ExactPopulationDeviation.RootRatio(numerator, denominator << (2 * shift));
            if (!double.IsInfinity(value)) return U(value) << shift;
        }
    }
    private static BigInteger Average(RocBankAverage average, BigInteger value, bool final)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(double.Epsilon, value);
        var result = average.Next(RocBankValue.Round(sum), final);
        return U(result.Mantissa) << result.UpperShift;
    }
    internal (double[] Outputs, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        var basis = Average(_basis, U(price), final);
        var previousBasis = _bases.Count == _lag ? _bases.Peek() : BigInteger.Zero;
        var difference = Round(U(price) - previousBasis, BigInteger.One);
        var scaledDifference = Round(100 * difference, BigInteger.One);
        var change = previousBasis.IsZero ? BigInteger.Zero : Round(scaledDifference << 1074, previousBasis);
        var square = Round(change * change, Unit);
        var mean = Average(_mean, change, final); var moment = Average(_moment, square, final);
        var sum = _sum; var squares = _squares; BigInteger variance;
        if (_simple)
        {
            var expired = _returns.Count == _length ? _returns.Peek() : BigInteger.Zero;
            sum += change - expired; squares += change * change - expired * expired;
            var count = Math.Min(_length, _returns.Count + 1L);
            var deviation = Root(count * squares - sum * sum, new BigInteger(count) * count);
            // Preserve the public SMA path: rounded deviation, then rounded square.
            variance = Round(deviation * deviation, Unit);
        }
        else variance = Round(moment - Round(mean * mean, Unit), BigInteger.One);
        var previousVariance = _variances.Count == _lag ? _variances.Peek() : BigInteger.Zero;
        var deviationLagged = Root(previousVariance << 1074, BigInteger.One);
        var width = Average(_width, deviationLagged, final);
        var outputs = new double[7];
        for (var band = 0; band < 3; band++)
        {
            var coefficient = band == 0 ? .01 : band == 1 ? .02 : .03;
            var distance = Round(U(coefficient) * width, Unit);
            var upper = Round(basis * Round(Unit + distance, BigInteger.One), Unit);
            var lower = Round(basis * Round(Unit - distance, BigInteger.One), Unit);
            outputs[2 * band] = ExactMeanAccumulator.UnitRatio(upper, BigInteger.One);
            outputs[2 * band + 1] = ExactMeanAccumulator.UnitRatio(lower, BigInteger.One);
        }
        outputs[6] = ExactMeanAccumulator.UnitRatio(width, BigInteger.One);
        var slope = deviationLagged - width;
        var trade = slope.Sign > 0 ? slope > _previousSlope ? Signal.StrongBuy : Signal.Buy
            : slope.Sign < 0 ? slope < _previousSlope ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (final)
        {
            if (_bases.Count == _lag) _bases.Dequeue(); _bases.Enqueue(basis);
            if (_variances.Count == _lag) _variances.Dequeue(); _variances.Enqueue(variance);
            if (_simple) { if (_returns.Count == _length) _returns.Dequeue(); _returns.Enqueue(change); }
            _sum = sum; _squares = squares; _previousSlope = slope;
        }
        return (outputs, trade);
    }
    internal void Reset()
    {
        _basis.Reset(); _mean.Reset(); _moment.Reset(); _width.Reset();
        _bases.Clear(); _variances.Clear(); _returns.Clear(); _sum = _squares = _previousSlope = default;
    }
    public void Dispose() { _basis.Dispose(); _mean.Dispose(); _moment.Dispose(); _width.Dispose(); _bases.Clear(); _variances.Clear(); _returns.Clear(); }
    internal static (Dictionary<string,List<double>> Outputs, List<Signal> Signals) Calculate(StockData data, MovingAvgType kind, int length1, int length2)
    {
        var (input, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        foreach (var values in new[] { input, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes })
            foreach (var value in values) StreamingInputValidation.Finite(value, nameof(data));
        using var window = new TimeMoneyWindow(kind, length1, length2);
        var outputs = Keys.ToDictionary(k => k, _ => new List<double>(input.Count)); var signals = new List<Signal>(input.Count);
        foreach (var value in input)
        {
            var point = window.Next(value, true);
            for (var j = 0; j < Keys.Length; j++) outputs[Keys[j]].Add(point.Outputs[j]);
            signals.Add(point.Trade);
        }
        return (outputs, signals);
    }
}
