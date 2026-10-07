using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Complete-window population-deviation bands with position, Z-score and relative width.</summary>
/// <remarks>Mean and deviation round once; their scaled deviation rounds with extended upper
/// exponents. Bands and ratios use that center and offset before band publication rounding,
/// preserving narrow-band ratios. Z-score uses the exact window distribution. Signed factors
/// reverse band orientation. Flat/zero-factor position and zero-center width are absent.
/// Each of the six outputs has a matching presence output at slot +6. History grows lazily.</remarks>
public sealed class WindowDeviationBands : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a positive-period window with a finite signed deviation factor.</summary>
    public WindowDeviationBands(int period = 20, double factor = 2, bool widthAsPercent = false)
        : base(12)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!double.IsFinite(factor))
            throw new ArgumentOutOfRangeException(nameof(factor));
        Period = period;
        Factor = factor;
        WidthAsPercent = widthAsPercent;
    }

    /// <summary>Required observations.</summary>
    public int Period { get; }

    /// <summary>Signed number of population deviations.</summary>
    public double Factor { get; }

    /// <summary>Whether relative width is multiplied by one hundred.</summary>
    public bool WidthAsPercent { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;

    /// <summary>Rounded window mean.</summary>
    public IIndicatorOutput Middle => Outputs[0];

    /// <summary>Mean plus scaled deviation.</summary>
    public IIndicatorOutput Upper => Outputs[1];

    /// <summary>Mean minus scaled deviation.</summary>
    public IIndicatorOutput Lower => Outputs[2];

    /// <summary>Position measured from the lower band in band-width units.</summary>
    public IIndicatorOutput PercentB => Outputs[3];

    /// <summary>Exact-distribution standardized current close.</summary>
    public IIndicatorOutput ZScore => Outputs[4];

    /// <summary>Band width divided by mean, optionally in percent.</summary>
    public IIndicatorOutput Width => Outputs[5];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(this);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 12)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        WindowDeviationBandsReference
                            .Values(bars, this)[slot % 6]
                            .Select(v =>
                                slot < 6 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(WindowDeviationBands owner) : IMultiOutputState
    {
        private static readonly BigInteger Grid = BigInteger.One << 1074;
        private readonly WindowDispersion.State _window = new(
            owner.Period,
            WindowDispersionOutput.StandardDeviation,
            false,
            1
        );
        private readonly BigInteger _factor = ExactVarianceWindow.Units(owner.Factor);

        public void Reset() => _window.Reset();

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var deviation = _window.Update(bar);
            if (_window.Count < owner.Period)
                return;
            var center = ExactVarianceWindow.Units(_window.Mean);
            var half = RocBankValue.RoundUnits(
                _factor * ExactVarianceWindow.Units(deviation),
                Grid
            );
            output[0] = _window.Mean;
            output[1] = ExactMeanAccumulator.UnitRatio(center + half, 1);
            output[2] = ExactMeanAccumulator.UnitRatio(center - half, 1);
            output[6] = output[7] = output[8] = 1;
            if (!half.IsZero)
            {
                output[3] = Ratio(
                    (ExactVarianceWindow.Units(bar.Close) - center + half) * Grid,
                    2 * half
                );
                output[9] = 1;
            }
            if (_window.HasVariance)
            {
                output[4] = _window.Reading(WindowDispersionOutput.ZScore);
                output[10] = 1;
            }
            if (!center.IsZero)
            {
                output[5] = Ratio(2 * half * Grid * (owner.WidthAsPercent ? 100 : 1), center);
                output[11] = 1;
            }
        }

        private static double Ratio(BigInteger n, BigInteger d) =>
            d.Sign < 0
                ? ExactMeanAccumulator.UnitRatio(-n, -d)
                : ExactMeanAccumulator.UnitRatio(n, d);
    }
}
