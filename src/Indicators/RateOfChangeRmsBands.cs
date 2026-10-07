using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Percentage change, its seeded EMA, and symmetric root-mean-square return bands.</summary>
/// <remarks>Unavailable numeric outputs use zero placeholders and explicit validity flags.
/// A zero denominator makes ROC unavailable. Any missing ROC permanently invalidates the EMA;
/// the bands recover when a full deviation window is available again. Bands are RMS, not standard deviation.
/// Published ROC rounds once; EMA and bands use those rounded returns with overflow-safe arithmetic.</remarks>
public sealed class RateOfChangeRmsBands
    : MultiOutputIndicatorBase,
        IMomentumIndicator,
        IIndicatorValidationContract
{
    /// <summary>Creates ROC bands. Deviation period must not exceed the positive ROC lag.</summary>
    public RateOfChangeRmsBands(int period = 12, int emaPeriod = 3, int deviationPeriod = 3)
        : base(7)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (emaPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(emaPeriod));
        if (deviationPeriod < 1 || deviationPeriod > period)
            throw new ArgumentOutOfRangeException(nameof(deviationPeriod));
        Period = period;
        EmaPeriod = emaPeriod;
        DeviationPeriod = deviationPeriod;
        Roc = Outputs[0];
        RocIsDefined = Outputs[1];
        Ema = Outputs[2];
        EmaIsDefined = Outputs[3];
        UpperBand = Outputs[4];
        LowerBand = Outputs[5];
        BandsAreDefined = Outputs[6];
    }

    /// <summary>ROC lag.</summary>
    public int Period { get; }

    /// <summary>EMA seed and smoothing period.</summary>
    public int EmaPeriod { get; }

    /// <summary>RMS window length.</summary>
    public int DeviationPeriod { get; }

    /// <summary>Percentage change, or zero when unavailable.</summary>
    public IIndicatorOutput Roc { get; }

    /// <summary>One when ROC is defined; otherwise zero.</summary>
    public IIndicatorOutput RocIsDefined { get; }

    /// <summary>SMA-seeded EMA of returns, or zero when unavailable.</summary>
    public IIndicatorOutput Ema { get; }

    /// <summary>One when the EMA is defined; otherwise zero.</summary>
    public IIndicatorOutput EmaIsDefined { get; }

    /// <summary>Root mean square of the last returns, or zero when unavailable.</summary>
    public IIndicatorOutput UpperBand { get; }

    /// <summary>Negative root mean square, or zero when unavailable.</summary>
    public IIndicatorOutput LowerBand { get; }

    /// <summary>One when both bands are defined; otherwise zero.</summary>
    public IIndicatorOutput BandsAreDefined { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, EmaPeriod, DeviationPeriod);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 7)
            .Select(slot =>
                slot == 0
                    ? IndicatorValidationRule.ReferenceWithOverflowRejection(
                        slot,
                        bars => Reference(bars)[slot],
                        IndicatorErrorBudget.Exact
                    )
                    : IndicatorValidationRule.Reference(slot, bars => Reference(bars)[slot], 0, 0)
            );

    private double[][] Reference(IReadOnlyList<Bar> bars)
    {
        var output = Enumerable.Range(0, 7).Select(_ => new double[bars.Count]).ToArray();
        var emaPossible = true;
        for (var i = Period; i < bars.Count; i++)
        {
            if (bars[i - Period].Close != 0)
            {
                var previous = ReferenceFraction.FromDouble(bars[i - Period].Close);
                output[0][i] = (
                    (ReferenceFraction.FromDouble(bars[i].Close) - previous)
                    / previous
                    * new ReferenceFraction(100)
                ).ToDouble();
                output[1][i] = 1;
            }
            else
                emaPossible = false;
            // Execution rejects an unrepresentable published ROC before any later output is observed.
            if (double.IsInfinity(output[0][i]))
                break;
            var firstEma = (long)Period + EmaPeriod - 1;
            if (emaPossible && i >= firstEma)
            {
                var total = new ReferenceFraction(0);
                if (i == firstEma)
                {
                    for (var j = Period; j <= i; j++)
                        total += ReferenceFraction.FromDouble(output[0][j]);
                    output[2][i] = (total / new ReferenceFraction(EmaPeriod)).ToDouble();
                }
                else
                    output[2][i] = (
                        (
                            ReferenceFraction.FromDouble(output[0][i]) * new ReferenceFraction(2)
                            + ReferenceFraction.FromDouble(output[2][i - 1])
                                * new ReferenceFraction(EmaPeriod - 1)
                        ) / new ReferenceFraction((long)EmaPeriod + 1)
                    ).ToDouble();
                output[3][i] = 1;
            }
            if (i < (long)Period + DeviationPeriod - 1)
                continue;
            var squares = new ReferenceFraction(0);
            var complete = true;
            for (var j = i - DeviationPeriod + 1; j <= i; j++)
            {
                if (output[1][j] == 0)
                {
                    complete = false;
                    break;
                }
                var value = ReferenceFraction.FromDouble(output[0][j]);
                squares += value * value;
            }
            if (!complete)
                continue;
            output[4][i] = (squares / new ReferenceFraction(DeviationPeriod)).SqrtToDouble();
            output[5][i] = -output[4][i];
            output[6][i] = 1;
        }
        return output;
    }

    private sealed class State(int period, int emaPeriod, int deviationPeriod) : IMultiOutputState
    {
        private readonly Queue<double> _prices = new();
        private readonly Queue<(double Value, bool Present)> _returns = new();
        private ExactMeanAccumulator _seed,
            _squares;
        private double _ema;
        private int _seedCount,
            _missing;
        private bool _emaPossible = true;

        public void Reset()
        {
            _prices.Clear();
            _returns.Clear();
            _seed = _squares = default;
            _ema = 0;
            _seedCount = _missing = 0;
            _emaPossible = true;
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            if (_prices.Count < period)
            {
                _prices.Enqueue(bar.Close);
                return;
            }
            var previous = _prices.Dequeue();
            _prices.Enqueue(bar.Close);
            var present = previous != 0;
            var roc = present ? RoundedPercentageChange.Of(bar.Close, previous) : 0;
            outputs[0] = roc;
            outputs[1] = present ? 1 : 0;
            if (double.IsInfinity(roc))
                return;
            UpdateEma(roc, present, outputs);
            if (_returns.Count == deviationPeriod)
            {
                var old = _returns.Dequeue();
                if (old.Present)
                    _squares.AddProduct(old.Value, old.Value, -1);
                else
                    _missing--;
            }
            _returns.Enqueue((roc, present));
            if (present)
                _squares.AddProduct(roc, roc);
            else
                _missing++;
            if (_returns.Count != deviationPeriod || _missing != 0)
                return;
            outputs[4] = _squares.SqrtMean(deviationPeriod);
            outputs[5] = -outputs[4];
            outputs[6] = 1;
        }

        private void UpdateEma(double roc, bool present, Span<double> outputs)
        {
            _emaPossible &= present;
            if (!_emaPossible)
                return;
            if (_seedCount < emaPeriod)
            {
                _seed.Add(roc);
                if (++_seedCount < emaPeriod)
                    return;
                _ema = _seed.Mean(emaPeriod);
            }
            else
            {
                var sum = new ExactMeanAccumulator();
                sum.Add(roc, 2);
                sum.Add(_ema, emaPeriod - 1);
                _ema = sum.Mean((long)emaPeriod + 1);
            }
            outputs[2] = _ema;
            outputs[3] = 1;
        }
    }
}
