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
