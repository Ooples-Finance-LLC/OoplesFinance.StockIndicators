using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class WindowExtremeTests
{
    public static IEnumerable<object[]> Names =>
        WindowExtremeComparison.Names.Select(n => new object[] { n });

    [Theory, MemberData(nameof(Names))]
    public void RepeatedExtremaEvictionAndPeriods(string name)
    {
        var pair = ComparisonPairs.Get("TaLib.Functions." + name);
        foreach (var period in new[] { 2, 3, 7, 20, int.MaxValue })
            ComparisonVerifier.Check(pair, WindowExtremeComparison.Fixture(), period);
        var data = CompetitorData.FromOhlc(
            [1, 2, 3, 4],
            [10, 20, 15, 8],
            [-1, -2, -5, -1],
            [3, 3, 2, 2]
        );
        ComparisonVerifier.Check(pair, data, 2);
        var output = pair.Ooples(data, 2).Outputs;
        if (name == "MinIndex")
            Assert.Equal(new[] { 1d, 2d, 3d }, output[name].Values.Skip(1));
        if (name == "MaxIndex")
            Assert.Equal(new[] { 1d, 1d, 3d }, output[name].Values.Skip(1));
        if (name == "MidPoint")
            Assert.Equal(new[] { 3d, 2.5, 2d }, output[name].Values.Skip(1));
        if (name == "MidPrice")
            Assert.Equal(new[] { 9d, 7.5, 5d }, output[name].Values.Skip(1));
    }

    [Theory, MemberData(nameof(Names))]
    public void NativePeriodOneRejectionIsExplicit(string name)
    {
        double[] input = [1, 2, 3],
            a = new double[3],
            b = new double[3];
        var indices = new int[3];
        var code = name switch
        {
            "MinIndex" => Functions.MinIndex<double>(input, System.Range.All, indices, out _, 1),
            "MaxIndex" => Functions.MaxIndex<double>(input, System.Range.All, indices, out _, 1),
            "MinMaxIndex" => Functions.MinMaxIndex<double>(input, System.Range.All, a, b, out _, 1),
            "MidPoint" => Functions.MidPoint<double>(input, System.Range.All, a, out _, 1),
            _ => Functions.MidPrice<double>(input, input, System.Range.All, a, out _, 1),
        };
        Assert.Equal(TALib.Core.RetCode.BadParam, code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LifecycleIndependentReferenceAndLazyHugePeriod(bool variant)
    {
        foreach (var period in new[] { 1, 3, 20 })
        {
            foreach (var midpoint in new[] { false, true })
            {
                var report = await IndicatorValidation.ValidateAsync(
                    new IndicatorValidationCase(
                        midpoint ? typeof(WindowRangeMidpoint) : typeof(WindowExtremeIndex),
                        "rolling window",
                        () =>
                            midpoint
                                ? new WindowRangeMidpoint(period, variant)
                                : new WindowExtremeIndex(period, variant)
                    )
                );
                Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
            }
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowExtremeIndex(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowRangeMidpoint(0));
    }

    [Fact]
    public async Task MaximumPeriodConsumesOnlyObservedHistory()
    {
        var minimum = new WindowExtremeIndex(int.MaxValue);
        var maximum = new WindowExtremeIndex(int.MaxValue, true);
        var midpoint = new WindowRangeMidpoint(int.MaxValue);
        var data = CompetitorData.FromCloses([1, 2, 3]);
        for (var repeat = 0; repeat < 2; repeat++)
        {
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(minimum, maximum, midpoint)
                .BuildAsync();
            Assert.Equal(new[] { 0d, 0d, 0d }, run[minimum.Outputs[0]].ToArray());
            Assert.Equal(new[] { 0d, 1d, 2d }, run[maximum.Outputs[0]].ToArray());
            Assert.Equal(new[] { 1d, 1.5, 2d }, run[midpoint.Outputs[0]].ToArray());
        }
    }

    [Fact]
    public async Task ExactTiesAndOverflowSafeMidpoints()
    {
        var min = new WindowExtremeIndex(2);
        var max = new WindowExtremeIndex(2, true);
        var midpoint = new WindowRangeMidpoint(2);
        var range = new WindowRangeMidpoint(2, true);
        var source = new[]
        {
            double.Epsilon,
            2 * double.Epsilon,
            2 * double.Epsilon,
            double.MaxValue,
            double.MaxValue,
        };
        var bars = source
            .Select((x, i) => new Bar(DateTime.UnixEpoch.AddDays(i), x, x, x, x, 1))
            .ToArray();
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(min, max, midpoint, range)
            .BuildAsync();
        Assert.Equal(new[] { 0d, 0d, 2d, 2d, 4d }, run[min.Outputs[0]].ToArray());
        Assert.Equal(new[] { 0d, 1d, 2d, 3d, 4d }, run[max.Outputs[0]].ToArray());
        Assert.Equal(2 * double.Epsilon, run[midpoint.Outputs[0]].ToArray()[1]);
        Assert.Equal(double.MaxValue, run[midpoint.Outputs[0]].ToArray()[^1]);
        Assert.Equal(double.MaxValue, run[range.Outputs[0]].ToArray()[^1]);
        double[] nativeInput = [double.MaxValue, double.MaxValue];
        var nativeOutput = new double[2];
        Assert.Equal(
            TALib.Core.RetCode.Success,
            Functions.MidPoint<double>(nativeInput, System.Range.All, nativeOutput, out _, 2)
        );
        Assert.True(double.IsPositiveInfinity(nativeOutput[0]));
        Assert.Equal(
            TALib.Core.RetCode.Success,
            Functions.MidPrice<double>(
                nativeInput,
                nativeInput,
                System.Range.All,
                nativeOutput,
                out _,
                2
            )
        );
        Assert.True(double.IsPositiveInfinity(nativeOutput[0]));
    }
}
