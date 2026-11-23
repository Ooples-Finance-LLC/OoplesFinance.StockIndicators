using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// Holds the fast arithmetic of every snapshot calculation to the exact one: the same values to within
/// floating-point rounding, absent on the same bars, and the same failure where the exact one fails.
/// </summary>
public sealed class FastSnapshotParityTests
{
    private const double Tolerance = 1e-9;

    /// <summary>Each case returns its published columns; a null entry is an absent value.</summary>
    public static IEnumerable<(string Name, Func<IReadOnlyList<Bar>, IndicatorNumerics, double?[][]> Run)> Cases() =>
    [
        ("Parabolic.Classic()", (bars, numerics) => [[.. ParabolicStopSnapshots.Classic(bars, numerics: numerics)]]),
        ("Parabolic.Classic(0.05, 0.5)", (bars, numerics) => [[.. ParabolicStopSnapshots.Classic(bars, .05, .5, numerics)]]),
        ("Parabolic.Classic(0, 0)", (bars, numerics) => [[.. ParabolicStopSnapshots.Classic(bars, 0, 0, numerics)]]),
        ("Parabolic.Extended()", (bars, numerics) => [[.. ParabolicStopSnapshots.Extended(bars, numerics: numerics)]]),
        ("Parabolic.Extended(start 90, offset 0.01)", (bars, numerics) =>
            [[.. ParabolicStopSnapshots.Extended(bars, 90, .01, .03, .02, .3, .01, .04, .15, numerics)]]),
        ("Parabolic.Extended(start -120)", (bars, numerics) =>
            [[.. ParabolicStopSnapshots.Extended(bars, -120, 0, .02, .02, .2, .02, .02, .2, numerics)]]),
        ("Parabolic.Confirmed()", (bars, numerics) => Confirmed(ParabolicStopSnapshots.Confirmed(bars, numerics: numerics))),
        ("Parabolic.Confirmed(0.03, 0.4, 0.1)", (bars, numerics) =>
            Confirmed(ParabolicStopSnapshots.Confirmed(bars, .03, .4, .1, numerics))),
        ("Parabolic.FourBar()", (bars, numerics) => [[.. ParabolicStopSnapshots.FourBar(bars, numerics: numerics)]]),
        ("Parabolic.FourBar(0.05, 0.3)", (bars, numerics) => [[.. ParabolicStopSnapshots.FourBar(bars, .05, .3, numerics)]]),
        ("VolatilityStop()", (bars, numerics) => Stops(VolatilityStopSnapshot.Calculate(bars, numerics: numerics))),
        ("VolatilityStop(2, 0.5)", (bars, numerics) => Stops(VolatilityStopSnapshot.Calculate(bars, 2, .5, numerics))),
        ("VolatilityStop(20, 1.5)", (bars, numerics) => Stops(VolatilityStopSnapshot.Calculate(bars, 20, 1.5, numerics))),
        ("VolatilityStop(300, 3)", (bars, numerics) => Stops(VolatilityStopSnapshot.Calculate(bars, 300, 3, numerics))),
        ("ZigZag()", (bars, numerics) => Swings(ZigZagSnapshot.Calculate(bars, numerics: numerics))),
        ("ZigZag(1)", (bars, numerics) => Swings(ZigZagSnapshot.Calculate(bars, 1, false, numerics))),
        ("ZigZag(0.25)", (bars, numerics) => Swings(ZigZagSnapshot.Calculate(bars, .25, false, numerics))),
        ("ZigZag(1, high/low)", (bars, numerics) => Swings(ZigZagSnapshot.Calculate(bars, 1, true, numerics))),
        ("ZigZag(3, high/low)", (bars, numerics) => Swings(ZigZagSnapshot.Calculate(bars, 3, true, numerics))),
        ("ZigZag(50, high/low)", (bars, numerics) => Swings(ZigZagSnapshot.Calculate(bars, 50, true, numerics))),
        ("Renko()", (bars, numerics) => Bricks(RenkoSnapshot.Calculate(bars, numerics: numerics))),
        ("Renko(0.5)", (bars, numerics) => Bricks(RenkoSnapshot.Calculate(bars, .5, false, numerics))),
        ("Renko(0.01)", (bars, numerics) => Bricks(RenkoSnapshot.Calculate(bars, .01, false, numerics))),
        ("Renko(0.37)", (bars, numerics) => Bricks(RenkoSnapshot.Calculate(bars, .37, false, numerics))),
        ("Renko(2.5, high/low)", (bars, numerics) => Bricks(RenkoSnapshot.Calculate(bars, 2.5, true, numerics))),
        ("Renko(0.25, high/low)", (bars, numerics) => Bricks(RenkoSnapshot.Calculate(bars, .25, true, numerics))),
        ("RenkoAtr()", (bars, numerics) => Bricks(RenkoSnapshot.CalculateAtr(bars, numerics: numerics))),
        ("RenkoAtr(2)", (bars, numerics) => Bricks(RenkoSnapshot.CalculateAtr(bars, 2, false, numerics))),
        ("RenkoAtr(50, high/low)", (bars, numerics) => Bricks(RenkoSnapshot.CalculateAtr(bars, 50, true, numerics))),
        ("Fwma()", (bars, numerics) => Plain(FibonacciWeightedSnapshot.Calculate(bars, numerics: numerics))),
        ("Fwma(1)", (bars, numerics) => Plain(FibonacciWeightedSnapshot.Calculate(bars, 1, numerics))),
        ("Fwma(2)", (bars, numerics) => Plain(FibonacciWeightedSnapshot.Calculate(bars, 2, numerics))),
        ("Fwma(90)", (bars, numerics) => Plain(FibonacciWeightedSnapshot.Calculate(bars, 90, numerics))),
        ("Fwma(300)", (bars, numerics) => Plain(FibonacciWeightedSnapshot.Calculate(bars, 300, numerics))),
        ("Sinc()", (bars, numerics) => Plain(WindowedSincSnapshot.Calculate(bars, numerics: numerics))),
        ("Sinc(1, 1, Rectangular)", (bars, numerics) =>
            Plain(WindowedSincSnapshot.Calculate(bars, 1, 1, SincWindow.Rectangular, numerics))),
        ("Sinc(5, 3, Hamming)", (bars, numerics) => Plain(WindowedSincSnapshot.Calculate(bars, 5, 3, SincWindow.Hamming, numerics))),
        ("Sinc(20, 64, Blackman)", (bars, numerics) =>
            Plain(WindowedSincSnapshot.Calculate(bars, 20, 64, SincWindow.Blackman, numerics))),
        ("Sinc(8, 301, BlackmanHarris)", (bars, numerics) =>
            Plain(WindowedSincSnapshot.Calculate(bars, 8, 301, SincWindow.BlackmanHarris, numerics))),
        ("Sinc(3, 40, Hann)", (bars, numerics) => Plain(WindowedSincSnapshot.Calculate(bars, 3, 40, SincWindow.Hann, numerics))),
    ];

