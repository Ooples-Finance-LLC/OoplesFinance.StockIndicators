using System.Collections.Generic;
using System.Diagnostics;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Benchmarks;

public static class StreamingPerformanceRunner
{
    private const int DefaultTickCount = 100_000;

    public static bool TryRun(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            return false;
        }

        var run = false;
        var tickCount = DefaultTickCount;
        var includeOutputs = false;
        var extended = false;
        var timeframeCount = 0;
        var indicatorCount = 0;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--streaming-perf", StringComparison.OrdinalIgnoreCase))
            {
                run = true;
            }
            else if (string.Equals(arg, "--include-outputs", StringComparison.OrdinalIgnoreCase))
            {
                includeOutputs = true;
            }
            else if (string.Equals(arg, "--extended", StringComparison.OrdinalIgnoreCase))
            {
                extended = true;
            }
            else if (string.Equals(arg, "--timeframes", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out var parsed))
                {
                    timeframeCount = Math.Max(0, parsed);
                }

                i++;
            }
            else if (string.Equals(arg, "--indicators", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out var parsed))
                {
                    indicatorCount = Math.Max(0, parsed);
                }

                i++;
            }
            else if (string.Equals(arg, "--ticks", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out var parsed))
                {
                    tickCount = Math.Max(1, parsed);
                }

                i++;
            }
        }

        if (!run)
        {
            return false;
        }

        var result = Run(tickCount, includeOutputs, extended, timeframeCount, indicatorCount);
        Console.WriteLine($"Streaming fanout: ticks={result.TickCount}, timeframes={result.TimeframeCount}, indicators={result.IndicatorCount}");
        Console.WriteLine($"Elapsed: {result.Elapsed.TotalSeconds:N2}s");
        Console.WriteLine($"Throughput: {result.TicksPerSecond:N0} ticks/sec");
        Console.WriteLine($"p95 latency: {result.P95LatencyMs:N2} ms");
        Console.WriteLine($"Updates: {result.UpdateCount:N0} callbacks");
        return true;
    }

    public static StreamingPerfResult Run(int tickCount = DefaultTickCount, bool includeOutputs = false,
        bool extended = false, int timeframeCount = 0, int indicatorCount = 0)
    {
        var timeframes = new[]
        {
            BarTimeframe.Tick,
            BarTimeframe.Seconds(1),
            BarTimeframe.Seconds(5),
            BarTimeframe.Minutes(1),
            BarTimeframe.Minutes(5)
        };
        if (timeframeCount > 0 && timeframeCount < timeframes.Length)
        {
            var trimmed = new BarTimeframe[timeframeCount];
            for (var i = 0; i < timeframeCount; i++)
            {
                trimmed[i] = timeframes[i];
            }

            timeframes = trimmed;
        }

        var indicators = new List<Func<IStreamingIndicatorState>>
        {
            () => new SimpleMovingAverageState(length: 20),
            () => new ExponentialMovingAverageState(length: 20),
            () => new RelativeStrengthIndexState(length: 14),
            () => new MovingAverageConvergenceDivergenceState(),
            () => new AverageTrueRangeState(length: 14),
            () => new AverageDirectionalIndexState(length: 14),
            () => new BollingerBandsState(length: 20, stdDevMult: 2),
            () => new OnBalanceVolumeState(length: 20),
            () => new MoneyFlowIndexState(length: 14),
            () => new RateOfChangeState(length: 12)
        };

        if (extended)
        {
            indicators.Add(() => new UlcerIndexState(length: 14));
            indicators.Add(() => new VortexIndicatorState(length: 14));
            indicators.Add(() => new AwesomeOscillatorState());
            indicators.Add(() => new AcceleratorOscillatorState());
            indicators.Add(() => new TrixState());
        }
        if (indicatorCount > 0 && indicatorCount < indicators.Count)
        {
            indicators.RemoveRange(indicatorCount, indicators.Count - indicatorCount);
        }

        var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
        {
            EmitUpdates = true
        });

        var subscriptionOptions = new IndicatorSubscriptionOptions
        {
            IncludeUpdates = true,
            IncludeOutputValues = includeOutputs,
            InputName = InputName.Close
        };

        var updateCount = 0L;
        var latencies = new long[tickCount];
        var startTimestamp = 0L;
        var maxLatencyTicks = 0L;

        void OnUpdate(StreamingIndicatorStateUpdate _)
        {
            var latency = Stopwatch.GetTimestamp() - startTimestamp;
            if (latency > maxLatencyTicks)
            {
                maxLatencyTicks = latency;
            }

            updateCount++;
        }

        const string symbol = "AAPL";
        for (var i = 0; i < indicators.Count; i++)
        {
            var indicatorFactory = indicators[i];
            for (var j = 0; j < timeframes.Length; j++)
            {
                engine.RegisterStatefulIndicator(symbol, timeframes[j], indicatorFactory(), OnUpdate, subscriptionOptions);
            }
        }

        var startTime = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < tickCount; i++)
        {
            maxLatencyTicks = 0;
            startTimestamp = Stopwatch.GetTimestamp();
            engine.OnTrade(new StreamTrade(symbol, startTime.AddMilliseconds(i), 100 + (i % 10), 1));
            latencies[i] = maxLatencyTicks;
        }

        stopwatch.Stop();

        var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
        var ticksPerSecond = elapsedSeconds > 0 ? tickCount / elapsedSeconds : 0;
        var p95LatencyMs = CalculatePercentile(latencies, 0.95) * 1000.0 / Stopwatch.Frequency;

        return new StreamingPerfResult(tickCount, timeframes.Length, indicators.Count, updateCount,
            stopwatch.Elapsed, ticksPerSecond, p95LatencyMs);
    }

    private static double CalculatePercentile(long[] samples, double percentile)
    {
        if (samples.Length == 0)
        {
            return 0;
        }

        Array.Sort(samples);
        var rank = (int)Math.Ceiling(percentile * samples.Length);
        rank = Math.Max(1, Math.Min(rank, samples.Length));
        return samples[rank - 1];
    }
}

public sealed class StreamingPerfResult
{
    public StreamingPerfResult(int tickCount, int timeframeCount, int indicatorCount, long updateCount,
        TimeSpan elapsed, double ticksPerSecond, double p95LatencyMs)
    {
        TickCount = tickCount;
        TimeframeCount = timeframeCount;
        IndicatorCount = indicatorCount;
        UpdateCount = updateCount;
        Elapsed = elapsed;
        TicksPerSecond = ticksPerSecond;
        P95LatencyMs = p95LatencyMs;
    }

    public int TickCount { get; }
    public int TimeframeCount { get; }
    public int IndicatorCount { get; }
    public long UpdateCount { get; }
    public TimeSpan Elapsed { get; }
    public double TicksPerSecond { get; }
    public double P95LatencyMs { get; }
}
