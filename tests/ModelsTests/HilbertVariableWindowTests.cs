using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class HilbertVariableWindowTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ChangingCountsWraparoundAndResetMatchExactRescan(int pattern)
    {
        var state = new HilbertTrendMeanState();
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            var prices = new List<JurikDyadic>();
            var means = new Queue<JurikDyadic>();
            var random = new Random(87103);
            JurikDyadic expected = default;
            for (int i = 0; i < 1024; i++)
            {
                double value = pattern switch
                {
                    0 => 100 + (i * 13 % 101) / 8d,
                    1 => (i % 8) switch
                    {
                        0 => double.MaxValue, 1 => double.Epsilon,
                        2 => -double.MaxValue, 3 => -double.Epsilon,
                        4 => -0d, 5 => 1, 6 => Math.ScaleB(1, -180), _ => -1
                    },
                    _ => Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1024))
                };
                var price = JurikDyadic.FromDouble(value);
                if (pattern == 1 && i % 8 == 0) price.Multiply(2);
                prices.Insert(0, price);
                if (prices.Count > 50) prices.RemoveAt(50);
                int? count = i % 137 < 7 ? null : i % 11 == 0 ? 0
                    : i % 127 < 60 ? 50 : i % 2 == 0 ? 1 : i % 51;
                state.Update(price, count);
                if (count.HasValue)
                {
                    // Independent full rescan of retained prices, including zero startup slots.
                    JurikDyadic sum = default;
                    foreach (var term in prices.Take(count.Value)) sum.AddExact(term);
                    var mean = count.Value == 0 ? default : sum.Rounded(count.Value);
                    means.Enqueue(mean);
                    if (means.Count > 4) means.Dequeue();
                    JurikDyadic weighted = default;
                    int weight = means.Count == 4 ? 1 : 5 - means.Count;
                    foreach (var term in means)
                    {
                        var product = term;
                        product.Multiply(weight++);
                        weighted.AddExact(product);
                    }
                    expected = weighted.Rounded(10);
                }
                var difference = state.Average;
                difference.Subtract(expected);
                Assert.Equal(0, difference.Sign);
                if (expected.TryDouble(out double exact) && state.Average.TryDouble(out double actual))
                    Assert.Equal(BitConverter.DoubleToInt64Bits(exact), BitConverter.DoubleToInt64Bits(actual));
            }
        }
    }
}
