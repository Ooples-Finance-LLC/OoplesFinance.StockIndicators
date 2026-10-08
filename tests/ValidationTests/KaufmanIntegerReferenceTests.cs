using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class KaufmanIntegerReferenceTests
{
    [Theory]
    [InlineData(1, .6022, .0645, 10)]
    [InlineData(3, .6022, .0645, 10)]
    [InlineData(3, .6022, .0645, -10)]
    [InlineData(2, 0, 1, -100)]
    [InlineData(2, 1, 0, 100)]
    public void DenominatorChainMatchesOriginalFractionDefinition(int length, double fast, double slow, double filter)
    {
        var prices = new[] { 1d, -2, 3, 3, -7, 0, 1, 9, -4, 2, double.Epsilon, -double.Epsilon };
        var bars = prices.Select((p,i) => new Bar(DateTime.UnixEpoch.AddMinutes(i),p,p,p,p,1)).ToArray();
        Assert.Equal(OriginalFractionDefinition(bars,length,fast,slow,filter)["Kbw"], BuiltInFormulaReferences.KaufmanBinaryValues(bars,length,fast,slow,filter)["Kbw"]);
    }
    internal static Dictionary<string, double[]> OriginalFractionDefinition(IReadOnlyList<Bar> bars, int length, double fast, double slow, double filter)
    {
        length = Math.Max(1, length);
        ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x);
        var zero = R(0); var one = R(1); var hundred = R(100);
        var prices = bars.Select(b => R(b.Close)).ToArray();
        var averages = new ReferenceFraction[prices.Length]; var changes = new ReferenceFraction[prices.Length];
        var result = new double[prices.Length]; var lastFall = zero; var lastRise = zero;
        for (var i = 0; i < prices.Length; i++)
        {
            var travel = zero;
            if (i >= length) for (var j = i - length + 1; j <= i; j++) travel += (prices[j] - prices[j - 1]).Abs();
            var efficiency = travel.Sign == 0 ? zero : (prices[i] - prices[i - length]).Abs() / travel;
            var coefficient = efficiency * R(fast) + R(slow); var gain = coefficient * coefficient;
            var prior = i == 0 ? prices[i] : averages[i - 1];
            averages[i] = gain * prices[i] + (one - gain) * prior; changes[i] = averages[i] - prior;
            var variance = zero;
            if (i + 1 >= length)
            {
                var mean = zero;
                for (var j = i - length + 1; j <= i; j++) mean += changes[j];
                mean /= R(length);
                for (var j = i - length + 1; j <= i; j++) { var d = changes[j] - mean; variance += d * d; }
                variance /= R(length);
            }
            if (changes[i].Sign < 0) lastFall = averages[i];
            if (changes[i].Sign > 0) lastRise = averages[i];
            var thresholdSquared = R(filter) * R(filter) * variance / hundred / hundred;
            bool Clears(ReferenceFraction distance)
            {
                if (thresholdSquared.Sign == 0) return distance.Sign > 0;
                if (filter > 0) return distance.Sign > 0 && (distance * distance).CompareTo(thresholdSquared) > 0;
                if (distance.Sign >= 0) return true;
                return (distance * distance).CompareTo(thresholdSquared) < 0;
            }
            result[i] = Clears(averages[i] - lastFall) ? 1 : Clears(lastRise - averages[i]) ? -1 : 0;
        }
        return new() { ["Kbw"] = result };
    }
}
