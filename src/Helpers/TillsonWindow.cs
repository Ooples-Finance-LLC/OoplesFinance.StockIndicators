using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class TillsonWindow : IDisposable
{
    private readonly RocBankAverage[]? _averages;
    private readonly IMovingAverageSmoother[]? _fallbacks;
    private readonly Coefficients _coefficients;
    internal sealed class Coefficients
    {
        private readonly BigInteger _third, _fourth, _fifth, _sixth;
        private readonly int _scale;
        internal Coefficients(double factor)
        {
            if (double.IsNaN(factor) || double.IsInfinity(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
            var bits = BitConverter.DoubleToInt64Bits(factor); var exponent = (int)((bits >> 52) & 2047);
            var significand = (bits & ((1L << 52) - 1)) + (exponent == 0 ? 0 : 1L << 52);
            var numerator = new BigInteger(bits < 0 ? -significand : significand);
            var power = Math.Max(1, exponent) - 1075;
            var denominator = BigInteger.One;
            if (power < 0) { denominator <<= -power; _scale = 3 * power; } else numerator <<= power;
            var sum = denominator + numerator;
            _third = sum * sum * sum;
            _fourth = -3 * numerator * sum * sum;
            _fifth = 3 * numerator * numerator * sum;
            _sixth = -numerator * numerator * numerator;
        }
        internal double Combine(double third, double fourth, double fifth, double sixth)
        {
            var total = new ExactMeanAccumulator(); total.Add(third, _third); total.Add(fourth, _fourth);
            total.Add(fifth, _fifth); total.Add(sixth, _sixth); total.ScaleByPowerOfTwo(_scale);
            return total.Mean(1);
        }
    }
    internal TillsonWindow(MovingAvgType kind, int length, double factor)
    {
        _coefficients = new(factor); length = Math.Max(1, length);
        if (StrengthWindow.Supports(kind)) _averages = Enumerable.Range(0, 6).Select(_ => new RocBankAverage(kind, length, int.MaxValue)).ToArray();
        else _fallbacks = Enumerable.Range(0, 6).Select(_ => MovingAverageSmootherFactory.Create(kind, length)).ToArray();
    }
    internal double Next(double price, bool commit)
    {
        var third = 0d; var fourth = 0d; var fifth = 0d;
        for (var stage = 0; stage < 6; stage++)
        {
            price = _averages is null ? _fallbacks![stage].Next(price, commit) : _averages[stage].Next(new RocBankValue(price), commit).Publish();
            if (stage == 2) third = price; else if (stage == 3) fourth = price; else if (stage == 4) fifth = price;
        }
        return _coefficients.Combine(third, fourth, fifth, price);
    }
    internal void Reset() { if (_averages is not null) foreach (var average in _averages) average.Reset(); if (_fallbacks is not null) foreach (var average in _fallbacks) average.Reset(); }
    public void Dispose() { if (_averages is not null) foreach (var average in _averages) average.Dispose(); if (_fallbacks is not null) foreach (var average in _fallbacks) average.Dispose(); }
}
