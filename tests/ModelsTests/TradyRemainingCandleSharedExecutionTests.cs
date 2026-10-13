using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class TradyRemainingCandleSharedExecutionTests
{
    private static IndicatorBase Create(int kind, int period, int delay = 3) => kind switch
    {
        0 => new DelayedDarkCloudCoverPattern(period, delay),
        1 => new RisingThreeMethodsPattern(period, delay),
        2 => new FallingThreeMethodsPattern(period, delay),
        _ => new DragonflyDojiCandle()
    };

    private static Bar B(int i, double o, double h, double l, double c) => new(DateTime.UnixEpoch.AddMinutes(i), o, h, l, c, i + 1);
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i =>
    {
        int phase = i % 80;
        var (o,h,l,c) = phase switch
        {
            64 => (50d,62d,49d,60d), 65 => (58d,61d,52d,58.5),
            66 => (57d,60d,53d,57.5), 67 => (58d,65d,54d,64d),
            68 => (65d,66d,54d,59d), 69 => (70d,70d,50d,70d),
            _ => (48 - 2d * (63 - phase),50 - 2d * (63 - phase),47 - 2d * (63 - phase),49 - 2d * (63 - phase))
        };
        return i / 80 % 2 == 0 ? B(i,o,h,l,c) : B(i,200-o,200-l,200-h,200-c);
    }).ToArray();

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task SharedRoutesRetainExactColumnsAndOwnedHistory(int kind)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (var (period, delay) in new[] { (1,1), (3,20), (5,63), (63,5) })
            foreach (int count in new[] { 511,512,513,1001,10000 })
            {
                var bars = Data(count);
                for (int i=124; i<count; i+=125) bars[i] = B(i,double.Epsilon,double.MaxValue,-double.MaxValue,-double.Epsilon);
                var owner = Create(kind,period,delay);
                var expected = Expected(bars,owner);
                foreach (int workers in new[] { 1,2,8 })
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    await CheckOwned(bars,owner,expected,mode);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task DelayedSignalOnWorkerBoundaryReplaysTrendAndReportingQueue()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (var (period, delay) in new[] { (1,1), (3,7), (63,5), (5,63) })
            foreach (int boundary in new[] { 125,250,500,875 })
            {
                var bars = Enumerable.Range(0,1001).Select(i => B(i,0,0,0,0)).ToArray();
                int anchor = boundary-delay;
                for (int i=anchor-period; i<=anchor; i++)
                {
                    double offset = 2d*(anchor-i);
                    bars[i] = B(i,10-offset,20-offset,-offset,16-offset);
                }
                bars[anchor+1] = B(anchor+1,17,18,8,10);
                for (int i=anchor+2; i<=boundary; i++) bars[i] = B(i,30+i,40+i,20+i,36+i);
                var owner = Create(0,period,delay);
                var state = (IIndicatorState)owner.CreateState();
                var expected = bars.Select(b => state.Update(b)).ToArray();
                Assert.Equal(1d,expected[boundary]);
                Assert.All(expected.Take(boundary), v => Assert.Equal(0d,v));
                ValidateReference(bars,owner,expected);
                foreach (int workers in new[] { 1,2,8 })
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    await CheckOwned(bars,owner,expected,mode);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task VariableLengthAnchorSurvivesHundredsOfBarsUntilRejection(int kind)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int boundary in new[] { 500,875 })
            foreach (bool reject in new[] { false,true })
            {
                var bars = Enumerable.Range(0,1001).Select(i => B(i,60,61,52,58)).ToArray();
                for (int i=0; i<20; i++) bars[i] = B(i,48-2d*(19-i),50-2d*(19-i),47-2d*(19-i),49-2d*(19-i));
                bars[20] = B(20,50,62,49,60);
                bars[21] = B(21,58,61,52,58.5);
                if (reject) bars[250] = B(250,60,61,52,60.5);
                bars[boundary-1] = B(boundary-1,57,60,53,57.5);
                bars[boundary] = B(boundary,58,65,54,64);
                if (kind == 2) bars = bars.Select(b => new Bar(b.Time,200-b.Open,200-b.Low,200-b.High,200-b.Close,b.Volume)).ToArray();
                var owner = Create(kind,3,20);
                var expected = TrendMethodsReference.Evaluate(bars,kind==1,3,20,.25m,.75m).ToArray();
                Assert.Equal(reject ? 0d : 1d,expected[boundary]);
                foreach (int workers in new[] { 1,2,8 })
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    await CheckOwned(bars,owner,expected,mode);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task StartupHugePeriodsAndExtremesMatchIndependentReference(int kind)
    {
        foreach (var (period, delay) in new[] { (1,1), (3,5), (int.MaxValue,1), (1,int.MaxValue), (int.MaxValue,int.MaxValue) })
        foreach (int count in new[] { 0,1,2,3,4,5,6,65 })
            await CheckReference(Data(count),Create(kind,period,delay));
        foreach (int seed in new[] { 837,947 })
        {
            var random = new Random(seed);
            double Next() => Math.ScaleB(random.NextDouble()*2-1,random.Next(-1074,1020));
            await CheckReference(Enumerable.Range(0,513).Select(_ => new Bar(default,Next(),Next(),Next(),Next(),1)).ToArray(),Create(kind,3,5));
        }
        foreach (double scale in new[] { double.Epsilon, Math.ScaleB(1d,1014), 1e-200, -0d })
            await CheckReference(Data(129).Select(b => new Bar(b.Time,b.Open*scale,b.High*scale,b.Low*scale,b.Close*scale,1)).ToArray(),Create(kind,3,20));
    }

    [Fact]
    public async Task MixedAndBoundedRootsPreserveValidationCancellationAndIdentity()
    {
        var bars = Data(1001);
        foreach (var roots in new IndicatorBase[][]
        {
            [Create(0,3,63),Create(0,63,3),new HikkakeCandle(),new RollingPriceSum(7)],
            [Create(0,3),Create(1,3,20),Create(2,3,20),Create(3,3),new RollingPriceSum(7)]
        })
        {
            var expected = roots.Select(r => Expected(bars,r)).ToArray();
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots.Concat(new[] { roots[0] }).ToArray()).ConfigureHistory(mode);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
                var valid=bars[124]; bars[124]=new(valid.Time,valid.Open,valid.High,valid.Low,valid.Close,double.NaN);
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
                bars[124]=valid;
                using var run=await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
                Assert.Equal(IndicatorExecutionBackend.Cpu,builder.LastExecution!.Backend);
                for (int i=0;i<roots.Length;i++) Assert.Equal(expected[i],run[roots[i]].ToArray());
                await Assert.ThrowsAnyAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            }
        }
    }

    private static void ValidateReference(Bar[] bars, IndicatorBase owner, double[] expected)
    {
        foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
            rule.Check(new IndicatorValidationContext("Trady candle shared execution",bars,[expected],0));
    }
    private static async Task CheckReference(Bar[] bars, IndicatorBase owner)
    {
        var expected=Expected(bars,owner);
        ValidateReference(bars,owner,expected);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
    }
    private static double[] Expected(Bar[] bars, IndicatorBase owner)
    {
        if (owner is DragonflyDojiCandle candle)
            return ShadowDojiReference.Evaluate(bars,candle.BodyFraction,candle.ShadowFraction,true).ToArray();
        var state=(IIndicatorState)owner.CreateState();
        return bars.Select(b => state.Update(b)).ToArray();
    }
    private static async Task CheckOwned(Bar[] bars, IndicatorBase owner, double[] expected, IndicatorHistoryMode mode)
    {
        var input=bars.ToArray();
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
        Array.Clear(input); run.Dispose(); Assert.Equal(expected,run[owner].ToArray());
        if (bars.Length>0) Assert.Equal(bars[^1],run.Latest.Bar);
        if (mode==IndicatorHistoryMode.Full)
        {
            int row=0; await foreach (var snapshot in run) Assert.Equal(bars[row++],snapshot.Bar);
            Assert.Equal(bars.Length,row);
        }
    }
}
