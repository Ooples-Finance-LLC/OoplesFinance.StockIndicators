namespace OoplesFinance.StockIndicators.Builder.VisualBuilder;

using System.Text;
using OoplesFinance.StockIndicators.Builder.Extensions;
using OoplesFinance.StockIndicators.Builder.VisualBuilder.NodeTypes;

/// <summary>
/// Generates executable C# code from a visual node graph.
/// </summary>
public sealed class CodeGenerator
{
    private readonly StringBuilder _codeBuilder = new();
    private int _indentLevel;
    private readonly Dictionary<string, string> _nodeVariables = new();
    private int _variableCounter;

    /// <summary>
    /// Generates C# code from a strategy definition.
    /// </summary>
    public GeneratedCode Generate(StrategyDefinition strategy)
    {
        _codeBuilder.Clear();
        _nodeVariables.Clear();
        _variableCounter = 0;
        _indentLevel = 0;

        var validation = strategy.Graph.Validate();
        if (!validation.IsValid)
        {
            return new GeneratedCode
            {
                IsSuccess = false,
                Errors = validation.Errors.ToList(),
                Warnings = validation.Warnings.ToList()
            };
        }

        try
        {
            GenerateStrategy(strategy);

            return new GeneratedCode
            {
                IsSuccess = true,
                Code = _codeBuilder.ToString(),
                ClassName = SanitizeClassName(strategy.Name),
                Namespace = "OoplesFinance.StockIndicators.Generated",
                Warnings = validation.Warnings.ToList()
            };
        }
        catch (Exception ex)
        {
            return new GeneratedCode
            {
                IsSuccess = false,
                Errors = new List<string> { $"Code generation failed: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Generates a preview snippet for quick testing.
    /// </summary>
    public string GeneratePreview(NodeGraph graph)
    {
        _codeBuilder.Clear();
        _nodeVariables.Clear();
        _variableCounter = 0;
        _indentLevel = 0;

        var executionOrder = graph.GetExecutionOrder();
        foreach (var node in executionOrder)
        {
            GenerateNodeCode(node, graph);
        }

        return _codeBuilder.ToString();
    }

    private void GenerateStrategy(StrategyDefinition strategy)
    {
        var className = SanitizeClassName(strategy.Name);

        // File header
        WriteLine("// Auto-generated strategy code");
        WriteLine($"// Strategy: {strategy.Name}");
        WriteLine($"// Version: {strategy.Version}");
        WriteLine($"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        WriteLine();

        // Using statements
        WriteLine("using System;");
        WriteLine("using System.Collections.Generic;");
        WriteLine("using System.Linq;");
        WriteLine("using OoplesFinance.StockIndicators;");
        WriteLine("using OoplesFinance.StockIndicators.Builder;");
        WriteLine("using OoplesFinance.StockIndicators.Builder.VisualBuilder;");
        WriteLine();

        // Namespace
        WriteLine("namespace OoplesFinance.StockIndicators.Generated;");
        WriteLine();

        // Class declaration
        WriteLine($"/// <summary>");
        WriteLine($"/// {strategy.Description}");
        WriteLine($"/// </summary>");
        WriteLine($"public sealed class {className} : IGeneratedStrategy");
        WriteLine("{");
        _indentLevel++;

        // Properties
        GenerateProperties(strategy);
        WriteLine();

        // Parameters
        GenerateParameters(strategy);
        WriteLine();

        // Constructor
        GenerateConstructor(className);
        WriteLine();

        // Evaluate method
        GenerateEvaluateMethod(strategy);
        WriteLine();

        // Helper methods
        GenerateHelperMethods(strategy);

        _indentLevel--;
        WriteLine("}");
    }

    private void GenerateProperties(StrategyDefinition strategy)
    {
        WriteLine("/// <summary>Gets the strategy ID.</summary>");
        WriteLine($"public string Id => \"{strategy.Id}\";");
        WriteLine();
        WriteLine("/// <summary>Gets the strategy name.</summary>");
        WriteLine($"public string Name => \"{EscapeString(strategy.Name)}\";");
        WriteLine();
        WriteLine("/// <summary>Gets the strategy version.</summary>");
        WriteLine($"public string Version => \"{strategy.Version}\";");
        WriteLine();
        WriteLine("/// <summary>Gets the target symbols.</summary>");
        WriteLine($"public IReadOnlyList<string> Symbols {{ get; }} = new[] {{ {string.Join(", ", strategy.Symbols.Select(s => $"\"{s}\""))} }};");
    }

    private void GenerateParameters(StrategyDefinition strategy)
    {
        WriteLine("#region Parameters");
        WriteLine();

        foreach (var param in strategy.Parameters)
        {
            var typeName = GetCSharpType(param.Type);
            var defaultValue = FormatDefaultValue(param.DefaultValue, param.Type);

            WriteLine($"/// <summary>{EscapeString(param.Description)}</summary>");
            WriteLine($"public {typeName} {SanitizeParameterName(param.Name)} {{ get; set; }} = {defaultValue};");
            WriteLine();
        }

        WriteLine("#endregion");
    }

    private void GenerateConstructor(string className)
    {
        WriteLine($"public {className}()");
        WriteLine("{");
        WriteLine("    // Initialize strategy");
        WriteLine("}");
    }

    private void GenerateEvaluateMethod(StrategyDefinition strategy)
    {
        WriteLine("/// <summary>");
        WriteLine("/// Evaluates the strategy and generates signals.");
        WriteLine("/// </summary>");
        WriteLine("public StrategySignal Evaluate(StrategyContext context)");
        WriteLine("{");
        _indentLevel++;

        // Null check
        WriteLine("if (context?.CurrentBar is null)");
        WriteLine("{");
        WriteLine("    return new StrategySignal { Type = SignalType.None };");
        WriteLine("}");
        WriteLine();

        // Get execution order
        var executionOrder = strategy.Graph.GetExecutionOrder();

        // Generate code for each node in order
        foreach (var node in executionOrder)
        {
            GenerateNodeCode(node, strategy.Graph);
        }

        // Find the final signal node
        var signalNodes = executionOrder.Where(n => n.Type is NodeType.Signal or NodeType.Action).ToList();
        if (signalNodes.Count > 0)
        {
            var lastSignalNode = signalNodes.Last();
            var signalVar = GetNodeVariable(lastSignalNode.Id);
            WriteLine();
            WriteLine($"return {signalVar};");
        }
        else
        {
            WriteLine();
            WriteLine("return new StrategySignal { Type = SignalType.None };");
        }

        _indentLevel--;
        WriteLine("}");
    }

    private void GenerateNodeCode(GraphNode node, NodeGraph graph)
    {
        var varName = AllocateVariable(node);
        _nodeVariables[node.Id] = varName;

        // Add comment
        WriteLine($"// Node: {node.Name} ({node.Type})");

        switch (node.Type)
        {
            case NodeType.DataSource:
                GenerateDataSourceNode(node, varName);
                break;
            case NodeType.Parameter:
                GenerateParameterNode(node, varName);
                break;
            case NodeType.Indicator:
                GenerateIndicatorNode(node, varName, graph);
                break;
            case NodeType.Math:
                GenerateMathNode(node, varName, graph);
                break;
            case NodeType.Logic:
                GenerateLogicNode(node, varName, graph);
                break;
            case NodeType.Signal:
                GenerateSignalNode(node, varName, graph);
                break;
            case NodeType.Action:
                GenerateActionNode(node, varName, graph);
                break;
            case NodeType.Aggregator:
                GenerateAggregatorNode(node, varName, graph);
                break;
            case NodeType.Filter:
                GenerateFilterNode(node, varName, graph);
                break;
            case NodeType.RiskControl:
                GenerateRiskControlNode(node, varName, graph);
                break;
            case NodeType.PositionSizing:
                GeneratePositionSizingNode(node, varName, graph);
                break;
            case NodeType.Comment:
                // Comments don't generate code
                WriteLine($"// {node.Configuration.GetValueOrDefault("text", "Comment")}");
                break;
        }

        WriteLine();
    }

    private void GenerateDataSourceNode(GraphNode node, string varName)
    {
        var symbol = node.Configuration.GetValueOrDefault("symbol", "context.CurrentBar.Symbol") ?? "context.CurrentBar.Symbol";
        WriteLine($"var {varName}_open = context.CurrentBar.Open;");
        WriteLine($"var {varName}_high = context.CurrentBar.High;");
        WriteLine($"var {varName}_low = context.CurrentBar.Low;");
        WriteLine($"var {varName}_close = context.CurrentBar.Close;");
        WriteLine($"var {varName}_volume = context.CurrentBar.Volume;");
    }

    private void GenerateParameterNode(GraphNode node, string varName)
    {
        var value = node.Configuration.GetValueOrDefault("value", 0);
        var paramName = SanitizeParameterName(node.Name);

        // Reference the class property instead of literal value
        WriteLine($"var {varName} = {paramName};");
    }

    private void GenerateIndicatorNode(GraphNode node, string varName, NodeGraph graph)
    {
        var indicatorName = node.Name.Replace(" ", "");
        var inputs = GetInputValues(node, graph);

        // Build parameter string from configuration
        var configParams = node.Configuration
            .Where(kvp => kvp.Value is not null)
            .Select(kvp => $"{kvp.Key}: {FormatValue(kvp.Value)}")
            .ToList();

        var paramsStr = configParams.Count > 0 ? string.Join(", ", configParams) : "";

        // Get the input source
        var sourceInput = inputs.GetValueOrDefault("source") ?? inputs.GetValueOrDefault("close") ?? "context.CurrentBar.Close";

        WriteLine($"var {varName} = context.IndicatorValues.GetValueOrDefault(\"{node.Id}\", 0m);");
        WriteLine($"// Indicator calculation: {indicatorName}({sourceInput}, {paramsStr})");
    }

    private void GenerateMathNode(GraphNode node, string varName, NodeGraph graph)
    {
        var operation = node.Configuration.GetValueOrDefault("operation", "Add")?.ToString() ?? "Add";
        var inputs = GetInputValues(node, graph);
        var a = inputs.GetValueOrDefault("a") ?? "0";
        var b = inputs.GetValueOrDefault("b") ?? "0";

        var expression = operation switch
        {
            "Add" => $"{a} + {b}",
            "Subtract" => $"{a} - {b}",
            "Multiply" => $"{a} * {b}",
            "Divide" => $"{b} != 0 ? {a} / {b} : 0",
            "Abs" => $"Math.Abs({a})",
            "Sqrt" => $"(decimal)Math.Sqrt((double){a})",
            "Log" => $"(decimal)Math.Log((double){a})",
            "Exp" => $"(decimal)Math.Exp((double){a})",
            "Min" => $"Math.Min({a}, {b})",
            "Max" => $"Math.Max({a}, {b})",
            "Average" => $"({a} + {b}) / 2",
            "Percentage" => $"{b} != 0 ? ({a} - {b}) / {b} * 100 : 0",
            _ => "0"
        };

        WriteLine($"var {varName} = {expression};");
    }

    private void GenerateLogicNode(GraphNode node, string varName, NodeGraph graph)
    {
        var comparison = node.Configuration.GetValueOrDefault("comparison", "GreaterThan")?.ToString() ?? "GreaterThan";
        var inputs = GetInputValues(node, graph);
        var a = inputs.GetValueOrDefault("a") ?? "0";
        var b = inputs.GetValueOrDefault("b") ?? "0";

        var expression = comparison switch
        {
            "GreaterThan" => $"{a} > {b}",
            "LessThan" => $"{a} < {b}",
            "Equal" => $"{a} == {b}",
            "GreaterOrEqual" => $"{a} >= {b}",
            "LessOrEqual" => $"{a} <= {b}",
            "NotEqual" => $"{a} != {b}",
            "CrossOver" => $"CrossOver({a}, {b}, context)",
            "CrossUnder" => $"CrossUnder({a}, {b}, context)",
            _ => "false"
        };

        WriteLine($"var {varName} = {expression};");
    }

    private void GenerateSignalNode(GraphNode node, string varName, NodeGraph graph)
    {
        var signalType = node.Configuration.GetValueOrDefault("signalType", "Buy")?.ToString() ?? "Buy";
        var inputs = GetInputValues(node, graph);
        var condition = inputs.GetValueOrDefault("condition") ?? "false";
        var strength = inputs.GetValueOrDefault("strength") ?? "1.0m";

        WriteLine($"var {varName} = {condition}");
        WriteLine($"    ? new StrategySignal");
        WriteLine($"    {{");
        WriteLine($"        Type = SignalType.{signalType},");
        WriteLine($"        Strength = {strength},");
        WriteLine($"        Timestamp = context.CurrentBar.Timestamp,");
        WriteLine($"        Symbol = context.CurrentBar.Symbol,");
        WriteLine($"        EntryPrice = context.CurrentBar.Close");
        WriteLine($"    }}");
        WriteLine($"    : new StrategySignal {{ Type = SignalType.None }};");
    }

    private void GenerateActionNode(GraphNode node, string varName, NodeGraph graph)
    {
        var actionType = node.Configuration.GetValueOrDefault("actionType", "MarketBuy")?.ToString() ?? "MarketBuy";
        var inputs = GetInputValues(node, graph);
        var trigger = inputs.GetValueOrDefault("trigger") ?? "new StrategySignal { Type = SignalType.None }";

        var signalType = actionType switch
        {
            "MarketBuy" or "LimitBuy" or "ScaleIn" => "Buy",
            "MarketSell" or "LimitSell" or "ScaleOut" => "Sell",
            "StopLoss" => "ExitLong",
            "TakeProfit" => "ExitLong",
            "ClosePosition" => "ExitLong",
            _ => "None"
        };

        WriteLine($"var {varName} = {trigger}.Type != SignalType.None");
        WriteLine($"    ? new StrategySignal");
        WriteLine($"    {{");
        WriteLine($"        Type = SignalType.{signalType},");
        WriteLine($"        Strength = {trigger}.Strength,");
        WriteLine($"        Timestamp = context.CurrentBar.Timestamp,");
        WriteLine($"        Symbol = context.CurrentBar.Symbol,");
        WriteLine($"        EntryPrice = context.CurrentBar.Close,");
        WriteLine($"        Reason = \"{actionType} triggered\"");
        WriteLine($"    }}");
        WriteLine($"    : new StrategySignal {{ Type = SignalType.None }};");
    }

    private void GenerateAggregatorNode(GraphNode node, string varName, NodeGraph graph)
    {
        var aggregation = node.Configuration.GetValueOrDefault("aggregation", "All")?.ToString() ?? "All";
        var inputs = GetInputValues(node, graph);

        var signalVars = inputs.Values.Where(v => v.Contains("signal", StringComparison.OrdinalIgnoreCase)).ToList();
        if (signalVars.Count == 0)
        {
            WriteLine($"var {varName} = new StrategySignal {{ Type = SignalType.None }};");
            return;
        }

        var signalList = string.Join(", ", signalVars);

        switch (aggregation)
        {
            case "All":
                WriteLine($"var {varName}_signals = new[] {{ {signalList} }};");
                WriteLine($"var {varName} = {varName}_signals.All(s => s.Type != SignalType.None)");
                WriteLine($"    ? {varName}_signals.First()");
                WriteLine($"    : new StrategySignal {{ Type = SignalType.None }};");
                break;
            case "Any":
                WriteLine($"var {varName}_signals = new[] {{ {signalList} }};");
                WriteLine($"var {varName} = {varName}_signals.FirstOrDefault(s => s.Type != SignalType.None)");
                WriteLine($"    ?? new StrategySignal {{ Type = SignalType.None }};");
                break;
            default:
                WriteLine($"var {varName} = AggregateSignals(new[] {{ {signalList} }}, \"{aggregation}\");");
                break;
        }
    }

    private void GenerateFilterNode(GraphNode node, string varName, NodeGraph graph)
    {
        var filterType = node.Configuration.GetValueOrDefault("filterType", "Custom")?.ToString() ?? "Custom";
        var inputs = GetInputValues(node, graph);
        var inputSignal = inputs.GetValueOrDefault("input") ?? "new StrategySignal { Type = SignalType.None }";

        WriteLine($"var {varName}_passed = ApplyFilter({inputSignal}, \"{filterType}\", context);");
        WriteLine($"var {varName} = {varName}_passed ? {inputSignal} : new StrategySignal {{ Type = SignalType.None }};");
    }

    private void GenerateRiskControlNode(GraphNode node, string varName, NodeGraph graph)
    {
        var inputs = GetInputValues(node, graph);
        var signalInput = inputs.GetValueOrDefault("signal") ?? "new StrategySignal { Type = SignalType.None }";

        var maxDailyLoss = node.Configuration.GetValueOrDefault("maxDailyLoss", 0.02m);
        var maxDrawdown = node.Configuration.GetValueOrDefault("maxDrawdown", 0.10m);

        WriteLine($"var {varName}_approved = CheckRiskLimits({signalInput}, context, {maxDailyLoss}m, {maxDrawdown}m);");
        WriteLine($"var {varName} = {varName}_approved ? {signalInput} : new StrategySignal {{ Type = SignalType.None }};");
    }

    private void GeneratePositionSizingNode(GraphNode node, string varName, NodeGraph graph)
    {
        var method = node.Configuration.GetValueOrDefault("method", "PercentOfEquity")?.ToString() ?? "PercentOfEquity";
        var value = node.Configuration.GetValueOrDefault("value", 0.10m);
        var inputs = GetInputValues(node, graph);
        var signalInput = inputs.GetValueOrDefault("signal") ?? "new StrategySignal { Type = SignalType.None }";

        WriteLine($"var {varName}_size = CalculatePositionSize(context, \"{method}\", {value}m);");
        WriteLine($"var {varName} = {signalInput} with {{ PositionSize = {varName}_size }};");
    }

    private void GenerateHelperMethods(StrategyDefinition strategy)
    {
        WriteLine("#region Helper Methods");
        WriteLine();

        // CrossOver helper
        WriteLine("private static bool CrossOver(decimal current, decimal threshold, StrategyContext context)");
        WriteLine("{");
        WriteLine("    if (context.History.Count < 2) return false;");
        WriteLine("    var previous = context.History[^2].Close;");
        WriteLine("    return previous < threshold && current > threshold;");
        WriteLine("}");
        WriteLine();

        // CrossUnder helper
        WriteLine("private static bool CrossUnder(decimal current, decimal threshold, StrategyContext context)");
        WriteLine("{");
        WriteLine("    if (context.History.Count < 2) return false;");
        WriteLine("    var previous = context.History[^2].Close;");
        WriteLine("    return previous > threshold && current < threshold;");
        WriteLine("}");
        WriteLine();

        // AggregateSignals helper
        WriteLine("private static StrategySignal AggregateSignals(StrategySignal[] signals, string method)");
        WriteLine("{");
        WriteLine("    var activeSignals = signals.Where(s => s.Type != SignalType.None).ToList();");
        WriteLine("    if (activeSignals.Count == 0) return new StrategySignal { Type = SignalType.None };");
        WriteLine();
        WriteLine("    return method switch");
        WriteLine("    {");
        WriteLine("        \"Majority\" when activeSignals.Count >= signals.Length / 2 => activeSignals.First(),");
        WriteLine("        \"WeightedAverage\" => activeSignals.OrderByDescending(s => s.Strength).First(),");
        WriteLine("        _ => activeSignals.First()");
        WriteLine("    };");
        WriteLine("}");
        WriteLine();

        // ApplyFilter helper
        WriteLine("private static bool ApplyFilter(StrategySignal signal, string filterType, StrategyContext context)");
        WriteLine("{");
        WriteLine("    if (signal.Type == SignalType.None) return false;");
        WriteLine();
        WriteLine("    return filterType switch");
        WriteLine("    {");
        WriteLine("        \"TimeOfDay\" => context.CurrentBar.Timestamp.Hour >= 9 && context.CurrentBar.Timestamp.Hour < 16,");
        WriteLine("        \"DayOfWeek\" => context.CurrentBar.Timestamp.DayOfWeek != DayOfWeek.Saturday && context.CurrentBar.Timestamp.DayOfWeek != DayOfWeek.Sunday,");
        WriteLine("        \"VolumeThreshold\" => context.CurrentBar.Volume > 1000000,");
        WriteLine("        _ => true");
        WriteLine("    };");
        WriteLine("}");
        WriteLine();

        // CheckRiskLimits helper
        WriteLine("private static bool CheckRiskLimits(StrategySignal signal, StrategyContext context, decimal maxDailyLoss, decimal maxDrawdown)");
        WriteLine("{");
        WriteLine("    if (signal.Type == SignalType.None) return false;");
        WriteLine();
        WriteLine("    var dailyLossPercent = context.DailyPnL / context.AccountEquity;");
        WriteLine("    if (dailyLossPercent < -maxDailyLoss) return false;");
        WriteLine();
        WriteLine("    return true;");
        WriteLine("}");
        WriteLine();

        // CalculatePositionSize helper
        WriteLine("private static decimal CalculatePositionSize(StrategyContext context, string method, decimal value)");
        WriteLine("{");
        WriteLine("    return method switch");
        WriteLine("    {");
        WriteLine("        \"Fixed\" => value,");
        WriteLine("        \"PercentOfEquity\" => context.AccountEquity * value / context.CurrentBar.Close,");
        WriteLine("        \"RiskBased\" => context.AccountEquity * value / context.CurrentBar.Close,");
        WriteLine("        _ => value");
        WriteLine("    };");
        WriteLine("}");
        WriteLine();

        WriteLine("#endregion");
    }

    private Dictionary<string, string> GetInputValues(GraphNode node, NodeGraph graph)
    {
        var inputs = new Dictionary<string, string>();

        foreach (var port in node.InputPorts)
        {
            var connection = graph.Connections.FirstOrDefault(c =>
                c.TargetNodeId == node.Id && c.TargetPortId == port.Id);

            if (connection is not null)
            {
                var sourceVar = _nodeVariables.GetValueOrDefault(connection.SourceNodeId);
                if (sourceVar is not null)
                {
                    // Check if it's a multi-output node (like DataSource)
                    var sourceNode = graph.Nodes.First(n => n.Id == connection.SourceNodeId);
                    if (sourceNode.Type == NodeType.DataSource)
                    {
                        inputs[port.Id] = $"{sourceVar}_{connection.SourcePortId}";
                    }
                    else
                    {
                        inputs[port.Id] = sourceVar;
                    }
                }
            }
            else if (port.DefaultValue is not null)
            {
                inputs[port.Id] = FormatValue(port.DefaultValue);
            }
        }

        return inputs;
    }

    private string GetNodeVariable(string nodeId)
    {
        return _nodeVariables.GetValueOrDefault(nodeId) ?? "unknownNode";
    }

    private string AllocateVariable(GraphNode node)
    {
        var baseName = node.Type switch
        {
            NodeType.DataSource => "data",
            NodeType.Parameter => "param",
            NodeType.Indicator => "ind",
            NodeType.Math => "calc",
            NodeType.Logic => "cond",
            NodeType.Signal => "signal",
            NodeType.Action => "action",
            NodeType.Aggregator => "agg",
            NodeType.Filter => "filter",
            NodeType.RiskControl => "risk",
            NodeType.PositionSizing => "size",
            _ => "node"
        };

        return $"{baseName}_{_variableCounter++}";
    }

    private void WriteLine(string line = "")
    {
        if (string.IsNullOrEmpty(line))
        {
            _codeBuilder.AppendLine();
        }
        else
        {
            _codeBuilder.Append(new string(' ', _indentLevel * 4));
            _codeBuilder.AppendLine(line);
        }
    }

    private static string SanitizeClassName(string name)
    {
        var sanitized = new StringBuilder();
        var capitalizeNext = true;

        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                sanitized.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                capitalizeNext = false;
            }
            else
            {
                capitalizeNext = true;
            }
        }

