using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Full-window Pearson correlation of two selected candle fields.</summary>
/// <remarks>Exact centered moments avoid overflow and false flat windows. The
/// final coefficient rounds once, even when individual variances exceed binary64.
/// Startup is absent. Flat windows are absent by default, or zero when requested.</remarks>
public sealed class WindowCorrelation : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a correlation between close and open, or independently selected fields.</summary>
    public WindowCorrelation(
        int period = 20,
        CandlePriceField left = CandlePriceField.Close,
        CandlePriceField right = CandlePriceField.Open,
        bool flatZero = false
    )
        : base(2)
    {
        PairStatisticsWindow.Validate(period, left, right);
        Period = period;
        Left = left;
        Right = right;
        FlatZero = flatZero;
    }

    /// <summary>Positive full-window length.</summary>
    public int Period { get; }

    /// <summary>First series field.</summary>
    public CandlePriceField Left { get; }

    /// <summary>Second series field.</summary>
    public CandlePriceField Right { get; }

    /// <summary>Whether zero replaces undefined flat-window correlation.</summary>
    public bool FlatZero { get; }

    /// <summary>Correlation, or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One for a defined coefficient.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, Left, Right, FlatZero);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars =>
                        PairStatisticsReference
                            .Calculate(bars, Period, Left, Right, FlatZero)[0]
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(
        int period,
        CandlePriceField left,
        CandlePriceField right,
        bool flatZero
    ) : IMultiOutputState
    {
        private readonly PairStatisticsWindow _window = new(period);

        public void Reset() => _window.Reset();

        public void Update(in Bar bar, Span<double> outputs)
        {
            _window.Add(
                PairStatisticsWindow.Select(bar, left),
                PairStatisticsWindow.Select(bar, right)
            );
            var value = _window.Read(0, flatZero);
            outputs[0] = value ?? 0;
            outputs[1] = value.HasValue ? 1 : 0;
        }
    }
}

/// <summary>Full-window correlation, R squared, covariance and population variances of two candle fields.</summary>
/// <remarks>Each statistic rounds once from exact moments; R squared rounds the
/// exact squared correlation. Startup is absent; zero variance leaves correlation
/// and R squared absent while covariance and variances remain defined. Histories
/// grow lazily. Genuine variance/covariance overflow is rejected by the runtime;
/// WindowCorrelation can calculate the bounded coefficient independently.</remarks>
public sealed class WindowPairStatistics : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates statistics for close/open or independently selected fields.</summary>
    public WindowPairStatistics(
        int period = 20,
        CandlePriceField left = CandlePriceField.Close,
        CandlePriceField right = CandlePriceField.Open
    )
        : base(10)
    {
        PairStatisticsWindow.Validate(period, left, right);
        Period = period;
        Left = left;
        Right = right;
    }

    /// <summary>Positive full-window length.</summary>
    public int Period { get; }

    /// <summary>First series field.</summary>
    public CandlePriceField Left { get; }

    /// <summary>Second series field.</summary>
    public CandlePriceField Right { get; }

    /// <summary>Pearson correlation.</summary>
    public IIndicatorOutput Correlation => Outputs[0];

    /// <summary>Squared Pearson correlation.</summary>
    public IIndicatorOutput RSquared => Outputs[1];

    /// <summary>Population covariance.</summary>
    public IIndicatorOutput Covariance => Outputs[2];

    /// <summary>First population variance.</summary>
    public IIndicatorOutput VarianceA => Outputs[3];

    /// <summary>Second population variance.</summary>
    public IIndicatorOutput VarianceB => Outputs[4];

    /// <summary>Correlation presence.</summary>
    public IIndicatorOutput IsCorrelationDefined => Outputs[5];

    /// <summary>R squared presence.</summary>
    public IIndicatorOutput IsRSquaredDefined => Outputs[6];

    /// <summary>Covariance presence.</summary>
    public IIndicatorOutput IsCovarianceDefined => Outputs[7];

    /// <summary>First variance presence.</summary>
    public IIndicatorOutput IsVarianceADefined => Outputs[8];

    /// <summary>Second variance presence.</summary>
    public IIndicatorOutput IsVarianceBDefined => Outputs[9];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, Left, Right);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 10)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        PairStatisticsReference
                            .Calculate(bars, Period, Left, Right, false)[slot % 5]
                            .Select(v =>
                                slot < 5 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period, CandlePriceField left, CandlePriceField right)
        : IMultiOutputState
    {
        private readonly PairStatisticsWindow _window = new(period);

        public void Reset() => _window.Reset();

        public void Update(in Bar bar, Span<double> outputs)
        {
            _window.Add(
                PairStatisticsWindow.Select(bar, left),
                PairStatisticsWindow.Select(bar, right)
            );
            for (var slot = 0; slot < 5; slot++)
            {
                var value = _window.Read(slot, false);
                outputs[slot] = value ?? 0;
                outputs[slot + 5] = value.HasValue ? 1 : 0;
            }
        }
    }
}

