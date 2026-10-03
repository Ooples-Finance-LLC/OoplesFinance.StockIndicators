using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

public sealed class DiscoveredNativeInputRejectionTests
{
    public static IEnumerable<object[]> Cases => typeof(IStreamingIndicatorState).Assembly.GetTypes()
        .Where(type => type.IsPublic && !type.IsAbstract && typeof(IStreamingIndicatorState).IsAssignableFrom(type))
        .OrderBy(type => type.FullName)
        .SelectMany(type => new[] { new object[] { type.Name, false }, new object[] { type.Name, true } });

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryDiscoveredNativeStateRejectsInvalidBarsWithoutAdvancing(string typeName, bool minimumPeriods)
    {
        var type = typeof(IStreamingIndicatorState).Assembly.GetType("OoplesFinance.StockIndicators.Streaming." + typeName)!;
        var constructor = type.GetConstructors().OrderBy(c => c.GetParameters().Length).First();
        var parameters = type == typeof(CustomInputState) ? Array.Empty<System.Reflection.ParameterInfo>() : constructor.GetParameters();
        Assert.All(parameters, p => Assert.True(p.HasDefaultValue, $"Register a construction case for required parameter {p.Name} on {typeName}."));
        var arguments = parameters.Select(p => minimumPeriods && p.ParameterType == typeof(int)
            && (p.Name!.IndexOf("length", StringComparison.OrdinalIgnoreCase) >= 0 || p.Name.IndexOf("period", StringComparison.OrdinalIgnoreCase) >= 0)
                ? (object)1 : p.DefaultValue).ToArray();
        IStreamingIndicatorState Create() => type == typeof(CustomInputState)
            ? minimumPeriods ? new CustomInputState(new SimpleMovingAverageState(1), InputSeries.Of(new SimpleMovingAverageState(2)))
                : new CustomInputState(new SimpleMovingAverageState(3), bar => bar.Close)
            : (IStreamingIndicatorState)constructor.Invoke(arguments);
        var subject = Create(); var control = Create();
        using var subjectLifetime = subject as IDisposable;
        using var controlLifetime = control as IDisposable;
        var index = 0;
        var warmup = Math.Max(8, arguments.OfType<int>().DefaultIfEmpty(6).Max() + 2);
        Assert.InRange(warmup, 8, 8192);
        for (; index < warmup; index++) CompareNext();
        foreach (var field in new[] { "open", "high", "low", "close", "volume" })
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            var error = Record.Exception(() => subject.Update(Bar(index, field, invalid), final, true));
            Assert.True(error is ArgumentOutOfRangeException,
                $"{typeName}: {field}={invalid:R}, final={final}: expected finite-input rejection, got {error?.GetType().Name ?? "no exception"}.");
            CompareNext(); index++;
        }
        for (var recovery = 0; recovery < warmup + 2; recovery++, index++) CompareNext();

        void CompareNext()
        {
            var expected = control.Update(Bar(index), true, true);
            var actual = subject.Update(Bar(index), true, true);
            Assert.Equal(expected.Value, actual.Value);
            Assert.Equal(expected.Outputs?.OrderBy(p => p.Key), actual.Outputs?.OrderBy(p => p.Key));
        }
    }

    private static OhlcvBar Bar(int index, string? field = null, double invalid = 0)
    {
        var close = 100 + 3 * Math.Sin(index * .3);
        var time = DateTime.UnixEpoch.AddMinutes(index);
        return new OhlcvBar("NATIVE", BarTimeframe.Minutes(1), time, time,
            field == "open" ? invalid : close, field == "high" ? invalid : close + 1,
            field == "low" ? invalid : close - 1, field == "close" ? invalid : close,
            field == "volume" ? invalid : 1000 + index % 7, true);
    }
}
