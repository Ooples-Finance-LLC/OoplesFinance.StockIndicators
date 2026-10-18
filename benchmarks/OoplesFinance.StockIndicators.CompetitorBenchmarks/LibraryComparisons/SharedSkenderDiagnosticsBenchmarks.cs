using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderDiagnosticsBenchmarks : SharedSkenderBenchmarkBase
{
    private const int Period = 20;
    [Params("Skender.GetMaEnvelopes", "Skender.GetSmaAnalysis", "Skender.GetPrs", "Skender.GetStdDevChannels")]
    public string Operation { get; set; } = "";
    private (DateTime Date, double Value)[] _prices = null!;
    private (DateTime Date, double Value)[] _basis = null!;
    private IEnumerable<(DateTime Date, double Value)>[] _windows = null!;
    private IEnumerable<(DateTime Date, double Value)>[] _basisWindows = null!;

    [GlobalSetup]
    public void Setup()
    {
        int? preceding = Operation switch
        {
            "Skender.GetMaEnvelopes" or "Skender.GetSmaAnalysis" => Period - 1,
            "Skender.GetPrs" => Period,
            "Skender.GetStdDevChannels" => null,
            _ => throw new ArgumentException("Unknown Skender diagnostic operation.")
        };
        if (Operation == "Skender.GetPrs") ArgumentOutOfRangeException.ThrowIfLessThan(Count, Period);
        var (pair, data) = PrepareNativeFixture(Operation, preceding);
        if (Operation is "Skender.GetPrs" or "Skender.GetStdDevChannels") ConfigureLongWindows();
        bool converted = Operation is "Skender.GetMaEnvelopes" or "Skender.GetSmaAnalysis";
        _prices = data.Dates.Select((date, i) => (date, converted ? (double)_quotes[i].Close : data.Closes[i])).ToArray();
        _basis = data.Dates.Select((date, i) => (date, data.Opens[i])).ToArray();
        _windows = _ends.Select((end, i) => _prices.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        _basisWindows = _ends.Select((end, i) => _basis.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        var expected = pair.Reference!(data, Period);
        if (Operation is "Skender.GetStdDevChannels" or "Skender.GetMaEnvelopes") VerifyOwnedDetails(expected);
        else SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, expected, pair.OutputNames!);
        VerifyNative(pair.CompetitorReference!(data, Period), NativeSingle(), NativeParallel(), Normalize, Operation, IndicatorErrorBudget.Exact);
    }

    private void ConfigureLongWindows()
    {
        int blocks = Count / Period;
        int workers = Math.Min(Math.Min(8, Environment.ProcessorCount), Math.Max(1, blocks));
        bool channels = Operation == "Skender.GetStdDevChannels";
        // Retrospective channel blocks are anchored at the final row. Each
        // prefix must end on that same block grid and retain absolute indices.
        _ends = Enumerable.Range(1, workers).Select(i => channels
            ? Count % Period + (int)((long)blocks * i / workers) * Period
            : (int)((long)Count * i / workers)).ToArray();
        // PRS requires at least 20 quotes, even for startup-only results.
        _seeds = Enumerable.Range(0, workers).Select(i => channels ? 0
            : Math.Max(0, (i == 0 ? 0 : _ends[i - 1]) - Period)).ToArray();
    }

    private void VerifyOwnedDetails(ComparisonSeries expected)
    {
        bool channels = Operation == "Skender.GetStdDevChannels";
        var names = channels ? RegressionChannelComparison.Names : EnvelopeComparison.Names;
        int[] flagSlots = channels ? [4, 5, 6, -1] : [3, 3, 3];
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner = Create();
            using var run = SharedBenchmarkSupport.BuildCpu(_bars, owner, history);
            run.Dispose();
            for (int slot = 0; slot < names.Length; slot++)
            {
                var column = expected.Outputs[names[slot]];
                var values = column.Values.Select((v, i) => column.Present![i] ? v : 0).ToArray();
                AsinFeasibilityBenchmarks.RequireSame(values, run[owner.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame([values[^1]], [run.Latest[owner.Outputs[slot]]]);
                // BreakPoint has no presence column; envelopes share one flag.
                int flag = flagSlots[slot];
                if (flag < 0) continue;
                var mask = column.Present!.Select(p => p ? 1d : 0d).ToArray();
                AsinFeasibilityBenchmarks.RequireSame(mask, run[owner.Outputs[flag]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame([mask[^1]], [run.Latest[owner.Outputs[flag]]]);
            }
        }
    }

    protected override IIndicator Create() => Operation switch
    {
        "Skender.GetMaEnvelopes" => new WindowAverageEnvelope(Period),
        "Skender.GetSmaAnalysis" => new MovingAverageDiagnostics(Period),
        "Skender.GetPrs" => new PriceRelativeStrength(Period, 3),
        "Skender.GetStdDevChannels" => new RegressionWithChannels(Period, 2),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark(Baseline = true)]
    public object NativeSingle() => Operation switch
    {
        // Envelopes return a deferred projection: materialize inside timing.
        "Skender.GetMaEnvelopes" => _prices.GetMaEnvelopes(Period, 2.5, MaType.SMA).ToList(),
        "Skender.GetSmaAnalysis" => _prices.GetSmaAnalysis(Period),
        "Skender.GetPrs" => _prices.GetPrs(_basis, Period, 3),
        "Skender.GetStdDevChannels" => _prices.GetStdDevChannels(Period, 2),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark]
    public object NativeParallel() => Operation switch
    {
        "Skender.GetMaEnvelopes" => Parallel(_windows, static prices => prices.GetMaEnvelopes(Period, 2.5, MaType.SMA).ToList()),
        "Skender.GetSmaAnalysis" => Parallel(_windows, static prices => prices.GetSmaAnalysis(Period)),
        "Skender.GetPrs" => ParallelPrs(),
        "Skender.GetStdDevChannels" => Parallel(_windows, static prices => prices.GetStdDevChannels(Period, 2)),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    private PrsResult[] ParallelPrs()
    {
        var joined = new PrsResult[Count];
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int first = worker == 0 ? 0 : _ends[worker - 1];
            var rows = (List<PrsResult>)_windows[worker].GetPrs(_basisWindows[worker], Period, 3);
            if (rows.Count != _ends[worker] - _seeds[worker]) throw new InvalidOperationException("Native PRS range length differs.");
            rows.CopyTo(first - _seeds[worker], joined, first, _ends[worker] - first);
        });
        return joined;
    }

    internal static ComparisonSeries Normalize(object result) => result switch
    {
        IReadOnlyList<MaEnvelopeResult> rows => EnvelopeComparison.FromRows(rows),
        IReadOnlyList<SmaAnalysis> rows => RetrospectivePriceComparison.Series(MeanErrorComparison.Names,
            [rows.Select(v => v.Sma).ToArray(), rows.Select(v => v.Mad).ToArray(), rows.Select(v => v.Mse).ToArray(), rows.Select(v => v.Mape).ToArray()]),
        IReadOnlyList<PrsResult> rows => PriceRelativeComparison.FromNative(rows),
        IReadOnlyList<StdDevChannelsResult> rows => RegressionChannelComparison.Series(
            [rows.Select(v => v.Centerline).ToArray(), rows.Select(v => v.UpperChannel).ToArray(), rows.Select(v => v.LowerChannel).ToArray(), rows.Select(v => (double?)(v.BreakPoint ? 1 : 0)).ToArray()]),
        _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
    };
}
