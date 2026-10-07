using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Population-deviation bands around any classical moving-average formula.</summary>
/// <remarks>The center follows ClassicMovingAverage's startup and rounding. Deviation uses
/// the complete current price window independently of the center. Publication requires both.
/// Each scaled rounded deviation and final band rounds once, with extended upper exponents
/// before publication. Separate nonnegative factors control upper and lower widths.
/// Slots zero through two are upper, middle, lower; slots three through five are their flags.</remarks>
public sealed class ClassicDeviationBands : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a positive period, finite nonnegative widths and explicit averaging startup.</summary>
    public ClassicDeviationBands(
        int period = 5,
        double upperFactor = 2,
        double lowerFactor = 2,
        ClassicAverageMethod method = ClassicAverageMethod.Sma,
        bool firstPriceSeed = false,
        int suppression = 0
    )
        : base(6)
    {
        if (!double.IsFinite(upperFactor) || upperFactor < 0)
            throw new ArgumentOutOfRangeException(nameof(upperFactor));
        if (!double.IsFinite(lowerFactor) || lowerFactor < 0)
            throw new ArgumentOutOfRangeException(nameof(lowerFactor));
        Average = new(period, method, firstPriceSeed, suppression);
        Period = period;
        UpperFactor = upperFactor;
        LowerFactor = lowerFactor;
        Method = method;
        FirstPriceSeed = firstPriceSeed;
        Suppression = suppression;
    }

    /// <summary>Price-deviation window and requested average period.</summary>
    public int Period { get; }

    /// <summary>Nonnegative upper deviation scale.</summary>
    public double UpperFactor { get; }

    /// <summary>Nonnegative lower deviation scale.</summary>
    public double LowerFactor { get; }

    /// <summary>Middle-band formula.</summary>
    public ClassicAverageMethod Method { get; }

    /// <summary>Exponential first-price compatibility option.</summary>
    public bool FirstPriceSeed { get; }

    /// <summary>Extra averaging startup suppression.</summary>
    public int Suppression { get; }

    /// <summary>Center plus scaled deviation.</summary>
    public IIndicatorOutput Upper => Outputs[0];

    /// <summary>Classical average aligned with the current deviation window.</summary>
    public IIndicatorOutput Middle => Outputs[1];

    /// <summary>Center minus scaled deviation.</summary>
    public IIndicatorOutput Lower => Outputs[2];
    internal ClassicMovingAverage Average { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(Period - 1, Average.WarmupBars);

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(this);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 6)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        ClassicDeviationBandsReference
                            .Values(bars, this)[slot % 3]
                            .Select(v =>
                                slot < 3 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State : IMultiOutputState, IDisposable
    {
        private static readonly BigInteger Grid = BigInteger.One << 1074;
        private readonly IMultiOutputState _average;
        private readonly WindowDispersion.State _deviation;
        private readonly int _period;
        private readonly BigInteger _upper,
            _lower;
        private readonly double[] _middle = new double[2];

        internal State(ClassicDeviationBands owner)
        {
            _average = (IMultiOutputState)owner.Average.CreateState();
            _deviation = new(owner.Period, WindowDispersionOutput.StandardDeviation, false, 1);
            _period = owner.Period;
            _upper = ExactVarianceWindow.Units(owner.UpperFactor);
            _lower = ExactVarianceWindow.Units(owner.LowerFactor);
        }

        public void Reset()
        {
            _average.Reset();
            _deviation.Reset();
            Array.Clear(_middle, 0, 2);
        }

        public void Dispose()
        {
            if (_average is IDisposable disposable)
                disposable.Dispose();
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _average.Update(bar, _middle);
            var deviation = _deviation.Update(bar);
            if (_middle[1] == 0 || _deviation.Count < _period)
                return;
            var center = ((IRoundedAverageUnits)_average).RoundedUnits;
            var spread = ExactVarianceWindow.Units(deviation);
            output[0] = ExactMeanAccumulator.UnitRatio(
                center + RocBankValue.RoundUnits(_upper * spread, Grid),
                1
            );
            output[1] = ExactMeanAccumulator.UnitRatio(center, 1);
            output[2] = ExactMeanAccumulator.UnitRatio(
                center - RocBankValue.RoundUnits(_lower * spread, Grid),
                1
            );
            output[3] = output[4] = output[5] = 1;
        }
    }
}
