namespace OoplesFinance.StockIndicators.Helpers;

internal static class ConfluenceVotes
{
    internal static int Compare(double value, double boundary)
    {
        var delta = value - boundary;
        return Math.Abs(delta) <= 1e-12 * Math.Max(1, Math.Max(Math.Abs(value), Math.Abs(boundary))) ? 0 : delta > 0 ? 1 : -1;
    }
    internal static double Score(double value, double previous, double signal)
    {
        var direction = Compare(value, 0);
        var momentum = Compare(value, previous);
        var position = Compare(value, signal);
        if (direction == 0 || momentum == 0 || position == 0) return 0;
        return direction * (1 + (momentum == direction ? 1 : 0) + (position == direction ? 1 : 0));
    }
    internal static double Publish(double total, double spread)
    {
        var direction = Compare(spread, 0);
        return direction == 0 ? 0 : Math.Sign(total) == direction ? total : total / 10;
    }
}
