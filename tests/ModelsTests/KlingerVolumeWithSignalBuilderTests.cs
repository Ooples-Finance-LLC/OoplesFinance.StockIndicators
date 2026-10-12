using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class KlingerVolumeWithSignalBuilderTests
{
    private static Bar[] Prices(int count) => Enumerable.Range(0, count).Select(i =>
    {
        double price = 100 + (i * 13 % 101) / 8d;
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price + 2, price - 2, price, 10 + i % 17);
    }).ToArray();

    [Theory]
    [InlineData(3, 5, 1)]
    [InlineData(34, 55, 13)]
    [InlineData(int.MaxValue - 1, int.MaxValue, int.MaxValue)]
    public async Task BothHistoryModesRetainValuesPresenceAndOwnedBars(int fast, int slow, int signal)
    {
        var bars = Prices(100);
        var expected = KlingerVolumeSnapshot.Calculate(bars, fast, slow, signal);
        var saved = bars.ToArray();
        var indicator = new KlingerVolumeWithSignal(fast, slow, signal);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(history);
            using var run = await builder.BuildAsync();
            run.Dispose();
            Assert.Equal(expected.Select(v => BitConverter.DoubleToInt64Bits(v.Oscillator ?? 0)),
                run[indicator.Oscillator].ToArray().Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(expected.Select(v => BitConverter.DoubleToInt64Bits(v.Signal ?? 0)),
                run[indicator.Signal].ToArray().Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(expected.Select(v => v.Oscillator.HasValue ? 1d : 0), run[indicator.OscillatorIsDefined].ToArray());
            Assert.Equal(expected.Select(v => v.Signal.HasValue ? 1d : 0), run[indicator.SignalIsDefined].ToArray());
            Assert.Equal(saved[^1], run.Latest.Bar);
            if (history == IndicatorHistoryMode.Full)
            {
                Assert.Contains("Fused CPU values", builder.LastExecution!.Reason);
                Array.Clear(bars);
                int index = 0;
                await foreach (var snapshot in run) Assert.Equal(saved[index++], snapshot.Bar);
                Array.Copy(saved, bars, bars.Length);
            }
        }
    }

    [Fact]
    public async Task ExtendedUnpublishedForceCanCancelIntoFiniteOutputs()
    {
        var bars = Enumerable.Range(0, 20).Select(i =>
            new Bar(default, 0, i < 2 ? 1 : 0, 0, i == 0 ? 100 : i == 1 ? 0 : i, double.MaxValue)).ToArray();
        var indicator = new KlingerVolumeWithSignal(3, 5, 2);
        var expected = KlingerVolumeSnapshot.Calculate(bars, 3, 5, 2);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.All(expected.Skip(7), value => Assert.Equal(new KlingerVolumeValue(0, 0), value));
        Assert.All(run[indicator.Oscillator].ToArray(), value => Assert.Equal(0d, value));
        Assert.Equal(expected.Select(v => v.Signal.HasValue ? 1d : 0), run[indicator.SignalIsDefined].ToArray());
    }

    [Fact]
    public void OrdinaryStateReplayAfterResetAllocatesNothing()
    {
        var bars = Prices(1000);
        var state = new KlingerVolumeWithSignal.State(34, 55, 13);
        var outputs = new double[4];
        void Replay() { foreach (var bar in bars) state.Update(bar, outputs); }
        for (int i = 0; i < 3; i++) { state.Reset(); Replay(); }
        var expected = outputs.ToArray(); state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread(); Replay();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(expected, outputs);
        state.Reset(); Array.Reverse(bars);
        var reference = KlingerVolumeSnapshot.Calculate(bars, 34, 55, 13);
        for (int i = 0; i < bars.Length; i++)
        {
            state.Update(bars[i], outputs);
            Assert.Equal(reference[i].Oscillator ?? 0, outputs[0]);
            Assert.Equal(reference[i].Signal ?? 0, outputs[1]);
        }
    }

    [Theory]
    [InlineData(3, 5, 2)]
    [InlineData(int.MaxValue - 1, int.MaxValue, int.MaxValue)]
    public async Task LifecycleAndMaximumPeriodsMatchBatchReference(int fast, int slow, int signal)
    {
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(typeof(KlingerVolumeWithSignal),
            "Klinger", () => new KlingerVolumeWithSignal(fast, slow, signal)));
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public async Task InputValidationPrecedesPublishedOverflowAndRecoveryIsIndependent()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new KlingerVolumeWithSignal(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KlingerVolumeWithSignal(4, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KlingerVolumeWithSignal(signalPeriod: 0));
        var bars = Enumerable.Range(0, 20).Select(i => new Bar(default, i, i + 2, i - 2, i, double.MaxValue)).ToArray();
        var indicator = new KlingerVolumeWithSignal(3, 4, 2);
        foreach (bool invalid in new[] { false, true })
        {
            if (invalid) bars[^1] = new Bar(default, 0, 1, 0, 1, double.NaN);
            var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
            Assert.NotNull(expected);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var actual = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureHistory(history).BuildAsync());
                Assert.NotNull(actual); Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
            }
        }
        var builder = Build(Prices(30), indicator);
        using var saved = await builder.BuildAsync();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancellation.Token));
        using var recovered = await builder.BuildAsync();
        Assert.Equal(saved[indicator.Signal].ToArray(), recovered[indicator.Signal].ToArray());
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu);
}
