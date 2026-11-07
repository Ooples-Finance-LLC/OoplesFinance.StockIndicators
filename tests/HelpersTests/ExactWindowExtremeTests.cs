using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.HelpersTests;

public sealed class ExactWindowExtremeTests
{
    public static IEnumerable<object[]> Cases =>
        from period in new[] { 1, 2, 7, 65, int.MaxValue }
        from maximum in new[] { false, true }
        select new object[] { period, maximum };

    [Theory, MemberData(nameof(Cases))]
    public void CandidatesMatchIndependentScanWithSignedWideValues(int period, bool maximum)
    {
        var window = new ExactWindowExtreme(period, maximum);
        var history = new Queue<BigInteger>();
        BigInteger Scan(BigInteger incoming)
        {
            var result = incoming;
            foreach (var value in history.Skip(history.Count == period ? 1 : 0))
                result = maximum ? BigInteger.Max(result, value) : BigInteger.Min(result, value);
            return result;
        }
        for (int replay = 0; replay < 2; replay++)
        {
            history.Clear(); window.Reset();
            for (int i = 0; i < 600; i++)
            {
                var value = i < 200 ? new BigInteger(100 - i) : i < 400 ? new BigInteger(i - 300)
                    : new BigInteger(i * 41 % 97 - 48) << (i % 7 * 256);
                Assert.Equal(Scan(-value), window.Preview(-value));
                Assert.Equal(Scan(value), window.Preview(value));
                window.Commit(value);
                if (history.Count == period) history.Dequeue();
                history.Enqueue(value);
            }
        }
    }
}
