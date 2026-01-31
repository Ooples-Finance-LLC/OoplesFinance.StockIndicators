using System.Text.Json;
using OoplesFinance.StrategyBuilder.Maui.ViewModels;

namespace OoplesFinance.StrategyBuilder.Maui;

#region Service Interfaces

/// <summary>
/// Service for managing strategy persistence and validation.
/// </summary>
public interface IStrategyService
{
    Task<StrategyDefinition?> LoadStrategyAsync(string path);
    Task<string?> SaveStrategyAsync(StrategyDefinition strategy);
    Task<ValidationResult> ValidateStrategyAsync(List<NodeViewModel> nodes, List<ConnectionViewModel> connections);
}

/// <summary>
/// Service for providing the indicator library.
/// </summary>
public interface IIndicatorLibraryService
{
    Task<List<IndicatorCategoryViewModel>> GetCategoriesAsync();
    Task<IndicatorItemViewModel?> GetIndicatorByIdAsync(string id);
}

/// <summary>
/// Service for managing node connections.
/// </summary>
public interface INodeConnectionService
{
    bool CanConnect(PortViewModel source, PortViewModel target);
    ConnectionViewModel CreateConnection(PortViewModel source, PortViewModel target);
}

/// <summary>
/// Service for generating code from the visual strategy.
/// </summary>
public interface ICodeGenerationService
{
    Task<string?> GenerateAsync(List<NodeViewModel> nodes, List<ConnectionViewModel> connections, string strategyName);
}

/// <summary>
/// Service for running backtests.
/// </summary>
public interface IBacktestService
{
    Task<BacktestResult?> RunBacktestAsync(List<NodeViewModel> nodes, List<ConnectionViewModel> connections);
}

#endregion

#region Data Models

/// <summary>
/// Represents a complete strategy definition for serialization.
/// </summary>
public class StrategyDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public List<NodeViewModel> Nodes { get; set; } = new();
    public List<ConnectionViewModel> Connections { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Result of strategy validation.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> ErrorNodes { get; set; } = new();
}

