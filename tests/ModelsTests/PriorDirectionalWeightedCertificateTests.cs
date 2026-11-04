using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class PriorDirectionalWeightedCertificateTests
{
    [Theory]
    [InlineData(PriorDirectionalMeasure.PositiveMovement)]
    [InlineData(PriorDirectionalMeasure.NegativeMovement)]
    [InlineData(PriorDirectionalMeasure.PositiveIndicator)]
    [InlineData(PriorDirectionalMeasure.NegativeIndicator)]
    [InlineData(PriorDirectionalMeasure.Index)]
    [InlineData(PriorDirectionalMeasure.Average)]
    [InlineData(PriorDirectionalMeasure.Rating)]
    public void OrdinaryAndExtendedStagesRetainExactResultsAcrossReset(PriorDirectionalMeasure measure)
    {
        var random = new Random(7342);
        var ordinary = Enumerable.Range(0, 193).Select(i =>
        {
            double center = 100 + random.NextDouble() * 20;
            return new Bar(default, center, center + random.NextDouble() * 5,
                center - random.NextDouble() * 5, center + random.NextDouble() - .5, 1);
        }).ToArray();
        var extended = ordinary.ToArray();
        extended[47] = new(default, 0, double.MaxValue, -double.MaxValue, double.MaxValue, 1);
        extended[48] = new(default, 0, double.Epsilon, -double.Epsilon, -double.MaxValue, 1);
        extended[49] = new(default, 0, -double.MaxValue, double.MaxValue, 0, 1);
        foreach (int period in new[] { 2, 3, 14, 31 })
        {
            var state = (IMultiOutputState)new PriorSeededDirectionalMeasure(measure, period, 2).CreateState();
            var output = new double[2];
            foreach (var bars in new[] { ordinary, extended, ordinary })
            {
                var expected = PriorDirectionalReference.Calculate(bars, measure, period, 2);
                state.Reset();
                for (int row = 0; row < bars.Length; row++)
                {
                    state.Update(in bars[row], output);
                    Assert.Equal(expected[row].HasValue ? 1d : 0d, output[1]);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[row] ?? 0),
                        BitConverter.DoubleToInt64Bits(output[0]));
                }
            }
        }
    }
}
