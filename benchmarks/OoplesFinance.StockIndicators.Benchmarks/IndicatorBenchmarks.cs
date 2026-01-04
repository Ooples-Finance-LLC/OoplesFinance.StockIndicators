#if BASELINE
extern alias Original;
#endif

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using Perfolizer.Horology;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using System.Collections.Generic;

namespace OoplesFinance.StockIndicators.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Config(typeof(BenchmarkConfig))]
public class IndicatorBenchmarks
{
    private BenchmarkData _data = null!;
    private StockData _stockData = null!;

#if BASELINE
    private Original::OoplesFinance.StockIndicators.Models.StockData _baselineData = null!;
#endif

    [ParamsSource(nameof(Counts))]
    public int Count { get; set; }

    public IEnumerable<int> Counts =>
        BenchmarkProfile.IsFull ? new[] { 10_000, 100_000 } : new[] { 10_000 };

    [ParamsSource(nameof(Lengths))]
    public int Length { get; set; }

    public IEnumerable<int> Lengths =>
        BenchmarkProfile.IsFull ? new[] { 14, 50, 200 } : new[] { 14, 50 };

    [GlobalSetup]
    public void GlobalSetup()
    {
        _data = BenchmarkDataFactory.CreateData(Count);
        _stockData = BenchmarkDataFactory.CreateStockData(_data);
#if BASELINE
        _baselineData = BenchmarkDataFactory.CreateBaselineStockData(_data);
#endif
    }

    [IterationSetup]
    public void IterationSetup()
    {
        BenchmarkDataFactory.Reset(_stockData);
#if BASELINE
        BenchmarkDataFactory.Reset(_baselineData);
#endif
    }

    [Benchmark]
    [BenchmarkCategory("SMA")]
    public object SimpleMovingAverage()
    {
        return _stockData.CalculateSimpleMovingAverage(length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SMA")]
    public object SimpleMovingAverage_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateSimpleMovingAverage(_baselineData, length: Length);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("EMA")]
    public object ExponentialMovingAverage()
    {
        return _stockData.CalculateExponentialMovingAverage(length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("EMA")]
    public object ExponentialMovingAverage_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateExponentialMovingAverage(_baselineData, length: Length);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("WMA")]
    public object WeightedMovingAverage()
    {
        return _stockData.CalculateWeightedMovingAverage(length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("WMA")]
    public object WeightedMovingAverage_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateWeightedMovingAverage(_baselineData, length: Length);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("TMA")]
    public object TriangularMovingAverage()
    {
        return _stockData.CalculateTriangularMovingAverage(MovingAvgType.SimpleMovingAverage, length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TMA")]
    public object TriangularMovingAverage_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateTriangularMovingAverage(
            _baselineData,
            Original::OoplesFinance.StockIndicators.Enums.MovingAvgType.SimpleMovingAverage,
            length: Length);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("HMA")]
    public object HullMovingAverage()
    {
        return _stockData.CalculateHullMovingAverage(MovingAvgType.WeightedMovingAverage, length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("HMA")]
    public object HullMovingAverage_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateHullMovingAverage(
            _baselineData,
            Original::OoplesFinance.StockIndicators.Enums.MovingAvgType.WeightedMovingAverage,
            length: Length);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("Bollinger")]
    public object BollingerBands()
    {
        return _stockData.CalculateBollingerBands(length: Length, stdDevMult: 2);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Bollinger")]
    public object BollingerBands_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateBollingerBands(_baselineData, length: Length, stdDevMult: 2);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("RSI")]
    public object RelativeStrengthIndex()
    {
        return _stockData.CalculateRelativeStrengthIndex(length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RSI")]
    public object RelativeStrengthIndex_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateRelativeStrengthIndex(_baselineData, length: Length);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("ATR")]
    public object AverageTrueRange()
    {
        return _stockData.CalculateAverageTrueRange(length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ATR")]
    public object AverageTrueRange_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateAverageTrueRange(_baselineData, length: Length);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("Ulcer")]
    public object UlcerIndex()
    {
        return _stockData.CalculateUlcerIndex(length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Ulcer")]
    public object UlcerIndex_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateUlcerIndex(_baselineData, length: Length);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("Vortex")]
    public object VortexIndicator()
    {
        return _stockData.CalculateVortexIndicator(length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Vortex")]
    public object VortexIndicator_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateVortexIndicator(_baselineData, length: Length);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("Varadi")]
    public object VaradiOscillator()
    {
        return _stockData.CalculateVaradiOscillator(length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Varadi")]
    public object VaradiOscillator_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateVaradiOscillator(_baselineData, length: Length);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("Spearman")]
    public object SpearmanIndicator()
    {
        return _stockData.CalculateSpearmanIndicator(length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Spearman")]
    public object SpearmanIndicator_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateSpearmanIndicator(_baselineData, length: Length);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("Donchian")]
    public object DonchianChannels()
    {
        return _stockData.CalculateDonchianChannels(length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Donchian")]
    public object DonchianChannels_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateDonchianChannels(_baselineData, length: Length);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("CMF")]
    public object ChaikinMoneyFlow()
    {
        return _stockData.CalculateChaikinMoneyFlow(length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CMF")]
    public object ChaikinMoneyFlow_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateChaikinMoneyFlow(_baselineData, length: Length);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("Choppiness")]
    public object ChoppinessIndex()
    {
        return _stockData.CalculateChoppinessIndex(length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Choppiness")]
    public object ChoppinessIndex_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateChoppinessIndex(_baselineData, length: Length);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("Stochastic")]
    public object StochasticOscillator()
    {
        return _stockData.CalculateStochasticOscillator(length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Stochastic")]
    public object StochasticOscillator_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateStochasticOscillator(_baselineData, length: Length);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("Chandelier")]
    public object ChandelierExit()
    {
        return _stockData.CalculateChandelierExit(length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Chandelier")]
    public object ChandelierExit_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateChandelierExit(_baselineData, length: Length);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("MACD")]
    public object MovingAverageConvergenceDivergence()
    {
        return _stockData.CalculateMovingAverageConvergenceDivergence();
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MACD")]
    public object MovingAverageConvergenceDivergence_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateMovingAverageConvergenceDivergence(_baselineData);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("PPO")]
    public object PercentagePriceOscillator()
    {
        return _stockData.CalculatePercentagePriceOscillator();
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PPO")]
    public object PercentagePriceOscillator_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculatePercentagePriceOscillator(_baselineData);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("PivotPoint")]
    public object CamarillaPivotPoints()
    {
        return _stockData.CalculateCamarillaPivotPoints();
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PivotPoint")]
    public object CamarillaPivotPoints_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateCamarillaPivotPoints(_baselineData);
    }
#endif

    [Benchmark]
    [BenchmarkCategory("Chande")]
    public object ChandeMomentumOscillator()
    {
        return _stockData.CalculateChandeMomentumOscillator(length: Length);
    }

#if BASELINE
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Chande")]
    public object ChandeMomentumOscillator_Original()
    {
        return Original::OoplesFinance.StockIndicators.Calculations.CalculateChandeMomentumOscillator(_baselineData, length: Length);
    }
#endif

    private sealed class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.Default
                .WithMinIterationTime(TimeInterval.FromMilliseconds(100))
                .WithWarmupCount(1)
                .WithIterationCount(1)
                .WithInvocationCount(1)
                .WithUnrollFactor(1));
            AddExporter(MarkdownExporter.GitHub);
            AddExporter(CsvExporter.Default);
        }
    }
}
