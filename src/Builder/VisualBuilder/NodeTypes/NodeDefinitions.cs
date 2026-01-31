namespace OoplesFinance.StockIndicators.Builder.VisualBuilder.NodeTypes;

/// <summary>
/// Factory for creating pre-configured nodes.
/// </summary>
public static class NodeFactory
{
    /// <summary>
    /// Creates a data source node for price data.
    /// </summary>
    public static GraphNode CreatePriceDataNode()
    {
        return new GraphNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = NodeType.DataSource,
            Name = "Price Data",
            Description = "OHLCV price data for a symbol",
            OutputPorts = new List<NodePort>
            {
                new() { Id = "open", Name = "Open", DataType = PortDataType.PriceSeries },
                new() { Id = "high", Name = "High", DataType = PortDataType.PriceSeries },
                new() { Id = "low", Name = "Low", DataType = PortDataType.PriceSeries },
                new() { Id = "close", Name = "Close", DataType = PortDataType.PriceSeries },
                new() { Id = "volume", Name = "Volume", DataType = PortDataType.PriceSeries }
            },
            Configuration = new Dictionary<string, object?>
            {
                ["symbol"] = "AAPL",
                ["timeframe"] = "Daily"
            },
            Style = new NodeStyle { HeaderColor = "#27AE60", Icon = "chart-line" }
        };
    }

    /// <summary>
    /// Creates a parameter/constant node.
    /// </summary>
    public static GraphNode CreateParameterNode(string name, object? defaultValue, PortDataType dataType)
    {
        return new GraphNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = NodeType.Parameter,
            Name = name,
            Description = $"Parameter: {name}",
            OutputPorts = new List<NodePort>
            {
                new() { Id = "value", Name = "Value", DataType = dataType }
            },
            Configuration = new Dictionary<string, object?>
            {
                ["value"] = defaultValue,
                ["isOptimizable"] = true,
                ["minValue"] = null,
                ["maxValue"] = null,
                ["step"] = null
            },
            Style = new NodeStyle { HeaderColor = "#9B59B6", Icon = "sliders-h" }
        };
    }

    /// <summary>
    /// Creates an indicator node.
    /// </summary>
    public static GraphNode CreateIndicatorNode(IndicatorDefinition definition)
    {
        var node = new GraphNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = NodeType.Indicator,
            Name = definition.Name,
            Description = definition.Description,
            InputPorts = definition.Inputs.Select(i => new NodePort
            {
                Id = i.Id,
                Name = i.Name,
                DataType = i.DataType,
                IsRequired = i.IsRequired,
                DefaultValue = i.DefaultValue,
                Description = i.Description
            }).ToList(),
            OutputPorts = definition.Outputs.Select(o => new NodePort
            {
                Id = o.Id,
                Name = o.Name,
                DataType = o.DataType,
                Description = o.Description
            }).ToList(),
            Configuration = definition.Parameters.ToDictionary(p => p.Name, p => p.DefaultValue),
            Style = new NodeStyle { HeaderColor = "#3498DB", Icon = "chart-area" }
        };

        return node;
    }

    /// <summary>
    /// Creates a comparison/logic node.
    /// </summary>
    public static GraphNode CreateComparisonNode(ComparisonType comparison)
    {
        var name = comparison switch
        {
            ComparisonType.GreaterThan => "Greater Than",
            ComparisonType.LessThan => "Less Than",
            ComparisonType.Equal => "Equal",
            ComparisonType.GreaterOrEqual => "Greater or Equal",
            ComparisonType.LessOrEqual => "Less or Equal",
            ComparisonType.CrossOver => "Cross Over",
            ComparisonType.CrossUnder => "Cross Under",
            _ => "Compare"
        };

        return new GraphNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = NodeType.Logic,
            Name = name,
            Description = $"Compares two values: {name}",
            InputPorts = new List<NodePort>
            {
                new() { Id = "a", Name = "A", DataType = PortDataType.Any, IsRequired = true },
                new() { Id = "b", Name = "B", DataType = PortDataType.Any, IsRequired = true }
            },
            OutputPorts = new List<NodePort>
            {
                new() { Id = "result", Name = "Result", DataType = PortDataType.Boolean }
            },
            Configuration = new Dictionary<string, object?>
            {
                ["comparison"] = comparison.ToString()
            },
            Style = new NodeStyle { HeaderColor = "#E67E22", Icon = "balance-scale" }
        };
    }

    /// <summary>
    /// Creates a math operation node.
    /// </summary>
    public static GraphNode CreateMathNode(MathOperation operation)
    {
        var name = operation switch
        {
            MathOperation.Add => "Add",
            MathOperation.Subtract => "Subtract",
            MathOperation.Multiply => "Multiply",
            MathOperation.Divide => "Divide",
            MathOperation.Abs => "Absolute",
            MathOperation.Sqrt => "Square Root",
            MathOperation.Log => "Logarithm",
            MathOperation.Exp => "Exponential",
            MathOperation.Min => "Minimum",
            MathOperation.Max => "Maximum",
            MathOperation.Average => "Average",
            MathOperation.Percentage => "Percentage Change",
            _ => "Math"
        };

        var isBinary = operation is MathOperation.Add or MathOperation.Subtract
            or MathOperation.Multiply or MathOperation.Divide
            or MathOperation.Min or MathOperation.Max or MathOperation.Percentage;

        var inputs = new List<NodePort>
        {
            new() { Id = "a", Name = "A", DataType = PortDataType.Decimal, IsRequired = true }
        };

        if (isBinary)
        {
            inputs.Add(new NodePort { Id = "b", Name = "B", DataType = PortDataType.Decimal, IsRequired = true });
        }

        return new GraphNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = NodeType.Math,
            Name = name,
            Description = $"Math operation: {name}",
            InputPorts = inputs,
            OutputPorts = new List<NodePort>
            {
                new() { Id = "result", Name = "Result", DataType = PortDataType.Decimal }
            },
            Configuration = new Dictionary<string, object?>
            {
                ["operation"] = operation.ToString()
            },
            Style = new NodeStyle { HeaderColor = "#1ABC9C", Icon = "calculator" }
        };
    }

    /// <summary>
    /// Creates a signal node.
    /// </summary>
    public static GraphNode CreateSignalNode()
    {
        return new GraphNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = NodeType.Signal,
            Name = "Signal",
            Description = "Generates trading signal based on condition",
            InputPorts = new List<NodePort>
            {
                new() { Id = "condition", Name = "Condition", DataType = PortDataType.Boolean, IsRequired = true },
                new() { Id = "strength", Name = "Strength", DataType = PortDataType.Decimal, IsRequired = false, DefaultValue = 1.0m }
            },
            OutputPorts = new List<NodePort>
            {
                new() { Id = "signal", Name = "Signal", DataType = PortDataType.Signal }
            },
            Configuration = new Dictionary<string, object?>
            {
                ["signalType"] = "Buy",
                ["confirmationBars"] = 0
            },
            Style = new NodeStyle { HeaderColor = "#E74C3C", Icon = "flag" }
        };
    }

    /// <summary>
    /// Creates an action node.
    /// </summary>
    public static GraphNode CreateActionNode(ActionType actionType)
    {
        var name = actionType switch
        {
            ActionType.MarketBuy => "Market Buy",
            ActionType.MarketSell => "Market Sell",
            ActionType.LimitBuy => "Limit Buy",
            ActionType.LimitSell => "Limit Sell",
            ActionType.StopLoss => "Stop Loss",
            ActionType.TakeProfit => "Take Profit",
            ActionType.ClosePosition => "Close Position",
            ActionType.ScaleIn => "Scale In",
            ActionType.ScaleOut => "Scale Out",
            _ => "Trade Action"
        };

        var inputs = new List<NodePort>
        {
            new() { Id = "trigger", Name = "Trigger", DataType = PortDataType.Signal, IsRequired = true }
        };

        if (actionType is ActionType.LimitBuy or ActionType.LimitSell)
        {
            inputs.Add(new NodePort { Id = "price", Name = "Price", DataType = PortDataType.Decimal, IsRequired = true });
        }

        if (actionType is ActionType.StopLoss or ActionType.TakeProfit)
        {
            inputs.Add(new NodePort { Id = "level", Name = "Level", DataType = PortDataType.Decimal, IsRequired = true });
        }

        return new GraphNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = NodeType.Action,
            Name = name,
            Description = $"Execute trade action: {name}",
            InputPorts = inputs,
            OutputPorts = new List<NodePort>
            {
                new() { Id = "executed", Name = "Executed", DataType = PortDataType.Boolean }
            },
            Configuration = new Dictionary<string, object?>
            {
                ["actionType"] = actionType.ToString(),
                ["positionSizeType"] = "PercentOfEquity",
                ["positionSizeValue"] = 0.10m
            },
            Style = new NodeStyle { HeaderColor = "#8E44AD", Icon = "exchange-alt" }
        };
    }

    /// <summary>
    /// Creates an aggregator node for combining multiple signals.
    /// </summary>
    public static GraphNode CreateAggregatorNode(AggregationType aggregation)
    {
        var name = aggregation switch
        {
            AggregationType.All => "All (AND)",
            AggregationType.Any => "Any (OR)",
            AggregationType.Majority => "Majority Vote",
            AggregationType.WeightedAverage => "Weighted Average",
            AggregationType.Consensus => "Consensus",
            _ => "Aggregate"
        };

        return new GraphNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = NodeType.Aggregator,
            Name = name,
            Description = $"Combines multiple signals: {name}",
            InputPorts = new List<NodePort>
            {
                new() { Id = "signal1", Name = "Signal 1", DataType = PortDataType.Signal, IsRequired = true },
                new() { Id = "signal2", Name = "Signal 2", DataType = PortDataType.Signal, IsRequired = false },
                new() { Id = "signal3", Name = "Signal 3", DataType = PortDataType.Signal, IsRequired = false },
                new() { Id = "signal4", Name = "Signal 4", DataType = PortDataType.Signal, IsRequired = false }
            },
            OutputPorts = new List<NodePort>
            {
                new() { Id = "combined", Name = "Combined", DataType = PortDataType.Signal }
            },
            Configuration = new Dictionary<string, object?>
            {
                ["aggregation"] = aggregation.ToString(),
                ["weights"] = new[] { 1.0, 1.0, 1.0, 1.0 },
                ["threshold"] = 0.5
            },
            Style = new NodeStyle { HeaderColor = "#F39C12", Icon = "layer-group" }
        };
    }

    /// <summary>
    /// Creates a filter node.
    /// </summary>
    public static GraphNode CreateFilterNode(FilterType filterType)
    {
        var name = filterType switch
        {
            FilterType.TimeOfDay => "Time Filter",
            FilterType.DayOfWeek => "Day Filter",
            FilterType.VolumeThreshold => "Volume Filter",
            FilterType.VolatilityThreshold => "Volatility Filter",
            FilterType.TrendFilter => "Trend Filter",
            FilterType.Custom => "Custom Filter",
            _ => "Filter"
        };

        var inputs = new List<NodePort>
        {
            new() { Id = "input", Name = "Input", DataType = PortDataType.Signal, IsRequired = true }
        };

        if (filterType is FilterType.VolumeThreshold)
        {
            inputs.Add(new NodePort { Id = "volume", Name = "Volume", DataType = PortDataType.PriceSeries, IsRequired = true });
        }

        return new GraphNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = NodeType.Filter,
            Name = name,
            Description = $"Filters signals: {name}",
            InputPorts = inputs,
            OutputPorts = new List<NodePort>
            {
                new() { Id = "filtered", Name = "Filtered", DataType = PortDataType.Signal },
                new() { Id = "passed", Name = "Passed", DataType = PortDataType.Boolean }
            },
            Configuration = new Dictionary<string, object?>
            {
                ["filterType"] = filterType.ToString()
            },
            Style = new NodeStyle { HeaderColor = "#16A085", Icon = "filter" }
        };
    }

    /// <summary>
    /// Creates a risk control node.
    /// </summary>
    public static GraphNode CreateRiskControlNode()
    {
        return new GraphNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = NodeType.RiskControl,
            Name = "Risk Control",
            Description = "Applies risk management rules",
            InputPorts = new List<NodePort>
            {
                new() { Id = "signal", Name = "Signal", DataType = PortDataType.Signal, IsRequired = true },
                new() { Id = "equity", Name = "Equity", DataType = PortDataType.Decimal, IsRequired = false }
            },
            OutputPorts = new List<NodePort>
            {
                new() { Id = "approved", Name = "Approved", DataType = PortDataType.Signal },
                new() { Id = "blocked", Name = "Blocked", DataType = PortDataType.Boolean }
            },
            Configuration = new Dictionary<string, object?>
            {
                ["maxDailyLoss"] = 0.02m,
                ["maxDrawdown"] = 0.10m,
                ["maxPositions"] = 10,
                ["maxExposure"] = 1.0m
            },
            Style = new NodeStyle { HeaderColor = "#C0392B", Icon = "shield-alt" }
        };
    }

    /// <summary>
    /// Creates a position sizing node.
    /// </summary>
    public static GraphNode CreatePositionSizingNode(PositionSizingMethod method)
    {
        var name = method switch
        {
            PositionSizingMethod.Fixed => "Fixed Size",
            PositionSizingMethod.PercentOfEquity => "Percent of Equity",
            PositionSizingMethod.RiskBased => "Risk-Based",
            PositionSizingMethod.KellyCriterion => "Kelly Criterion",
            PositionSizingMethod.Volatility => "Volatility-Adjusted",
            _ => "Position Size"
        };

        var inputs = new List<NodePort>
        {
            new() { Id = "signal", Name = "Signal", DataType = PortDataType.Signal, IsRequired = true }
        };

        if (method is PositionSizingMethod.RiskBased or PositionSizingMethod.Volatility)
        {
            inputs.Add(new NodePort { Id = "atr", Name = "ATR", DataType = PortDataType.IndicatorSeries, IsRequired = true });
        }

        return new GraphNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = NodeType.PositionSizing,
            Name = name,
            Description = $"Calculates position size: {name}",
            InputPorts = inputs,
            OutputPorts = new List<NodePort>
            {
                new() { Id = "size", Name = "Size", DataType = PortDataType.Decimal },
                new() { Id = "signal", Name = "Sized Signal", DataType = PortDataType.Signal }
            },
            Configuration = new Dictionary<string, object?>
            {
                ["method"] = method.ToString(),
                ["value"] = 0.10m,
                ["riskPerTrade"] = 0.01m
            },
            Style = new NodeStyle { HeaderColor = "#2980B9", Icon = "balance-scale-left" }
        };
    }

    /// <summary>
    /// Creates a comment node for documentation.
    /// </summary>
    public static GraphNode CreateCommentNode(string text = "")
    {
        return new GraphNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = NodeType.Comment,
            Name = "Comment",
            Description = text,
            Style = new NodeStyle
            {
                HeaderColor = "#95A5A6",
                BodyColor = "#34495E",
                Icon = "comment",
                Width = 250,
                Height = 80
            },
            Configuration = new Dictionary<string, object?>
            {
                ["text"] = text
            }
        };
    }
}