        if (sanitized.Length == 0 || char.IsDigit(sanitized[0]))
        {
            sanitized.Insert(0, "Strategy");
        }

        return sanitized.ToString();
    }

    private static string SanitizeParameterName(string name)
    {
        var sanitized = new StringBuilder();
        var capitalizeNext = false;

        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                sanitized.Append(capitalizeNext ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                capitalizeNext = true;
            }
        }

        if (sanitized.Length == 0 || char.IsDigit(sanitized[0]))
        {
            sanitized.Insert(0, "param");
        }

        return sanitized.ToString();
    }

    private static string GetCSharpType(ParameterType type)
    {
        return type switch
        {
            ParameterType.Integer => "int",
            ParameterType.Decimal => "decimal",
            ParameterType.Boolean => "bool",
            ParameterType.String => "string",
            ParameterType.Enum => "string",
            ParameterType.DateRange => "DateTime",
            _ => "object"
        };
    }

    private static string FormatDefaultValue(object? value, ParameterType type)
    {
        if (value is null) return "default";

        return type switch
        {
            ParameterType.Integer => value.ToString() ?? "0",
            ParameterType.Decimal => $"{value}m",
            ParameterType.Boolean => value.ToString()?.ToLowerInvariant() ?? "false",
            ParameterType.String => $"\"{EscapeString(value.ToString())}\"",
            ParameterType.Enum => $"\"{value}\"",
            _ => value.ToString() ?? "default"
        };
    }

    private static string FormatValue(object? value)
    {
        if (value is null) return "null";

        return value switch
        {
            bool b => b.ToString().ToLowerInvariant(),
            int i => i.ToString(),
            long l => $"{l}L",
            float f => $"{f}f",
            double d => $"{d}d",
            decimal m => $"{m}m",
            string s => $"\"{EscapeString(s)}\"",
            _ => value.ToString() ?? "null"
        };
    }

    private static string EscapeString(string? value)
    {
        if (value is null) return string.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}

/// <summary>
/// Result of code generation.
/// </summary>
public sealed class GeneratedCode
{
    /// <summary>Gets or sets whether generation succeeded.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Gets or sets the generated code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the class name.</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Gets or sets the namespace.</summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>Gets or sets generation errors.</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Gets or sets generation warnings.</summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Interface for generated strategies.
/// </summary>
public interface IGeneratedStrategy
{
    /// <summary>Gets the strategy ID.</summary>
    string Id { get; }

    /// <summary>Gets the strategy name.</summary>
    string Name { get; }

    /// <summary>Gets the strategy version.</summary>
    string Version { get; }

    /// <summary>Gets the target symbols.</summary>
    IReadOnlyList<string> Symbols { get; }

    /// <summary>Evaluates the strategy.</summary>
    StrategySignal Evaluate(StrategyContext context);
}
