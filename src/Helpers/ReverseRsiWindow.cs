using OoplesFinance.StockIndicators.Streaming;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
using Interval = OoplesFinance.StockIndicators.Helpers.CertifiedKaufmanBinaryWindow.Interval;

namespace OoplesFinance.StockIndicators.Helpers;

// Solve the seeded Wilder inverse with one signed moment. Outward bounds certify
// the rounded price and signal; ambiguous cases replay the exact recurrence.
internal sealed class ReverseRsiWindow
{
    private readonly int _length;
    private readonly F _target, _seed;
    private readonly List<double> _observations = new();
    private Interval _moment, _delta, _previousSlope;
    private F _previousPrice, _previousKick, _exactMoment, _exactPrice, _exactSlope;
    private int _previousBranch, _exactCount;
    internal int ExactReplayUpdates { get; private set; }
    internal ReverseRsiWindow(int length, double target)
    {
        Validate(length, target); _length = length; _target = F.Of(target) / 100; _seed = 2 * _target - 1;
        Reset();
    }
    internal static void Validate(int length, double target)
    {
        if (length < 2) throw new ArgumentOutOfRangeException(nameof(length), "The inverse requires a period of at least two.");
        if (!(target > 0 && target < 100)) throw new ArgumentOutOfRangeException(nameof(target), "The inverse requires a finite target strictly between zero and 100.");
    }
    private F Kick(F change) => change.Sign >= 0 ? -(1 - _target) * change : -_target * change;
    private F Factor(int branch) => (_length - 1) / (branch > 0 ? 1 - _target : _target);
    private static Signal? Certify(Interval slope, Interval acceleration)
    {
        if (slope.IsZero) return Signal.None;
        if (slope.Lower.Sign > 0)
            return acceleration.Lower.Sign > 0 ? Signal.StrongBuy : acceleration.Upper.Sign <= 0 ? Signal.Buy : null;
        if (slope.Upper.Sign < 0)
            return acceleration.Upper.Sign < 0 ? Signal.StrongSell : acceleration.Lower.Sign >= 0 ? Signal.Sell : null;
        return null;
    }
    internal (double Value, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        var value = F.Of(price); var started = _observations.Count > 0;
        var kick = started ? Kick(value - _previousPrice) : (F)0;
        var moment = (_moment * Interval.Exact(_length - 1) + Interval.Exact(kick)).Divide(_length);
        // Recurring the difference directly avoids cancellation as a trend converges.
        var delta = started ? (_delta * Interval.Exact(_length - 1) + Interval.Exact(kick - _previousKick)).Divide(_length)
            : Interval.Exact(-_seed).Divide(_length);
        var branch = moment.Lower.Sign >= 0 ? 1 : moment.Upper.Sign <= 0 ? -1 : 0;
        var displacement = branch == 0 ? Interval.Hull(moment * Interval.Exact(Factor(1)), moment * Interval.Exact(Factor(-1)))
            : moment * Interval.Exact(Factor(branch));
        var inverse = Interval.Exact(value) + displacement;
        var slope = Interval.Exact(0) - displacement;
        var acceleration = started && branch != 0 && branch == _previousBranch
            ? delta * Interval.Exact(-Factor(branch)) : slope - _previousSlope;
        var lower = inverse.Lower.Publish(); var upper = inverse.Upper.Publish(); var trade = Certify(slope, acceleration);
        (double Value, Signal Trade) result;
#pragma warning disable S1244 // Equal rounded bounds certify the same published binary64 result.
        if (lower == upper && trade.HasValue) result = (lower, trade.Value);
#pragma warning restore S1244
        else
        {
            while (_exactCount < _observations.Count) ExactNext(_observations[_exactCount], true);
            result = ExactNext(price, final);
            if (branch == 0) branch = final ? (_exactMoment.Sign >= 0 ? 1 : -1) : 0;
        }
        if (final)
        {
            _observations.Add(price); _moment = moment; _delta = delta; _previousSlope = slope;
            _previousPrice = value; _previousKick = kick; _previousBranch = branch;
        }
        return result;
    }
    private (double Value, Signal Trade) ExactNext(double price, bool final)
    {
        ExactReplayUpdates++;
        var value = F.Of(price); var kick = _exactCount == 0 ? (F)0 : Kick(value - _exactPrice);
        var moment = ((_length - 1) * _exactMoment + kick) / _length;
        var displacement = moment * Factor(moment.Sign >= 0 ? 1 : -1); var slope = -displacement;
        var trade = slope.Sign > 0 ? slope.CompareTo(_exactSlope) > 0 ? Signal.StrongBuy : Signal.Buy
            : slope.Sign < 0 ? slope.CompareTo(_exactSlope) < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (final) { _exactMoment = moment; _exactPrice = value; _exactSlope = slope; _exactCount++; }
        return ((value + displacement).Publish(), trade);
    }
    internal void Reset()
    {
        _observations.Clear(); _moment = Interval.Exact(_seed); _delta = _previousSlope = default;
        _previousPrice = _previousKick = _exactPrice = _exactSlope = default; _exactMoment = _seed;
        _previousBranch = _exactCount = ExactReplayUpdates = 0;
    }
    internal static (List<double> Values, List<Signal> Signals) Calculate(StockData data, int length, double target)
    {
        Validate(length, target);
        var (input, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        foreach (var values in new[] { input, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes })
            foreach (var value in values) StreamingInputValidation.Finite(value, nameof(data));
        var state = new ReverseRsiWindow(length, target); var result = new List<double>(input.Count); var signals = new List<Signal>(input.Count);
        foreach (var value in input) { var point = state.Next(value, true); result.Add(point.Value); signals.Add(point.Trade); }
        return (result, signals);
    }
}
