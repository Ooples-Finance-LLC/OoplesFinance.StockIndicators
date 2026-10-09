using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Equal value-array and equal owned-payload comparisons isolate arithmetic and
// storage. Payload wrappers do not implement the complete public builder API.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class PilotCostBoundaryBenchmarks
{
    [Params("Asin", "SmaGrid", "SmaDecimal")] public string Case { get; set; } = "";
    private const int Count = 100_000;
    private Bar[] _bars = null!;
    private double[] _close = null!;

    [GlobalSetup]
    public void Setup()
    {
        _close = Enumerable.Range(0, Count).Select(i => Case == "SmaDecimal"
            ? 100 + i % 19 / 100d : (i % 127 - 63) / 64d).ToArray();
        _bars = _close.Select(x => new Bar(default, x, x, x, x, 1)).ToArray();
        var ours = OoplesValues();
        var theirs = TalibValues();
        IIndicator indicator = Case == "Asin" ? new PriceCircularTransform(PriceCircularOperation.ArcSine) : new Sma(20);
        var built = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync().GetAwaiter().GetResult();
        AsinFeasibilityBenchmarks.RequireSame(ours, built[indicator].ToArray());
        // Decimal arithmetic contracts differ: expose the discrepancy instead of
        // claiming bit equivalence or relaxing the library's numerical contract.
        long mismatches = 0;
        double maxDifference = 0;
        for (int i = 0; i < Count; i++)
        {
            if (BitConverter.DoubleToInt64Bits(ours[i]) != BitConverter.DoubleToInt64Bits(theirs[i])) mismatches++;
            maxDifference = Math.Max(maxDifference, Math.Abs(ours[i] - theirs[i]));
        }
        if (Case != "SmaDecimal" && mismatches != 0) throw new InvalidOperationException("Matched outputs differ.");
        Console.WriteLine($"CONTRACT case={Case} bitDifferences={mismatches} maxAbsoluteDifference={maxDifference:R}");
        foreach (bool competitor in new[] { false, true })
        {
            var payload = Payload(competitor);
            AsinFeasibilityBenchmarks.RequireSame(competitor ? theirs : ours, payload.Values);
            if (!payload.Bars.SequenceEqual(_bars)) throw new InvalidOperationException("History mismatch.");
            if (Case == "Asin" && payload.Presence!.Any(x => x != 1)) throw new InvalidOperationException("Presence mismatch.");
        }
        var latestPayload = TalibLatestOnlyPayload();
        AsinFeasibilityBenchmarks.RequireSame(theirs, latestPayload.Values);
        if (!latestPayload.Latest.Equals(_bars[^1]) || (Case == "Asin" && latestPayload.Presence!.Any(x => x != 1)))
            throw new InvalidOperationException("Latest payload mismatch.");
        var inPlace = TalibInPlaceLatestOnlyPayload();
        AsinFeasibilityBenchmarks.RequireSame(theirs, inPlace.Values);
        if (!inPlace.Latest.Equals(_bars[^1]) || (Case == "Asin" && inPlace.Presence!.Any(x => x != 1)))
            throw new InvalidOperationException("In-place payload mismatch.");
        Console.WriteLine($"LAYOUT barBytes={System.Runtime.CompilerServices.Unsafe.SizeOf<Bar>()} count={Count}");
    }

    private double[] Values(double[] close, bool competitor)
    {
        var output = new double[Count];
        if (Case == "Asin")
        {
            if (competitor) Functions.Asin<double>(close, System.Range.All, output, out _);
            else for (int i = 0; i < Count; i++) output[i] = Math.Asin(close[i]);
        }
        else if (competitor)
            Functions.Sma<double>(close, System.Range.All, output.AsSpan(19), out _, 20);
        else CpuFeasibilityPrototypes.CurrentSma(close, output, 20);
        return output;
    }

    private OwnedPayload Payload(bool competitor)
    {
        var owned = (Bar[])_bars.Clone();
        var close = new double[Count];
        double[]? presence = Case == "Asin" ? new double[Count] : null;
        for (int i = 0; i < Count; i++)
        {
            IndicatorInputDomain.Finite.Validate(in owned[i]);
            close[i] = owned[i].Close;
            if (presence is not null) presence[i] = close[i] is >= -1 and <= 1 ? 1 : 0;
        }
        return new(owned, Values(close, competitor), presence);
    }

    // Same Bar[] input, full-field validation, owned values/presence and latest bar
    // as LatestOnly. This wrapper is a payload boundary, not a TALib builder API.
    [Benchmark]
    public LatestPayload TalibLatestOnlyPayload()
    {
        var close = new double[Count];
        double[]? presence = Case == "Asin" ? new double[Count] : null;
        Bar latest = default;
        for (int i = 0; i < Count; i++)
        {
            var bar = _bars[i];
            latest = bar;
            if (!double.IsFinite(bar.Open) || !double.IsFinite(bar.High) || !double.IsFinite(bar.Low)
                || !double.IsFinite(bar.Close) || !double.IsFinite(bar.Volume))
                IndicatorInputDomain.Finite.Validate(in bar);
            close[i] = bar.Close;
            if (presence is not null) presence[i] = close[i] is >= -1 and <= 1 ? 1 : 0;
        }
        return new(latest, Values(close, true), presence);
    }

    // TALib reads each expiring SMA input before writing compact output, so exact
    // input/output aliasing is safe. Align the compact result only after calculation.
    [Benchmark]
    public LatestPayload TalibInPlaceLatestOnlyPayload()
    {
        var values = GC.AllocateUninitializedArray<double>(Count);
        double[]? presence = Case == "Asin" ? GC.AllocateUninitializedArray<double>(Count) : null;
        Bar latest = default;
        for (int i = 0; i < Count; i++)
        {
            var bar = _bars[i];
            latest = bar;
            if (!double.IsFinite(bar.Open) || !double.IsFinite(bar.High) || !double.IsFinite(bar.Low)
                || !double.IsFinite(bar.Close) || !double.IsFinite(bar.Volume))
                IndicatorInputDomain.Finite.Validate(in bar);
            bool defined = presence is null || bar.Close is >= -1 and <= 1;
            values[i] = defined ? bar.Close : 0;
            if (presence is not null) presence[i] = defined ? 1 : 0;
        }
        if (Case == "Asin") Functions.Asin<double>(values, System.Range.All, values, out _);
        else
        {
            Functions.Sma<double>(values, System.Range.All, values, out _, 20);
            values.AsSpan(0, Count - 19).CopyTo(values.AsSpan(19));
            values.AsSpan(0, 19).Clear();
        }
        return new(latest, values, presence);
    }

    [Benchmark(Baseline = true)] public double[] TalibValues() => Values(_close, true);
    [Benchmark] public double[] OoplesValues() => Values(_close, false);
    [Benchmark] public OwnedPayload TalibOwnedPayload() => Payload(true);
    [Benchmark] public OwnedPayload OoplesOwnedPayload() => Payload(false);
    [Benchmark] public IIndicatorRun OoplesLatestOnlyBuilder()
    {
        IIndicator indicator = Case == "Asin" ? new PriceCircularTransform(PriceCircularOperation.ArcSine) : new Sma(20);
        return new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureExecution(IndicatorExecutionBackend.Cpu).ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync().GetAwaiter().GetResult();
    }
    [Benchmark] public int OoplesBuilder()
    {
        IIndicator indicator = Case == "Asin" ? new PriceCircularTransform(PriceCircularOperation.ArcSine) : new Sma(20);
        using var run = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
        return run.BarCount;
    }

    public sealed record LatestPayload(Bar Latest, double[] Values, double[]? Presence);
    public sealed record OwnedPayload(Bar[] Bars, double[] Values, double[]? Presence);
}
