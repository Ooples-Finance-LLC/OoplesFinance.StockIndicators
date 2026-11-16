using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class NamedComparisonPublisherTests
{
    private static readonly string[] Ids = ["Skender.GetAwesome", "QuanTAlib.Historical", "QuanTAlib.Realized",
        "QuanTAlib.Mgdi", "Skender.GetDynamic", "Skender.GetElderRay", "QuanTAlib.Slope", "Skender.GetSlope"];

    public static IEnumerable<object[]> Cases => Ids.SelectMany(id => new[] { 37, 257 }
        .Select(count => new object[] { id, count }));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EveryAdapterPreservesAllOriginalColumnsAfterDisposal(string id, int count)
    {
        const int period = 20;
        var pair = ComparisonPairs.All.Single(p => p.Id == id);
        var data = ComparisonVerifier.BenchmarkFixture(pair, count);
        string[] names = id switch
        {
            "Skender.GetAwesome" => ["Oscillator", "Normalized"],
            "Skender.GetElderRay" => ["Ema", "BullPower", "BearPower"],
            "QuanTAlib.Slope" or "Skender.GetSlope" => ["Slope", "Intercept", "StdDev", "RSquared", "Line"],
            _ => ["Value"]
        };
        IIndicator indicator = id switch
        {
            "Skender.GetAwesome" => new AwesomeWithDetails(5, 34),
            "QuanTAlib.Historical" => new WindowLogVolatility(period, false, true),
            "QuanTAlib.Realized" => new WindowLogVolatility(period, true, true),
            "QuanTAlib.Mgdi" => new QuarticDynamicAverage(period, .6, McGinleyStartup.Immediate),
            "Skender.GetDynamic" => new QuarticDynamicAverage(period, .6, McGinleyStartup.ResetDelay),
            "Skender.GetElderRay" => new ElderRayWithDetails(period),
            "QuanTAlib.Slope" => new WindowRegressionStatistics(period),
            "Skender.GetSlope" => new RegressionWithFinalLine(period),
            _ => throw new InvalidOperationException(id)
        };
        var expected = new Dictionary<string, ComparisonOutput>();
        using (var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator).BuildAsync())
        {
            for (int slot = 0; slot < names.Length; slot++)
            {
                var values = run[indicator.Outputs[slot]].ToArray();
                var flags = run[indicator.Outputs[id == "Skender.GetElderRay" ? 3 : slot + names.Length]].ToArray();
                expected.Add(names[slot], Legacy(values, flags));
            }
        }
        var actual = pair.Ooples(data, period);
        Assert.Equal(names, actual.Outputs.Keys);
        foreach (string name in names) Same(expected[name], actual.Outputs[name]);
        for (int i = 0; i < names.Length; i++)
            for (int j = i + 1; j < names.Length; j++)
            {
                Assert.NotSame(actual.Outputs[names[i]].Values, actual.Outputs[names[j]].Values);
                Assert.NotSame(actual.Outputs[names[i]].Present, actual.Outputs[names[j]].Present);
            }
        foreach (var column in actual.Outputs.Values)
        {
            column.Values[0] = 42;
            var present = column.Present!;
            present[0] = !present[0];
        }
        var repeated = pair.Ooples(data, period);
        foreach (string name in names) Same(expected[name], repeated.Outputs[name]);
    }

    [Fact]
    public void ColumnPreservesSpecialBitsAndIndependentOwnership()
    {
        double[] values = [0d, -0d, double.Epsilon, -double.Epsilon, double.MaxValue,
            double.PositiveInfinity, double.NegativeInfinity, double.NaN,
            BitConverter.Int64BitsToDouble(unchecked((long)0xfff8000000001234UL))];
        foreach (double flag in new[] { 0d, -0d, 1d, -1d, double.NaN, double.PositiveInfinity,
            double.NegativeInfinity, double.Epsilon, -double.Epsilon })
        {
            var input = values.ToArray();
            var flags = Enumerable.Repeat(flag, input.Length).ToArray();
            var expected = Legacy(input, flags);
            var actual = NamedComparisonPublisher.Column(input, flags);
            Array.Fill(input, 42); Array.Fill(flags, 42);
            Same(expected, actual);
        }
    }

    [Fact]
    public void SharedInputMaskDoesNotAliasPublishedColumns()
    {
        double[] flags = [1, 0];
        var first = NamedComparisonPublisher.Column([1, 2], flags);
        var second = NamedComparisonPublisher.Column([3, 4], flags);
        first.Values[0] = 42; first.Present![0] = false;
        Assert.Equal(3, second.Values[0]); Assert.True(second.Present![0]); Assert.Equal(1, flags[0]);
    }

    [Fact]
    public void EmptySingleRetainsItsValueColumn()
    {
        var result = NamedComparisonPublisher.Single([], []);
        Assert.Equal(new[] { "Value" }, result.Outputs.Keys);
        Same(Legacy([], []), result.Outputs["Value"]);
    }

    [Fact]
    public void MismatchedPresenceIsRejected() =>
        Assert.Throws<ArgumentException>(() => NamedComparisonPublisher.Column([1], []));

    private static ComparisonOutput Legacy(double[] values, double[] flags)
    {
        var nullable = values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray();
        return new(0, nullable.Select(v => v ?? double.NaN).ToArray(), nullable.Select(v => v.HasValue).ToArray());
    }

    private static void Same(ComparisonOutput expected, ComparisonOutput actual)
    {
        Assert.Equal(expected.FirstValid, actual.FirstValid);
        Assert.Equal(expected.Present, actual.Present);
        Assert.Equal(expected.Values.Select(BitConverter.DoubleToInt64Bits), actual.Values.Select(BitConverter.DoubleToInt64Bits));
    }
}
