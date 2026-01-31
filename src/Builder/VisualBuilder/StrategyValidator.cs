namespace OoplesFinance.StockIndicators.Builder.VisualBuilder;

using System.Reflection;
#if !NET461
using System.Runtime.Loader;
#endif
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OoplesFinance.StockIndicators.Builder.Extensions;
using OoplesFinance.StockIndicators.Builder.VisualBuilder.NodeTypes;

/// <summary>
/// Validates and compiles visual strategies.
/// </summary>
public sealed class StrategyValidator
{
    private readonly IndicatorLibrary _indicatorLibrary;
    private readonly CodeGenerator _codeGenerator;

    /// <summary>
    /// Initializes a new instance of the StrategyValidator class.
    /// </summary>
    public StrategyValidator()
    {
        _indicatorLibrary = new IndicatorLibrary();
        _codeGenerator = new CodeGenerator();
    }

    /// <summary>
    /// Validates a strategy definition.
    /// </summary>
    public ValidationResult Validate(StrategyDefinition strategy)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        // Basic metadata validation
        ValidateMetadata(strategy, errors, warnings);

        // Graph validation
        ValidateGraph(strategy.Graph, errors, warnings);

        // Node validation
        ValidateNodes(strategy.Graph, errors, warnings);

        // Parameter validation
        ValidateParameters(strategy, errors, warnings);

