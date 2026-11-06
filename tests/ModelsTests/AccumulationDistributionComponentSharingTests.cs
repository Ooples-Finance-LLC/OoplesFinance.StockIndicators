using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class AccumulationDistributionComponentSharingTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, -1)]
    [InlineData(true, -1)]
    public async Task FlowRoundsIndependentlyAndExactZeroBranchesPreserveAllColumns(bool full, int sign)
    {
        double multiplier = (new ReferenceFraction(1) / new ReferenceFraction(3)).ToDouble();
        double flow = (new ReferenceFraction(sign * 5) / new ReferenceFraction(3)).ToDouble();
        Assert.NotEqual(BitConverter.DoubleToInt64Bits(flow),
            BitConverter.DoubleToInt64Bits(multiplier * (sign * 5)));
        Bar[] bars =
        [
            new(DateTime.UnixEpoch, 0, 3, 0, 2, sign * 5),
            new(DateTime.UnixEpoch.AddDays(1), 0, 3, 0, 1, sign * 5),
            new(DateTime.UnixEpoch.AddDays(2), 0, 3, 0, 1.5, sign * 5),
            new(DateTime.UnixEpoch.AddDays(3), 0, 3, 0, 2, Math.CopySign(0, sign)),
            new(DateTime.UnixEpoch.AddDays(4), 0, 2, 2, 3, double.MaxValue),
            // The product is exact and normal, but division underflows. Flow
            // publication canonicalizes even a negative underflowed zero.
            new(DateTime.UnixEpoch.AddDays(5), 0, Math.ScaleB(1, 1021), -Math.ScaleB(1, 1021),
                Math.ScaleB(1, 969), sign * double.Epsilon)
        ];
        var indicator = new AccumulationDistributionWithAverage(2);
        using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .ConfigureHistory(full ? IndicatorHistoryMode.Full : IndicatorHistoryMode.LatestOnly)
            .BuildAsync();
        double[][] expected =
        [
            [multiplier, -multiplier, 0, multiplier, 0, Math.ScaleB(1, -52)],
            [flow, -flow, 0, 0, 0, 0],
            [flow, 0, 0, 0, 0, 0],
            [0, flow / 2, 0, 0, 0, 0],
            [0, 1, 1, 1, 1, 1]
        ];
        for (int slot = 0; slot < expected.Length; slot++)
            Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),
                actual[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }
}
