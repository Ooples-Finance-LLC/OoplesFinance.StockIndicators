using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CascadeFallbackNumericalTests
{
    private static StockData Data(double[] values) => new(values, values, values, values, values.Select(_ => 1d), values.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void BatchOnlyAverageDoesNotRequireAnUnusedStreamingSmoother(int variant)
    {
        var input = new[] { 1d, 4, 2, 7, -1 }; const int length = 3; const MovingAvgType kind = MovingAvgType.DampedSineWaveWeightedFilter;
        var adjusted = variant == 3 ? input.Select((v, i) => i == 0 ? v : v + (v - input[i - 1])).ToArray() : input;
        var first = Data(adjusted).CalculateDampedSineWaveWeightedFilter(length).CustomValuesList.ToArray();
        var second = Data(first).CalculateDampedSineWaveWeightedFilter(length).CustomValuesList.ToArray();
        var expected = variant == 3 ? first : first.Select((v, i) =>
            (new ReferenceFraction(variant == 1 ? 3 : 2) * ReferenceFraction.FromDouble(v) - new ReferenceFraction(variant == 1 ? 2 : 1) * ReferenceFraction.FromDouble(second[i])).ToDouble()).ToArray();
        if (variant == 4)
        {
            var third = Data(second).CalculateDampedSineWaveWeightedFilter(length).CustomValuesList.ToArray();
            expected = third.Select((value, i) => i == 0 || third[i - 1] == 0 ? 0 :
                (new ReferenceFraction(100) * (ReferenceFraction.FromDouble(value) - ReferenceFraction.FromDouble(third[i - 1])) / ReferenceFraction.FromDouble(Math.Abs(third[i - 1]))).ToDouble()).ToArray();
        }
        var batch = variant switch {
            0 => Data(input).CalculateGeneralizedDoubleExponentialMovingAverage(kind, length, 1),
            1 => Data(input).CalculateMcNichollMovingAverage(kind, length),
            2 => Data(input).CalculateZeroLagTripleExponentialMovingAverage(kind, length),
            4 => Data(input).CalculateTrix(kind, length),
            _ => Data(input).CalculateEhlersZeroLagExponentialMovingAverage(kind, length) };
        Assert.Equal(expected, batch.CustomValuesList);
        if (variant == 2) return; // The raw Zero-Lag TEMA API has no average-type parameter.
        using var context = new ComputeContext();
        using var actual = variant switch {
            0 => IndicatorCompute.ComputeGeneralizedDoubleEmaFast(Data(input), context, length, kind, 1),
            1 => IndicatorCompute.ComputeMcNichollMovingAverageFast(Data(input), context, length, kind),
            4 => IndicatorCompute.ComputeTrixFast(Data(input), context, length, kind),
            _ => IndicatorCompute.ComputeEhlersZeroLagEmaFast(Data(input), context, length, kind) };
        Assert.Equal(expected, actual.Span.ToArray());
    }
}