        // Risk settings validation
        ValidateRiskSettings(strategy.RiskSettings, errors, warnings);

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Validates and compiles a strategy to executable code.
    /// </summary>
    public CompilationResult Compile(StrategyDefinition strategy)
    {
        var validation = Validate(strategy);
        if (!validation.IsValid)
        {
            return new CompilationResult
            {
                IsSuccess = false,
                ValidationErrors = validation.Errors.Select(e => e.Message).ToList()
            };
        }

        // Generate code
        var generated = _codeGenerator.Generate(strategy);
        if (!generated.IsSuccess)
        {
            return new CompilationResult
            {
                IsSuccess = false,
                ValidationErrors = generated.Errors
            };
        }

        // Try to compile
        try
        {
            var compilation = CompileCode(generated.Code);
            if (!compilation.Success)
            {
                return new CompilationResult
                {
                    IsSuccess = false,
                    Code = generated.Code,
                    CompilationErrors = compilation.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(d => d.GetMessage())
                        .ToList()
                };
            }

            return new CompilationResult
            {
                IsSuccess = true,
                Code = generated.Code,
                ClassName = generated.ClassName,
                Namespace = generated.Namespace,
                Assembly = compilation.Assembly
            };
        }
        catch (Exception ex)
        {
            return new CompilationResult
            {
                IsSuccess = false,
                Code = generated.Code,
                CompilationErrors = new List<string> { $"Compilation failed: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Creates an instance of a compiled strategy.
    /// </summary>
    public IGeneratedStrategy? CreateInstance(CompilationResult compilation)
    {
        if (!compilation.IsSuccess || compilation.Assembly is null)
            return null;

        var fullTypeName = $"{compilation.Namespace}.{compilation.ClassName}";
        var type = compilation.Assembly.GetType(fullTypeName);

        if (type is null) return null;

        return Activator.CreateInstance(type) as IGeneratedStrategy;
    }

    private void ValidateMetadata(StrategyDefinition strategy, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        if (string.IsNullOrWhiteSpace(strategy.Name))
        {
            errors.Add(new ValidationError("METADATA_001", "Strategy name is required."));
        }
        else if (strategy.Name.Length > 100)
        {
            errors.Add(new ValidationError("METADATA_002", "Strategy name must be 100 characters or less."));
        }

        if (strategy.Description.Length > 2000)
        {
            warnings.Add(new ValidationWarning("METADATA_W01", "Description is very long. Consider shortening it."));
        }

        if (strategy.Symbols.Count == 0)
        {
            warnings.Add(new ValidationWarning("METADATA_W02", "No symbols specified. Strategy will use default symbols."));
        }

        if (strategy.Symbols.Count > 100)
        {
            errors.Add(new ValidationError("METADATA_003", "Maximum 100 symbols allowed."));
        }
    }

    private void ValidateGraph(NodeGraph graph, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        if (graph.Nodes.Count == 0)
        {
            errors.Add(new ValidationError("GRAPH_001", "Strategy must have at least one node."));
            return;
        }

        if (graph.Nodes.Count > 500)
        {
            errors.Add(new ValidationError("GRAPH_002", "Strategy cannot have more than 500 nodes."));
        }

        // Check for entry points
        var entryNodes = graph.Nodes.Where(n =>
            n.Type is NodeType.DataSource or NodeType.Parameter &&
            !graph.Connections.Any(c => c.TargetNodeId == n.Id)).ToList();

        if (entryNodes.Count == 0)
        {
            errors.Add(new ValidationError("GRAPH_003", "Strategy must have at least one data source or parameter node."));
        }

        // Check for output nodes
        var outputNodes = graph.Nodes.Where(n =>
            n.Type is NodeType.Signal or NodeType.Action).ToList();

        if (outputNodes.Count == 0)
        {
            errors.Add(new ValidationError("GRAPH_004", "Strategy must have at least one signal or action node."));
        }

        // Check for cycles using graph validation
        var graphValidation = graph.Validate();
        errors.AddRange(graphValidation.Errors.Select(e => new ValidationError("GRAPH_CYCLE", e)));
        warnings.AddRange(graphValidation.Warnings.Select(w => new ValidationWarning("GRAPH_W01", w)));

        // Check for disconnected subgraphs
        var connectedNodes = GetConnectedNodes(graph);
        var disconnectedNodes = graph.Nodes.Where(n =>
            !connectedNodes.Contains(n.Id) &&
            n.Type != NodeType.Comment).ToList();

        if (disconnectedNodes.Count > 0)
        {
            var nodeNames = string.Join(", ", disconnectedNodes.Select(n => n.Name));
            warnings.Add(new ValidationWarning("GRAPH_W02", $"Disconnected nodes found: {nodeNames}"));
        }
    }

    private void ValidateNodes(NodeGraph graph, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        foreach (var node in graph.Nodes)
        {
            // Validate required inputs are connected
            foreach (var port in node.InputPorts.Where(p => p.IsRequired))
            {
                var isConnected = graph.Connections.Any(c =>
                    c.TargetNodeId == node.Id && c.TargetPortId == port.Id);

                if (!isConnected && port.DefaultValue is null)
                {
                    errors.Add(new ValidationError(
                        "NODE_001",
                        $"Required input '{port.Name}' on node '{node.Name}' is not connected and has no default value."));
                }
            }

            // Validate indicator nodes
            if (node.Type == NodeType.Indicator)
            {
                var indicator = _indicatorLibrary.GetIndicator(node.Name);
                if (indicator is null)
                {
                    errors.Add(new ValidationError(
                        "NODE_002",
                        $"Unknown indicator '{node.Name}' on node."));
                }
                else
                {
                    // Validate indicator parameters
                    foreach (var paramDef in indicator.Parameters)
                    {
                        if (node.Configuration.TryGetValue(paramDef.Name, out var value) && value is not null)
                        {
                            ValidateParameterValue(paramDef, value, node.Name, errors);
                        }
                    }
                }
            }

            // Validate logic nodes
            if (node.Type == NodeType.Logic)
            {
                var comparison = node.Configuration.GetValueOrDefault("comparison")?.ToString();
                if (!Enum.TryParse<ComparisonType>(comparison, out _))
                {
                    errors.Add(new ValidationError(
                        "NODE_003",
                        $"Invalid comparison type '{comparison}' on node '{node.Name}'."));
                }
            }

            // Validate math nodes
            if (node.Type == NodeType.Math)
            {
                var operation = node.Configuration.GetValueOrDefault("operation")?.ToString();
                if (!Enum.TryParse<MathOperation>(operation, out _))
                {
                    errors.Add(new ValidationError(
                        "NODE_004",
                        $"Invalid math operation '{operation}' on node '{node.Name}'."));
                }
            }

            // Validate node positions (for UI)
            if (node.Position.X < -10000 || node.Position.X > 10000 ||
                node.Position.Y < -10000 || node.Position.Y > 10000)
            {
                warnings.Add(new ValidationWarning(
                    "NODE_W01",
                    $"Node '{node.Name}' has extreme position coordinates."));
            }
        }
    }

    private void ValidateParameters(StrategyDefinition strategy, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        var paramNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var param in strategy.Parameters)
        {
            if (string.IsNullOrWhiteSpace(param.Name))
            {
                errors.Add(new ValidationError("PARAM_001", "Parameter name is required."));
                continue;
            }

            if (!paramNames.Add(param.Name))
            {
                errors.Add(new ValidationError("PARAM_002", $"Duplicate parameter name: {param.Name}"));
            }

            // Validate parameter ranges
            if (param.MinValue is not null && param.MaxValue is not null)
            {
                try
                {
                    var min = Convert.ToDecimal(param.MinValue);
                    var max = Convert.ToDecimal(param.MaxValue);
                    if (min > max)
                    {
                        errors.Add(new ValidationError("PARAM_003", $"Parameter '{param.Name}' has min > max."));
                    }

                    if (param.DefaultValue is not null)
                    {
                        var def = Convert.ToDecimal(param.DefaultValue);
                        if (def < min || def > max)
                        {
                            errors.Add(new ValidationError("PARAM_004", $"Parameter '{param.Name}' default value is outside min/max range."));
                        }
                    }
                }
                catch
                {
                    // Non-numeric parameters don't need range validation
                }
            }

            // Validate step is positive
            if (param.Step is not null && param.IsOptimizable)
            {
                try
                {
                    var step = Convert.ToDecimal(param.Step);
                    if (step <= 0)
                    {
                        errors.Add(new ValidationError("PARAM_005", $"Parameter '{param.Name}' step must be positive."));
                    }
                }
                catch
                {
                    // Non-numeric step
                }
            }
        }
    }

    private void ValidateRiskSettings(RiskSettings settings, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        if (settings.MaxPositionSizePercent <= 0 || settings.MaxPositionSizePercent > 1)
        {
            errors.Add(new ValidationError("RISK_001", "Max position size must be between 0 and 100%."));
        }

        if (settings.MaxPositionSizePercent > 0.25m)
        {
            warnings.Add(new ValidationWarning("RISK_W01", "Max position size > 25% is aggressive."));
        }

        if (settings.MaxConcurrentPositions <= 0 || settings.MaxConcurrentPositions > 1000)
        {
            errors.Add(new ValidationError("RISK_002", "Max concurrent positions must be between 1 and 1000."));
        }

        if (settings.MaxDailyLossPercent <= 0 || settings.MaxDailyLossPercent > 0.5m)
        {
            errors.Add(new ValidationError("RISK_003", "Max daily loss must be between 0 and 50%."));
        }

        if (settings.MaxDrawdownPercent <= 0 || settings.MaxDrawdownPercent > 1)
        {
            errors.Add(new ValidationError("RISK_004", "Max drawdown must be between 0 and 100%."));
        }

        if (settings.DefaultStopLossPercent < settings.DefaultTakeProfitPercent * 0.3m)
        {
            warnings.Add(new ValidationWarning("RISK_W02", "Stop loss is very tight relative to take profit. Risk/reward may be unfavorable."));
        }

        if (settings.UseTrailingStop)
        {
            if (settings.TrailingStopActivationPercent <= 0)
            {
                errors.Add(new ValidationError("RISK_005", "Trailing stop activation percent must be positive."));
            }

            if (settings.TrailingStopDistancePercent <= 0)
            {
                errors.Add(new ValidationError("RISK_006", "Trailing stop distance percent must be positive."));
            }

            if (settings.TrailingStopDistancePercent >= settings.TrailingStopActivationPercent)
            {
                warnings.Add(new ValidationWarning("RISK_W03", "Trailing stop distance >= activation may cause immediate stop-out."));
            }
        }
    }

    private void ValidateParameterValue(ParameterDefinition paramDef, object value, string nodeName, List<ValidationError> errors)
    {
        try
        {
            var numValue = Convert.ToDecimal(value);

            if (paramDef.MinValue is not null)
            {
                var min = Convert.ToDecimal(paramDef.MinValue);
                if (numValue < min)
                {
                    errors.Add(new ValidationError(
                        "NODE_PARAM_001",
                        $"Parameter '{paramDef.Name}' on '{nodeName}' is below minimum value ({min})."));
                }
            }

            if (paramDef.MaxValue is not null)
            {
                var max = Convert.ToDecimal(paramDef.MaxValue);
                if (numValue > max)
                {
                    errors.Add(new ValidationError(
                        "NODE_PARAM_002",
                        $"Parameter '{paramDef.Name}' on '{nodeName}' is above maximum value ({max})."));
                }
            }
        }
        catch
        {
            // Non-numeric parameter, skip range validation
        }
    }

    private static HashSet<string> GetConnectedNodes(NodeGraph graph)
    {
        var connected = new HashSet<string>();

        // Start from entry nodes
        var entryNodes = graph.Nodes
            .Where(n => n.Type is NodeType.DataSource or NodeType.Parameter)
            .Select(n => n.Id);

        var queue = new Queue<string>(entryNodes);

        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            if (!connected.Add(nodeId)) continue;

            // Find all nodes connected from this one
            var outgoing = graph.Connections.Where(c => c.SourceNodeId == nodeId);
            foreach (var connection in outgoing)
            {
                if (!connected.Contains(connection.TargetNodeId))
                {
                    queue.Enqueue(connection.TargetNodeId);
                }
            }
        }

        return connected;
    }

    private static (bool Success, IEnumerable<Diagnostic> Diagnostics, Assembly? Assembly) CompileCode(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();

        // Add common references that might not be loaded
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location) ?? string.Empty;
        var additionalRefs = new[]
        {
            "System.Runtime.dll",
            "System.Collections.dll",
            "System.Linq.dll",
            "netstandard.dll"
        };

        foreach (var refName in additionalRefs)
        {
            var refPath = Path.Combine(runtimeDir, refName);
            if (File.Exists(refPath) && !references.Any(r => r.Display?.Contains(refName) == true))
            {
                references.Add(MetadataReference.CreateFromFile(refPath));
            }
        }

        var compilation = CSharpCompilation.Create(
            $"GeneratedStrategy_{Guid.NewGuid():N}",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            return (false, result.Diagnostics, null);
        }

        ms.Seek(0, SeekOrigin.Begin);
#if NET461
        var assembly = Assembly.Load(ms.ToArray());
#else
        var assembly = AssemblyLoadContext.Default.LoadFromStream(ms);
#endif

        return (true, result.Diagnostics, assembly);
    }
}

/// <summary>
/// Result of strategy validation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>Gets or sets whether validation passed.</summary>
    public bool IsValid { get; set; }

