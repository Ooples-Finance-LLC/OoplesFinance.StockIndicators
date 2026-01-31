namespace OoplesFinance.StockIndicators.Builder.VisualBuilder;

using OoplesFinance.StockIndicators.Builder.Extensions;
using OoplesFinance.StockIndicators.Builder.VisualBuilder.NodeTypes;

/// <summary>
/// Library of available indicators for the visual strategy builder.
/// Integrates with the existing indicator catalog.
/// </summary>
public sealed class IndicatorLibrary
{
    private readonly Dictionary<string, IndicatorDefinition> _indicators = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _categories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes the indicator library with all available indicators.
    /// </summary>
    public IndicatorLibrary()
    {
        RegisterBuiltInIndicators();
    }

    /// <summary>
    /// Gets all indicator definitions.
    /// </summary>
    public IReadOnlyDictionary<string, IndicatorDefinition> Indicators => _indicators;

    /// <summary>
    /// Gets all categories.
    /// </summary>
    public IReadOnlyDictionary<string, List<string>> Categories => _categories;

    /// <summary>
    /// Gets an indicator definition by name.
    /// </summary>
    public IndicatorDefinition? GetIndicator(string name)
    {
        return _indicators.GetValueOrDefault(name);
    }

    /// <summary>
    /// Gets all indicators in a category.
    /// </summary>
    public IEnumerable<IndicatorDefinition> GetByCategory(string category)
    {
        if (!_categories.TryGetValue(category, out var names)) yield break;

        foreach (var name in names)
        {
            if (_indicators.TryGetValue(name, out var indicator))
            {
                yield return indicator;
            }
        }
    }

    /// <summary>
    /// Searches indicators by name or description.
    /// </summary>
    public IEnumerable<IndicatorDefinition> Search(string query)
    {
        var lowerQuery = query.ToLowerInvariant();
        return _indicators.Values.Where(i =>
            i.Name.ToLowerInvariant().Contains(lowerQuery) ||
            i.Description.ToLowerInvariant().Contains(lowerQuery) ||
            i.Category.ToLowerInvariant().Contains(lowerQuery));
    }

    /// <summary>
    /// Creates a node from an indicator definition.
    /// </summary>
    public GraphNode CreateNode(string indicatorName)
    {
        var definition = GetIndicator(indicatorName);
        if (definition is null)
        {
            throw new ArgumentException($"Unknown indicator: {indicatorName}", nameof(indicatorName));
        }

        return NodeFactory.CreateIndicatorNode(definition);
    }

    /// <summary>
    /// Registers a custom indicator.
    /// </summary>
    public void RegisterIndicator(IndicatorDefinition definition)
    {
        _indicators[definition.Name] = definition;

        if (!_categories.TryGetValue(definition.Category, out var list))
        {
            list = new List<string>();
            _categories[definition.Category] = list;
        }

        if (!list.Contains(definition.Name))
        {
            list.Add(definition.Name);
        }
    }

    private void RegisterBuiltInIndicators()
    {
        // ========== Trend Indicators ==========
        RegisterTrendIndicators();

        // ========== Momentum Indicators ==========
        RegisterMomentumIndicators();

        // ========== Volatility Indicators ==========
        RegisterVolatilityIndicators();

        // ========== Volume Indicators ==========
        RegisterVolumeIndicators();

        // ========== Support/Resistance Indicators ==========
        RegisterSupportResistanceIndicators();

        // ========== Custom/Advanced Indicators ==========
        RegisterAdvancedIndicators();
    }

