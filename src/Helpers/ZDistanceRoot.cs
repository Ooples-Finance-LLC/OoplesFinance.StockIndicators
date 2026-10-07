using Number = OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;

namespace OoplesFinance.StockIndicators.Helpers;

// Signed radicals are compared before their binary64 publication. At most three
// terms are needed for a score's slope and change in slope.
internal readonly struct ZDistanceRoot
{
    internal Number Square { get; }
    internal int Sign { get; }
    internal ZDistanceRoot(Number square, int sign) { Square = square; Sign = square.Sign == 0 ? 0 : Math.Sign(sign); }
    internal ZDistanceRoot Times(int coefficient) => new(Square.Times((long)coefficient * coefficient), Sign * Math.Sign(coefficient));
    private static int PairSign(ZDistanceRoot a, ZDistanceRoot b)
    {
        if (a.Sign == 0) return b.Sign;
        if (b.Sign == 0 || a.Sign == b.Sign) return a.Sign;
        return (a.Square - b.Square).Sign * a.Sign;
    }
    private static int RationalAndRootSign(Number rational, Number square, int rootSign)
    {
        if (square.Sign == 0 || rootSign == 0) return rational.Sign;
        if (rational.Sign == 0) return rootSign;
        if (rational.Sign == rootSign) return rootSign;
        var comparison = (rational * rational - square).Sign;
        return comparison == 0 ? 0 : comparison > 0 ? rational.Sign : rootSign;
    }
    internal static int SumSign(ZDistanceRoot a, ZDistanceRoot b, ZDistanceRoot c)
    {
        var pair = PairSign(a,b);
        if (c.Sign == 0) return pair;
        if (pair == 0 || pair == c.Sign) return c.Sign;
        // (a+b)^2-c^2 = A+B-C + sign(a*b)*sqrt(4*A*B).
        var squaredDifference = RationalAndRootSign(a.Square + b.Square - c.Square,
            (a.Square * b.Square).Times(4), a.Sign * b.Sign);
        return squaredDifference == 0 ? 0 : squaredDifference > 0 ? pair : c.Sign;
    }
    internal static Signal Vote(ZDistanceRoot current, ZDistanceRoot previous, ZDistanceRoot older)
    {
        var slope = SumSign(current, previous.Times(-1), default);
        var acceleration = SumSign(current, previous.Times(-2), older);
        return slope > 0 ? acceleration > 0 ? Signal.StrongBuy : Signal.Buy
            : slope < 0 ? acceleration < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
    }
}
