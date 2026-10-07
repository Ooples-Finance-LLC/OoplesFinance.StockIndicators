using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Market-return subset used for beta statistics.</summary>
public enum ReturnBetaSelection
{
    Standard,
    Up,
    Down,
    All,
}

/// <summary>Regression slope of evaluation returns against market returns.</summary>
/// <remarks>Default field order follows TA-Lib: first series close is the market
/// denominator, second series open is evaluated. Startup needs period returns
/// (period+1 prices). A zero previous price gives zero return. Each return rounds
/// once with an extended upper exponent; beta rounds once from exact moments.
/// Flat market-return windows give zero. Genuine final overflow is rejected.</remarks>
public sealed class WindowReturnBeta : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a positive-period beta with explicitly selected market/evaluation fields.</summary>
    public WindowReturnBeta(
        int period = 20,
        CandlePriceField market = CandlePriceField.Close,
        CandlePriceField evaluation = CandlePriceField.Open
    )
        : base(2)
    {
        PairStatisticsWindow.Validate(period, market, evaluation);
        Period = period;
        Market = market;
        Evaluation = evaluation;
    }

    /// <summary>Number of returns in the complete window.</summary>
    public int Period { get; }

    /// <summary>Market field, whose return variance is the denominator.</summary>
    public CandlePriceField Market { get; }

    /// <summary>Evaluation series field.</summary>
    public CandlePriceField Evaluation { get; }

    /// <summary>Beta, or zero before startup.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One after complete return-window startup.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, Market, Evaluation);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        ReturnBetaReference
                            .Calculate(
                                bars,
                                Period,
                                Market,
                                Evaluation,
                                ReturnBetaSelection.Standard,
                                false
                            )[0]
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period, CandlePriceField market, CandlePriceField evaluation)
        : IMultiOutputState
    {
        private readonly ReturnBetaWindow _window = new(period, ReturnBetaSelection.Standard);

        public void Reset() => _window.Reset();

        public void Update(in Bar bar, Span<double> output)
        {
            _window.Add(
                PairStatisticsWindow.Select(bar, market),
                PairStatisticsWindow.Select(bar, evaluation)
            );
            var value = _window.Beta(0, true);
            output[0] = value ?? 0;
            output[1] = value.HasValue ? 1 : 0;
        }
    }
}

/// <summary>Selected standard/up/down beta, ratio, convexity and both return series.</summary>
/// <remarks>Defaults follow Skender: evaluate close against market open. Positive
/// and negative rounded market returns select up/down subsets; zero belongs only
/// to standard beta. Beta is absent for flat/empty subsets. Ratio and convexity
/// are emitted only in All mode when both directional betas exist; zero down beta
/// makes ratio absent. Each return, beta and derived result rounds once. First and
/// zero-denominator returns are zero. Genuine published overflow is rejected.</remarks>
public sealed class WindowBetaStatistics : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates beta statistics with positive period and explicit subset selection.</summary>
    public WindowBetaStatistics(
        int period = 20,
        ReturnBetaSelection selection = ReturnBetaSelection.Standard,
        CandlePriceField market = CandlePriceField.Open,
        CandlePriceField evaluation = CandlePriceField.Close
    )
        : base(14)
    {
        PairStatisticsWindow.Validate(period, market, evaluation);
        if (!Enum.IsDefined(selection.GetType(), selection))
            throw new ArgumentOutOfRangeException(nameof(selection));
        Period = period;
        Selection = selection;
        Market = market;
        Evaluation = evaluation;
    }

    /// <summary>Full return-window length.</summary>
    public int Period { get; }

    /// <summary>Requested market-return subsets.</summary>
    public ReturnBetaSelection Selection { get; }

    /// <summary>Market series field.</summary>
    public CandlePriceField Market { get; }

    /// <summary>Evaluation series field.</summary>
    public CandlePriceField Evaluation { get; }

    /// <summary>Standard beta.</summary>
    public IIndicatorOutput Beta => Outputs[0];

    /// <summary>Positive-market-return beta.</summary>
    public IIndicatorOutput BetaUp => Outputs[1];

    /// <summary>Negative-market-return beta.</summary>
    public IIndicatorOutput BetaDown => Outputs[2];

    /// <summary>Up beta divided by down beta.</summary>
    public IIndicatorOutput Ratio => Outputs[3];

    /// <summary>Squared difference of up/down beta.</summary>
    public IIndicatorOutput Convexity => Outputs[4];

    /// <summary>Evaluation simple return.</summary>
    public IIndicatorOutput ReturnsEval => Outputs[5];

    /// <summary>Market simple return.</summary>
    public IIndicatorOutput ReturnsMarket => Outputs[6];

    /// <summary>Standard-beta presence.</summary>
    public IIndicatorOutput IsBetaDefined => Outputs[7];

    /// <summary>Up-beta presence.</summary>
    public IIndicatorOutput IsBetaUpDefined => Outputs[8];

    /// <summary>Down-beta presence.</summary>
    public IIndicatorOutput IsBetaDownDefined => Outputs[9];

    /// <summary>Ratio presence.</summary>
    public IIndicatorOutput IsRatioDefined => Outputs[10];

    /// <summary>Convexity presence.</summary>
    public IIndicatorOutput IsConvexityDefined => Outputs[11];

    /// <summary>Evaluation-return presence.</summary>
    public IIndicatorOutput IsReturnsEvalDefined => Outputs[12];

    /// <summary>Market-return presence.</summary>
    public IIndicatorOutput IsReturnsMarketDefined => Outputs[13];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, Selection, Market, Evaluation);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 14)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        ReturnBetaReference
                            .Calculate(bars, Period, Market, Evaluation, Selection, true)[slot % 7]
                            .Select(v =>
                                slot < 7 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(
        int period,
        ReturnBetaSelection selection,
        CandlePriceField market,
        CandlePriceField evaluation
    ) : IMultiOutputState
    {
        private readonly ReturnBetaWindow _window = new(period, selection);

        public void Reset() => _window.Reset();

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _window.Add(
                PairStatisticsWindow.Select(bar, market),
                PairStatisticsWindow.Select(bar, evaluation)
            );
            output[5] = ExactMeanAccumulator.UnitRatio(_window.EvaluationReturn, 1);
            output[12] = 1;
            output[6] = ExactMeanAccumulator.UnitRatio(_window.MarketReturn, 1);
            output[13] = 1;
            for (var i = 0; i < 3; i++)
            {
                var value = _window.Beta(i, false);
                output[i] = value ?? 0;
                output[i + 7] = value.HasValue ? 1 : 0;
            }
            if (
                selection != ReturnBetaSelection.All
                || output[8] == 0
                || output[9] == 0
                || !FrameworkCompatibility.IsFinite(output[1])
                || !FrameworkCompatibility.IsFinite(output[2])
            )
                return;
            var up = new ExactMeanAccumulator();
            up.Add(output[1]);
            var down = new ExactMeanAccumulator();
            down.Add(output[2]);
            if (output[2] != 0)
            {
                output[3] = up.Ratio(down);
                output[10] = 1;
            }
            var difference =
                ExactVarianceWindow.Units(output[1]) - ExactVarianceWindow.Units(output[2]);
            output[4] = ExactMeanAccumulator.UnitRatio(
                difference * difference,
                BigInteger.One << 1074
            );
            output[11] = 1;
        }
    }
}