/// <summary>
/// Comparison types for logic nodes.
/// </summary>
public enum ComparisonType
{
    GreaterThan,
    LessThan,
    Equal,
    GreaterOrEqual,
    LessOrEqual,
    NotEqual,
    CrossOver,
    CrossUnder
}

/// <summary>
/// Math operations.
/// </summary>
public enum MathOperation
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Abs,
    Sqrt,
    Log,
    Exp,
    Min,
    Max,
    Average,
    Percentage
}

/// <summary>
/// Trade action types.
/// </summary>
public enum ActionType
{
    MarketBuy,
    MarketSell,
    LimitBuy,
    LimitSell,
    StopLoss,
    TakeProfit,
    ClosePosition,
    ScaleIn,
    ScaleOut
}

/// <summary>
/// Signal aggregation types.
/// </summary>
public enum AggregationType
{
    All,
    Any,
    Majority,
    WeightedAverage,
    Consensus
}

/// <summary>
/// Filter types.
/// </summary>
public enum FilterType
{
    TimeOfDay,
    DayOfWeek,
    VolumeThreshold,
    VolatilityThreshold,
    TrendFilter,
    Custom
}

/// <summary>
/// Position sizing methods.
/// </summary>
public enum PositionSizingMethod
{
    Fixed,
    PercentOfEquity,
    RiskBased,
    KellyCriterion,
    Volatility
}

/// <summary>
/// Definition for creating indicator nodes.
/// </summary>
public sealed class IndicatorDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<PortDefinition> Inputs { get; set; } = new();
    public List<PortDefinition> Outputs { get; set; } = new();
    public List<ParameterDefinition> Parameters { get; set; } = new();
}

/// <summary>
/// Port definition for indicator nodes.
/// </summary>
public sealed class PortDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PortDataType DataType { get; set; }
    public bool IsRequired { get; set; } = true;
    public object? DefaultValue { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Parameter definition for indicator configuration.
/// </summary>
public sealed class ParameterDefinition
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Type Type { get; set; } = typeof(int);
    public object? DefaultValue { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public string Description { get; set; } = string.Empty;
}
