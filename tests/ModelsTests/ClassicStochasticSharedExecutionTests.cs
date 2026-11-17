using System.Numerics;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class ClassicStochasticSharedExecutionTests
{
    private static Bar[] Data(int count) => Enumerable.Range(0,count).Select(i =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i),32+i%7,75+i%17,16+i%3,48+i%31+(i%7)/8d,i+1)).ToArray();
    public static IEnumerable<object[]> Methods => Enum.GetValues<ClassicAverageMethod>().Select(m => new object[] { m });
    [Theory,MemberData(nameof(Methods))]
    public async Task ClassicalStagesPreserveWorkersSeedsSuppressionAndOwnership(ClassicAverageMethod method)
    {
        int prior=CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            bool finite=method is ClassicAverageMethod.Sma or ClassicAverageMethod.Wma or ClassicAverageMethod.Trima;
            foreach (var (seed,suppression) in new[] { (false,0),(true,2) })
            foreach (int count in finite ? new[] { 511,512,513,1001 } : new[] { 513 })
            foreach (IIndicator owner in new IIndicator[] { new ClassicStochastic(20,3,5,method,method,seed,suppression),new ClassicStochasticRsi(3,5,3,method,seed,suppression,suppression) })
            {
                var bars=Data(count);var expected=Expected(bars,owner);
                foreach (int workers in new[] { 1,2,8 })
                {
                    CpuParallelSettings.MaxDegreeOfParallelism=workers;
                    foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
                }
            }
            var large=Data(10000);var largeOwner=new ClassicStochastic(20,3,3,method,method);var largeExpected=Expected(large,largeOwner);
            foreach(var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(large,largeOwner,largeExpected,mode);
            await CheckReference(Data(65),new ClassicStochastic(3,2,3,method,method,true,2));
            await CheckReference(Data(65),new ClassicStochasticRsi(3,5,2,method,true,1,2));
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism=prior; }
    }
    [Fact]
    public async Task MixedFiniteAverageStagesReplayCompleteHistory()
    {
        int prior=CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism=8;
            foreach(var k in new[] { ClassicAverageMethod.Sma,ClassicAverageMethod.Wma,ClassicAverageMethod.Trima })
            foreach(var d in new[] { ClassicAverageMethod.Sma,ClassicAverageMethod.Wma,ClassicAverageMethod.Trima })
            foreach(int kp in new[] { 1,3 })
            {
                var owner=new ClassicStochastic(5,kp,2,k,d);var bars=Data(1001);var expected=Expected(bars,owner);
                foreach(var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism=prior; }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RsiConventionsPreserveStartupFlatRecoveryAndEveryColumn(bool nullable)
    {
        foreach(int period in new[] { 1,3,20,63 })
        foreach(int count in new[] { 0,1,2,20,65,513,10000 })
        {
            var bars=Data(count);IIndicator owner=nullable ? new NullableStochasticRsi(period) : new SmoothedStochasticRsi(period,5,3,2);var expected=Expected(bars,owner);
            foreach(var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
        }
        var flat=Enumerable.Repeat(new Bar(default,10,10,10,10,1),65).ToArray();
        IIndicator reference=nullable ? new NullableStochasticRsi(3) : new SmoothedStochasticRsi(3,5,3,2);
        await CheckReference(flat,reference);Assert.Equal(nullable ? .5 : 0,Expected(flat,reference)[0][^1]);
        await CheckReference(flat.Concat(Data(65)).ToArray(),reference);
    }
    [Fact]
    public async Task HugeLookbacksIdentityAndScaledInputsStayExact()
    {
        foreach(int period in new[] { 1,3,4097,int.MaxValue })
        foreach(int count in new[] { 0,1,3,21 })
        foreach(IIndicator owner in new IIndicator[] { new ClassicStochastic(period,1,1,ClassicAverageMethod.Mama,ClassicAverageMethod.T3,true,2),new ClassicStochastic(1,period,period),new ClassicStochasticRsi(period,period,period),new NullableStochasticRsi(period),new SmoothedStochasticRsi(period,period,period,period) })
        {
            var bars=Data(count);var expected=Expected(bars,owner);
            foreach(var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
        }
        foreach(double scale in new[] { double.Epsilon,Math.ScaleB(1d,400),1e-200,-0d })
        foreach(IIndicator owner in new IIndicator[] { new ClassicStochastic(3,2,3),new ClassicStochasticRsi(3,5,2),new NullableStochasticRsi(3),new SmoothedStochasticRsi(3,5,3,2) })
            await CheckReference(Data(65).Select(b => new Bar(b.Time,b.Open*scale,b.High*scale,b.Low*scale,b.Close*scale,1)).ToArray(),owner);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PrimaryAndSecondaryOverflowRetainRawInputPriority(bool secondary)
    {
        var bars=Enumerable.Repeat(new Bar(default,0,1,0,0,1),1001).ToArray();
        bars[124]=bars[125]=new(default,0,1,0,secondary ? 1.7e306 : double.MaxValue,1);
        var owner=secondary ? new ClassicStochastic(1,1,3,ClassicAverageMethod.Sma,ClassicAverageMethod.Tema) : new ClassicStochastic(1,1,1);
        var values=Expected(bars,owner);Assert.Contains(values[secondary ? 1 : 0],double.IsInfinity);
        if(secondary) Assert.All(values[0],v => Assert.True(double.IsFinite(v)));
        foreach(var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var shared=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);
            var ordinary=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode).ConfigureBehavior(_ => { });
            var expected=await Assert.ThrowsAsync<IndicatorOutputException>(() => ordinary.BuildAsync());
            var actual=await Assert.ThrowsAsync<IndicatorOutputException>(() => shared.BuildAsync());Assert.Equal(expected.Message,actual.Message);
            var valid=bars[900];bars[900]=new(valid.Time,valid.Open,valid.High,valid.Low,valid.Close,double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => shared.BuildAsync());bars[900]=valid;
        }
    }
    [Fact]
    public async Task MixedChainedRootsRetainCancellationAndRecovery()
    {
        var bars=Data(513);var roots=new IIndicator[] { new ClassicStochastic(3,2,5),new ClassicStochasticRsi(3,5,2),new NullableStochasticRsi(3),new SmoothedStochasticRsi(3,5,3,2) };
        foreach(var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots.Concat(new[] { roots[0] }).ToArray()).ConfigureHistory(mode);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            var valid=bars[124];bars[124]=new(valid.Time,valid.Open,valid.High,valid.Low,valid.Close,double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());bars[124]=valid;
            using var run=await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();Assert.Equal(IndicatorExecutionBackend.Cpu,builder.LastExecution!.Backend);
            foreach(var root in roots) CheckColumns(run,root,Expected(bars,root));
            await Assert.ThrowsAnyAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        }
        foreach(var root in roots)
        {
            ((MultiOutputIndicatorBase)root).Of(new FirstValueEma(5));
            using var ordinary=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).ConfigureBehavior(_ => { }).BuildAsync();
            foreach(var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,root,root.Outputs.Select(o => ordinary[o].ToArray()).ToArray(),mode);
        }
    }
    [Fact]
    public async Task ShortSmaStagesPreserveExactReadinessAndChunkBoundaries()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            foreach (var (price, k, d) in new[] { (5, 3, 3), (5, 1, 3), (1, 1, 1), (7, 61, 61) })
            {
                var bars = Data(1027);
                var owner = new ClassicStochastic(price, k, d);
                await CheckGridReferenceAndOwnership(bars, owner);
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task ShortSmaStagesPreserveMixedGridFallbackAndSignedZeroRecovery()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            var bars = Data(1539);
            foreach (int index in new[] { 255, 769 })
            {
                double scale = Math.ScaleB(1d, index == 255 ? 400 : -400);
                var bar = bars[index];
                bars[index] = new(bar.Time, bar.Open * scale, bar.High * scale,
                    bar.Low * scale, bar.Close * scale, bar.Volume);
            }
            for (int i = 500; i < 530; i++)
                bars[i] = new(bars[i].Time, -0d, i % 2 == 0 ? 0d : double.Epsilon,
                    -0d, i % 3 == 0 ? double.Epsilon : -0d, 1);
            // Finite inputs need not be ordered OHLC ranges. They retain the
            // original signed denominator rather than being clamped or rejected.
            for (int i = 700; i < 731; i++) bars[i] = new(bars[i].Time, 0, -1, 1, -.25, 1);
            await CheckGridReferenceAndOwnership(bars, new ClassicStochastic(5, 3, 3));
            await CheckGridReferenceAndOwnership(Data(513), new ClassicStochastic(8, 64, 64));
            await CheckGridReferenceAndOwnership(Data(513), new ClassicStochastic(5, 3, 3,
                ClassicAverageMethod.Wma, ClassicAverageMethod.Sma, true, 2));
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task ShortSmaFallbackKeepsDeferredInputPriorityCancellationAndRecovery()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            var bars = Enumerable.Range(0, 1027).Select(i =>
                new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 1, 0, .5, 1)).ToArray();
            bars[255] = new(bars[255].Time, 0, 1, 0, double.MaxValue, 1);
            var owner = new ClassicStochastic(1, 1, 1);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var shared = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(owner).ConfigureHistory(mode);
                var ordinary = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(owner).ConfigureHistory(mode).ConfigureBehavior(_ => { });
                var expected = await Assert.ThrowsAsync<IndicatorOutputException>(() => ordinary.BuildAsync());
                var actual = await Assert.ThrowsAsync<IndicatorOutputException>(() => shared.BuildAsync());
                Assert.Equal(expected.Message, actual.Message);
                var valid = bars[900];
                bars[900] = new(valid.Time, valid.Open, valid.High, valid.Low, valid.Close, double.NaN);
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => shared.BuildAsync());
                bars[900] = valid;
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => shared.BuildAsync(new CancellationToken(true)));
            }
            bars[255] = new(bars[255].Time, 0, 1, 0, .5, 1);
            await CheckGridReferenceAndOwnership(bars, owner);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public void FixedGridMeanAdapterPreservesIndependentRoundingAndScaleBoundaries()
    {
        foreach (long coefficient in new[] { long.MinValue, -((1L << 53) + 1), -1L, 0L,
            (1L << 53) + 1, long.MaxValue })
        foreach (int exponent in new[] { -1076, -1074, -1022, -55, 0, 970, 1023 })
        foreach (int count in new[] { 1, 3, 64 })
        {
            var divisor = new ExactMeanAccumulator.WordMeanDivisor(count);
            var exact = new ReferenceFraction(coefficient) / new ReferenceFraction(count);
            var scale = new ReferenceFraction(BigInteger.One << Math.Abs(exponent));
            exact = exponent < 0 ? exact / scale : exact * scale;
            double actual = ExactMeanAccumulator.MeanScaledWord(coefficient, exponent, in divisor);
            Assert.Equal(BitConverter.DoubleToInt64Bits(exact.ToDouble()), BitConverter.DoubleToInt64Bits(actual));
        }
    }

    private static async Task CheckGridReferenceAndOwnership(Bar[] bars, ClassicStochastic owner)
    {
        var expected = Expected(bars, owner);
        var reference = ClassicStochasticReference.Values(bars, owner);
        for (int i = 0; i < bars.Length; i++)
        {
            Assert.Equal(reference[0][i] ?? 0, expected[0][i]);
            Assert.Equal(reference[1][i] ?? 0, expected[1][i]);
            Assert.Equal(reference[0][i].HasValue ? 1d : 0d, expected[2][i]);
            Assert.Equal(reference[1][i].HasValue ? 1d : 0d, expected[3][i]);
        }
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            await CheckOwned(bars, owner, expected, mode);
    }

    private static double[][] Expected(Bar[] bars,IIndicator owner)
    {
        object state=owner is IndicatorBase scalar ? scalar.CreateState() : ((MultiOutputIndicatorBase)owner).CreateState();
        using var lifetime=state as IDisposable;
        var result=Enumerable.Range(0,owner.Outputs.Count).Select(_ => new double[bars.Length]).ToArray();
        var values=new double[result.Length];
        for (int i=0;i<bars.Length;i++)
        {
            if (state is IIndicatorState single) values[0]=single.Update(bars[i]);
            else { Array.Clear(values); ((IMultiOutputState)state).Update(bars[i],values); }
            for (int slot=0;slot<values.Length;slot++) result[slot][i]=values[slot];
        }
        return result;
    }
    private static async Task CheckReference(Bar[] bars,IIndicator owner)
    {
        var expected=Expected(bars,owner);
        foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
            rule.Check(new IndicatorValidationContext("Classical stochastic shared execution",bars,expected,0));
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
    }
    private static void CheckColumns(IIndicatorRun run,IIndicator owner,double[][] expected)
    {
        for (int slot=0;slot<expected.Length;slot++)
            Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),run[owner.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }
    private static async Task CheckOwned(Bar[] bars,IIndicator owner,double[][] expected,IndicatorHistoryMode mode)
    {
        var input=bars.ToArray();
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
        Array.Clear(input); run.Dispose(); CheckColumns(run,owner,expected);
        if (bars.Length>0) Assert.Equal(bars[^1],run.Latest.Bar);
        if (mode==IndicatorHistoryMode.Full)
        {
            int row=0; await foreach (var snapshot in run) Assert.Equal(bars[row++],snapshot.Bar);
            Assert.Equal(bars.Length,row);
        }
    }
}
