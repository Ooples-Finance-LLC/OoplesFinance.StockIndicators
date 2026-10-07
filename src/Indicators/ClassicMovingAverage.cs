using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal interface IRoundedAverageUnits
{
    BigInteger RoundedUnits { get; }
}

// Internal composition accepts rounded 2^-1074 units with an extended upper exponent.
internal interface IWideAverageState : IRoundedAverageUnits
{
    void Reset();
    void UpdateUnits(BigInteger input, Span<double> outputs);
}

/// <summary>Classical moving-average formulas with explicit startup conventions.</summary>
public enum ClassicAverageMethod
{
    /// <summary>Arithmetic window mean.</summary>
    Sma,

    /// <summary>Exponential average.</summary>
    Ema,

    /// <summary>Linearly weighted window mean.</summary>
    Wma,

    /// <summary>Double exponential extrapolation.</summary>
    Dema,

    /// <summary>Triple exponential extrapolation.</summary>
    Tema,

    /// <summary>Symmetric triangular window.</summary>
    Trima,

    /// <summary>Kaufman efficiency-adaptive average with fast/slow periods 2/30.</summary>
    Kama,

    /// <summary>Zero-seeded phase-adaptive mother average with limits .5/.05.</summary>
    Mama,

    /// <summary>Six-stage Tillson average with volume factor .7.</summary>
    T3,
}