    public static TheoryData<string> CaseNames()
    {
        var names = new TheoryData<string>();
        foreach (var (name, _) in Cases()) names.Add(name);
        return names;
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void FastAgreesWithExact(string name)
    {
        var run = Cases().Single(c => c.Name == name).Run;
        var compared = 0;
        foreach (var (fixture, bars) in FastNumericsParityTests.Fixtures())
        {
            var where = $"{name} / {fixture}";
            double?[][] exact;
            try
            {
                exact = run(bars, IndicatorNumerics.Exact);
            }
            catch (Exception failure) when (failure is OverflowException or ArgumentException)
            {
                var fastFailure = Assert.ThrowsAny<Exception>(() => run(bars, IndicatorNumerics.Fast));
                Assert.True(failure.GetType() == fastFailure.GetType(),
                    $"{where}: exact failed with {failure.GetType().Name}, fast with {fastFailure.GetType().Name}");
                continue;
            }
            var fast = run(bars, IndicatorNumerics.Fast);
            Assert.Equal(exact.Length, fast.Length);
            for (var slot = 0; slot < exact.Length; slot++)
            {
                Assert.Equal(exact[slot].Length, fast[slot].Length);
                for (var i = 0; i < exact[slot].Length; i++)
                {
                    double? expected = exact[slot][i], actual = fast[slot][i];
                    Assert.True(expected.HasValue == actual.HasValue,
                        $"{where}: slot {slot}, bar {i} expected {Show(expected)}, was {Show(actual)}");
                    if (expected is not double value || actual is not double other) continue;
                    compared++;
                    // Bricks outnumber or undershoot the bars, so a row may have no bar to take a scale from.
                    var scale = Math.Max(Math.Abs(value), i < bars.Length ? Math.Abs(bars[i].Close) : 0);
                    Assert.True(Math.Abs(value - other) <= Tolerance * Math.Max(scale, 1e-300),
                        $"{where}: slot {slot}, bar {i} expected {value:R}, was {other:R}");
                }
            }
        }
        // A case that publishes nothing on any fixture would pass without comparing anything.
        Assert.True(compared > 0, $"{name}: no value was compared");
    }

    private static string Show(double? value) => value is double present ? present.ToString("R") : "absent";

    // Bricks are a list of their own length; a different number of bricks fails the length comparison.
    private static double?[][] Bricks(IEnumerable<RenkoBrick> bricks)
    {
        var rows = bricks.ToArray();
        double?[] Column(Func<RenkoBrick, double> read) => [.. rows.Select(row => (double?)read(row))];
        return
        [
            Column(row => row.Open), Column(row => row.High), Column(row => row.Low), Column(row => row.Close),
            Column(row => row.Volume), Column(row => row.IsUp ? 1 : 0), Column(row => row.Date.Ticks),
        ];
    }

    private static double?[][] Plain(IReadOnlyList<double> values) => [[.. values.Select(value => (double?)value)]];

    private static double?[][] Swings(IReadOnlyList<ZigZagValue> rows) =>
    [
        [.. rows.Select(row => row.ZigZag)],
        [.. rows.Select(row => row.PointType is ZigZagPointKind kind ? (double)kind : (double?)null)],
        [.. rows.Select(row => row.RetraceHigh)],
        [.. rows.Select(row => row.RetraceLow)],
    ];

    private static double?[][] Stops(IReadOnlyList<VolatilityStopValue> rows) =>
    [
        [.. rows.Select(row => row.Sar)],
        [.. rows.Select(row => row.UpperBand)],
        [.. rows.Select(row => row.LowerBand)],
        [.. rows.Select(row => row.IsStop is bool stop ? stop ? 1d : 0d : (double?)null)],
    ];

    private static double?[][] Confirmed(IReadOnlyList<ConfirmedParabolicValue> rows) =>
    [
        [.. rows.Select(row => row.Sar)],
        [.. rows.Select(row => row.IsReversal is bool reversal ? reversal ? 1d : 0d : (double?)null)],
    ];
}
