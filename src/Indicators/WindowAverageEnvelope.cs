using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Centerline formulas supported by moving-average percentage envelopes.</summary>
public enum EnvelopeAverage
{
    /// <summary>Full-window Arnaud Legoux average with offset .85 and sigma 6.</summary>
    Alma,

    /// <summary>Double EMA with a shared SMA seed.</summary>
    Dema,

    /// <summary>SMA-seeded exponential average.</summary>
    Ema,

    /// <summary>Least-squares regression endpoint.</summary>
    Epma,

    /// <summary>Full-window Hull average with floor half/root periods.</summary>
    Hma,

    /// <summary>Simple arithmetic average.</summary>
    Sma,

    /// <summary>SMA-seeded Wilder average.</summary>
    Smma,

    /// <summary>Triple EMA with a shared SMA seed.</summary>
    Tema,

    /// <summary>Linear weighted average.</summary>
    Wma,
}

/// <summary>A centerline and percentage envelopes around one of nine moving averages.</summary>
/// <remarks>Upper=center*(100+percent)/100 and Lower=center*(100-percent)/100,
/// each rounded once from the published centerline. Negative centers retain these labels
/// without sorting. All outputs are absent until the selected average's full startup.
/// The centerline uses existing average states; histories grow lazily.</remarks>
public sealed class WindowAverageEnvelope : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates envelopes with a positive finite percentage and a valid average period.</summary>
    public WindowAverageEnvelope(
        int period = 14,
        double percentOffset = 2.5,
        EnvelopeAverage average = EnvelopeAverage.Sma
    )
        : base(4)
    {
        if (!Enum.IsDefined(typeof(EnvelopeAverage), average))
            throw new ArgumentOutOfRangeException(nameof(average));
        if (
            period
            < (
                average is EnvelopeAverage.Alma or EnvelopeAverage.Hma or EnvelopeAverage.Epma
                    ? 2
                    : 1
            )
        )
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!FrameworkCompatibility.IsFinite(percentOffset) || percentOffset <= 0)
            throw new ArgumentOutOfRangeException(nameof(percentOffset));
        Period = period;
        PercentOffset = percentOffset;
        Average = average;
    }

    /// <summary>Base lookback period.</summary>
    public int Period { get; }

    /// <summary>Positive percentage distance from the centerline.</summary>
    public double PercentOffset { get; }

    /// <summary>Centerline formula.</summary>
    public EnvelopeAverage Average { get; }

    /// <summary>Published moving average.</summary>
    public IIndicatorOutput Centerline => Outputs[0];

    /// <summary>Centerline plus its percentage offset.</summary>
    public IIndicatorOutput UpperEnvelope => Outputs[1];

    /// <summary>Centerline minus its percentage offset.</summary>
    public IIndicatorOutput LowerEnvelope => Outputs[2];

    /// <summary>One when all outputs are present; otherwise zero.</summary>
    public IIndicatorOutput IsDefined => Outputs[3];
    private long First =>
        (long)Period - 1 + (Average == EnvelopeAverage.Hma ? (int)Math.Sqrt(Period) - 1 : 0);

    /// <inheritdoc/>
    public override int WarmupBars => (int)Math.Min(int.MaxValue, First);

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, PercentOffset, Average, First);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 4)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars => Reference(bars)[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    private double[][] Reference(IReadOnlyList<Bar> bars)
    {
        var center = ReferenceCenter(bars);
        var output = Enumerable.Range(0, 4).Select(_ => new double[bars.Count]).ToArray();
        var hundred = new ReferenceFraction(100);
        var percent = ReferenceFraction.FromDouble(PercentOffset);
        for (var i = First; i < bars.Count; i++)
        {
            output[0][i] = center[(int)i];
            output[3][i] = 1;
            if (!FrameworkCompatibility.IsFinite(center[(int)i]))
            {
                output[1][i] = output[2][i] = center[(int)i];
                continue;
            }
            var value = ReferenceFraction.FromDouble(center[(int)i]);
            output[1][i] = (value * (hundred + percent) / hundred).ToDouble();
            output[2][i] = (value * (hundred - percent) / hundred).ToDouble();
        }
        return output;
    }

    private IReadOnlyList<double> ReferenceCenter(IReadOnlyList<Bar> bars) =>
        Average switch
        {
            EnvelopeAverage.Alma => new ArnaudLegouxWindow(Period).Reference(bars)[0],
            EnvelopeAverage.Hma => new HullWindow(Period).Reference(bars)[0],
            EnvelopeAverage.Dema => new SeededExponentialAverage(Period, 2).Reference(bars),
            EnvelopeAverage.Tema => new SeededExponentialAverage(Period, 3).Reference(bars),
            EnvelopeAverage.Smma => new WilderMovingAverage(Period).Reference(bars),
            EnvelopeAverage.Epma => new WindowLinearRegression(Period).Reference(bars),
            EnvelopeAverage.Ema => EmaReference(bars),
            _ => NormalizedKernelReference.Values(
                bars,
                Period,
                lag => new ReferenceFraction(Average == EnvelopeAverage.Wma ? Period - lag : 1)
            ),
        };

    private double[] EmaReference(IReadOnlyList<Bar> bars)
    {
        var result = new double[bars.Count];
        var seed = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var value = ReferenceFraction.FromDouble(bars[i].Close);
            if (i < Period)
            {
                seed += value;
                result[i] = (seed / new ReferenceFraction(i + 1)).ToDouble();
            }
            else
                result[i] = (
                    (
                        new ReferenceFraction(2) * value
                        + new ReferenceFraction(Period - 1)
                            * ReferenceFraction.FromDouble(result[i - 1])
                    ) / new ReferenceFraction((long)Period + 1)
                ).ToDouble();
        }
        return result;
    }

    private static object AverageState(int period, EnvelopeAverage average) =>
        average switch
        {
            EnvelopeAverage.Alma => new ArnaudLegouxWindow(period).CreateState(),
            EnvelopeAverage.Hma => new HullWindow(period).CreateState(),
            EnvelopeAverage.Dema => new SeededExponentialAverage(period, 2).CreateState(),
            EnvelopeAverage.Tema => new SeededExponentialAverage(period, 3).CreateState(),
            EnvelopeAverage.Smma => new WilderMovingAverage(period).CreateState(),
            EnvelopeAverage.Epma => new WindowLinearRegression(period).CreateState(),
            EnvelopeAverage.Wma => new FixedPeriodWma(period).CreateState(),
            _ => new PlainMean(period, average == EnvelopeAverage.Ema),
        };

    private sealed class State(int period, double percent, EnvelopeAverage average, long first)
        : IMultiOutputState,
            IDisposable
    {
        private readonly object _average = AverageState(period, average);
        private long _count;

        public void Reset()
        {
            _count = 0;
            if (_average is IIndicatorState single)
                single.Reset();
            else
                ((IMultiOutputState)_average).Reset();
        }

        public void Dispose() => (_average as IDisposable)?.Dispose();

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            double center;
            if (_average is IIndicatorState single)
                center = single.Update(bar);
            else
            {
                Span<double> values = stackalloc double[2];
                ((IMultiOutputState)_average).Update(bar, values);
                center = values[0];
            }
            if (_count < first)
            {
                _count++;
                return;
            }
            output[0] = center;
            output[3] = 1;
            if (!FrameworkCompatibility.IsFinite(center))
                return; // The enclosing runtime rejects the nonfinite centerline.
            var upper = new ExactMeanAccumulator();
            upper.Add(center, 100);
            upper.AddProduct(center, percent);
            var lower = new ExactMeanAccumulator();
            lower.Add(center, 100);
            lower.AddProduct(center, percent, -1);
            output[1] = upper.Mean(100);
            output[2] = lower.Mean(100);
        }
    }

    private sealed class PlainMean(int period, bool exponential) : IIndicatorState
    {
        private readonly EmaState? _ema = exponential ? new EmaState(period) : null;
        private readonly Queue<double> _window = new();
        private ExactMeanAccumulator _sum;

        public void Reset()
        {
            _ema?.Reset();
            _window.Clear();
            _sum = default;
        }

        public double Update(in Bar bar)
        {
            if (_ema is not null)
                return _ema.GetNext(bar.Close, true);
            if (_window.Count == period)
                _sum.Add(_window.Dequeue(), -1);
            _window.Enqueue(bar.Close);
            _sum.Add(bar.Close);
            return _sum.Mean(_window.Count);
        }
    }
}
