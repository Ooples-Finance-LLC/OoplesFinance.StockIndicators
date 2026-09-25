namespace OoplesFinance.StockIndicators.Helpers;

internal static class FisherArithmetic
{
    internal static double Position(double value, double lower, double upper) =>
        upper == lower ? .5 : ExactRangePosition.Fraction(value, lower, upper);

    internal static double Transform(double value)
    {
        // atanh's odd series preserves tiny signals without subtracting nearly equal logarithms.
        // For |x|<1/8, the omitted tail is at most |x|^21/(21*(1-x*x)).
        if (Math.Abs(value) < .125)
        {
            var square = value * value; var term = value; var sum = value;
            for (var n = 3; n <= 19; n += 2) { term *= square; sum += term / n; }
            return sum;
        }
        var magnitude = Math.Abs(value);
        return Math.Sign(value) * .5 * Math.Log((1 + magnitude) / (1 - magnitude));
    }
}
