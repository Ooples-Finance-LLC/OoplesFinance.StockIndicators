using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ReturnBetaExecutionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public void CompactMomentsPreserveReferenceBitsAndReset(int period)
    {
        var ordinary = Enumerable.Range(0, 81).Select(i => new Bar(default,
            100 + (i * 17 % 31) / 8d, 0, 0, 95 - (i * 7 % 19) / 8d, 1)).ToArray();
        double[] wide = [0, double.Epsilon, double.MaxValue, -double.MaxValue, 0,
            -double.Epsilon, double.Epsilon, 1, Math.BitIncrement(1), 1, 1, -1, 0];
        var extreme = wide.Select((v, i) => new Bar(default, v, 0, 0, wide[(i + 3) % wide.Length], 1))
            .Concat(ordinary).ToArray();
        var flat = Enumerable.Repeat(new Bar(default, 3, 0, 0, 7, 1), 45).ToArray();
        foreach (var selection in Enum.GetValues<ReturnBetaSelection>())
        foreach (bool full in new[] { false, true })
        {
            if (!full && selection != ReturnBetaSelection.Standard) continue;
            var state = (IMultiOutputState)(full
                ? new WindowBetaStatistics(period, selection).CreateState()
                : new WindowReturnBeta(period, CandlePriceField.Open, CandlePriceField.Close).CreateState());
            var output = new double[full ? 14 : 2];
            foreach (var bars in new[] { ordinary, extreme, flat, ordinary })
            {
                var expected = ReturnBetaReference.Calculate(bars, period, CandlePriceField.Open,
                    CandlePriceField.Close, selection, full);
                state.Reset();
                for (int i = 0; i < bars.Length; i++)
                {
                    state.Update(in bars[i], output);
                    for (int slot = 0; slot < expected.Length; slot++)
                    {
                        Assert.Equal(expected[slot][i].HasValue ? 1d : 0d, output[slot + expected.Length]);
                        Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i] ?? 0),
                            BitConverter.DoubleToInt64Bits(output[slot]));
                    }
                }
            }
        }
    }

    [Fact]
    public void OrdinaryRollingMomentsAllocateNoPerBarObjects()
    {
        var window = new ReturnBetaWindow(20, ReturnBetaSelection.All);
        var prices = Enumerable.Range(0, 97).Select(i => (100 + i / 8d, 98 + (i * 7 % 97) / 8d)).ToArray();
        for (int i = 0; i < 1000; i++) window.Add(prices[i % prices.Length].Item1, prices[i % prices.Length].Item2);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++) window.Add(prices[i % prices.Length].Item1, prices[i % prices.Length].Item2);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(window.Beta(0, false).HasValue);
    }
}
