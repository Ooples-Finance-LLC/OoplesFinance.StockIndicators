using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class CommodityChannelComparisonTests
{
    [Theory]
    [InlineData(1, false, false)]
    [InlineData(3, false, false)]
    [InlineData(3, false, true)]
    [InlineData(1, true, false)]
    [InlineData(3, true, false)]
    [InlineData(3, true, true)]
    public async Task IndependentLifecycleContracts(int period, bool rolling, bool flatZero)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WindowCommodityChannelIndex),
                "commodity channel",
                () => new WindowCommodityChannelIndex(period, rolling, flatZero)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    private static Bar[] Raw(double[] prices) =>
        prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0)).ToArray();

    [Fact]
    public void KnownCoefficientsAndDistinctCentersHaveCorrectStartup()
    {
        var bars = Raw([1, 2, 3, 4, 5]);
        var standard = CommodityChannelComparison.Owned(bars, 3, 0).Outputs["Value"];
        var rolling = CommodityChannelComparison.Owned(bars, 3, 2).Outputs["Value"];
        Assert.Equal(new[] { false, false, true, true, true }, standard.Present);
        Assert.Equal(new[] { false, false, false, false, true }, rolling.Present);
        Assert.Equal(100, standard.Values[4]);
        Assert.Equal(200d / 3, rolling.Values[4]);
        var flat = Raw([3, 3, 3, 4, 3, 3, 3, 3, 3]);
        var a = CommodityChannelComparison.Owned(flat, 3, 0).Outputs["Value"];
        var b = CommodityChannelComparison.Owned(flat, 3, 2).Outputs["Value"];
        Assert.False(a.Present![2]);
        Assert.True(a.Present[3]);
        Assert.False(a.Present[6]);
        Assert.True(b.Present![6]);
        Assert.Equal(0, b.Values[6]);
        Assert.True(b.Present[8]);
        Assert.Equal(0, b.Values[8]);
        Assert.Equal(0, CommodityChannelComparison.Owned(flat, 3, 1).Outputs["Value"].Values[2]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void IndependentReferencesCoverTinyWideAndMaximumPeriodInputs(int variant)
    {
        foreach (
            var prices in new[]
            {
                new[] { 0d, 0, double.Epsilon, 2 * double.Epsilon, 0, 3 * double.Epsilon },
                new[]
                {
                    double.MaxValue,
                    -double.MaxValue,
                    0,
                    double.MaxValue / 2,
                    -double.MaxValue / 2,
                    0,
                },
                new[] { 1d, Math.BitIncrement(1), 1, 1, Math.BitDecrement(1), 1 },
            }
        )
        foreach (var period in new[] { 2, 3, int.MaxValue })
        {
            var bars = Raw(prices);
            ComparisonVerifier.Compare(
                VolumePriceComparison.Mask(
                    CommodityChannelComparison.Reference(bars, period, variant)
                ),
                CommodityChannelComparison.Owned(bars, period, variant),
                "wide/tiny CCI"
            );
        }
        var data = CommodityChannelComparison.Fixture();
        foreach (var period in variant == 1 ? new[] { 2, 3, 7 } : new[] { 1, 2, 3, 7 })
            ComparisonVerifier.Check(CommodityChannelComparison.Create(variant), data, period);
        Assert.Equal(
            400d / 3,
            CommodityChannelComparison
                .Owned(Raw([0, double.Epsilon]), 2, 0)
                .Outputs["Value"]
                .Values[1]
        );
    }

    [Fact]
    public void NativePrecisionAndOverflowFailuresRemainVisible()
    {
        var tiny = new[] { 0d, double.Epsilon };
        var packed = new double[2];
        Assert.Equal(
            TALib.Core.RetCode.Success,
            Functions.Cci<double>(tiny, tiny, tiny, System.Range.All, packed, out _, 2)
        );
        Assert.Equal(0, packed[0]);
        var data = CompetitorData.FromOhlc(tiny, tiny, tiny, tiny);
        Assert.Null(data.Quotes.GetCci(2).Last().Cci);
        Assert.True(
            CommodityChannelComparison.Owned(data.IndicatorBars, 2, 0).Outputs["Value"].Present![1]
        );
        var huge = new[] { double.MaxValue, -double.MaxValue, 0d };
        var values = new double[3];
        Assert.Equal(
            TALib.Core.RetCode.Success,
            Functions.Cci<double>(huge, huge, huge, System.Range.All, values, out _, 2)
        );
        Assert.False(double.IsFinite(values[0]));
        Assert.Throws<OverflowException>(() =>
            new T.CommodityChannelIndexByTuple(
                new[] { (decimal.MaxValue, decimal.MaxValue, decimal.MaxValue) },
                1
            )
                .Compute()
                .ToArray()
        );
        decimal v = .0000000000000000000000000004m;
        Assert.Throws<DivideByZeroException>(() =>
            new T.CommodityChannelIndexByTuple(new[] { (0m, 0m, 0m), (0m, 0m, 0m), (v, v, v) }, 2)
                .Compute()
                .ToArray()
        );
        Assert.True(
            double.IsFinite(
                CommodityChannelComparison
                    .Owned(Raw([0, 0, (double)v]), 2, 2)
                    .Outputs["Value"]
                    .Values[2]
            )
        );
    }

    [Fact]
    public void TradyGenericTupleIndexRangeAndRepeatedEnumerationRoutes()
    {
        var data = CommodityChannelComparison.Fixture();
        var inputs = data.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray();
        var expected = new T.CommodityChannelIndex(data.Candles, 3)
            .Compute()
            .Select(v => v.Tick)
            .ToArray();
        var tuple = new T.CommodityChannelIndexByTuple(inputs, 3);
        Assert.Equal(expected, tuple.Compute());
        Assert.Equal(expected, tuple.Compute());
        var indexes = new[] { 8, 2, 2, 0, 5 };
        Assert.Equal(indexes.Select(i => expected[i]), tuple.Compute((IEnumerable<int>)indexes));
        Assert.Equal(expected.Skip(2).Take(6), tuple.Compute(startIndex: 2, endIndex: 7));
        foreach (var i in indexes)
            Assert.Equal(expected[i], tuple[i]);
        Assert.Equal(
            expected,
            new T.CommodityChannelIndex<int, decimal?>(
                Enumerable.Range(0, inputs.Length),
                i => inputs[i],
                3
            ).Compute()
        );
        Assert.Equal(expected, CommodityChannelComparison.TradyReference(inputs, 3));
    }

    [Fact]
    public void NativeParameterBoundariesAndSkenderSortingDefault()
    {
        var data = CommodityChannelComparison.Fixture();
        Assert.Equal(
            data.Quotes.GetCci(3).Select(r => (r.Date, r.Cci)),
            data.Quotes.AsEnumerable().Reverse().GetCci(3).Select(r => (r.Date, r.Cci))
        );
        Assert.Equal(
            data.Quotes.GetCci(20).Select(r => r.Cci),
            data.Quotes.GetCci().Select(r => r.Cci)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetCci(0).ToArray());
        Assert.Throws<InvalidOperationException>(() =>
            new T.CommodityChannelIndex(data.Candles, 0).Compute().ToArray()
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new T.CommodityChannelIndex(data.Candles, -1).Compute().ToArray()
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new T.CommodityChannelIndex(data.Candles, int.MaxValue).Compute().ToArray()
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowCommodityChannelIndex(0));
    }

    [Fact]
    public void TaLibRangesAliasingFloatAndOneElementLimit()
    {
        var data = CommodityChannelComparison.Fixture();
        var output = new double[data.Count];
        Assert.Equal(
            TALib.Core.RetCode.Success,
            Functions.Cci<double>(
                data.Highs,
                data.Lows,
                data.Closes,
                4..8,
                output,
                out var range,
                3
            )
        );
        Assert.Equal(4..9, range);
        var expected = CommodityChannelComparison.FloatingReference(
            data.IndicatorBars.Skip(2).Take(7).Select(b => (b.High, b.Low, b.Close)).ToArray(),
            3,
            true
        );
        Assert.Equal(expected.Skip(2).Select(v => v!.Value), output.Take(5));
        var alias = (double[])data.Closes.Clone();
        Assert.Equal(
            TALib.Core.RetCode.Success,
            Functions.Cci<double>(data.Highs, data.Lows, alias, System.Range.All, alias, out _, 3)
        );
        Assert.Equal(
            CommodityChannelComparison
                .Create(1)
                .Competitor(data, 3)
                .Outputs["Value"]
                .Values.Skip(2),
            alias.Take(data.Count - 2)
        );
        Assert.Equal(
            TALib.Core.RetCode.BadParam,
            Functions.Cci<double>(
                data.Highs,
                data.Lows,
                data.Closes,
                System.Range.All,
                output,
                out _,
                1
            )
        );
        Assert.Equal(
            TALib.Core.RetCode.OutOfRangeParam,
            Functions.Cci<double>(
                new[] { 1d },
                new[] { 1d },
                new[] { 1d },
                System.Range.All,
                new double[1],
                out _,
                2
            )
        );
        Assert.Equal(-1, Functions.CciLookback(1));
        Assert.Equal(13, Functions.CciLookback());
        var floats = new[] { 1f, 2f, 3f };
        var floatOutput = new float[3];
        Assert.Equal(
            TALib.Core.RetCode.Success,
            Functions.Cci<float>(floats, floats, floats, System.Range.All, floatOutput, out _, 2)
        );
        Assert.Equal(.5f / (.015f * .5f), floatOutput[1]);
    }

    [Fact]
    public async Task ChainingReplacesCloseAndPreservesCandleRanges()
    {
        var data = CommodityChannelComparison.Fixture();
        var closes = FixedWeightedComparison.Stage(data.Closes, 2, false);
        var bars = data
            .IndicatorBars.Select(
                (b, i) => new Bar(b.Time, b.Open, b.High, b.Low, closes[i], b.Volume)
            )
            .ToArray();
        foreach (var variant in new[] { 0, 1, 2 })
        {
            var indicator = new WindowCommodityChannelIndex(3, variant == 2, variant != 0);
            indicator.Of(new FixedPeriodWma(2));
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(data.IndicatorBars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            var expected = CommodityChannelComparison.Reference(bars, 3, variant);
            Assert.Equal(expected.Select(v => v ?? 0), run[indicator.Value].ToArray());
            Assert.Equal(
                expected.Select(v => v.HasValue ? 1d : 0),
                run[indicator.IsDefined].ToArray()
            );
        }
    }

    [Fact]
    public void ValuesPresenceCentersAndStartupMutationsAreDetected()
    {
        foreach (var pair in CommodityChannelComparison.Pairs)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                if (presence)
                    result.Outputs["Value"].Present![^1] = false;
                else
                    result.Outputs["Value"].Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    CommodityChannelComparison.Fixture(),
                    3
                )
            );
        }
        var rolling = CommodityChannelComparison.Create(2);
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                rolling with
                {
                    Library = CommodityChannelComparison.Create(0).Ooples,
                },
                CommodityChannelComparison.Fixture(),
                3
            )
        );
    }
}
