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

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PairCertificateEdgesAndRejectedWideCancellationKeepEveryBit(bool full)
    {
        foreach (int exponent in new[] { -399, -398, 0, 399, 400 })
        {
            double a = Math.ScaleB(1.1, exponent + 48), b = Math.ScaleB(1.3, exponent);
            await Check(FromVolumes([a, b, -a]), full);
        }
        // One extra zero changes the conservative count bound from 103 to
        // 104 bits. Wider exponent spans also retain the word/wide routes.
        double ordinary = Math.ScaleB(1.1, 48);
        await Check(FromVolumes([ordinary, 1.3, -ordinary, 0]), full);
        foreach (int span in new[] { 51, 52 })
        {
            double a = Math.ScaleB(1.1, span);
            await Check(FromVolumes([a, 1.3, -a]), full);
        }
        // Unguarded compensated addition loses the small term in this history.
        // The private certificate must reject it and preserve the exact prefix.
        double huge = Math.ScaleB(1, 200), tiny = Math.ScaleB(1, -200);
        await Check(FromVolumes([huge, 1, tiny, -huge, -1]), full, tiny);
    }

    [Theory]
    [InlineData(8191, false)] [InlineData(8191, true)]
    [InlineData(8192, false)] [InlineData(8192, true)]
    public async Task BothSidesOfSizeGuardPreservePairWordAndWidePrefixes(int count, bool full)
    {
        foreach (int span in new[] { 35, 38 })
        {
            double large = Math.ScaleB(1.1, span);
            double[] cycle = [large, 1.3, -large, -1.3];
            await Check(FromVolumes(Enumerable.Range(0, count).Select(i => cycle[i % 4]).ToArray()), full);
        }
        double huge = Math.ScaleB(1, 200), tiny = Math.ScaleB(1, -200);
        double[] volumes = new double[count];
        new[] { huge, 1, tiny, -huge, -1 }.CopyTo(volumes, 0);
        await Check(FromVolumes(volumes), full, tiny);
    }

    private static Bar[] FromVolumes(double[] volumes) => volumes.Select((volume, i) =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 3, 1, 3, volume)).ToArray();

    private static async Task Check(Bar[] bars, bool full, double? finalExpected = null)
    {
        var indicator = new Adl(1);
        // The independent extended rational formula retains hidden flows above
        // binary64 range. Only the rounded cumulative prefix is published.
        var expected = BuiltInFormulaReferences.MoneyFlowAccumulationOutputs(bars, indicator)["Adl"];
        Assert.All(expected, value => Assert.True(double.IsFinite(value)));
        if (finalExpected.HasValue)
            Assert.Equal(BitConverter.DoubleToInt64Bits(finalExpected.Value), BitConverter.DoubleToInt64Bits(expected[^1]));
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