    /// <summary>Gets or sets validation errors.</summary>
    public IReadOnlyList<ValidationError> Errors { get; set; } = Array.Empty<ValidationError>();

    /// <summary>Gets or sets validation warnings.</summary>
    public IReadOnlyList<ValidationWarning> Warnings { get; set; } = Array.Empty<ValidationWarning>();
}

/// <summary>
/// Validation error.
/// </summary>
public sealed class ValidationError
{
    /// <summary>Gets or sets the error code.</summary>
    public string Code { get; }

    /// <summary>Gets or sets the error message.</summary>
    public string Message { get; }

    public ValidationError(string code, string message)
    {
        Code = code;
        Message = message;
    }
}

/// <summary>
/// Validation warning.
/// </summary>
public sealed class ValidationWarning
{
    /// <summary>Gets or sets the warning code.</summary>
    public string Code { get; }

    /// <summary>Gets or sets the warning message.</summary>
    public string Message { get; }

    public ValidationWarning(string code, string message)
    {
        Code = code;
        Message = message;
    }
}

/// <summary>
/// Result of strategy compilation.
/// </summary>
public sealed class CompilationResult
{
    /// <summary>Gets or sets whether compilation succeeded.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Gets or sets the generated code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the class name.</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Gets or sets the namespace.</summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>Gets or sets the compiled assembly.</summary>
    public Assembly? Assembly { get; set; }

    /// <summary>Gets or sets validation errors.</summary>
    public List<string> ValidationErrors { get; set; } = new();

    /// <summary>Gets or sets compilation errors.</summary>
    public List<string> CompilationErrors { get; set; } = new();

    /// <summary>Gets all errors.</summary>
    public IEnumerable<string> AllErrors => ValidationErrors.Concat(CompilationErrors);
}
