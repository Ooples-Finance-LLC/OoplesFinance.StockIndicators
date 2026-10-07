using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class UltimateFractionalFixtureTests
{
    // Independent Decimal evaluation: direct weights exp(p*ln(k)), exact input
    // conversion, and direct centered population moments. Binary64 expectations
    // agree at 240 and 400 decimal digits. Inputs cover ordinary, 2^1020 and
    // minimum-subnormal scales; no production helper generated these values.
    public static IEnumerable<object[]> Cases
    {
        get
        {
        yield return new object[] { new double[] { 1.0d, 3.0d, 2.0d, 4.0d, 1.0d, 5.0d }, new double[] { 1.0d, 2.0d, 3.0d, 4.0d, 5.0d, 6.0d }, new double[][] { new double[] { 0.8804347826086957d, 0.8804347826086957d, 0.8804347826086957d }, new double[] { 2.7572463768115942d, 2.7572463768115942d, 2.7572463768115942d }, new double[] { 2.1666666666666665d, 3.7996598285221186d, 0.5336735048112146d }, new double[] { 3.5702145953116644d, 5.2032077571671165d, 1.9372214334562123d }, new double[] { 1.9923343742388273d, 4.486772632088122d, -0.502103883610467d }, new double[] { 4.383823124679083d, 7.783169467074273d, 0.9844767822838932d } } };
        yield return new object[] { new double[] { 1.1235582092889474e+307d, 3.3706746278668423e+307d, 2.247116418577895e+307d, 4.49423283715579e+307d, 1.1235582092889474e+307d, 5.617791046444737e+307d }, new double[] { 1.1235582092889474e+307d, 2.247116418577895e+307d, 3.3706746278668423e+307d, 4.49423283715579e+307d, 5.617791046444737e+307d, 6.741349255733685e+307d }, new double[][] { new double[] { 9.892197277435298e+306d, 9.892197277435298e+306d, 9.892197277435298e+306d }, new double[] { 3.097926801698873e+307d, 3.097926801698873e+307d, 3.097926801698873e+307d }, new double[] { 2.4343761201260526e+307d, 4.2691389928414607e+307d, 5.996132474106447e+306d }, new double[] { 4.011343917485638e+307d, 5.846106790201046e+307d, 2.1765810447702298e+307d }, new double[] { 2.2385036418245925e+307d, 5.041150223995587e+307d, -5.641429403464024e+306d }, new double[] { 4.925480459803908e+307d, 8.744843949018381e+307d, 1.1061169705894361e+307d } } };
        yield return new object[] { new double[] { 5e-324d, 1.5e-323d, 1e-323d, 2e-323d, 5e-324d, 2.5e-323d }, new double[] { 5e-324d, 1e-323d, 1.5e-323d, 2e-323d, 2.5e-323d, 3e-323d }, new double[][] { new double[] { 5e-324d, 5e-324d, 5e-324d }, new double[] { 1.5e-323d, 1.5e-323d, 1.5e-323d }, new double[] { 1e-323d, 2e-323d, 5e-324d }, new double[] { 2e-323d, 2.5e-323d, 1e-323d }, new double[] { 1e-323d, 2e-323d, -5e-324d }, new double[] { 2e-323d, 4e-323d, 5e-324d } } };
        }
    }
    private static StockData Data(double[] prices, double[] volumes) => new(prices, prices, prices, prices, volumes,
        prices.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
    private static void Near(double expected, double actual)
    {
        if (Math.Abs(expected) <= 16 * double.Epsilon) Assert.Equal(expected, actual);
        else
        {
            Assert.Equal(Math.Sign(expected), Math.Sign(actual));
            Assert.InRange(actual, Math.BitDecrement(expected), Math.BitIncrement(expected));
        }
    }
    [Theory, MemberData(nameof(Cases))]
    public void PublicRoutesMatchIndependentFractionalPowers(double[] prices, double[] volumes, double[][] expected)
    {
        var batch = Data(prices, volumes).CalculateUltimateMovingAverage(minLength: 3, maxLength: 3);
        var bands = Data(prices, volumes).CalculateUltimateMovingAverageBands(minLength: 3, maxLength: 3);
        var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, volumes[i])).ToArray();
        var oracle = BuiltInFormulaReferences.UltimateValues(bars, 3, 3, MovingAvgType.SimpleMovingAverage, 2, true);
        using var context = new ComputeContext();
        using var fast = IndicatorCompute.ComputeUltimateMovingAverageFast(Data(prices, volumes), context, 3, 3);
        using var native = new UltimateMovingAverageState(minLength: 3, maxLength: 3);
        using var nativeBands = new UltimateMovingAverageBandsState(minLength: 3, maxLength: 3);
        for (var i = 0; i < prices.Length; i++)
        {
            var time = DateTime.UnixEpoch.AddMinutes(i); var p = prices[i];
            var bar = new OhlcvBar("UMA", BarTimeframe.Minutes(1), time, time, p, p, p, p, volumes[i], true);
            Near(expected[i][0], batch.CustomValuesList[i]); Near(expected[i][0], fast.Span[i]);
            Near(expected[i][0], native.Update(bar, true, false).Value);
            var point = nativeBands.Update(bar, true, true);
            foreach (var pair in new[] { ("MiddleBand", 0), ("UpperBand", 1), ("LowerBand", 2) })
            {
                Near(expected[i][pair.Item2], bands.OutputValues[pair.Item1][i]);
                Near(expected[i][pair.Item2], oracle[pair.Item1][i]);
                Near(expected[i][pair.Item2], point.Outputs![pair.Item1]);
            }
        }
        foreach (var pair in new[] { (IndicatorCompute.ChannelBand.Middle, 0), (IndicatorCompute.ChannelBand.Upper, 1), (IndicatorCompute.ChannelBand.Lower, 2) })
        {
            using var output = IndicatorCompute.ComputeUltimateMovingAverageBandsFast(Data(prices, volumes), context, 3, 3, 2, band: pair.Item1);
            for (var i = 0; i < prices.Length; i++) Near(expected[i][pair.Item2], output.Span[i]);
        }
    }
}
