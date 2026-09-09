using System.Globalization;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Catalogs;
using OoplesFinance.StockIndicators.Builder.Notifications;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.DevConsole;

/// <summary>
/// Interactive playground for the OoplesFinance.StockIndicators v2.0 Builder API.
/// </summary>
internal static class Program
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static void Main(string[] args)
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("  OoplesFinance.StockIndicators v2.0 Playground");
        Console.WriteLine("==============================================");
        Console.WriteLine();
        Console.WriteLine("This interactive console demonstrates the new");
        Console.WriteLine("v2.0 Builder API features and facade pattern.");
        Console.WriteLine();

        if (TryRunFromArgs(args))
        {
            return;
        }

        RunMainMenu();
    }

    private static void RunMainMenu()
    {
        while (true)
        {
            PrintMainMenu();
            var input = Console.ReadLine()?.Trim().ToUpperInvariant();

            switch (input)
            {
                case "1":
                    RunBuilderApiDemos();
                    break;
                case "2":
                    RunIndicatorDemos();
                    break;
                case "3":
                    RunSignalDemos();
                    break;
                case "4":
                    RunStreamingDemos();
                    break;
                case "5":
                    RunNotificationDemos();
                    break;
                case "6":
                    RunBacktestingDemos();
                    break;
                case "7":
                    RunLegacyExamples();
                    break;
                case "Q":
                    return;
                default:
                    Console.WriteLine("Invalid choice. Please try again.");
                    break;
            }
        }
    }

    private static void PrintMainMenu()
    {
        Console.WriteLine();
        Console.WriteLine("============ MAIN MENU ============");
        Console.WriteLine();
        Console.WriteLine("  v2.0 Builder API Demos:");
        Console.WriteLine("  -----------------------");
        Console.WriteLine("  1) Builder API Quick Start");
        Console.WriteLine("  2) Indicator Configuration");
        Console.WriteLine("  3) Signal Generation");
        Console.WriteLine("  4) Streaming Engine");
        Console.WriteLine("  5) Notifications");
        Console.WriteLine("  6) Backtesting");
        Console.WriteLine();
        Console.WriteLine("  Legacy:");
        Console.WriteLine("  -------");
        Console.WriteLine("  7) Legacy v1.0 Examples");
        Console.WriteLine();
        Console.WriteLine("  Q) Quit");
        Console.WriteLine();
        Console.Write("Select option: ");
    }

    #region Builder API Demos

    private static void RunBuilderApiDemos()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("======== BUILDER API QUICK START ========");
            Console.WriteLine();
            Console.WriteLine("  1) Basic Usage - Calculate SMA, RSI, MACD");
            Console.WriteLine("  2) Chained Indicators - RSI of SMA");
            Console.WriteLine("  3) Multiple Timeframes");
            Console.WriteLine("  4) Custom Formulas");
            Console.WriteLine("  5) Full Example - Complete Setup");
            Console.WriteLine();
            Console.WriteLine("  B) Back to Main Menu");
            Console.WriteLine();
            Console.Write("Select option: ");

            var input = Console.ReadLine()?.Trim().ToUpperInvariant();
            switch (input)
            {
                case "1":
                    DemoBasicUsage();
                    break;
                case "2":
                    DemoChainedIndicators();
                    break;
                case "3":
                    DemoMultipleTimeframes();
                    break;
                case "4":
                    DemoCustomFormulas();
                    break;
                case "5":
                    DemoFullExample();
                    break;
                case "B":
                    return;
                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }
            Pause();
        }
    }

    private static void DemoBasicUsage()
    {
        Console.WriteLine();
        Console.WriteLine("--- Basic Usage: Calculate SMA, RSI, MACD ---");
        Console.WriteLine();
        Console.WriteLine("Code:");
        Console.WriteLine(@"
  var data = BuildSampleData(200);
  var source = IndicatorDataSource.FromBatch(new StockData(data));

  SeriesHandle sma = default, rsi = default;
  MacdResult macd = default;

  var runtime = new StockIndicatorBuilder(source)
      .ConfigureIndicators(indicators =>
      {
          sma = indicators.Sma(20);
          rsi = indicators.Rsi(14);
          macd = indicators.Macd(12, 26, 9);
      })
      .Build();

  runtime.Start();
  var snapshot = runtime.Latest!;
  Console.WriteLine($""SMA(20) = {snapshot.GetLastValue(sma)}"");
  Console.WriteLine($""RSI(14) = {snapshot.GetLastValue(rsi)}"");
  Console.WriteLine($""MACD    = {snapshot.GetLastValue(macd.Primary)}"");
");

        // Execute the demo
        var data = BuildSampleData(200, 100d, DateTime.UtcNow.AddDays(-200));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        SeriesHandle sma = default, rsi = default;
        MacdResult macd = default;

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(indicators =>
            {
                sma = indicators.Sma(20);
                rsi = indicators.Rsi(14);
                macd = indicators.Macd(12, 26, 9);
            })
            .Build();

        runtime.Start();
        var snapshot = runtime.Latest!;

        Console.WriteLine("Output:");
        Console.WriteLine($"  SMA(20) = {FormatValue(snapshot.GetLastValue(sma))}");
        Console.WriteLine($"  RSI(14) = {FormatValue(snapshot.GetLastValue(rsi))}");
        Console.WriteLine($"  MACD    = {FormatValue(snapshot.GetLastValue(macd.Primary))}");
    }

    private static void DemoChainedIndicators()
    {
        Console.WriteLine();
        Console.WriteLine("--- Chained Indicators: RSI of SMA ---");
        Console.WriteLine();
        Console.WriteLine("Code:");
        Console.WriteLine(@"
  var runtime = new StockIndicatorBuilder(source)
      .ConfigureIndicators(indicators =>
      {
          var price = indicators.Price();
          var sma = indicators.Sma(20, price);

          // Chain: Calculate RSI of the SMA values
          rsiOfSma = indicators.Then(sma).Rsi(14);
      })
      .Build();
");

        var data = BuildSampleData(200, 100d, DateTime.UtcNow.AddDays(-200));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        SeriesHandle rsiOfSma = default;

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(indicators =>
            {
                var price = indicators.Price();
                var sma = indicators.Sma(20, price);
                rsiOfSma = indicators.Then(sma).Rsi(14);
            })
            .Build();

        runtime.Start();
        var snapshot = runtime.Latest!;

        Console.WriteLine("Output:");
        Console.WriteLine($"  RSI(14) of SMA(20) = {FormatValue(snapshot.GetLastValue(rsiOfSma))}");
    }

    private static void DemoMultipleTimeframes()
    {
        Console.WriteLine();
        Console.WriteLine("--- Multiple Timeframes ---");
        Console.WriteLine();
        Console.WriteLine("The v2.0 API supports calculating indicators across");
        Console.WriteLine("multiple timeframes from a single data source.");
        Console.WriteLine();
        Console.WriteLine("This feature is particularly useful for streaming data");
        Console.WriteLine("where you want to track indicators on 1-min, 5-min,");
        Console.WriteLine("and daily bars simultaneously.");
    }

    private static void DemoCustomFormulas()
    {
        Console.WriteLine();
        Console.WriteLine("--- Custom Formulas ---");
        Console.WriteLine();
        Console.WriteLine("Code:");
        Console.WriteLine(@"
  var runtime = new StockIndicatorBuilder(source)
      .ConfigureIndicators(indicators =>
      {
          var sma20 = indicators.Sma(20);
          var sma50 = indicators.Sma(50);

          // Custom formula: SMA20 - SMA50 (spread)
          spread = indicators.Formula(sma20, sma50, FormulaOp.Subtract);

          // Or use a custom function
          ratio = indicators.Formula(sma20, sma50, (a, b) => b != 0 ? a / b : 0);
      })
      .Build();
");

        var data = BuildSampleData(200, 100d, DateTime.UtcNow.AddDays(-200));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        SeriesHandle spread = default, ratio = default;

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(indicators =>
            {
                var sma20 = indicators.Sma(20);
                var sma50 = indicators.Sma(50);
                spread = indicators.Formula(sma20, sma50, FormulaOp.Subtract);
                ratio = indicators.Formula(sma20, sma50, (a, b) => b != 0 ? a / b : 0);
            })
            .Build();

        runtime.Start();
        var snapshot = runtime.Latest!;

        Console.WriteLine("Output:");
        Console.WriteLine($"  SMA(20) - SMA(50) = {FormatValue(snapshot.GetLastValue(spread))}");
        Console.WriteLine($"  SMA(20) / SMA(50) = {FormatValue(snapshot.GetLastValue(ratio))}");
    }

    private static void DemoFullExample()
    {
        Console.WriteLine();
        Console.WriteLine("--- Full Example: Complete Setup ---");
        Console.WriteLine();

        var data = BuildSampleData(200, 100d, DateTime.UtcNow.AddDays(-200));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        SeriesHandle sma = default, rsi = default;
        BollingerBandsResult bands = default;
        SignalHandle buySignal = default, sellSignal = default;

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(indicators =>
            {
                sma = indicators.Sma(20);
                rsi = indicators.Rsi(14);
                bands = indicators.BollingerBands(20, 2);
            })
            .ConfigureSignals(signals =>
            {
                // Buy when RSI crosses above 30 (oversold recovery)
                buySignal = signals.When(rsi).CrossesAbove(30).Emit("BuySignal");

                // Sell when RSI crosses below 70 (overbought reversal)
                sellSignal = signals.When(rsi).CrossesBelow(70).Emit("SellSignal");
            })
            .ConfigureNotifications(notifications =>
            {
                // Console notifications fire automatically when signals trigger
                notifications.Console();
            })
            .Build();

        runtime.Start();
        var snapshot = runtime.Latest!;

        Console.WriteLine("Indicator Values:");
        Console.WriteLine($"  SMA(20)         = {FormatValue(snapshot.GetLastValue(sma))}");
        Console.WriteLine($"  RSI(14)         = {FormatValue(snapshot.GetLastValue(rsi))}");
        Console.WriteLine($"  BB Upper        = {FormatValue(snapshot.GetLastValue(bands.Upper))}");
        Console.WriteLine($"  BB Middle       = {FormatValue(snapshot.GetLastValue(bands.Middle))}");
        Console.WriteLine($"  BB Lower        = {FormatValue(snapshot.GetLastValue(bands.Lower))}");
        Console.WriteLine();
        Console.WriteLine("Signal Handles Registered:");
        Console.WriteLine($"  Buy Signal ID:  {buySignal.Id}");
        Console.WriteLine($"  Sell Signal ID: {sellSignal.Id}");
    }

    #endregion

    #region Indicator Demos

    private static void RunIndicatorDemos()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("======== INDICATOR CONFIGURATION ========");
            Console.WriteLine();
            Console.WriteLine("  Moving Averages:");
            Console.WriteLine("  1) SMA - Simple Moving Average");
            Console.WriteLine("  2) EMA - Exponential Moving Average");
            Console.WriteLine("  3) WMA/HMA/TEMA/DEMA");
            Console.WriteLine();
            Console.WriteLine("  Oscillators:");
            Console.WriteLine("  4) RSI - Relative Strength Index");
            Console.WriteLine("  5) MACD - Moving Average Convergence Divergence");
            Console.WriteLine("  6) Stochastic Oscillator");
            Console.WriteLine();
            Console.WriteLine("  Volatility:");
            Console.WriteLine("  7) Bollinger Bands");
            Console.WriteLine("  8) ATR - Average True Range");
            Console.WriteLine("  9) Keltner/Donchian Channels");
            Console.WriteLine();
            Console.WriteLine("  B) Back to Main Menu");
            Console.WriteLine();
            Console.Write("Select option: ");

            var input = Console.ReadLine()?.Trim().ToUpperInvariant();
            switch (input)
            {
                case "1":
                    DemoSma();
                    break;
                case "2":
                    DemoEma();
                    break;
                case "3":
                    DemoAdvancedMa();
                    break;
                case "4":
                    DemoRsi();
                    break;
                case "5":
                    DemoMacd();
                    break;
                case "6":
                    DemoStochastic();
                    break;
                case "7":
                    DemoBollingerBands();
                    break;
                case "8":
                    DemoAtr();
                    break;
                case "9":
                    DemoChannels();
                    break;
                case "B":
                    return;
                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }
            Pause();
        }
    }

    private static void DemoSma()
    {
        Console.WriteLine();
        Console.WriteLine("--- SMA - Simple Moving Average ---");

        var data = BuildSampleData(100, 100d, DateTime.UtcNow.AddDays(-100));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        SeriesHandle sma10 = default, sma20 = default, sma50 = default;

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(ind =>
            {
                sma10 = ind.Sma(10);
                sma20 = ind.Sma(20);
                sma50 = ind.Sma(50);
            })
            .Build();

        runtime.Start();
        var snapshot = runtime.Latest!;

        Console.WriteLine($"  SMA(10) = {FormatValue(snapshot.GetLastValue(sma10))}");
        Console.WriteLine($"  SMA(20) = {FormatValue(snapshot.GetLastValue(sma20))}");
        Console.WriteLine($"  SMA(50) = {FormatValue(snapshot.GetLastValue(sma50))}");
    }

    private static void DemoEma()
    {
        Console.WriteLine();
        Console.WriteLine("--- EMA - Exponential Moving Average ---");

        var data = BuildSampleData(100, 100d, DateTime.UtcNow.AddDays(-100));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        SeriesHandle ema12 = default, ema26 = default;

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(ind =>
            {
                ema12 = ind.Ema(12);
                ema26 = ind.Ema(26);
            })
            .Build();

        runtime.Start();
        var snapshot = runtime.Latest!;

        Console.WriteLine($"  EMA(12) = {FormatValue(snapshot.GetLastValue(ema12))}");
        Console.WriteLine($"  EMA(26) = {FormatValue(snapshot.GetLastValue(ema26))}");
    }

    private static void DemoAdvancedMa()
    {
        Console.WriteLine();
        Console.WriteLine("--- Advanced Moving Averages ---");

        var data = BuildSampleData(100, 100d, DateTime.UtcNow.AddDays(-100));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        SeriesHandle wma = default, hma = default, tema = default, dema = default;

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(ind =>
            {
                wma = ind.Wma(20);
                hma = ind.Hma(20);
                tema = ind.Tema(20);
                dema = ind.Dema(20);
            })
            .Build();

        runtime.Start();
        var snapshot = runtime.Latest!;

        Console.WriteLine($"  WMA(20)  = {FormatValue(snapshot.GetLastValue(wma))}");
        Console.WriteLine($"  HMA(20)  = {FormatValue(snapshot.GetLastValue(hma))}");
        Console.WriteLine($"  TEMA(20) = {FormatValue(snapshot.GetLastValue(tema))}");
        Console.WriteLine($"  DEMA(20) = {FormatValue(snapshot.GetLastValue(dema))}");
    }

    private static void DemoRsi()
    {
        Console.WriteLine();
        Console.WriteLine("--- RSI - Relative Strength Index ---");

        var data = BuildSampleData(100, 100d, DateTime.UtcNow.AddDays(-100));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        SeriesHandle rsi = default;

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(ind => rsi = ind.Rsi(14))
            .Build();

        runtime.Start();
        var snapshot = runtime.Latest!;
        var value = snapshot.GetLastValue(rsi);

        Console.WriteLine($"  RSI(14) = {FormatValue(value)}");
        Console.WriteLine();
        if (value < 30) Console.WriteLine("  Status: OVERSOLD (potential buy)");
        else if (value > 70) Console.WriteLine("  Status: OVERBOUGHT (potential sell)");
        else Console.WriteLine("  Status: NEUTRAL");
    }

    private static void DemoMacd()
    {
        Console.WriteLine();
        Console.WriteLine("--- MACD - Moving Average Convergence Divergence ---");

        var data = BuildSampleData(100, 100d, DateTime.UtcNow.AddDays(-100));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        MacdResult macd = default;

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(ind => macd = ind.Macd(12, 26, 9))
            .Build();

        runtime.Start();
        var snapshot = runtime.Latest!;

        Console.WriteLine($"  MACD Line   = {FormatValue(snapshot.GetLastValue(macd.Primary))}");
        Console.WriteLine($"  Signal Line = {FormatValue(snapshot.GetLastValue(macd.Signal))}");
        Console.WriteLine($"  Histogram   = {FormatValue(snapshot.GetLastValue(macd.Histogram))}");
    }

    private static void DemoStochastic()
    {
        Console.WriteLine();
        Console.WriteLine("--- Stochastic Oscillator ---");

        var data = BuildSampleData(100, 100d, DateTime.UtcNow.AddDays(-100));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        StochasticResult stoch = default;

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(ind => stoch = ind.Stochastic(14, 3))
            .Build();

        runtime.Start();
        var snapshot = runtime.Latest!;

        Console.WriteLine($"  %K = {FormatValue(snapshot.GetLastValue(stoch.K))}");
        Console.WriteLine($"  %D = {FormatValue(snapshot.GetLastValue(stoch.D))}");
    }

    private static void DemoBollingerBands()
    {
        Console.WriteLine();
        Console.WriteLine("--- Bollinger Bands ---");

        var data = BuildSampleData(100, 100d, DateTime.UtcNow.AddDays(-100));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        BollingerBandsResult bands = default;

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(ind => bands = ind.BollingerBands(20, 2))
            .Build();

        runtime.Start();
        var snapshot = runtime.Latest!;
        var lastPrice = data[data.Count - 1].Close;

        Console.WriteLine($"  Upper Band  = {FormatValue(snapshot.GetLastValue(bands.Upper))}");
        Console.WriteLine($"  Middle Band = {FormatValue(snapshot.GetLastValue(bands.Middle))}");
        Console.WriteLine($"  Lower Band  = {FormatValue(snapshot.GetLastValue(bands.Lower))}");
        Console.WriteLine($"  Last Price  = {FormatValue((double)lastPrice)}");
    }

    private static void DemoAtr()
    {
        Console.WriteLine();
        Console.WriteLine("--- ATR - Average True Range ---");

        var data = BuildSampleData(100, 100d, DateTime.UtcNow.AddDays(-100));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        SeriesHandle atr = default;

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(ind => atr = ind.Atr(14))
            .Build();

        runtime.Start();
        var snapshot = runtime.Latest!;

        Console.WriteLine($"  ATR(14) = {FormatValue(snapshot.GetLastValue(atr))}");
    }

    private static void DemoChannels()
    {
        Console.WriteLine();
        Console.WriteLine("--- Keltner & Donchian Channels ---");

        var data = BuildSampleData(100, 100d, DateTime.UtcNow.AddDays(-100));
        var source = IndicatorDataSource.FromBatch(new StockData(data));

        KeltnerChannelResult keltner = default;
        DonchianChannelResult donchian = default;

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(ind =>
            {
                keltner = ind.KeltnerChannels(20, 2);
                donchian = ind.DonchianChannels(20);
            })
            .Build();

        runtime.Start();
        var snapshot = runtime.Latest!;

        Console.WriteLine("  Keltner Channels (20, 2x ATR):");
        Console.WriteLine($"    Upper  = {FormatValue(snapshot.GetLastValue(keltner.Upper))}");
        Console.WriteLine($"    Middle = {FormatValue(snapshot.GetLastValue(keltner.Middle))}");
        Console.WriteLine($"    Lower  = {FormatValue(snapshot.GetLastValue(keltner.Lower))}");
        Console.WriteLine();
        Console.WriteLine("  Donchian Channels (20):");
        Console.WriteLine($"    Upper  = {FormatValue(snapshot.GetLastValue(donchian.Upper))}");
        Console.WriteLine($"    Middle = {FormatValue(snapshot.GetLastValue(donchian.Middle))}");
        Console.WriteLine($"    Lower  = {FormatValue(snapshot.GetLastValue(donchian.Lower))}");
    }

    #endregion

    #region Signal Demos

    private static void RunSignalDemos()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("======== SIGNAL GENERATION ========");
            Console.WriteLine();
            Console.WriteLine("  1) Crossover Signals (SMA Cross)");
            Console.WriteLine("  2) Threshold Signals (RSI Overbought/Oversold)");
            Console.WriteLine("  3) Range Signals (Between/Outside)");
            Console.WriteLine("  4) Combined Signals (Multiple Conditions)");
            Console.WriteLine();
            Console.WriteLine("  B) Back to Main Menu");
            Console.WriteLine();
            Console.Write("Select option: ");

            var input = Console.ReadLine()?.Trim().ToUpperInvariant();
            switch (input)
            {
                case "1":
                    DemoCrossoverSignals();
                    break;
                case "2":
                    DemoThresholdSignals();
                    break;
                case "3":
                    DemoRangeSignals();
                    break;
                case "4":
                    DemoCombinedSignals();
                    break;
                case "B":
                    return;
                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }
            Pause();
        }
    }

    private static void DemoCrossoverSignals()
    {
        Console.WriteLine();
        Console.WriteLine("--- Crossover Signals: SMA Golden/Death Cross ---");
        Console.WriteLine();
        Console.WriteLine("Code:");
        Console.WriteLine(@"
  .ConfigureSignals(signals =>
  {
      // Golden Cross: SMA(20) crosses above SMA(50)
      goldenCross = signals.When(sma20).CrossesAbove(sma50).Emit(""GoldenCross"");

      // Death Cross: SMA(20) crosses below SMA(50)
      deathCross = signals.When(sma20).CrossesBelow(sma50).Emit(""DeathCross"");
  })
");
        Console.WriteLine("Signal triggers are captured during Calculate() and can be");
        Console.WriteLine("used to trigger notifications or trading actions.");
    }

    private static void DemoThresholdSignals()
    {
        Console.WriteLine();
        Console.WriteLine("--- Threshold Signals: RSI Levels ---");
        Console.WriteLine();
        Console.WriteLine("Code:");
        Console.WriteLine(@"
  .ConfigureSignals(signals =>
  {
      // Oversold: RSI crosses above 30
      oversoldRecovery = signals.When(rsi).CrossesAbove(30).Emit(""OversoldRecovery"");

      // Overbought: RSI crosses below 70
      overboughtReversal = signals.When(rsi).CrossesBelow(70).Emit(""OverboughtReversal"");
  })
");
    }

    private static void DemoRangeSignals()
    {
        Console.WriteLine();
        Console.WriteLine("--- Range Signals: Between/Outside ---");
        Console.WriteLine();
        Console.WriteLine("Code:");
        Console.WriteLine(@"
  .ConfigureSignals(signals =>
  {
      // Signal when RSI enters neutral zone
      neutralZone = signals.When(rsi).Between(40, 60).Emit(""NeutralZone"");

      // Signal when price breaks outside Bollinger Bands
      bandBreakout = signals.When(price).Outside(bands.Lower, bands.Upper).Emit(""BandBreakout"");
  })
");
    }

    private static void DemoCombinedSignals()
    {
        Console.WriteLine();
        Console.WriteLine("--- Combined Signals: Multiple Conditions ---");
        Console.WriteLine();
        Console.WriteLine("You can combine multiple indicators for complex signals:");
        Console.WriteLine();
        Console.WriteLine("  - RSI oversold + Price below lower BB = Strong buy signal");
        Console.WriteLine("  - MACD crossover + Volume spike = Momentum confirmation");
        Console.WriteLine("  - Golden cross + RSI rising = Trend confirmation");
    }

    #endregion

    #region Streaming Demos

    private static void RunStreamingDemos()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("======== STREAMING ENGINE ========");
            Console.WriteLine();
            Console.WriteLine("  1) Basic Streaming (Tick-by-Tick)");
            Console.WriteLine("  2) Multi-Timeframe Streaming");
            Console.WriteLine("  3) Replay Source (Historical Simulation)");
            Console.WriteLine();
            Console.WriteLine("  B) Back to Main Menu");
            Console.WriteLine();
            Console.Write("Select option: ");

            var input = Console.ReadLine()?.Trim().ToUpperInvariant();
            switch (input)
            {
                case "1":
                    DemoBasicStreaming();
                    break;
                case "2":
                    DemoMultiTimeframeStreaming();
                    break;
                case "3":
                    DemoReplaySource();
                    break;
                case "B":
                    return;
                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }
            Pause();
        }
    }

    private static void DemoBasicStreaming()
    {
        Console.WriteLine();
        Console.WriteLine("--- Basic Streaming: Tick-by-Tick SMA ---");

        var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions { EmitUpdates = false });
        var updates = new List<StreamingIndicatorStateUpdate>();

        engine.RegisterStatefulIndicator(
            "AAPL",
            BarTimeframe.Tick,
            new SimpleMovingAverageState(5),
            update => updates.Add(update),
            new IndicatorSubscriptionOptions { IncludeUpdates = false });

        var start = DateTime.UtcNow;
        var price = 100d;
        for (var i = 0; i < 10; i++)
        {
            price += (i % 2 == 0) ? 0.5 : -0.2;
            engine.OnTrade(new StreamTrade("AAPL", start.AddSeconds(i), price, 100));
        }

        Console.WriteLine($"  Trades processed: 10");
        Console.WriteLine($"  Updates received: {updates.Count}");
        if (updates.Count > 0)
        {
            Console.WriteLine($"  Last SMA(5) = {FormatValue(updates[^1].Value)}");
        }
    }

    private static void DemoMultiTimeframeStreaming()
    {
        Console.WriteLine();
        Console.WriteLine("--- Multi-Timeframe Streaming ---");
        Console.WriteLine();
        Console.WriteLine("The streaming engine supports registering the same indicator");
        Console.WriteLine("across multiple timeframes (tick, 1-min, 5-min, etc.).");
        Console.WriteLine();
        Console.WriteLine("Each timeframe aggregates data independently and emits");
        Console.WriteLine("indicator updates when bars complete.");
    }

    private static void DemoReplaySource()
    {
        Console.WriteLine();
        Console.WriteLine("--- Replay Source: Historical Simulation ---");
        Console.WriteLine();
        Console.WriteLine("Use ReplayStreamSource to simulate streaming from historical data.");
        Console.WriteLine("This is useful for:");
        Console.WriteLine("  - Backtesting streaming strategies");
        Console.WriteLine("  - Testing indicator behavior in real-time scenarios");
        Console.WriteLine("  - Debugging signal generation timing");
    }

    #endregion

    #region Notification Demos

    private static void RunNotificationDemos()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("======== NOTIFICATIONS ========");
            Console.WriteLine();
            Console.WriteLine("  1) Console Notifications");
            Console.WriteLine("  2) Email Notifications (SMTP)");
            Console.WriteLine("  3) Telegram Notifications");
            Console.WriteLine("  4) SMS Notifications (Twilio)");
            Console.WriteLine("  5) Webhook Notifications");
            Console.WriteLine();
            Console.WriteLine("  B) Back to Main Menu");
            Console.WriteLine();
            Console.Write("Select option: ");

            var input = Console.ReadLine()?.Trim().ToUpperInvariant();
            switch (input)
            {
                case "1":
                    DemoConsoleNotifications();
                    break;
                case "2":
                    DemoEmailNotifications();
                    break;
                case "3":
                    DemoTelegramNotifications();
                    break;
                case "4":
                    DemoSmsNotifications();
                    break;
                case "5":
                    DemoWebhookNotifications();
                    break;
                case "B":
                    return;
                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }
            Pause();
        }
    }

    private static void DemoConsoleNotifications()
    {
        Console.WriteLine();
        Console.WriteLine("--- Console Notifications ---");
        Console.WriteLine();
        Console.WriteLine("Code:");
        Console.WriteLine(@"
  .ConfigureNotifications(notifications =>
  {
      // Add console notification channel
      // Notifications fire automatically when signals trigger
      notifications.Console();
  })
");
        Console.WriteLine("When a signal fires, all registered notification channels");
        Console.WriteLine("receive the notification automatically.");
    }

    private static void DemoEmailNotifications()
    {
        Console.WriteLine();
        Console.WriteLine("--- Email Notifications (SMTP) ---");
        Console.WriteLine();
        Console.WriteLine("Code:");
        Console.WriteLine(@"
  .ConfigureNotifications(notifications =>
  {
      notifications.Email(new EmailOptions
      {
          SmtpHost = ""smtp.example.com"",
          SmtpPort = 587,
          From = ""alerts@example.com"",
          To = ""trader@example.com""
      });
  })
");
        Console.WriteLine("Email notifications will be sent whenever any signal fires.");
    }

    private static void DemoTelegramNotifications()
    {
        Console.WriteLine();
        Console.WriteLine("--- Telegram Notifications ---");
        Console.WriteLine();
        Console.WriteLine("Code:");
        Console.WriteLine(@"
  .ConfigureNotifications(notifications =>
  {
      notifications.Telegram(new TelegramOptions
      {
          BotToken = ""YOUR_BOT_TOKEN"",
          ChatId = ""YOUR_CHAT_ID""
      });
  })
");
        Console.WriteLine("Telegram messages will be sent whenever any signal fires.");
    }

    private static void DemoSmsNotifications()
    {
        Console.WriteLine();
        Console.WriteLine("--- SMS Notifications (Twilio) ---");
        Console.WriteLine();
        Console.WriteLine("Code:");
        Console.WriteLine(@"
  .ConfigureNotifications(notifications =>
  {
      notifications.Sms(new SmsOptions
      {
          AccountSid = ""YOUR_TWILIO_SID"",
          AuthToken = ""YOUR_TWILIO_TOKEN"",
          FromNumber = ""+1234567890"",
          ToNumber = ""+0987654321""
      });
  })
");
        Console.WriteLine("SMS messages will be sent whenever any signal fires.");
    }

    private static void DemoWebhookNotifications()
    {
        Console.WriteLine();
        Console.WriteLine("--- Webhook Notifications ---");
        Console.WriteLine();
        Console.WriteLine("Code:");
        Console.WriteLine(@"
  .ConfigureNotifications(notifications =>
  {
      notifications.Webhook(new WebhookOptions
      {
          Url = ""https://api.example.com/webhook"",
          Headers = new Dictionary<string, string>
          {
              [""Authorization""] = ""Bearer YOUR_TOKEN""
          }
      });
  })
");
        Console.WriteLine("Webhook calls will be made whenever any signal fires.");
    }

    #endregion

    #region Backtesting Demos

    private static void RunBacktestingDemos()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("======== BACKTESTING ========");
            Console.WriteLine();
            Console.WriteLine("  1) Basic Backtest Setup");
            Console.WriteLine("  2) Performance Metrics");
            Console.WriteLine("  3) Trade History");
            Console.WriteLine();
            Console.WriteLine("  B) Back to Main Menu");
            Console.WriteLine();
            Console.Write("Select option: ");

            var input = Console.ReadLine()?.Trim().ToUpperInvariant();
            switch (input)
            {
                case "1":
                    DemoBasicBacktest();
                    break;
                case "2":
                    DemoPerformanceMetrics();
                    break;
                case "3":
                    DemoTradeHistory();
                    break;
                case "B":
                    return;
                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }
            Pause();
        }
    }

    private static void DemoBasicBacktest()
    {
        Console.WriteLine();
        Console.WriteLine("--- Basic Backtest Setup ---");
        Console.WriteLine();
        Console.WriteLine("Code:");
        Console.WriteLine(@"
  var runtime = new StockIndicatorBuilder(source)
      .ConfigureIndicators(indicators => { ... })
      .ConfigureSignals(signals => { ... })
      .ConfigureBacktesting(new BacktestOptions
      {
          InitialCapital = 100000m,
          PositionSizing = PositionSizingMethod.FixedPercent,
          PositionPercent = 0.1m,  // 10% per trade
          Commission = 0.001m,      // 0.1% commission
          Slippage = 0.0005m        // 0.05% slippage
      })
      .ConfigureAutoTrading(trading =>
      {
          trading.OnSignal(buySignal, order => order.Buy(1));
          trading.OnSignal(sellSignal, order => order.Sell(1));
      })
      .Build();
");
    }

    private static void DemoPerformanceMetrics()
    {
        Console.WriteLine();
        Console.WriteLine("--- Performance Metrics ---");
        Console.WriteLine();
        Console.WriteLine("After running a backtest, you can analyze:");
        Console.WriteLine();
        Console.WriteLine("  - Total Return (%)");
        Console.WriteLine("  - Sharpe Ratio");
        Console.WriteLine("  - Maximum Drawdown");
        Console.WriteLine("  - Win Rate (%)");
        Console.WriteLine("  - Profit Factor");
        Console.WriteLine("  - Average Trade Duration");
        Console.WriteLine("  - Number of Trades");
    }

    private static void DemoTradeHistory()
    {
        Console.WriteLine();
        Console.WriteLine("--- Trade History ---");
        Console.WriteLine();
        Console.WriteLine("The backtest engine records all trades with:");
        Console.WriteLine();
        Console.WriteLine("  - Entry/Exit timestamps");
        Console.WriteLine("  - Entry/Exit prices");
        Console.WriteLine("  - Position size");
        Console.WriteLine("  - P&L per trade");
        Console.WriteLine("  - Signal that triggered the trade");
    }

    #endregion

    #region Additional Examples

    private static void RunLegacyExamples()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("======== ADDITIONAL EXAMPLES ========");
            Console.WriteLine();
            Console.WriteLine("  1) Batch Calculations (v2.0 Builder API)");
            Console.WriteLine("  2) Streaming Concepts (v2.0 Builder API)");
            Console.WriteLine("  3) Facade Design Iterations (V1-V6)");
            Console.WriteLine();
            Console.WriteLine("  B) Back to Main Menu");
            Console.WriteLine();
            Console.Write("Select option: ");

            var input = Console.ReadLine()?.Trim().ToUpperInvariant();
            switch (input)
            {
                case "1":
                    RunLegacyBatchExample();
                    break;
                case "2":
                    RunLegacyStreamingExample();
                    break;
                case "3":
                    RunFacadeSketchesMenu();
                    break;
                case "B":
                    return;
                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }
            Pause();
        }
    }

    private static void RunLegacyBatchExample()
    {
        Console.WriteLine();
        Console.WriteLine("--- v2.0 Batch Calculations (Builder API) ---");
        Console.WriteLine();
        Console.WriteLine("This example shows the v2.0 Builder API which replaces");
        Console.WriteLine("the deprecated Calculate* extension methods.");
        Console.WriteLine();

        var data = BuildSampleData(200, 100d, DateTime.UtcNow.AddDays(-200));
        var stockData = new StockData(data);
        var source = IndicatorDataSource.FromBatch(stockData);

        SeriesHandle sma = default, rsi = default;
        MacdResult macd = default;

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(indicators =>
            {
                sma = indicators.Sma(20);
                rsi = indicators.Rsi(14);
                macd = indicators.Macd();
            })
            .Build();

        runtime.Start();
        var snapshot = runtime.Latest!;

        Console.WriteLine($"  SMA(20) last = {FormatValue(snapshot.GetLastValue(sma))}");
        Console.WriteLine($"  RSI(14) last = {FormatValue(snapshot.GetLastValue(rsi))}");
        Console.WriteLine($"  MACD last    = {FormatValue(snapshot.GetLastValue(macd.Primary))}");
    }

    private static void RunLegacyStreamingExample()
    {
        Console.WriteLine();
        Console.WriteLine("--- v2.0 Streaming Engine (Builder API) ---");
        Console.WriteLine();
        Console.WriteLine("The v2.0 Builder API provides unified batch and streaming support.");
        Console.WriteLine("Use IndicatorDataSource.FromStreaming() for real-time data processing.");
        Console.WriteLine();

        // Demonstrate batch mode with Builder API (streaming requires real-time data source)
        var data = BuildSampleData(12, 100d, DateTime.UtcNow.AddSeconds(-12));
        var stockData = new StockData(data);
        var source = IndicatorDataSource.FromBatch(stockData);

        SeriesHandle sma = default;

        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(indicators =>
            {
                sma = indicators.Sma(5);
            })
            .Build();

        runtime.Start();
        var snapshot = runtime.Latest!;

        Console.WriteLine($"  Data points processed = {data.Count}");
        Console.WriteLine($"  SMA(5) last = {FormatValue(snapshot.GetLastValue(sma))}");
        Console.WriteLine();
        Console.WriteLine("For true streaming, use:");
        Console.WriteLine("  var source = IndicatorDataSource.FromStreaming(streamOptions);");
        Console.WriteLine("  runtime.Updated += (s, e) => { /* handle update */ };");
    }

    private static void RunFacadeSketchesMenu()
    {
        Console.WriteLine();
        Console.WriteLine("--- Facade Sketches (Development Iterations) ---");
        Console.WriteLine();
        Console.WriteLine("  1) V1 - Initial facade concept");
        Console.WriteLine("  2) V2 - Subscription patterns");
        Console.WriteLine("  3) V3 - Signal configuration");
        Console.WriteLine("  4) V4 - Notification integration");
        Console.WriteLine("  5) V5 - Backtesting support");
        Console.WriteLine("  6) V6 - Final unified API");
        Console.WriteLine("  A) Run all");
        Console.WriteLine();
        Console.Write("Select: ");

        var input = Console.ReadLine()?.Trim().ToUpperInvariant();
        switch (input)
        {
            case "1": FacadeSketches.Run(); break;
            case "2": FacadeSketchesV2.Run(); break;
            case "3": FacadeSketchesV3.Run(); break;
            case "4": FacadeSketchesV4.Run(); break;
            case "5": FacadeSketchesV5.Run(); break;
            case "6": FacadeSketchesV6.Run(); break;
            case "A":
                FacadeSketches.Run();
                FacadeSketchesV2.Run();
                FacadeSketchesV3.Run();
                FacadeSketchesV4.Run();
                FacadeSketchesV5.Run();
                FacadeSketchesV6.Run();
                break;
        }
    }

    #endregion

    #region Command Line Support

    private static bool TryRunFromArgs(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            return false;
        }

        if (string.Equals(args[0], "--fxmacrodata", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 5)
            {
                PrintUsage();
                throw new ArgumentException("--fxmacrodata requires BASE QUOTE START END.");
            }
            RunFxMacroDataDemo(args[1], args[2], args[3], args[4]);
            return true;
        }

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) || arg == "-h")
            {
                PrintUsage();
                return true;
            }
            if (string.Equals(arg, "--run-all", StringComparison.OrdinalIgnoreCase))
            {
                RunAllDemos();
                return true;
            }
        }

        return false;
    }

    private static void PrintUsage()
    {
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  --help, -h      Show this help");
        Console.WriteLine("  --run-all       Run all demos non-interactively");
        Console.WriteLine("  --fxmacrodata BASE QUOTE START END");
        Console.WriteLine("                   Calculate indicators from FXMacroData daily FX rows");
        Console.WriteLine();
        Console.WriteLine("Or run without arguments for interactive mode.");
    }

    private static void RunAllDemos()
    {
        DemoBasicUsage();
        DemoChainedIndicators();
        DemoCustomFormulas();
        DemoFullExample();
        Console.WriteLine();
        Console.WriteLine("All demos completed.");
    }

    private static void RunFxMacroDataDemo(string baseCurrency, string quoteCurrency, string start, string end)
    {
        if (!DateOnly.TryParseExact(start, "yyyy-MM-dd", Invariant, DateTimeStyles.None, out var startDate)
            || !DateOnly.TryParseExact(end, "yyyy-MM-dd", Invariant, DateTimeStyles.None, out var endDate))
        {
            throw new ArgumentException("START and END must use YYYY-MM-DD format.");
        }

        // ?? only falls through on null, so a set-but-empty FXMACRODATA_API_KEY would win and the
        // check below would then reject the request even though FXMD_API_KEY was configured.
        var apiKey = Environment.GetEnvironmentVariable("FXMACRODATA_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = Environment.GetEnvironmentVariable("FXMD_API_KEY");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "FX history requires FXMACRODATA_API_KEY or FXMD_API_KEY in the process environment.");
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var client = new FXMacroDataClient(httpClient, apiKey);
        var rows = client
            .GetDailyFxAsync(baseCurrency, quoteCurrency, startDate, endDate)
            .GetAwaiter()
            .GetResult();
        if (rows.Count < 20)
        {
            throw new InvalidDataException("At least 20 daily rows are required for the SMA(20) example.");
        }

        var source = IndicatorDataSource.FromBatch(new StockData(rows.ToList()));
        SeriesHandle sma = default;
        var runtime = new StockIndicatorBuilder(source)
            .ConfigureIndicators(indicators => sma = indicators.Sma(20))
            .Build();
        runtime.Start();
        var latestSma = runtime.Latest!.GetLastValue(sma);
        Console.WriteLine();
        Console.WriteLine($"FXMacroData {baseCurrency.ToUpperInvariant()}/{quoteCurrency.ToUpperInvariant()}");
        Console.WriteLine($"Rows: {rows.Count} ({rows[0].Date:yyyy-MM-dd} to {rows[^1].Date:yyyy-MM-dd})");
        Console.WriteLine($"Latest close: {rows[^1].Close.ToString("F6", Invariant)}");
        Console.WriteLine($"Latest SMA(20): {latestSma.ToString("F6", Invariant)}");
    }

    #endregion

    #region Helpers

    private static List<TickerData> BuildSampleData(int count, double startPrice, DateTime start)
    {
        var data = new List<TickerData>(count);
        var random = new Random(42);
        var price = startPrice;
        var timestamp = start;

        for (var i = 0; i < count; i++)
        {
            var change = (random.NextDouble() - 0.5d) * 2d;
            var open = price;
            var close = Math.Max(1d, price + change);
            var high = Math.Max(open, close) + random.NextDouble();
            var low = Math.Max(0.1d, Math.Min(open, close) - random.NextDouble());
            var volume = 1000d + (random.NextDouble() * 100d);

            data.Add(new TickerData
            {
                Date = timestamp,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume
            });

            price = close;
            timestamp = timestamp.AddDays(1);
        }

        return data;
    }

    private static string FormatValue(double value)
    {
        return double.IsNaN(value) ? "NaN" : value.ToString("F4", Invariant);
    }

    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("Press ENTER to continue...");
        Console.ReadLine();
    }

    #endregion
}