internal sealed class ReturnBetaWindow(int period, ReturnBetaSelection selection)
{
    private readonly Queue<(BigInteger X, BigInteger Y)> _history = new();
    private readonly Moments[] _moments = [new(), new(), new()];
    private bool _started;
    private double _previousMarket,
        _previousEvaluation;
    internal BigInteger MarketReturn { get; private set; }
    internal BigInteger EvaluationReturn { get; private set; }

    internal void Reset()
    {
        _history.Clear();
        foreach (var m in _moments)
            m.Reset();
        _started = false;
        _previousMarket = _previousEvaluation = 0;
        MarketReturn = EvaluationReturn = 0;
    }

    internal void Add(double market, double evaluation)
    {
        MarketReturn = Return(market, _previousMarket);
        EvaluationReturn = Return(evaluation, _previousEvaluation);
        _previousMarket = market;
        _previousEvaluation = evaluation;
        if (!_started)
        {
            _started = true;
            return;
        }
        if (_history.Count == period)
        {
            var old = _history.Dequeue();
            Accumulate(old.X, old.Y, -1);
        }
        _history.Enqueue((MarketReturn, EvaluationReturn));
        Accumulate(MarketReturn, EvaluationReturn, 1);
    }

    private static BigInteger Return(double current, double previous)
    {
        if (previous == 0)
            return 0;
        var denominator = ExactVarianceWindow.Units(previous);
        var numerator = (ExactVarianceWindow.Units(current) - denominator) << 1074;
        if (denominator.Sign < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }
        return RocBankValue.RoundUnits(numerator, denominator);
    }

    private bool Included(int slot) =>
        selection == ReturnBetaSelection.All || (int)selection == slot;

    private void Accumulate(BigInteger x, BigInteger y, int sign)
    {
        if (Included(0))
            _moments[0].Add(x, y, sign);
        if (x.Sign > 0 && Included(1))
            _moments[1].Add(x, y, sign);
        if (x.Sign < 0 && Included(2))
            _moments[2].Add(x, y, sign);
    }

    internal double? Beta(int slot, bool flatZero) =>
        _history.Count < period || !Included(slot) ? null : _moments[slot].Beta(flatZero);

    private sealed class Moments
    {
        private int _count;
        private BigInteger _x,
            _y,
            _xx,
            _xy;

        internal void Reset()
        {
            _count = 0;
            _x = _y = _xx = _xy = 0;
        }

        internal void Add(BigInteger x, BigInteger y, int sign)
        {
            _count += sign;
            _x += sign * x;
            _y += sign * y;
            _xx += sign * x * x;
            _xy += sign * x * y;
        }

        internal double? Beta(bool flatZero)
        {
            var denominator = _count * _xx - _x * _x;
            return denominator.IsZero
                ? flatZero
                    ? 0
                    : null
                : ExactMeanAccumulator.UnitRatio((_count * _xy - _x * _y) << 1074, denominator);
        }
    }
}
