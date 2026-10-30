using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Retrospective fitted channels in complete blocks aligned to the final observation.</summary>
/// <remarks>Appending bars can repaint every block. Only finite sources are supported.
/// A null period fits the whole input, which must contain at least two bars. An explicit
/// period leaves the leading incomplete block absent, with zero values and presence flags.</remarks>
public sealed class RegressionWithChannels : MultiOutputIndicatorBase, IHistoricalIndicator, IIndicatorValidationContract
{
    /// <summary>Creates channels with a period of at least two, or null for the whole input.</summary>
    public RegressionWithChannels(int? period = 20, double deviations = 2) : base(7)
    {
        if (period is < 2) throw new ArgumentOutOfRangeException(nameof(period));
        if (!FrameworkCompatibility.IsFinite(deviations) || deviations <= 0)
            throw new ArgumentOutOfRangeException(nameof(deviations));
        Period = period; Deviations = deviations;
    }
    /// <summary>Block length, or null for a whole-input fit.</summary>
    public int? Period { get; }
    /// <summary>Positive multiplier applied to the rounded population deviation.</summary>
    public double Deviations { get; }
    /// <summary>Fitted center at each row in a complete block.</summary>
    public IIndicatorOutput Centerline => Outputs[0];
    /// <summary>Center plus the rounded, extended-exponent width.</summary>
    public IIndicatorOutput UpperChannel => Outputs[1];
    /// <summary>Center minus the rounded, extended-exponent width.</summary>
    public IIndicatorOutput LowerChannel => Outputs[2];
    /// <summary>One at the beginning of each complete block, otherwise zero.</summary>
    public IIndicatorOutput BreakPoint => Outputs[3];
    /// <summary>One inside a complete block.</summary>
    public IIndicatorOutput CenterlineIsDefined => Outputs[4];
    /// <summary>One inside a complete block.</summary>
    public IIndicatorOutput UpperChannelIsDefined => Outputs[5];
    /// <summary>One inside a complete block.</summary>
    public IIndicatorOutput LowerChannelIsDefined => Outputs[6];
    /// <inheritdoc/>
    public override IIndicatorOutput PrimaryOutput => Centerline;
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules => Enumerable.Range(0, 7).Select(slot =>
        IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
            bars => Reference(bars, Period, Deviations)[slot], IndicatorErrorBudget.Exact));

    double[][] IHistoricalIndicator.CalculateHistory(IReadOnlyList<Bar> bars)
    {
        var values = Enumerable.Range(0, 7).Select(slot => slot >= 5 ? Array.Empty<double>() : new double[bars.Count]).ToArray();
        values[5] = values[6] = values[4];
        using var writer = new Writer(Period, Deviations, bars.Count);
        Span<double> scratch = stackalloc double[10];
        for (int i = 0; i < bars.Count; i++) writer.Append(bars[i], i, values, scratch, default);
        writer.Complete();
        return values;
    }

    internal sealed class Writer : IDisposable
    {
        private readonly int _length, _leading;
        private readonly double _deviations;
        private readonly WindowRegressionStatistics.State? _window;
        internal Writer(int? period, double deviations, int count)
        {
            _length = period ?? count;
            _leading = _length < 2 ? count : count % _length;
            _deviations = deviations;
            if (_length >= 2 && count >= _length) _window = new(_length);
        }
        internal void Append(in Bar bar, int index, double[][] values, Span<double> scratch, CancellationToken cancellation)
        {
            if (_window is null || index < _leading) return;
            if ((index - _leading + 1) % _length != 0)
            {
                _window.Accumulate(bar);
                return;
            }
            _window.Update(bar, scratch);
            var width = JurikDyadic.FromDouble(scratch[2]);
            width.Multiply(_deviations);
            width = width.Rounded();
            int start = index - _length + 1;
            for (int j = 0; j < _length; j++)
            {
                cancellation.ThrowIfCancellationRequested();
                int row = start + j;
                double center = _window.LineAt(j);
                values[0][row] = center;
                values[3][row] = j == 0 ? 1 : 0;
                values[4][row] = values[5][row] = values[6][row] = 1;
                if (!FrameworkCompatibility.IsFinite(center))
                {
                    // Output policy reports the nonfinite center after raw input
                    // validation; do not feed it back into exact arithmetic.
                    values[1][row] = values[2][row] = center;
                    continue;
                }
                var upper = JurikDyadic.FromDouble(center);
                upper.AddExact(width);
                var lower = JurikDyadic.FromDouble(center);
                lower.Subtract(width);
                values[1][row] = upper.Mean(1);
                values[2][row] = lower.Mean(1);
            }
            _window.Reset();
        }
        internal void Complete()
        {
            if (_length < 2) throw new ArgumentOutOfRangeException("period", "A whole-input fit requires at least two bars.");
        }
        public void Dispose() => _window?.Dispose();
    }

    private static double[][] Reference(IReadOnlyList<Bar> bars, int? period, double deviations)
    {
        int length = period ?? bars.Count;
        if (length < 2) throw new ArgumentOutOfRangeException(nameof(period));
        var result = Enumerable.Range(0, 7).Select(_ => new double[bars.Count]).ToArray();
        var n = new ReferenceFraction(length);
        var centerX = new ReferenceFraction(length - 1L) / new ReferenceFraction(2);
        for (int end = bars.Count; end >= length; end -= length)
        {
            int start = end - length;
            var mean = new ReferenceFraction(0);
            for (int j = start; j < end; j++) mean += ReferenceFraction.FromDouble(bars[j].Close);
            mean /= n;
            var xx = new ReferenceFraction(0);
            var yy = new ReferenceFraction(0);
            var xy = new ReferenceFraction(0);
            for (int j = 0; j < length; j++)
            {
                var x = new ReferenceFraction(j) - centerX;
                var y = ReferenceFraction.FromDouble(bars[start + j].Close) - mean;
                xx += x * x; yy += y * y; xy += x * y;
            }
            var slope = xy / xx;
            var width = (ReferenceFraction.FromDouble((yy / n).SqrtToDouble())
                * ReferenceFraction.FromDouble(deviations)).RoundExtendedBinary64();
            for (int j = 0; j < length; j++)
            {
                int row = start + j;
                double center = (mean + slope * (new ReferenceFraction(j) - centerX)).ToDouble();
                result[0][row] = center;
                result[3][row] = j == 0 ? 1 : 0;
                result[4][row] = result[5][row] = result[6][row] = 1;
                result[1][row] = FrameworkCompatibility.IsFinite(center) ? (ReferenceFraction.FromDouble(center) + width).ToDouble() : center;
                result[2][row] = FrameworkCompatibility.IsFinite(center) ? (ReferenceFraction.FromDouble(center) - width).ToDouble() : center;
            }
        }
        return result;
    }
}