/// <summary>Nine classical averages with explicit presence, exponential seeding and suppression.</summary>
/// <remarks>Period one is the identity for every method, ignoring seeding/suppression.
/// EMA/DEMA/TEMA cascade full means or first-price seeds and withhold period-1+suppression
/// observations at each stage. Each exact exponential recurrence and extrapolation rounds
/// once with extended upper exponents. SMA/WMA/TRIMA ignore suppression and seed options.
/// KAMA, MAMA and T3 use their own startup plus suppression and ignore the exponential seed
/// option. MAMA ignores periods above one. All lookbacks use Int64 and history grows lazily.</remarks>
public sealed class ClassicMovingAverage
    : MultiOutputIndicatorBase,
        IMovingAverage,
        IIndicatorValidationContract
{
    /// <summary>Creates a positive-period average with nonnegative extra startup suppression.</summary>
    public ClassicMovingAverage(
        int period = 30,
        ClassicAverageMethod method = ClassicAverageMethod.Sma,
        bool firstPriceSeed = false,
        int suppression = 0
    )
        : base(2)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!Enum.IsDefined(typeof(ClassicAverageMethod), method))
            throw new ArgumentOutOfRangeException(nameof(method));
        if (suppression < 0)
            throw new ArgumentOutOfRangeException(nameof(suppression));
        Period = period;
        Method = method;
        FirstPriceSeed = firstPriceSeed;
        Suppression = suppression;
    }

    /// <summary>Requested period; one selects identity.</summary>
    public int Period { get; }

    /// <summary>Selected averaging formula.</summary>
    public ClassicAverageMethod Method { get; }

    /// <summary>First-price instead of full-mean seeds for exponential extrapolations.</summary>
    public bool FirstPriceSeed { get; }

    /// <summary>Additional suppressed observations for adaptive/exponential methods.</summary>
    public int Suppression { get; }

    /// <summary>Average or zero before publication.</summary>
    public IIndicatorOutput Average => Outputs[0];

    /// <summary>One when the average is present.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];
    internal int Order =>
        Method == ClassicAverageMethod.Dema ? 2
        : Method == ClassicAverageMethod.Tema ? 3
        : 1;
    internal long First =>
        Period == 1
            ? 0
            : Method switch
            {
                ClassicAverageMethod.Ema
                or ClassicAverageMethod.Dema
                or ClassicAverageMethod.Tema => Order * ((long)Period - 1 + Suppression),
                ClassicAverageMethod.Kama => (long)Period + Suppression,
                ClassicAverageMethod.Mama => 32L + Suppression,
                ClassicAverageMethod.T3 => 6L * (Period - 1) + Suppression,
                _ => Period - 1L,
            };

    /// <inheritdoc/>
    public override int WarmupBars => (int)Math.Min(int.MaxValue, First);

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(this, First);

    internal object CreateAlignedState(long first)
    {
        if (first < First)
            throw new ArgumentOutOfRangeException(nameof(first));
        return new State(this, first);
    }

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        ClassicAverageReference
                            .Values(bars, this)
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    internal SeededAdaptiveAverage Adaptive() =>
        new(Period, 2, 30, false, false, false, true, 1L + Suppression);

    private object Engine(long offset) =>
        Method switch
        {
            ClassicAverageMethod.Ema or ClassicAverageMethod.Dema or ClassicAverageMethod.Tema =>
                new ExponentialState(Period, Order, FirstPriceSeed, Suppression, offset),
            ClassicAverageMethod.Wma or ClassicAverageMethod.Trima => new WideClassicalWindowState(
                Period,
                Method
            ),
            ClassicAverageMethod.Kama => Adaptive().CreateState(),
            ClassicAverageMethod.Mama => new DelayedPhaseAdaptiveAverage(
                suppression: Suppression
            ).CreateState(),
            ClassicAverageMethod.T3 => new TillsonAverage(
                Period,
                .7,
                TillsonSeed.CascadedMeans,
                Suppression
            ).CreateState(),
            _ => new WideClassicalWindowState(Period, Method),
        };

    private sealed class State : IMultiOutputState, IDisposable, IWideAverageState
    {
        private readonly long _first,
            _offset;
        private readonly bool _exponential;
        private readonly IWideAverageState? _engine;
        private readonly double[] _scratch = new double[4];
        private long _index = -1;
        private BigInteger _lastValue;
        public BigInteger RoundedUnits => _engine?.RoundedUnits ?? _lastValue;

        internal State(ClassicMovingAverage owner, long first)
        {
            _first = first;
            _offset = first - owner.First;
            _exponential =
                owner.Method
                    is ClassicAverageMethod.Ema
                        or ClassicAverageMethod.Dema
                        or ClassicAverageMethod.Tema;
            _engine = owner.Period == 1 ? null : (IWideAverageState)owner.Engine(_offset);
        }

        public void Reset()
        {
            _index = -1;
            _lastValue = 0;
            _engine?.Reset();
            Array.Clear(_scratch, 0, 4);
        }

        public void Dispose()
        {
            if (_engine is IDisposable disposable)
                disposable.Dispose();
        }

        public void Update(in Bar bar, Span<double> output) =>
            UpdateUnits(ExactVarianceWindow.Units(bar.Close), output);

        public void UpdateUnits(BigInteger input, Span<double> output)
        {
            output.Clear();
            _index++;
            if (!_exponential && _index < _offset)
                return;
            _lastValue = input;
            _engine?.UpdateUnits(input, _scratch);
            if (_index < _first)
                return;
            output[0] = _engine is null ? ExactMeanAccumulator.UnitRatio(input, 1) : _scratch[0];
            output[1] = 1;
        }
    }

    private sealed class ExponentialState(
        int period,
        int order,
        bool first,
        int suppression,
        long offset
    ) : IWideAverageState
    {
        private BigInteger _combined;
        private long _index = -1;
        public BigInteger RoundedUnits => RocBankValue.RoundUnits(_combined, 1);
        private readonly BigInteger[] _sums = new BigInteger[order],
            _values = new BigInteger[order];
        private readonly long[] _counts = new long[order];

        public void Reset()
        {
            _combined = 0;
            _index = -1;
            Array.Clear(_sums, 0, order);
            Array.Clear(_values, 0, order);
            Array.Clear(_counts, 0, order);
        }

        public void UpdateUnits(BigInteger value, Span<double> output)
        {
            output.Clear();
            _index++;
            for (var j = 0; j < order; j++)
            {
                if (!(first && j == 0) && _index < offset + j * (period - 1L + suppression))
                    return;
                var count = ++_counts[j];
                if (first)
                    _values[j] =
                        count == 1
                            ? value
                            : RocBankValue.RoundUnits(
                                2 * value + (period - 1L) * _values[j],
                                period + 1L
                            );
                else if (count <= period)
                {
                    _sums[j] += value;
                    if (count == period)
                        _values[j] = RocBankValue.RoundUnits(_sums[j], period);
                }
                else
                    _values[j] = RocBankValue.RoundUnits(
                        2 * value + (period - 1L) * _values[j],
                        period + 1L
                    );
                if (count < (long)period + suppression)
                    return;
                value = _values[j];
            }
            var combined =
                order == 1 ? _values[0]
                : order == 2 ? 2 * _values[0] - _values[1]
                : 3 * (_values[0] - _values[1]) + _values[2];
            _combined = combined;
            output[0] = ExactMeanAccumulator.UnitRatio(combined, 1);
            output[1] = 1;
        }
    }
}
