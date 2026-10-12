using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Three buying-pressure/true-range ratios weighted 4:2:1 and scaled by 100/7.</summary>
/// <remarks>Default periods must strictly increase and any zero denominator makes
/// the result absent. With zeroRangeContribution, positive periods are sorted,
/// duplicates are allowed, and zero denominators contribute zero, matching TA-Lib.
/// Differences, rolling sums and the weighted rational expression remain exact
/// until the final result rounds once. Startup needs longestPeriod changes after
/// the initial candle; histories grow lazily.</remarks>
public sealed class WindowUltimateOscillator
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates the oscillator with three positive periods and an explicit zero-range convention.</summary>
    public WindowUltimateOscillator(
        int shortPeriod = 7,
        int middlePeriod = 14,
        int longPeriod = 28,
        bool zeroRangeContribution = false
    )
        : base(2)
    {
        if (shortPeriod < 1 || middlePeriod < 1 || longPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(shortPeriod));
        var periods = new[] { shortPeriod, middlePeriod, longPeriod };
        if (zeroRangeContribution)
            Array.Sort(periods);
        else if (shortPeriod >= middlePeriod || middlePeriod >= longPeriod)
            throw new ArgumentException("Periods must strictly increase.", nameof(middlePeriod));
        ShortPeriod = periods[0];
        MiddlePeriod = periods[1];
        LongPeriod = periods[2];
        ZeroRangeContribution = zeroRangeContribution;
    }

    /// <summary>Shortest window, weighted four.</summary>
    public int ShortPeriod { get; }

    /// <summary>Middle window, weighted two.</summary>
    public int MiddlePeriod { get; }

    /// <summary>Longest window, weighted one.</summary>
    public int LongPeriod { get; }

    /// <summary>Whether zero ranges contribute zero and period sorting is enabled.</summary>
    public bool ZeroRangeContribution { get; }

    /// <summary>Oscillator, or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One when defined.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
#if !NETFRAMEWORK
        new CompactState([ShortPeriod, MiddlePeriod, LongPeriod], ZeroRangeContribution);
#else
        new State([ShortPeriod, MiddlePeriod, LongPeriod], ZeroRangeContribution);
#endif

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        TrueRangeRatioReference
                            .Ultimate(
                                bars,
                                [ShortPeriod, MiddlePeriod, LongPeriod],
                                ZeroRangeContribution
                            )
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

#if !NETFRAMEWORK
    internal sealed class CompactState(int[] periods, bool zero, int count = 0) : IMultiOutputState
    {
        private readonly Queue<(JurikDyadic Pressure, JurikDyadic Range)>[] _windows =
            periods.Select(p => new Queue<(JurikDyadic, JurikDyadic)>(Math.Min(p, Math.Max(0, count - 1)))).ToArray();
        private readonly JurikDyadic[] _pressure = new JurikDyadic[3], _range = new JurikDyadic[3];
        private double _previous;
        private bool _started;

        public void Reset()
        {
            foreach (var window in _windows) window.Clear();
            Array.Clear(_pressure); Array.Clear(_range);
            _previous = 0; _started = false;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            if (!_started) { _started = true; _previous = bar.Close; return; }
            double low = Math.Min(bar.Low, _previous);
            var pressure = ExactTrueInterval.Difference(bar.Close, low);
            var range = zero ? ExactTrueInterval.Range(bar.High, bar.Low, _previous)
                : ExactTrueInterval.Difference(Math.Max(bar.High, _previous), low);
            _previous = bar.Close;
            for (int k = 0; k < 3; k++)
            {
                if (_windows[k].Count == periods[k])
                {
                    var old = _windows[k].Dequeue();
                    _pressure[k].Subtract(old.Pressure); _range[k].Subtract(old.Range);
                }
                _windows[k].Enqueue((pressure, range));
                _pressure[k].AddExact(pressure); _range[k].AddExact(range);
            }
            if (_windows[2].Count < periods[2]) return;
            var numerator = new JurikDyadic();
            var denominator = JurikDyadic.FromDouble(1);
            for (int k = 0; k < 3; k++)
            {
                if (_range[k].Sign == 0)
                {
                    if (!zero) return;
                    continue;
                }
                numerator.Multiply(_range[k]);
                var term = _pressure[k];
                term.Multiply(4 >> k); term.Multiply(denominator);
                numerator.AddExact(term);
                denominator.Multiply(_range[k]);
            }
            numerator.Multiply(100); denominator.Multiply(7);
            output[0] = numerator.Ratio(denominator);
            output[1] = 1;
        }
    }
#else
    private sealed class State(int[] periods, bool zero) : IMultiOutputState
    {
        private readonly Queue<(BigInteger Pressure, BigInteger Range)>[] _windows =
        [
            new(),
            new(),
            new(),
        ];
        private readonly BigInteger[] _pressure = new BigInteger[3],
            _range = new BigInteger[3];
        private double _previous;
        private bool _started;

        public void Reset()
        {
            foreach (var w in _windows)
                w.Clear();
            Array.Clear(_pressure, 0, 3);
            Array.Clear(_range, 0, 3);
            _previous = 0;
            _started = false;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            if (!_started)
            {
                _started = true;
                _previous = bar.Close;
                return;
            }
            var h = ExactVarianceWindow.Units(bar.High);
            var l = ExactVarianceWindow.Units(bar.Low);
            var c = ExactVarianceWindow.Units(_previous);
            var pressure = ExactVarianceWindow.Units(bar.Close) - BigInteger.Min(l, c);
            var range = zero
                ? BigInteger.Max(
                    h - l,
                    BigInteger.Max(BigInteger.Abs(h - c), BigInteger.Abs(l - c))
                )
                : BigInteger.Max(h, c) - BigInteger.Min(l, c);
            _previous = bar.Close;
            for (var k = 0; k < 3; k++)
            {
                if (_windows[k].Count == periods[k])
                {
                    var old = _windows[k].Dequeue();
                    _pressure[k] -= old.Pressure;
                    _range[k] -= old.Range;
                }
                _windows[k].Enqueue((pressure, range));
                _pressure[k] += pressure;
                _range[k] += range;
            }
            if (_windows[2].Count < periods[2])
                return;
            BigInteger numerator = 0,
                denominator = 1;
            for (var k = 0; k < 3; k++)
            {
                if (_range[k].IsZero)
                {
                    if (!zero)
                        return;
                    continue;
                }
                numerator = numerator * _range[k] + (4 >> k) * _pressure[k] * denominator;
                denominator *= _range[k];
            }
            output[0] = ExactMeanAccumulator.UnitRatio((100 * numerator) << 1074, 7 * denominator);
            output[1] = 1;
        }
    }
#endif
}