internal sealed class PairStatisticsWindow(int period)
{
    private readonly Queue<(double A, double B)> _history = new();
    // Keep the binary exponent separate from the moments. The finest observed
    // grid is retained until reset; wider inputs still use exact BigInteger math.
    private int _grid;
    private bool _hasGrid;
    private BigInteger _x,
        _y,
        _xx,
        _yy,
        _xy,
        _a,
        _b,
        _c;

    internal static void Validate(int period, CandlePriceField left, CandlePriceField right)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!Enum.IsDefined(left.GetType(), left))
            throw new ArgumentOutOfRangeException(nameof(left));
        if (!Enum.IsDefined(right.GetType(), right))
            throw new ArgumentOutOfRangeException(nameof(right));
    }

    internal static double Select(in Bar bar, CandlePriceField field) =>
        field switch
        {
            CandlePriceField.Open => bar.Open,
            CandlePriceField.High => bar.High,
            CandlePriceField.Low => bar.Low,
            _ => bar.Close,
        };

    internal void Reset()
    {
        _history.Clear();
        _x = _y = _xx = _yy = _xy = _a = _b = _c = 0;
        _grid = 0;
        _hasGrid = false;
    }

    private void Accumulate(double a, double b, int sign)
    {
        var left = ExactMeanAccumulator.DecomposeFinite(a);
        var right = ExactMeanAccumulator.DecomposeFinite(b);
        if (left.Integer != 0 || right.Integer != 0)
        {
            var grid = left.Integer == 0 ? right.Exponent : right.Integer == 0 ? left.Exponent
                : Math.Min(left.Exponent, right.Exponent);
            if (!_hasGrid || grid < _grid)
            {
                if (_hasGrid)
                {
                    var shift = _grid - grid;
                    _x <<= shift; _y <<= shift;
                    _xx <<= 2 * shift; _yy <<= 2 * shift; _xy <<= 2 * shift;
                }
                _grid = grid;
                _hasGrid = true;
            }
        }
        var x = left.Integer == 0 ? BigInteger.Zero : new BigInteger(left.Integer) << (left.Exponent - _grid);
        var y = right.Integer == 0 ? BigInteger.Zero : new BigInteger(right.Integer) << (right.Exponent - _grid);
        _x += sign * x;
        _y += sign * y;
        _xx += sign * x * x;
        _yy += sign * y * y;
        _xy += sign * x * y;
    }

    internal void Add(double a, double b)
    {
        if (_history.Count == period)
        {
            var old = _history.Dequeue();
            Accumulate(old.A, old.B, -1);
        }
        _history.Enqueue((a, b));
        Accumulate(a, b, 1);
        var n = new BigInteger(_history.Count);
        _a = n * _xx - _x * _x;
        _b = n * _yy - _y * _y;
        _c = n * _xy - _x * _y;
    }

    internal double? Read(int slot, bool flatZero)
    {
        if (_history.Count < period)
            return null;
        if (slot >= 2)
        {
            var n = new BigInteger(_history.Count);
            return ExactMeanAccumulator.ScaledRatio(
                slot == 2 ? _c
                    : slot == 3 ? _a
                    : _b,
                n * n, 2 * _grid
            );
        }
        if (_a.IsZero || _b.IsZero)
            return flatZero ? 0 : null;
        return slot == 0
            ? _c.Sign * ExactPopulationDeviation.ScaledRootRatio(_c * _c, _a * _b, 0)
            : ExactMeanAccumulator.ScaledRatio(_c * _c, _a * _b, 0);
    }
}
