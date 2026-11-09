using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class FixedGridAccumulationDistributionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CertifiedAndWiderPrefixesPreserveIndependentFractions(bool full)
    {
        double[][] patterns = [
            [1, Math.BitIncrement(1), -1, -Math.BitIncrement(1),
                Math.BitDecrement(1), 1, -1, -Math.BitDecrement(1)],
            [1, Math.ScaleB(1, -44), -1, -Math.ScaleB(1, -44)],
            [1, Math.ScaleB(1, -180), -1, -Math.ScaleB(1, -180)],
            [0, -0d]];
        foreach (int count in new[] { 512, 513 })
        foreach (int exponent in new[] { -1021, 0, 1021 })
        foreach (double[] pattern in patterns)
        {
            // At normal scales, the 44-binade pattern straddles the 106-bit
            // bound at these adjacent counts. Wider spans keep the fallback.
            var flows = Enumerable.Range(0, count)
                .Select(i => Math.ScaleB(pattern[i % pattern.Length], exponent)).ToArray();
            var bars = flows.Select((flow, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                0, 1, 0, 1, flow)).ToArray();
            await Check(flows, bars, full);
        }
    }

    private static async Task Check(double[] flows, Bar[] bars, bool full)
    {
        var prefixes = new double[flows.Length];
        var means = new double[flows.Length];
        var sum = new ReferenceFraction(0);
        for (int i = 0; i < flows.Length; i++)
        {
            sum += ReferenceFraction.FromDouble(flows[i]);
            prefixes[i] = sum.ToDouble();
            if (i != 0) means[i] = ((ReferenceFraction.FromDouble(prefixes[i - 1])
                + ReferenceFraction.FromDouble(prefixes[i])) / new ReferenceFraction(2)).ToDouble();
        }
        var indicator = new AccumulationDistributionWithAverage(2);
        using var actual = await Build(bars, indicator, full).BuildAsync();
        Assert.Equal(prefixes.Select(BitConverter.DoubleToInt64Bits),
            actual[indicator.Outputs[2]].ToArray().Select(BitConverter.DoubleToInt64Bits));
        Assert.Equal(means.Select(BitConverter.DoubleToInt64Bits),
            actual[indicator.Outputs[3]].ToArray().Select(BitConverter.DoubleToInt64Bits));
        using var generic = await Build(bars, indicator, full).ConfigureBehavior(_ => { }).BuildAsync();
        foreach (var output in indicator.Outputs)
            Assert.Equal(generic[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }

    [Fact]
    public async Task CertifiedOverflowRemainsDeferredAndInputErrorsStillPrecedeIt()
    {
        foreach (bool full in new[] { false, true })
        foreach (bool invalid in new[] { false, true })
        {
            var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, 0, 1, 0, 1,
                i < 2 ? double.MaxValue : i < 4 ? -double.MaxValue : 0)).ToArray();
            if (invalid) bars[^1] = new(default, double.NaN, 1, 0, 1, 0);
            var indicator = new AccumulationDistributionWithAverage(2);
            var expected = await Record.ExceptionAsync(async () =>
            {
                using var run = await Build(bars, indicator, full)
                    .ConfigureBehavior(_ => { }).BuildAsync();
            });
            var actual = await Record.ExceptionAsync(async () =>
            {
                using var run = await Build(bars, indicator, full).BuildAsync();
            });
            Assert.NotNull(expected); Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(expected.Message, actual.Message);
            if (invalid) Assert.IsType<ArgumentOutOfRangeException>(actual);
            else Assert.IsType<IndicatorOutputException>(actual);
        }
    }

    private static StockIndicatorBuilder Build(Bar[] bars,
        AccumulationDistributionWithAverage indicator, bool full) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
        .ConfigureHistory(full ? IndicatorHistoryMode.Full : IndicatorHistoryMode.LatestOnly);
}
