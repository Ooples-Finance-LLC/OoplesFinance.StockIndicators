using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VolumeMeanNumericalTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 2)]
    [InlineData(5, 2)]
    public void WideAndSmallVolumeTotalsRoundSubnormalMidpointsToEven(int ticks, int expectedTicks)
    {
        foreach (var wide in new[] { false, true })
        foreach (var sign in new[] { -1, 1 })
        {
            var mean = new ExactVolumeMean();
            mean.Add(0, 1);
            mean.Add(sign * ticks * double.Epsilon, 1);
            if (wide)
            {
                mean.Add(0, double.Epsilon);
                mean.Add(sign * ticks * double.Epsilon, double.Epsilon);
            }
            var expected = sign * (expectedTicks * double.Epsilon);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(mean.Value()));
        }
    }

    [Fact]
    public void OrdinaryBinaryPricesAndIntegralVolumeDoNotAllocate()
    {
        static double Run()
        {
            var mean = new ExactVolumeMean();
            mean.Add(100.125, 1000);
            mean.Add(99.875, 1000);
            return mean.Value();
        }
        for (var i = 0; i < 100; i++) Run();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0d;
        for (var i = 0; i < 1000; i++) sum += Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(100000, sum);
    }

    [Fact]
    public void ChainedVwapUsesTheSelectedSeriesAndRetainsItAfterReset()
    {
        var close = new List<double> { 10, 20, 30 };
        var selected = new List<double> { double.Epsilon, 2 * double.Epsilon, 3 * double.Epsilon };
        var volumes = new List<double> { double.MaxValue, double.MaxValue, double.MaxValue };
        var dates = Enumerable.Range(0, 3).Select(i => DateTime.UnixEpoch.AddDays(i)).ToList();
        StockData Data() => new(close, close, close, close, volumes, dates) { CustomValuesList = selected.ToList() };
        var expected = new[] { double.Epsilon, 2 * double.Epsilon, 2 * double.Epsilon };
        Assert.Equal(expected, Data().CalculateVolumeWeightedAveragePrice().CustomValuesList);
        using var context = new ComputeContext();
        using var buffer = IndicatorCompute.ComputeVwapFast(Data(), context);
        Assert.Equal(expected, buffer.ToArray());
        var state = new VolumeWeightedAveragePriceState();
        ((ICustomInputConsumer)state).ReadCloseAsInput();
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < selected.Count; i++)
            {
                var bar = new OhlcvBar("CHAIN", BarTimeframe.Minutes(1), dates[i], dates[i], close[i], close[i], close[i], selected[i], volumes[i], true);
                Assert.Equal(expected[i], state.Update(bar, false, true).Value);
                Assert.Equal(expected[i], state.Update(bar, true, true).Value);
            }
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator b && (b.BatchName is
            IndicatorName.VolumeWeightedAveragePrice or IndicatorName.WindowedVolumeWeightedMovingAverage || BuiltInFormulaReferences.HasSimpleVolumeMean(b)))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task DiscoveredConfigurationsReceiveEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Fact]
    public void ProductsAndRatiosMatchIndependentRationalsAcrossTheBinary64Range()
    {
        var random = new Random(245);
        for (var trial = 0; trial < 500; trial++)
        {
            var mean = new ExactVolumeMean();
            var numerator = new ReferenceFraction(0);
            var denominator = new ReferenceFraction(0);
            for (var term = 0; term < 8; term++)
            {
                double Next()
                {
                    var value = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
                    return double.IsFinite(value) ? value : double.Epsilon;
                }
                var price = Next(); var volume = Next(); var taper = random.Next(-20, 21);
                mean.Add(price, volume, taper);
                var weight = ReferenceFraction.FromDouble(volume) * new ReferenceFraction(taper);
                numerator += ReferenceFraction.FromDouble(price) * weight;
                denominator += weight;
                Assert.Equal(denominator.Sign == 0 ? 0 : (numerator / denominator).ToDouble(), mean.Value());
            }
        }
    }

    [Theory]
    [InlineData(double.MaxValue, double.MaxValue)]
    [InlineData(double.Epsilon, double.Epsilon)]
    [InlineData(1d, -double.MaxValue)]
    public void ProductsNeedNotBeRepresentableAndPreviewCopiesRemainIndependent(double price, double volume)
    {
        var mean = new ExactVolumeMean();
        mean.Add(price, volume);
        Assert.Equal(price, mean.Value());
        var preview = mean;
        preview.Add(-price, volume);
        Assert.Equal(0, preview.Value());
        Assert.Equal(price, mean.Value());
        mean.Add(price, -volume);
        Assert.Equal(0, mean.Value());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task AllRoutesPreviewAndResetMatchRationalReferences(int period)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(48, 245))
        {
            var bars = fixture.Bars.ToArray();
            var expected = BuiltInFormulaReferences.RoundedVolumeWeightedPrice(bars);
            var rolling = BuiltInFormulaReferences.RoundedRollingVolumeMean(bars, period);
            var windowed = BuiltInFormulaReferences.RoundedWindowedVolumeMean(bars, period);
            var high = bars.Select(b => b.High).ToArray(); var low = bars.Select(b => b.Low).ToArray();
            var close = bars.Select(b => b.Close).ToArray(); var volume = bars.Select(b => b.Volume).ToArray();
            var actual = new double[bars.Length];
            VolumeCore.VolumeWeightedAveragePrice(high, low, close, volume, actual);
            Assert.Equal(expected, actual);
            MovingAverageCore.VolumeWeightedAveragePrice(close, high, low, volume, actual);
            Assert.Equal(expected, actual);
            MovingAverageCore.WindowedVolumeWeightedMovingAverage(close, volume, actual, period);
            Assert.Equal(windowed, actual);
            MovingAverageCore.VolumeWeightedMovingAverage(close, volume, actual, period);
            Assert.Equal(rolling, actual);
            var vwma = new VolumeWeightedMovingAverage(period);
            var vwap = new Vwap(period); var window = new WindowedVolumeWeightedMovingAverage(period);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(vwap, window, vwma).BuildAsync();
            Assert.Equal(expected, run[vwap].ToArray());
            Assert.Equal(windowed, run[window].ToArray());
            Assert.Equal(rolling, run[vwma].ToArray());
            StockData Data() => new(bars.Select(b => b.Open).ToList(), high.ToList(), low.ToList(), close.ToList(),
                volume.ToList(), bars.Select(b => b.Time).ToList());
            Assert.Equal(expected, Data().CalculateVolumeWeightedAveragePrice().CustomValuesList);
            Assert.Equal(windowed, Data().CalculateWindowedVolumeWeightedMovingAverage(period).CustomValuesList);
            Assert.Equal(rolling, Data().CalculateVolumeWeightedMovingAverage(length: period).CustomValuesList);
            using var rollingState = new VolumeWeightedMovingAverageState(length: period);
            var state = new VolumeWeightedAveragePriceState();
            using var windowState = new WindowedVolumeWeightedMovingAverageState(period);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset(); windowState.Reset(); rollingState.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("VOLUME", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    Assert.Equal(rolling[i], rollingState.Update(input, false, true).Value);
                    Assert.Equal(rolling[i], rollingState.Update(input, true, true).Value);
                    Assert.Equal(expected[i], state.Update(input, false, true).Value);
                    Assert.Equal(expected[i], state.Update(input, true, true).Value);
                    Assert.Equal(windowed[i], windowState.Update(input, false, true).Value);
                    Assert.Equal(windowed[i], windowState.Update(input, true, true).Value);
                }
            }
        }
    }
}
