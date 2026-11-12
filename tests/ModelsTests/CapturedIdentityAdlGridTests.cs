using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class CapturedIdentityAdlGridTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MixedSignsAndNonIntegerPrefixesMatchIndependentRationalHistory(bool full)
    {
        double[] volumes = [Math.ScaleB(1.1, 20), 1.3, -Math.ScaleB(1.1, 20), -1.3];
        await Check(Enumerable.Range(0, 257).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 3, 1, 3, volumes[i % 4])).ToArray(), full);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task LateWideGridRestoresEveryEarlierExactPrefix(bool full)
    {
        double[] volumes = [Math.ScaleB(1.1, 20), 1.3, -Math.ScaleB(1.1, 20), -1.3,
            double.Epsilon, Math.ScaleB(1, 130), -Math.ScaleB(1, 130), -double.Epsilon, 1];
        await Check(volumes.Select((volume, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 3, 1, 3, volume)).ToArray(), full);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HiddenExtendedFlowCanCancelAgainstAFiniteEarlierPrefix(bool full)
    {
        await Check([
            new(DateTime.UnixEpoch, 0, 1, 0, 1, double.MaxValue * .75),
            new(DateTime.UnixEpoch.AddMinutes(1), 0, 1, 0, -.25, double.MaxValue),
            new(DateTime.UnixEpoch.AddMinutes(2), 0, 1, 0, 1, double.MaxValue * .25),
            new(DateTime.UnixEpoch.AddMinutes(3), 0, 1, 1, double.MaxValue, double.MaxValue)
        ], full);
    }

    private static async Task Check(Bar[] bars, bool full)
    {
        var indicator = new Adl(1);
        // The independent extended rational formula retains hidden flows above
        // binary64 range. Only the rounded cumulative prefix is published.
        var expected = BuiltInFormulaReferences.MoneyFlowAccumulationOutputs(bars, indicator)["Adl"];
        Assert.All(expected, value => Assert.True(double.IsFinite(value)));
        var input = bars.ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
            .ConfigureIndicators(indicator)
            .ConfigureHistory(full ? IndicatorHistoryMode.Full : IndicatorHistoryMode.LatestOnly).BuildAsync();
        Array.Clear(input);
        foreach (var output in indicator.Outputs)
            Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits),
                run[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
        Assert.Equal(bars[^1], run.Latest.Bar);
        if (!full) return;
        int row = 0;
        await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
        Assert.Equal(bars.Length, row);
    }
}
