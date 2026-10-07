using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

public sealed class NativeInputRejectionTests
{
    public static IEnumerable<object[]> InvalidBars()
    {
        foreach (var kind in new[] { "sma", "ema", "wma", "deviation" })
        foreach (var field in new[] { "open", "high", "low", "close", "volume" })
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
            yield return new object[] { kind, field, invalid, final };
    }

    private static IStreamingIndicatorState Create(string kind) => kind switch
    {
        "sma" => new SimpleMovingAverageState(3),
        "ema" => new ExponentialMovingAverageState(3),
        "wma" => new WeightedMovingAverageState(3),
        _ => new StandardDeviationState(length: 3)
    };

    private static OhlcvBar Bar(int index, double value, string? field = null, double invalid = 0)
    {
        var time = DateTime.UnixEpoch.AddMinutes(index);
        return new OhlcvBar("FINITE", BarTimeframe.Minutes(1), time, time,
            field == "open" ? invalid : value, field == "high" ? invalid : value + 1,
            field == "low" ? invalid : value - 1, field == "close" ? invalid : value,
            field == "volume" ? invalid : 1000, true);
    }

    [Theory]
    [MemberData(nameof(InvalidBars))]
    public void RejectedNativeEventsCannotChangeFutureOutputs(string kind, string field, double invalid, bool final)
    {
        var subject = Create(kind); var control = Create(kind);
        using var firstLifetime = subject as IDisposable;
        using var secondLifetime = control as IDisposable;
        for (var i = 0; i < 3; i++)
        {
            subject.Update(Bar(i, 100 + i), true, true);
            control.Update(Bar(i, 100 + i), true, true);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => subject.Update(Bar(3, 99, field, invalid), final, true));
        for (var i = 3; i < 12; i++)
        {
            var expected = control.Update(Bar(i, 90 + i), true, true);
            var actual = subject.Update(Bar(i, 90 + i), true, true);
            Assert.Equal(expected.Value, actual.Value);
            Assert.Equal(expected.Outputs!.OrderBy(pair => pair.Key), actual.Outputs!.OrderBy(pair => pair.Key));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void ScalarEmaRejectsNonfiniteValuesEvenOnItsIdentityPath(int period)
    {
        var subject = new EmaState(period);
        var control = new EmaState(period);
        for (var i = 0; i < period; i++) { subject.GetNext(100, true); control.GetNext(100, true); }
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var commit in new[] { false, true })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => subject.GetNext(invalid, commit));
            Assert.Equal(control.GetNext(103, true), subject.GetNext(103, true));
        }
    }

    [Fact]
    public void ACustomSelectorCannotIntroduceNonfiniteValues()
    {
        var resolver = new StreamingInputResolver(InputName.Close, _ => double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.GetValue(Bar(0, 100)));
    }

    [Fact]
    public void CustomWrapperRejectsSelectorOutputBeforeAdvancingItsInnerState()
    {
        var reject = false;
        using var subject = new CustomInputState(new SimpleMovingAverageState(2), bar => reject ? double.NaN : bar.Close);
        using var control = new CustomInputState(new SimpleMovingAverageState(2), bar => bar.Close);
        subject.Update(Bar(0, 100), true, true);
        control.Update(Bar(0, 100), true, true);
        reject = true;
        Assert.Throws<ArgumentOutOfRangeException>(() => subject.Update(Bar(1, 105), true, true));
        reject = false;
        for (var i = 1; i < 5; i++)
            Assert.Equal(control.Update(Bar(i, 100 + i), true, true).Value,
                subject.Update(Bar(i, 100 + i), true, true).Value);
    }
}
