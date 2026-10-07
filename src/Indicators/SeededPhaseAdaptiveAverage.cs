using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Six-price-seeded phase-adaptive mother and following averages.</summary>
/// <remarks>Hilbert stages start at index six with zero filter history. The seed
/// is the first six prices' mean; optional startup publishes expanding means.
/// FIR sums, corrections, phasor differences, homodyne products, smoothing and
/// convex output updates round at their logical stage with extended upper
/// exponents. Period/phase controls use binary64 operations and Math.Atan of a
/// rounded exact ratio. Zero phase/period inputs normally use zero; the optional
/// older-value mode uses their values from two bars earlier. All history is bounded.</remarks>
public sealed class SeededPhaseAdaptiveAverage
    : MultiOutputIndicatorBase,
        IMovingAverage,
        IIndicatorValidationContract
{
    /// <summary>Creates finite limits 0 &lt; slowLimit &lt;= fastLimit &lt;= 1.</summary>
    public SeededPhaseAdaptiveAverage(
        double fastLimit = .5,
        double slowLimit = .05,
        bool publishStartup = false,
        bool retainOlderZeroPhase = false,
        bool useMidpoint = false
    )
        : base(4)
    {
        if (!FrameworkCompatibility.IsFinite(fastLimit) || fastLimit <= 0 || fastLimit > 1)
            throw new ArgumentOutOfRangeException(nameof(fastLimit));
        if (!FrameworkCompatibility.IsFinite(slowLimit) || slowLimit <= 0 || slowLimit > fastLimit)
            throw new ArgumentOutOfRangeException(nameof(slowLimit));
        FastLimit = fastLimit;
        SlowLimit = slowLimit;
        PublishStartup = publishStartup;
        RetainOlderZeroPhase = retainOlderZeroPhase;
        UseMidpoint = useMidpoint;
    }

    /// <summary>Upper adaptive gain.</summary>
    public double FastLimit { get; }

    /// <summary>Lower adaptive gain.</summary>
    public double SlowLimit { get; }

    /// <summary>Whether expanding means are published during the first five bars.</summary>
    public bool PublishStartup { get; }

    /// <summary>Whether zero phase inputs use two-bar-old period/phase values.</summary>
    public bool RetainOlderZeroPhase { get; }

    /// <summary>Whether to use the rounded high/low midpoint instead of close.</summary>
    public bool UseMidpoint { get; }

    /// <inheritdoc/>
    public override int WarmupBars => PublishStartup ? 0 : 5;

    /// <summary>Mother average, or zero before publication.</summary>
    public IIndicatorOutput Mama => Outputs[0];

    /// <summary>Following average, or zero before publication.</summary>
    public IIndicatorOutput Fama => Outputs[1];

    /// <summary>Mother-average presence.</summary>
    public IIndicatorOutput MamaIsDefined => Outputs[2];

    /// <summary>Following-average presence.</summary>
    public IIndicatorOutput FamaIsDefined => Outputs[3];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(
            new Configuration(
                FastLimit,
                SlowLimit,
                PublishStartup,
                RetainOlderZeroPhase,
                UseMidpoint
            )
        );

    internal static object CreateDelayedState(double fast, double slow, int suppression) =>
        new State(new Configuration(fast, slow, false, false, false, true, suppression));

    private sealed record Configuration(
        double FastLimit,
        double SlowLimit,
        bool PublishStartup,
        bool RetainOlderZeroPhase,
        bool UseMidpoint,
        bool DelayedZeroSeed = false,
        int Suppression = 0
    );

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 4)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        SeededPhaseReference
                            .Values(bars, this)[slot % 2]
                            .Select(v =>
                                slot < 2 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(Configuration owner) : IMultiOutputState, IWideAverageState
    {
        private static readonly BigInteger Grid = BigInteger.One << 1074;

        private static BigInteger U(double value) => ExactVarianceWindow.Units(value);

        private static BigInteger Round(BigInteger n, BigInteger d) =>
            RocBankValue.RoundUnits(n, d);

        private readonly HilbertPhaseState _hilbert = new(
            owner.DelayedZeroSeed ? 12 : 6,
            owner.DelayedZeroSeed,
            owner.RetainOlderZeroPhase
        );
        private BigInteger _mama,
            _fama,
            _seed;
        private double _phase;
        private long _index = -1;
        public BigInteger RoundedUnits => _mama;

        public void Reset()
        {
            _hilbert.Reset();
            _mama = _fama = _seed = 0;
            _phase = 0;
            _index = -1;
        }

        private static BigInteger Adaptive(BigInteger value, BigInteger old, double alpha) =>
            Round(value * U(alpha) + old * (Grid - U(alpha)), Grid);

        public void Update(in Bar bar, Span<double> output) =>
            UpdateUnits(
                owner.UseMidpoint ? Round(U(bar.High) + U(bar.Low), 2) : U(bar.Close),
                output
            );

        public void UpdateUnits(BigInteger price, Span<double> output)
        {
            output.Clear();
            _index++;
            _hilbert.Update(price);
            if (_index < (owner.DelayedZeroSeed ? 12 : 6))
            {
                if (!owner.DelayedZeroSeed)
                {
                    _seed += price;
                    _mama = _fama = Round(_seed, _index + 1);
                }
            }
            else
            {
                var phase = _hilbert.Phase;
                var delta = Math.Max(_phase - phase, 1);
                var alpha =
                    owner.DelayedZeroSeed && delta <= 1
                        ? owner.FastLimit
                        : Math.Max(owner.FastLimit / delta, owner.SlowLimit);
                _mama = Adaptive(price, _mama, alpha);
                _fama = Adaptive(_mama, _fama, .5 * alpha);
                _phase = phase;
            }
            if (
                _index
                >= (
                    owner.DelayedZeroSeed ? 32L + owner.Suppression
                    : owner.PublishStartup ? 0
                    : 5
                )
            )
            {
                output[0] = ExactMeanAccumulator.UnitRatio(_mama, 1);
                output[1] = ExactMeanAccumulator.UnitRatio(_fama, 1);
                output[2] = output[3] = 1;
            }
        }
    }
}
