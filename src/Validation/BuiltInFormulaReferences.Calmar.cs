using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) CalmarValues(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Power(ReferenceFraction value, int exponent)
        {
            var product = R(1); while (exponent != 0) { if ((exponent & 1) != 0) product *= value; exponent >>= 1; if (exponent != 0) value *= value; } return product;
        }
        double Quotient(ReferenceFraction ratio, ReferenceFraction depth)
        {
            if (ratio.Sign == 0) return ((R(0) - R(1)) / depth).ToDouble();
            var direction = Math.Sign(ratio.CompareTo(R(1))); if (direction == 0) return 0;
            // Solve the unreduced annualization identity (1 + value*depth)^length = ratio^24.
            // Blind binary64 bisection is independent of the production estimate and reduced exponent.
            var target = Power(ratio, 24);
            int Compare(ReferenceFraction candidate)
            {
                var baseValue = R(1) + R(direction) * candidate * depth;
                return baseValue.Sign < 0 ? direction : direction * target.CompareTo(Power(baseValue, length));
            }
            long lower = 0, upper = 0x7fefffffffffffff;
            while (lower < upper)
            {
                var middle = lower + (upper - lower + 1) / 2;
                if (Compare(R(BitConverter.Int64BitsToDouble(middle))) >= 0) lower = middle; else upper = middle - 1;
            }
            var low = R(BitConverter.Int64BitsToDouble(lower)); if (Compare(low) == 0) return direction * low.ToDouble();
            var high = lower == 0x7fefffffffffffff ? new ReferenceFraction(BigInteger.One << 1024) : R(BitConverter.Int64BitsToDouble(lower + 1));
            var atMidpoint = Compare((low + high) / R(2)); var bits = atMidpoint > 0 || atMidpoint == 0 && (lower & 1) != 0 ? lower + 1 : lower;
            return direction * BitConverter.Int64BitsToDouble(bits);
        }
        var drawdowns = new ReferenceFraction[bars.Count]; var values = new double[bars.Count]; var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var peak = Window(bars, i, Math.Max(2, length)).Max(b => b.Close); drawdowns[i] = peak == 0 ? R(0) : (R(bars[i].Close) - R(peak)) / R(peak);
            var minimum = drawdowns[i]; for (var j = Math.Max(0, i - length + 1); j < i; j++) if (drawdowns[j].CompareTo(minimum) < 0) minimum = drawdowns[j];
            if (i >= length && bars[i - length].Close != 0 && minimum.Sign != 0)
            { var ratio = R(bars[i].Close) / R(bars[i - length].Close); if (ratio.Sign >= 0) values[i] = Quotient(ratio, minimum.Abs()); }
            var previous = i == 0 ? 0 : values[i - 1]; var value = values[i]; signals[i] = value > 2 && value > previous ? Signal.StrongBuy : value < 2 && value < previous ? Signal.StrongSell : value > 2 ? Signal.Buy : value < 2 ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { { "Cr", values } }, signals);
    }
}
