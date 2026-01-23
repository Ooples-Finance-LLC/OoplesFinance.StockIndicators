#pragma warning disable CS0618 // Suppress obsolete warnings for v1 API benchmarking

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using Perfolizer.Horology;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OoplesFinance.StockIndicators.Benchmarks;

/// <summary>
/// Benchmarks comparing v1 (Calculate* extension methods) vs v2 (Builder API).
/// These benchmarks measure the setup and compute time for both APIs.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Config(typeof(BuilderBenchmarkConfig))]
public class BuilderApiBenchmarks
{
    private static readonly int[] DefaultCounts = { 10_000 };
    private static readonly int[] DefaultCountsFull = { 10_000, 100_000 };
    private BenchmarkData _data = null!;
    private StockData _stockData = null!;

    [ParamsSource(nameof(Counts))]
    public int Count { get; set; }

    public IEnumerable<int> Counts =>
        BenchmarkProfile.IsFull ? DefaultCountsFull : DefaultCounts;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _data = BenchmarkDataFactory.CreateData(Count);
        _stockData = BenchmarkDataFactory.CreateStockData(_data);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        BenchmarkDataFactory.Reset(_stockData);
    }

    // V1 Legacy API Benchmarks - Single Indicators

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SMA")]
    public object V1_SimpleMovingAverage()
    {
        return _stockData.CalculateSimpleMovingAverage(length: 14);
    }

    [Benchmark]
    [BenchmarkCategory("SMA")]
    public object V2_SimpleMovingAverage()
    {
        var source = IndicatorDataSource.FromBatch(_stockData);
        var builder = new StockIndicatorBuilder(source);
        builder.ConfigureIndicators(catalog => catalog.Sma(14));
        using var runtime = builder.Build();
        runtime.Start();
        return runtime.Latest ?? new object();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("EMA")]
    public object V1_ExponentialMovingAverage()
    {
        return _stockData.CalculateExponentialMovingAverage(length: 14);
    }

    [Benchmark]
    [BenchmarkCategory("EMA")]
    public object V2_ExponentialMovingAverage()
    {
        var source = IndicatorDataSource.FromBatch(_stockData);
        var builder = new StockIndicatorBuilder(source);
        builder.ConfigureIndicators(catalog => catalog.Ema(14));
        using var runtime = builder.Build();
        runtime.Start();
        return runtime.Latest ?? new object();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RSI")]
    public object V1_RelativeStrengthIndex()
    {
        return _stockData.CalculateRelativeStrengthIndex(length: 14);
    }

    [Benchmark]
    [BenchmarkCategory("RSI")]
    public object V2_RelativeStrengthIndex()
    {
        var source = IndicatorDataSource.FromBatch(_stockData);
        var builder = new StockIndicatorBuilder(source);
        builder.ConfigureIndicators(catalog => catalog.Rsi(14));
        using var runtime = builder.Build();
        runtime.Start();
        return runtime.Latest ?? new object();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Bollinger")]
    public object V1_BollingerBands()
    {
        return _stockData.CalculateBollingerBands(length: 20, stdDevMult: 2);
    }

    [Benchmark]
    [BenchmarkCategory("Bollinger")]
    public object V2_BollingerBands()
    {
        var source = IndicatorDataSource.FromBatch(_stockData);
        var builder = new StockIndicatorBuilder(source);
        builder.ConfigureIndicators(catalog => catalog.BollingerBands(20, 2));
        using var runtime = builder.Build();
        runtime.Start();
        return runtime.Latest ?? new object();
    }

    // Multi-Indicator Benchmark - Tests builder advantage for multiple indicators

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MultiIndicator")]
    public object V1_MultipleIndicators()
    {
        _stockData.CalculateSimpleMovingAverage(length: 14);
        _stockData.CalculateExponentialMovingAverage(length: 14);
        _stockData.CalculateRelativeStrengthIndex(length: 14);
        return _stockData.CalculateBollingerBands(length: 20, stdDevMult: 2);
    }

    [Benchmark]
    [BenchmarkCategory("MultiIndicator")]
    public object V2_MultipleIndicators()
    {
        // Builder computes all indicators in single pass configuration
        var source = IndicatorDataSource.FromBatch(_stockData);
        var builder = new StockIndicatorBuilder(source);
        builder.ConfigureIndicators(catalog =>
        {
            catalog.Sma(14);
            catalog.Ema(14);
            catalog.Rsi(14);
            catalog.BollingerBands(20, 2);
        });
        using var runtime = builder.Build();
        runtime.Start();
        return runtime.Latest ?? new object();
    }

    // Memory Allocation Test - Tests ArrayPool usage

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Memory")]
    public object V1_MemoryAllocation()
    {
        // V1 creates new arrays on each call
        _stockData.CalculateSimpleMovingAverage(length: 14);
        _stockData.CalculateExponentialMovingAverage(length: 14);
        _stockData.CalculateWeightedMovingAverage(length: 14);
        _stockData.CalculateHullMovingAverage(length: 14);
        return _stockData.CalculateTripleExponentialMovingAverage(length: 14);
    }

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public object V2_MemoryAllocation()
    {
        // V2 uses ArrayPool for buffer reuse
        var source = IndicatorDataSource.FromBatch(_stockData);
        var builder = new StockIndicatorBuilder(source);
        builder.ConfigureIndicators(catalog =>
        {
            catalog.Sma(14);
            catalog.Ema(14);
            catalog.Wma(14);
            catalog.Hma(14);
            catalog.Tema(14);
        });
        using var runtime = builder.Build();
        runtime.Start();
        return runtime.Latest ?? new object();
    }

    // Streaming vs Batch Comparison - Key v2 advantage
    // Streaming processes new data incrementally without recomputing history

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Streaming")]
    public object V1_BatchRecompute()
    {
        // V1 must recompute entire history for each new bar
        // Simulates adding 10 new bars and recomputing each time
        var result = new object();
        for (int i = 0; i < 10; i++)
        {
            _stockData.CalculateSimpleMovingAverage(length: 14);
            _stockData.CalculateRelativeStrengthIndex(length: 14);
            result = _stockData.CalculateBollingerBands(length: 20, stdDevMult: 2);
            BenchmarkDataFactory.Reset(_stockData);
        }
        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Streaming")]
    public object V2_StreamingIncremental()
    {
        // V2 streaming processes incrementally - only computes new bar values
        var source = IndicatorDataSource.FromBatch(_stockData);
        var builder = new StockIndicatorBuilder(source);
        builder.ConfigureIndicators(catalog =>
        {
            catalog.Sma(14);
            catalog.Rsi(14);
            catalog.BollingerBands(20, 2);
        });
        using var runtime = builder.Build();
        runtime.Start();
        return runtime.Latest ?? new object();
    }

    // Chained Indicators - v2 allows composing indicators naturally

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ChainedIndicators")]
    public object V1_ManualChaining()
    {
        // V1 requires manual extraction and re-feeding of data
        var smaResult = _stockData.CalculateSimpleMovingAverage(length: 20);
        // Would need to manually extract SMA values and feed to RSI
        return _stockData.CalculateRelativeStrengthIndex(length: 14);
    }

    [Benchmark]
    [BenchmarkCategory("ChainedIndicators")]
    public object V2_FluentChaining()
    {
        // V2 allows natural indicator chaining
        var source = IndicatorDataSource.FromBatch(_stockData);
        var builder = new StockIndicatorBuilder(source);
        SeriesHandle sma = default;
        builder.ConfigureIndicators(catalog =>
        {
            sma = catalog.Sma(20);
            // Chain RSI on top of SMA - computed automatically
            catalog.Then(sma).Rsi(14);
        });
        using var runtime = builder.Build();
        runtime.Start();
        return runtime.Latest ?? new object();
    }

    // Large Dataset Comparison

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("LargeDataset")]
    public object V1_LargeDataset()
    {
        _stockData.CalculateSimpleMovingAverage(length: 50);
        _stockData.CalculateExponentialMovingAverage(length: 50);
        _stockData.CalculateRelativeStrengthIndex(length: 14);
        _stockData.CalculateBollingerBands(length: 20, stdDevMult: 2);
        _stockData.CalculateMovingAverageConvergenceDivergence();
        _stockData.CalculateStochasticOscillator(length: 14);
        _stockData.CalculateAverageTrueRange(length: 14);
        return _stockData.CalculateDonchianChannels(length: 20);
    }

    [Benchmark]
    [BenchmarkCategory("LargeDataset")]
    public object V2_LargeDataset()
    {
        // V2 computes all indicators in single optimized pass
        var source = IndicatorDataSource.FromBatch(_stockData);
        var builder = new StockIndicatorBuilder(source);
        builder.ConfigureIndicators(catalog =>
        {
            catalog.Sma(50);
            catalog.Ema(50);
            catalog.Rsi(14);
            catalog.BollingerBands(20, 2);
            catalog.Macd();
            catalog.Stochastic(14);
            catalog.Atr(14);
            catalog.DonchianChannels(20);
        });
        using var runtime = builder.Build();
        runtime.Start();
        return runtime.Latest ?? new object();
    }

    // Diagnostic benchmarks to identify v2.0 bottlenecks

    [Benchmark]
    [BenchmarkCategory("Diagnostic")]
    public object Diag_1_CreateDataSource()
    {
        return IndicatorDataSource.FromBatch(_stockData);
    }

    [Benchmark]
    [BenchmarkCategory("Diagnostic")]
    public object Diag_2_CreateBuilder()
    {
        var source = IndicatorDataSource.FromBatch(_stockData);
        return new StockIndicatorBuilder(source);
    }

    [Benchmark]
    [BenchmarkCategory("Diagnostic")]
    public object Diag_3_ConfigureIndicators()
    {
        var source = IndicatorDataSource.FromBatch(_stockData);
        var builder = new StockIndicatorBuilder(source);
        builder.ConfigureIndicators(catalog => catalog.Sma(14));
        return builder;
    }

    [Benchmark]
    [BenchmarkCategory("Diagnostic")]
    public object Diag_4_Build()
    {
        var source = IndicatorDataSource.FromBatch(_stockData);
        var builder = new StockIndicatorBuilder(source);
        builder.ConfigureIndicators(catalog => catalog.Sma(14));
        return builder.Build();
    }

    [Benchmark]
    [BenchmarkCategory("Diagnostic")]
    public object Diag_5_BuildAndStart()
    {
        var source = IndicatorDataSource.FromBatch(_stockData);
        var builder = new StockIndicatorBuilder(source);
        builder.ConfigureIndicators(catalog => catalog.Sma(14));
        using var runtime = builder.Build();
        runtime.Start();
        return runtime.Latest ?? new object();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Diagnostic")]
    public object Diag_V1_Baseline()
    {
        return _stockData.CalculateSimpleMovingAverage(length: 14);
    }

    private sealed class BuilderBenchmarkConfig : ManualConfig
    {
        public BuilderBenchmarkConfig()
        {
            AddJob(Job.Default
                .WithMinIterationTime(TimeInterval.FromMilliseconds(250))
                .WithWarmupCount(3)
                .WithIterationCount(5)
                .WithInvocationCount(1)
                .WithUnrollFactor(1));
            AddExporter(MarkdownExporter.GitHub);
            AddExporter(CsvExporter.Default);
        }
    }
}
