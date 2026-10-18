using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Symmetrically weighted average over exactly Period closes.</summary>
/// <remarks>Weights rise from one to the center and fall to one. Even periods have a two-value plateau.
/// Unlike two Period-length SMAs, this window contains exactly Period prices. Startup returns zero.
/// Two exact rolling sums implement the convolution in constant update time, rounding only the final mean.</remarks>
public sealed class TriangularWindowAverage
    : IndicatorBase,
        IMovingAverage,
        ITrendIndicator,
        IIndicatorValidationContract
{
    /// <summary>Creates a triangular window with a positive number of prices.</summary>
    public TriangularWindowAverage(int period = 14)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Number of prices in the weighted window.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [IndicatorValidationRule.Reference(0, Reference, 0, 0)];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var result = new double[bars.Count];
        for (var i = Period - 1; i < bars.Count; i++)
        {
            var sum = new ReferenceFraction(0);
            long weightSum = 0;
            for (var j = 0; j < Period; j++)
            {
                var weight = Math.Min(j + 1, Period - j);
                sum +=
                    ReferenceFraction.FromDouble(bars[i - Period + 1 + j].Close)
                    * new ReferenceFraction(weight);
                weightSum += weight;
            }
            result[i] = (sum / new ReferenceFraction(weightSum)).ToDouble();
        }
        return result;
    }

    internal sealed class State : IIndicatorState
    {
        private readonly int _firstLength,
            _secondLength;
        private readonly Queue<double> _prices = new();
        private readonly Queue<ExactMeanAccumulator> _sums = new();
        private readonly long _mass;
#if !NETFRAMEWORK
        private readonly Queue<double> _fastSums = new();
        private Core.SmaCpuKernel.Certificate _certificate;
        private bool _certified;
        private double _fastFirst, _fastSecond;
#endif
        private ExactMeanAccumulator _first,
            _second;

        internal State(int period)
        {
            _firstLength = (int)(((long)period + 1) / 2);
            _secondLength = period - _firstLength + 1;
            _mass = (long)_firstLength * _secondLength;
#if !NETFRAMEWORK
            ResetCertificate();
#endif
        }

        public void Reset()
        {
            _prices.Clear();
            _sums.Clear();
            _first = _second = default;
#if !NETFRAMEWORK
            _fastSums.Clear();
            _fastFirst = _fastSecond = 0;
            ResetCertificate();
#endif
        }

#if !NETFRAMEWORK
        private void ResetCertificate()
        {
            _certified = _mass <= int.MaxValue;
            _certificate = new(_certified ? (int)_mass : int.MaxValue);
        }
#endif

        public double Update(in Bar bar)
        {
#if !NETFRAMEWORK
            if (_certified && !_certificate.Include(bar.Close))
            {
                // Every retained box sum and the convolution total is still
                // exact. Promote before admitting the first uncertified input.
                _first.Add(_fastFirst);
                _second.Add(_fastSecond);
                foreach (double value in _fastSums)
                {
                    var sum = new ExactMeanAccumulator(); sum.Add(value);
                    _sums.Enqueue(sum);
                }
                _fastSums.Clear();
                _certified = false;
            }
            if (_certified)
            {
                // Subtract before adding: all intermediates have absolute
                // coefficient mass at most the certified triangular mass.
                if (_prices.Count == _firstLength) _fastFirst -= _prices.Dequeue();
                _prices.Enqueue(bar.Close);
                _fastFirst += bar.Close;
                if (_prices.Count < _firstLength) return 0;
                if (_fastSums.Count == _secondLength) _fastSecond -= _fastSums.Dequeue();
                _fastSums.Enqueue(_fastFirst);
                _fastSecond += _fastFirst;
                return _fastSums.Count < _secondLength || _fastSecond == 0 ? 0 : _fastSecond / _mass;
            }
#endif
            if (_prices.Count == _firstLength)
                _first.Add(_prices.Dequeue(), -1);
            _prices.Enqueue(bar.Close);
            _first.Add(bar.Close);
            if (_prices.Count < _firstLength)
                return 0;
            if (_sums.Count == _secondLength)
                _second.Subtract(_sums.Dequeue());
            _sums.Enqueue(_first);
            _second.AddExact(_first);
            return _sums.Count < _secondLength
                ? 0
                : _second.Mean(_mass);
        }
    }
}
