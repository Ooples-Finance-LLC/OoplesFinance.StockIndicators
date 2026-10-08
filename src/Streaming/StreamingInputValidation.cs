namespace OoplesFinance.StockIndicators.Streaming;

// Validate before buffering or fan-out: a rejected event must not advance any consumer.
internal static class StreamingInputValidation
{
    internal static void Finite(double value, string parameter)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(parameter, value, "Streaming input must be finite.");
    }

    internal static void Validate(StreamTrade trade)
    {
        Finite(trade.Price, nameof(trade.Price));
        Finite(trade.Size, nameof(trade.Size));
    }

    internal static void Validate(StreamQuote quote)
    {
        Finite(quote.BidPrice, nameof(quote.BidPrice));
        Finite(quote.AskPrice, nameof(quote.AskPrice));
        Finite(quote.BidSize, nameof(quote.BidSize));
        Finite(quote.AskSize, nameof(quote.AskSize));
    }

    internal static void Validate(OhlcvBar bar)
    {
        Finite(bar.Open, nameof(bar.Open));
        Finite(bar.High, nameof(bar.High));
        Finite(bar.Low, nameof(bar.Low));
        Finite(bar.Close, nameof(bar.Close));
        Finite(bar.Volume, nameof(bar.Volume));
    }

    // Keep ordinary rounding and tiny values, avoiding overflow only when the sum would overflow.
    internal static double Midpoint(double left, double right)
    {
        var sum = left + right;
        return double.IsInfinity(sum) ? left / 2 + right / 2 : sum / 2;
    }
}