/// <summary>
/// Result of a backtest run.
/// </summary>
public class BacktestResult
{
    public string StrategyName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal InitialCapital { get; set; }
    public decimal FinalCapital { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal AnnualizedReturn { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal SortinoRatio { get; set; }
    public decimal MaxDrawdown { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal AverageWin { get; set; }
    public decimal AverageLoss { get; set; }
    public List<TradeRecord> Trades { get; set; } = new();
    public List<EquityPoint> EquityCurve { get; set; } = new();
}

public class TradeRecord
{
    public DateTime EntryDate { get; set; }
    public DateTime ExitDate { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal ProfitLoss { get; set; }
    public decimal ProfitLossPercent { get; set; }
}

public class EquityPoint
{
    public DateTime Date { get; set; }
    public decimal Equity { get; set; }
    public decimal Drawdown { get; set; }
}

#endregion

#region Service Implementations

/// <summary>
/// Implementation of IStrategyService for file-based strategy persistence.
/// </summary>
public class StrategyService : IStrategyService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<StrategyDefinition?> LoadStrategyAsync(string path)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<StrategyDefinition>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load strategy: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> SaveStrategyAsync(StrategyDefinition strategy)
    {
        try
        {
#if WINDOWS
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var strategiesFolder = Path.Combine(folder, "OoplesStrategies");
            Directory.CreateDirectory(strategiesFolder);

            var fileName = $"{strategy.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.ostrat";
            var path = Path.Combine(strategiesFolder, fileName);

            strategy.ModifiedAt = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(strategy, JsonOptions);
            await File.WriteAllTextAsync(path, json);

            return path;
#else
            // For mobile platforms, use app data directory
            var folder = FileSystem.AppDataDirectory;
            var fileName = $"{strategy.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.ostrat";
            var path = Path.Combine(folder, fileName);

            strategy.ModifiedAt = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(strategy, JsonOptions);
            await File.WriteAllTextAsync(path, json);

            return path;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save strategy: {ex.Message}");
            return null;
        }
    }

    public Task<ValidationResult> ValidateStrategyAsync(List<NodeViewModel> nodes, List<ConnectionViewModel> connections)
    {
        var result = new ValidationResult { IsValid = true };

        // Check for empty strategy
        if (nodes.Count == 0)
        {
            result.IsValid = false;
            result.Errors.Add("Strategy has no nodes");
            return Task.FromResult(result);
        }

        // Check for disconnected nodes
        var connectedNodeIds = new HashSet<string>();
        foreach (var conn in connections)
        {
            if (conn.SourceNode is not null) connectedNodeIds.Add(conn.SourceNode.Id);
            if (conn.TargetNode is not null) connectedNodeIds.Add(conn.TargetNode.Id);
        }

        foreach (var node in nodes)
        {
            // Data source nodes don't need inputs
            if (node.NodeType == "datasource") continue;

            if (!connectedNodeIds.Contains(node.Id) && node.InputPorts.Count > 0)
            {
                result.Warnings.Add($"Node '{node.Name}' has no connections");
            }
        }

        // Check for cycles (simplified - just check if any output connects back to same node)
        foreach (var conn in connections)
        {
            if (conn.SourceNode == conn.TargetNode)
            {
                result.IsValid = false;
                result.Errors.Add($"Node '{conn.SourceNode?.Name}' has a self-referencing connection");
                if (conn.SourceNode is not null)
                    result.ErrorNodes.Add(conn.SourceNode.Id);
            }
        }

        // Check for required action nodes (entry/exit)
        var hasEntry = nodes.Any(n => n.Name.Contains("Entry", StringComparison.OrdinalIgnoreCase));
        var hasExit = nodes.Any(n => n.Name.Contains("Exit", StringComparison.OrdinalIgnoreCase));

        if (!hasEntry)
        {
            result.Warnings.Add("Strategy has no entry action node");
        }

        if (!hasExit)
        {
            result.Warnings.Add("Strategy has no exit action node");
        }

        return Task.FromResult(result);
    }
}

/// <summary>
/// Implementation of IIndicatorLibraryService providing the indicator catalog.
/// </summary>
public class IndicatorLibraryService : IIndicatorLibraryService
{
    private readonly List<IndicatorCategoryViewModel> _categories;

    public IndicatorLibraryService()
    {
        _categories = BuildIndicatorLibrary();
    }

    public Task<List<IndicatorCategoryViewModel>> GetCategoriesAsync()
    {
        return Task.FromResult(_categories);
    }

    public Task<IndicatorItemViewModel?> GetIndicatorByIdAsync(string id)
    {
        foreach (var category in _categories)
        {
            var indicator = category.Indicators.FirstOrDefault(i => i.Id == id);
            if (indicator is not null)
                return Task.FromResult<IndicatorItemViewModel?>(indicator);
        }
        return Task.FromResult<IndicatorItemViewModel?>(null);
    }

    private static List<IndicatorCategoryViewModel> BuildIndicatorLibrary()
    {
        return new List<IndicatorCategoryViewModel>
        {
            new()
            {
                CategoryName = "Data Sources",
                Indicators = new System.Collections.ObjectModel.ObservableCollection<IndicatorItemViewModel>
                {
                    new() { Id = "price", Name = "Price Data", Description = "OHLCV price data input", NodeType = "datasource" },
                    new() { Id = "volume", Name = "Volume Data", Description = "Volume data input", NodeType = "datasource" },
                }
            },
            new()
            {
                CategoryName = "Trend Indicators",
                Indicators = new System.Collections.ObjectModel.ObservableCollection<IndicatorItemViewModel>
                {
                    new() { Id = "sma", Name = "SMA", Description = "Simple Moving Average" },
                    new() { Id = "ema", Name = "EMA", Description = "Exponential Moving Average" },
                    new() { Id = "wma", Name = "WMA", Description = "Weighted Moving Average" },
                    new() { Id = "dema", Name = "DEMA", Description = "Double Exponential Moving Average" },
                    new() { Id = "tema", Name = "TEMA", Description = "Triple Exponential Moving Average" },
                    new() { Id = "macd", Name = "MACD", Description = "Moving Average Convergence Divergence" },
                    new() { Id = "adx", Name = "ADX", Description = "Average Directional Index" },
                    new() { Id = "aroon", Name = "Aroon", Description = "Aroon Up/Down Indicator" },
                    new() { Id = "supertrend", Name = "SuperTrend", Description = "SuperTrend Indicator" },
                }
            },
            new()
            {
                CategoryName = "Momentum Indicators",
                Indicators = new System.Collections.ObjectModel.ObservableCollection<IndicatorItemViewModel>
                {
                    new() { Id = "rsi", Name = "RSI", Description = "Relative Strength Index" },
                    new() { Id = "stoch", Name = "Stochastic", Description = "Stochastic Oscillator" },
                    new() { Id = "cci", Name = "CCI", Description = "Commodity Channel Index" },
                    new() { Id = "roc", Name = "ROC", Description = "Rate of Change" },
                    new() { Id = "williams", Name = "Williams %R", Description = "Williams Percent Range" },
                    new() { Id = "mfi", Name = "MFI", Description = "Money Flow Index" },
                    new() { Id = "tsi", Name = "TSI", Description = "True Strength Index" },
                }
            },
            new()
            {
                CategoryName = "Volatility Indicators",
                Indicators = new System.Collections.ObjectModel.ObservableCollection<IndicatorItemViewModel>
                {
                    new() { Id = "bb", Name = "Bollinger Bands", Description = "Bollinger Bands" },
                    new() { Id = "atr", Name = "ATR", Description = "Average True Range" },
                    new() { Id = "kc", Name = "Keltner Channel", Description = "Keltner Channel" },
                    new() { Id = "dc", Name = "Donchian Channel", Description = "Donchian Channel" },
                    new() { Id = "stddev", Name = "Standard Deviation", Description = "Standard Deviation" },
                }
            },
            new()
            {
                CategoryName = "Volume Indicators",
                Indicators = new System.Collections.ObjectModel.ObservableCollection<IndicatorItemViewModel>
                {
                    new() { Id = "obv", Name = "OBV", Description = "On-Balance Volume" },
                    new() { Id = "ad", Name = "A/D Line", Description = "Accumulation/Distribution Line" },
                    new() { Id = "vwap", Name = "VWAP", Description = "Volume Weighted Average Price" },
                    new() { Id = "cmf", Name = "CMF", Description = "Chaikin Money Flow" },
                }
            },
            new()
            {
                CategoryName = "Signal Generators",
                Indicators = new System.Collections.ObjectModel.ObservableCollection<IndicatorItemViewModel>
                {
                    new() { Id = "crossover", Name = "Crossover", Description = "Detect when two lines cross", NodeType = "signal" },
                    new() { Id = "threshold", Name = "Threshold", Description = "Signal when value crosses threshold", NodeType = "signal" },
                    new() { Id = "compare", Name = "Compare", Description = "Compare two values", NodeType = "signal" },
                    new() { Id = "and", Name = "AND Gate", Description = "Logical AND of signals", NodeType = "signal" },
                    new() { Id = "or", Name = "OR Gate", Description = "Logical OR of signals", NodeType = "signal" },
                    new() { Id = "not", Name = "NOT Gate", Description = "Invert signal", NodeType = "signal" },
                }
            },
            new()
            {
                CategoryName = "Actions",
                Indicators = new System.Collections.ObjectModel.ObservableCollection<IndicatorItemViewModel>
                {
                    new() { Id = "entry", Name = "Entry Order", Description = "Enter position when signal fires", NodeType = "action" },
                    new() { Id = "exit", Name = "Exit Order", Description = "Exit position when signal fires", NodeType = "action" },
                    new() { Id = "stoploss", Name = "Stop Loss", Description = "Set stop loss level", NodeType = "action" },
                    new() { Id = "takeprofit", Name = "Take Profit", Description = "Set take profit level", NodeType = "action" },
                    new() { Id = "trailstop", Name = "Trailing Stop", Description = "Set trailing stop", NodeType = "action" },
                }
            },
        };
    }
}

/// <summary>
/// Implementation of INodeConnectionService.
/// </summary>
public class NodeConnectionService : INodeConnectionService
{
    public bool CanConnect(PortViewModel source, PortViewModel target)
    {
        // Must be output -> input
        if (source.IsInput == target.IsInput) return false;

        // Must be different nodes
        if (source.ParentNode == target.ParentNode) return false;

        // Check type compatibility
        return AreTypesCompatible(source.DataType, target.DataType);
    }

    public ConnectionViewModel CreateConnection(PortViewModel source, PortViewModel target)
    {
        return new ConnectionViewModel
        {
            Id = Guid.NewGuid().ToString(),
            SourceNode = source.ParentNode,
            SourcePort = source,
            TargetNode = target.ParentNode,
            TargetPort = target,
            DataType = source.DataType
        };
    }

    private static bool AreTypesCompatible(string sourceType, string targetType)
    {
        if (sourceType == targetType) return true;
        if (targetType == "any") return true;
        if (sourceType == "decimal" && targetType == "double") return true;
        if (sourceType == "double" && targetType == "decimal") return true;
        if (sourceType.EndsWith("[]") && targetType.EndsWith("[]"))
        {
            var sourceBase = sourceType[..^2];
            var targetBase = targetType[..^2];
            return AreTypesCompatible(sourceBase, targetBase);
        }
        return false;
    }
}

/// <summary>
/// Implementation of ICodeGenerationService.
/// </summary>
public class CodeGenerationService : ICodeGenerationService
{
    public Task<string?> GenerateAsync(List<NodeViewModel> nodes, List<ConnectionViewModel> connections, string strategyName)
    {
        // This would integrate with the VisualBuilder backend's CodeGenerator
        // For now, generate a placeholder
        var code = $@"// Auto-generated strategy: {strategyName}
// Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

using OoplesFinance.StockIndicators;

public class {strategyName.Replace(" ", "")}Strategy : IStrategy
{{
    // {nodes.Count} nodes, {connections.Count} connections

    public void Execute(StockData data)
    {{
        // TODO: Implement generated strategy logic
    }}
}}";

        return Task.FromResult<string?>(code);
    }
}

/// <summary>
/// Implementation of IBacktestService.
/// </summary>
public class BacktestService : IBacktestService
{
    public Task<BacktestResult?> RunBacktestAsync(List<NodeViewModel> nodes, List<ConnectionViewModel> connections)
    {
        // This would integrate with the BacktestEngine
        // For now, return sample data
        var result = new BacktestResult
        {
            StrategyName = "Sample Strategy",
            StartDate = DateTime.Now.AddYears(-1),
            EndDate = DateTime.Now,
            InitialCapital = 100000m,
            FinalCapital = 125000m,
            TotalReturn = 0.25m,
            AnnualizedReturn = 0.25m,
            SharpeRatio = 1.5m,
            SortinoRatio = 2.0m,
            MaxDrawdown = 0.08m,
            TotalTrades = 50,
            WinningTrades = 30,
            LosingTrades = 20,
            WinRate = 0.60m,
            ProfitFactor = 1.8m,
            AverageWin = 1200m,
            AverageLoss = 600m
        };

        return Task.FromResult<BacktestResult?>(result);
    }
}

#endregion
