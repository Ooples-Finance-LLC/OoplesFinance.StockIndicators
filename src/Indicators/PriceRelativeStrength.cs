using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Evaluation/base price ratio, optional ratio mean and difference of fractional returns.</summary>
/// <remarks>Fields select synchronized evaluation and base series from each bar.
/// Zero base prices make the ratio absent; a mean requires a complete present
/// window. Returns need nonzero prior evaluation and base prices. Each complete
/// ratio/return expression rounds once; the mean uses rounded ratio values.
/// Histories grow lazily and final overflow is rejected.</remarks>
public sealed class PriceRelativeStrength : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a relative-strength calculation with optional positive lookback and mean periods.</summary>
    public PriceRelativeStrength(
        int? period = null,
        int? meanPeriod = null,
        CandlePriceField evaluation = CandlePriceField.Close,
        CandlePriceField basis = CandlePriceField.Open
    )
        : base(6)
    {
        if (period is <= 0)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (meanPeriod is <= 0)
            throw new ArgumentOutOfRangeException(nameof(meanPeriod));
        PairStatisticsWindow.Validate(1, basis, evaluation);
        Period = period;
        MeanPeriod = meanPeriod;
        Evaluation = evaluation;
        Basis = basis;
    }

    /// <summary>Optional return lookback.</summary>
    public int? Period { get; }

    /// <summary>Optional ratio-mean period.</summary>
    public int? MeanPeriod { get; }

    /// <summary>Evaluation price field.</summary>
    public CandlePriceField Evaluation { get; }

    /// <summary>Base price field.</summary>
    public CandlePriceField Basis { get; }

    /// <summary>Ratio or zero when absent.</summary>
    public IIndicatorOutput Ratio => Outputs[0];

    /// <summary>Mean of ratios or zero when absent.</summary>
    public IIndicatorOutput Mean => Outputs[1];

    /// <summary>Difference of fractional returns or zero when absent.</summary>
    public IIndicatorOutput ReturnDifference => Outputs[2];

    /// <summary>Ratio presence.</summary>
    public IIndicatorOutput RatioIsDefined => Outputs[3];

    /// <summary>Mean presence.</summary>
    public IIndicatorOutput MeanIsDefined => Outputs[4];

    /// <summary>Return-difference presence.</summary>
    public IIndicatorOutput ReturnDifferenceIsDefined => Outputs[5];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, MeanPeriod, Evaluation, Basis);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 6)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        PriceRelativeReference
                            .Values(bars, Period, MeanPeriod, Evaluation, Basis)[slot % 3]
                            .Select(v =>
                                slot < 3 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(
        int? period,
        int? mean,
        CandlePriceField evaluation,
        CandlePriceField basis
    ) : IMultiOutputState
    {
        private readonly Queue<(BigInteger Eval, BigInteger Base)> _history = new();
        private readonly Queue<double?> _ratios = new();
        private BigInteger _sum;
        private int _missing;

        public void Reset()
        {
            _history.Clear();
            _ratios.Clear();
            _sum = 0;
            _missing = 0;
        }

        private static double Divide(BigInteger n, BigInteger d) =>
            ExactMeanAccumulator.UnitRatio((n * d.Sign) << 1074, BigInteger.Abs(d));

        private static void Put(double? v, int slot, Span<double> output)
        {
            if (v.HasValue)
            {
                output[slot] = v.Value;
                output[slot + 3] = 1;
            }
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var e = ExactVarianceWindow.Units(PairStatisticsWindow.Select(bar, evaluation));
            var b = ExactVarianceWindow.Units(PairStatisticsWindow.Select(bar, basis));
            double? ratio = b.IsZero ? null : Divide(e, b);
            Put(ratio, 0, output);
            if (ratio.HasValue && !double.IsFinite(ratio.Value))
                return;
            if (mean.HasValue)
            {
                if (_ratios.Count == mean.Value)
                {
                    var old = _ratios.Dequeue();
                    if (old.HasValue)
                        _sum -= ExactVarianceWindow.Units(old.Value);
                    else
                        _missing--;
                }
                _ratios.Enqueue(ratio);
                if (ratio.HasValue)
                    _sum += ExactVarianceWindow.Units(ratio.Value);
                else
                    _missing++;
                if (_ratios.Count == mean.Value && _missing == 0)
                    Put(ExactMeanAccumulator.UnitRatio(_sum, mean.Value), 1, output);
            }
            if (!period.HasValue)
                return;
            if (_history.Count == period.Value)
            {
                var old = _history.Dequeue();
                if (!old.Eval.IsZero && !old.Base.IsZero)
                    Put(Divide(e * old.Base - b * old.Eval, old.Eval * old.Base), 2, output);
            }
            _history.Enqueue((e, b));
        }
    }
}
