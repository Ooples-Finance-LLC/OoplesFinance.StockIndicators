using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderStatisticsBenchmarks : SharedSkenderBenchmarkBase
{
    private const int Period = 20;
    [Params("Skender.GetBeta", "Skender.GetCorrelation", "Skender.GetEpma")]
    public string Operation { get; set; } = "";
    private (DateTime Date, double Value)[] _closes = null!;
    private (DateTime Date, double Value)[] _opens = null!;
    private IEnumerable<(DateTime Date, double Value)>[] _closeWindows = null!;
    private IEnumerable<(DateTime Date, double Value)>[] _openWindows = null!;

    [GlobalSetup]
    public void Setup()
    {
        int? preceding = Operation switch
        {
            "Skender.GetBeta" => Period,
            "Skender.GetCorrelation" => Period - 1,
            "Skender.GetEpma" => null,
            _ => throw new ArgumentException("Unknown Skender statistics operation.")
        };
        var (pair, data) = PrepareNativeFixture(Operation, preceding);
        // Pair statistics already use raw tuples. EPMA preserves the quote
        // conversion of its existing comparator before calling the public tuple API.
        _closes = data.Dates.Select((date, i) => (date, Operation == "Skender.GetEpma"
            ? (double)_quotes[i].Close : data.Closes[i])).ToArray();
        _opens = data.Dates.Select((date, i) => (date, data.Opens[i])).ToArray();
        _closeWindows = _ends.Select((end, i) => _closes.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        _openWindows = _ends.Select((end, i) => _opens.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, pair.Reference!(data, Period),
            pair.OutputNames ?? ["Value"], includeScalarStartup: Operation == "Skender.GetEpma");
        var nativeExpected = Operation == "Skender.GetEpma"
            ? EpmaReference(_closes.Select(v => v.Value).ToArray()) : pair.CompetitorReference!(data, Period);
        VerifyNative(nativeExpected, NativeSingle(), NativeParallel(), Normalize, Operation, IndicatorErrorBudget.Exact);
    }

    protected override IIndicator Create() => Operation switch
    {
        "Skender.GetBeta" => new WindowBetaStatistics(Period, ReturnBetaSelection.All),
        "Skender.GetCorrelation" => new WindowPairStatistics(Period),
        "Skender.GetEpma" => new WindowLinearRegression(Period, WindowRegressionOutput.Endpoint),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark(Baseline = true)]
    public object NativeSingle() => Operation switch
    {
        "Skender.GetBeta" => _closes.GetBeta(_opens, Period, BetaType.All),
        "Skender.GetCorrelation" => _closes.GetCorrelation(_opens, Period),
        "Skender.GetEpma" => _closes.GetEpma(Period),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark]
    public object NativeParallel() => Operation switch
    {
        "Skender.GetBeta" => ParallelPair(static (evaluation, market) => evaluation.GetBeta(market, Period, BetaType.All)),
        "Skender.GetCorrelation" => ParallelPair(static (a, b) => a.GetCorrelation(b, Period)),
        // Absolute regression indices affect rounded endpoint arithmetic.
        // Retain full prefixes even though the mathematical window is finite.
        "Skender.GetEpma" => Parallel(_closeWindows, static prices => prices.GetEpma(Period)),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    private T[] ParallelPair<T>(Func<IEnumerable<(DateTime, double)>, IEnumerable<(DateTime, double)>, IEnumerable<T>> calculate)
    {
        var joined = new T[Count];
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int first = worker == 0 ? 0 : _ends[worker - 1];
            int length = _ends[worker] - first;
            var rows = (List<T>)calculate(_closeWindows[worker], _openWindows[worker]);
            if (rows.Count != _ends[worker] - _seeds[worker]) throw new InvalidOperationException("Paired native range length differs.");
            rows.CopyTo(first - _seeds[worker], joined, first, length);
        });
        return joined;
    }

    internal static ComparisonSeries Normalize(object result) => result switch
    {
        IReadOnlyList<BetaResult> rows => ReturnBetaComparison.FromSkender(rows),
        IReadOnlyList<CorrResult> rows => PairStatisticsComparison.FromSkender(rows),
        IReadOnlyList<EpmaResult> rows => VolumePriceComparison.Mask(rows.Select(row => row.Epma).ToArray()),
        _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
    };

    private static ComparisonSeries EpmaReference(double[] prices)
    {
        var values = new double?[prices.Length];
        for (int last = Period - 1; last < prices.Length; last++)
        {
            int first = last - Period + 1;
            double meanX = (first + last + 2d) / 2;
            double meanY = Divide(prices.Skip(first).Take(Period).Aggregate(0d, Add), Period);
            double covariance = 0, variance = 0;
            for (int i = first; i <= last; i++)
            {
                double x = Subtract(i + 1d, meanX);
                covariance = Add(covariance, Multiply(x, Subtract(prices[i], meanY)));
                variance = Add(variance, Multiply(x, x));
            }
            double slope = Divide(covariance, variance);
            double intercept = Subtract(meanY, Multiply(slope, meanX));
            values[last] = Add(Multiply(slope, last + 1d), intercept);
        }
        return VolumePriceComparison.Mask(values);
    }
}