    private void RegisterTrendIndicators()
    {
        // Simple Moving Average
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "SMA",
            Description = "Simple Moving Average - Average of closing prices over a period",
            Category = "Trend",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "source", Name = "Source", DataType = PortDataType.PriceSeries, IsRequired = true, Description = "Price data input" }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "value", Name = "SMA", DataType = PortDataType.IndicatorSeries, Description = "SMA value" }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "length", DisplayName = "Length", Type = typeof(int), DefaultValue = 14, MinValue = 1, MaxValue = 500, Description = "Lookback period" }
            }
        });

        // Exponential Moving Average
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "EMA",
            Description = "Exponential Moving Average - Weighted average giving more importance to recent prices",
            Category = "Trend",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "source", Name = "Source", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "value", Name = "EMA", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "length", DisplayName = "Length", Type = typeof(int), DefaultValue = 14, MinValue = 1, MaxValue = 500 }
            }
        });

        // MACD
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "MACD",
            Description = "Moving Average Convergence Divergence - Shows relationship between two EMAs",
            Category = "Trend",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "source", Name = "Source", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "macd", Name = "MACD Line", DataType = PortDataType.IndicatorSeries },
                new() { Id = "signal", Name = "Signal Line", DataType = PortDataType.IndicatorSeries },
                new() { Id = "histogram", Name = "Histogram", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "fastLength", DisplayName = "Fast Length", Type = typeof(int), DefaultValue = 12 },
                new() { Name = "slowLength", DisplayName = "Slow Length", Type = typeof(int), DefaultValue = 26 },
                new() { Name = "signalLength", DisplayName = "Signal Length", Type = typeof(int), DefaultValue = 9 }
            }
        });

        // ADX
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "ADX",
            Description = "Average Directional Index - Measures trend strength regardless of direction",
            Category = "Trend",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "adx", Name = "ADX", DataType = PortDataType.IndicatorSeries },
                new() { Id = "plusDi", Name = "+DI", DataType = PortDataType.IndicatorSeries },
                new() { Id = "minusDi", Name = "-DI", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "length", DisplayName = "Length", Type = typeof(int), DefaultValue = 14, MinValue = 1 }
            }
        });

        // Parabolic SAR
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "ParabolicSAR",
            Description = "Parabolic Stop and Reverse - Identifies potential reversals",
            Category = "Trend",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "sar", Name = "SAR", DataType = PortDataType.IndicatorSeries },
                new() { Id = "trend", Name = "Trend", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "start", DisplayName = "Start", Type = typeof(decimal), DefaultValue = 0.02m },
                new() { Name = "increment", DisplayName = "Increment", Type = typeof(decimal), DefaultValue = 0.02m },
                new() { Name = "maximum", DisplayName = "Maximum", Type = typeof(decimal), DefaultValue = 0.2m }
            }
        });

        // Supertrend
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "Supertrend",
            Description = "Supertrend - ATR-based trend following indicator",
            Category = "Trend",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "supertrend", Name = "Supertrend", DataType = PortDataType.IndicatorSeries },
                new() { Id = "direction", Name = "Direction", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "length", DisplayName = "ATR Length", Type = typeof(int), DefaultValue = 10 },
                new() { Name = "multiplier", DisplayName = "Multiplier", Type = typeof(decimal), DefaultValue = 3.0m }
            }
        });
    }

    private void RegisterMomentumIndicators()
    {
        // RSI
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "RSI",
            Description = "Relative Strength Index - Measures speed and magnitude of price changes",
            Category = "Momentum",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "source", Name = "Source", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "value", Name = "RSI", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "length", DisplayName = "Length", Type = typeof(int), DefaultValue = 14, MinValue = 2 }
            }
        });

        // Stochastic
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "Stochastic",
            Description = "Stochastic Oscillator - Compares closing price to price range over period",
            Category = "Momentum",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "k", Name = "%K", DataType = PortDataType.IndicatorSeries },
                new() { Id = "d", Name = "%D", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "kLength", DisplayName = "%K Length", Type = typeof(int), DefaultValue = 14 },
                new() { Name = "kSmooth", DisplayName = "%K Smoothing", Type = typeof(int), DefaultValue = 3 },
                new() { Name = "dSmooth", DisplayName = "%D Smoothing", Type = typeof(int), DefaultValue = 3 }
            }
        });

        // CCI
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "CCI",
            Description = "Commodity Channel Index - Identifies cyclical trends",
            Category = "Momentum",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "value", Name = "CCI", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "length", DisplayName = "Length", Type = typeof(int), DefaultValue = 20 }
            }
        });

        // Williams %R
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "WilliamsR",
            Description = "Williams %R - Momentum indicator showing overbought/oversold levels",
            Category = "Momentum",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "value", Name = "%R", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "length", DisplayName = "Length", Type = typeof(int), DefaultValue = 14 }
            }
        });

        // MFI
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "MFI",
            Description = "Money Flow Index - Volume-weighted RSI",
            Category = "Momentum",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "volume", Name = "Volume", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "value", Name = "MFI", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "length", DisplayName = "Length", Type = typeof(int), DefaultValue = 14 }
            }
        });
    }

    private void RegisterVolatilityIndicators()
    {
        // ATR
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "ATR",
            Description = "Average True Range - Measures market volatility",
            Category = "Volatility",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "value", Name = "ATR", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "length", DisplayName = "Length", Type = typeof(int), DefaultValue = 14 }
            }
        });

        // Bollinger Bands
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "BollingerBands",
            Description = "Bollinger Bands - Volatility bands around a moving average",
            Category = "Volatility",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "source", Name = "Source", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "upper", Name = "Upper Band", DataType = PortDataType.IndicatorSeries },
                new() { Id = "middle", Name = "Middle Band", DataType = PortDataType.IndicatorSeries },
                new() { Id = "lower", Name = "Lower Band", DataType = PortDataType.IndicatorSeries },
                new() { Id = "bandwidth", Name = "Bandwidth", DataType = PortDataType.IndicatorSeries },
                new() { Id = "percentB", Name = "%B", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "length", DisplayName = "Length", Type = typeof(int), DefaultValue = 20 },
                new() { Name = "stdDev", DisplayName = "Std Dev", Type = typeof(decimal), DefaultValue = 2.0m }
            }
        });

        // Keltner Channel
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "KeltnerChannel",
            Description = "Keltner Channel - ATR-based volatility channel",
            Category = "Volatility",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "upper", Name = "Upper", DataType = PortDataType.IndicatorSeries },
                new() { Id = "middle", Name = "Middle", DataType = PortDataType.IndicatorSeries },
                new() { Id = "lower", Name = "Lower", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "length", DisplayName = "Length", Type = typeof(int), DefaultValue = 20 },
                new() { Name = "atrLength", DisplayName = "ATR Length", Type = typeof(int), DefaultValue = 10 },
                new() { Name = "multiplier", DisplayName = "Multiplier", Type = typeof(decimal), DefaultValue = 1.5m }
            }
        });

        // Standard Deviation
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "StdDev",
            Description = "Standard Deviation - Measures price dispersion",
            Category = "Volatility",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "source", Name = "Source", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "value", Name = "StdDev", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "length", DisplayName = "Length", Type = typeof(int), DefaultValue = 20 }
            }
        });
    }

    private void RegisterVolumeIndicators()
    {
        // OBV
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "OBV",
            Description = "On Balance Volume - Cumulative volume indicator",
            Category = "Volume",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "volume", Name = "Volume", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "value", Name = "OBV", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>()
        });

        // VWAP
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "VWAP",
            Description = "Volume Weighted Average Price - Average price weighted by volume",
            Category = "Volume",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "volume", Name = "Volume", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "value", Name = "VWAP", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>()
        });

        // AD
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "AD",
            Description = "Accumulation/Distribution Line - Volume-price momentum",
            Category = "Volume",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "volume", Name = "Volume", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "value", Name = "A/D", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>()
        });

        // CMF
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "CMF",
            Description = "Chaikin Money Flow - Measures money flow volume over period",
            Category = "Volume",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "volume", Name = "Volume", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "value", Name = "CMF", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "length", DisplayName = "Length", Type = typeof(int), DefaultValue = 20 }
            }
        });
    }

    private void RegisterSupportResistanceIndicators()
    {
        // Pivot Points
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "PivotPoints",
            Description = "Pivot Points - Support and resistance levels",
            Category = "Support/Resistance",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "pivot", Name = "Pivot", DataType = PortDataType.IndicatorSeries },
                new() { Id = "r1", Name = "R1", DataType = PortDataType.IndicatorSeries },
                new() { Id = "r2", Name = "R2", DataType = PortDataType.IndicatorSeries },
                new() { Id = "r3", Name = "R3", DataType = PortDataType.IndicatorSeries },
                new() { Id = "s1", Name = "S1", DataType = PortDataType.IndicatorSeries },
                new() { Id = "s2", Name = "S2", DataType = PortDataType.IndicatorSeries },
                new() { Id = "s3", Name = "S3", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "type", DisplayName = "Type", Type = typeof(string), DefaultValue = "Standard", Description = "Standard, Fibonacci, Woodie, Camarilla" }
            }
        });

        // Donchian Channel
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "DonchianChannel",
            Description = "Donchian Channel - Highest high and lowest low over period",
            Category = "Support/Resistance",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "upper", Name = "Upper", DataType = PortDataType.IndicatorSeries },
                new() { Id = "middle", Name = "Middle", DataType = PortDataType.IndicatorSeries },
                new() { Id = "lower", Name = "Lower", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "length", DisplayName = "Length", Type = typeof(int), DefaultValue = 20 }
            }
        });
    }

    private void RegisterAdvancedIndicators()
    {
        // Ichimoku Cloud
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "Ichimoku",
            Description = "Ichimoku Cloud - Multi-component trend and momentum indicator",
            Category = "Advanced",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "tenkan", Name = "Tenkan-sen", DataType = PortDataType.IndicatorSeries },
                new() { Id = "kijun", Name = "Kijun-sen", DataType = PortDataType.IndicatorSeries },
                new() { Id = "senkouA", Name = "Senkou Span A", DataType = PortDataType.IndicatorSeries },
                new() { Id = "senkouB", Name = "Senkou Span B", DataType = PortDataType.IndicatorSeries },
                new() { Id = "chikou", Name = "Chikou Span", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "tenkanLength", DisplayName = "Tenkan Length", Type = typeof(int), DefaultValue = 9 },
                new() { Name = "kijunLength", DisplayName = "Kijun Length", Type = typeof(int), DefaultValue = 26 },
                new() { Name = "senkouBLength", DisplayName = "Senkou B Length", Type = typeof(int), DefaultValue = 52 }
            }
        });

        // Squeeze Momentum
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "SqueezeMomentum",
            Description = "Squeeze Momentum - Combines Bollinger Bands and Keltner Channel",
            Category = "Advanced",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "momentum", Name = "Momentum", DataType = PortDataType.IndicatorSeries },
                new() { Id = "squeeze", Name = "Squeeze", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "bbLength", DisplayName = "BB Length", Type = typeof(int), DefaultValue = 20 },
                new() { Name = "bbMult", DisplayName = "BB Multiplier", Type = typeof(decimal), DefaultValue = 2.0m },
                new() { Name = "kcLength", DisplayName = "KC Length", Type = typeof(int), DefaultValue = 20 },
                new() { Name = "kcMult", DisplayName = "KC Multiplier", Type = typeof(decimal), DefaultValue = 1.5m }
            }
        });

        // Elder Ray
        RegisterIndicator(new IndicatorDefinition
        {
            Name = "ElderRay",
            Description = "Elder Ray Index - Bull and Bear Power",
            Category = "Advanced",
            Inputs = new List<PortDefinition>
            {
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries, IsRequired = true },
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries, IsRequired = true }
            },
            Outputs = new List<PortDefinition>
            {
                new() { Id = "bullPower", Name = "Bull Power", DataType = PortDataType.IndicatorSeries },
                new() { Id = "bearPower", Name = "Bear Power", DataType = PortDataType.IndicatorSeries }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "length", DisplayName = "EMA Length", Type = typeof(int), DefaultValue = 13 }
            }
        });
    }
}
