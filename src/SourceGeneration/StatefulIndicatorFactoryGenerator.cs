using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace OoplesFinance.StockIndicators.SourceGeneration;

/// <summary>
/// Roslyn source generator that creates the StatefulIndicatorFactory.Generated.g.cs file.
/// Scans all classes implementing IStreamingIndicatorState and generates factory methods
/// for creating indicator states from IndicatorName enum values and GenericIndicatorOptions.
/// </summary>
[Generator]
public class StatefulIndicatorFactoryGenerator : IIncrementalGenerator
{
    // Multi-stock indicators that require special handling (SeriesKey parameters)
    // These are NOT generated and must be handled manually
    private static readonly HashSet<string> MultiStockIndicators = new(StringComparer.OrdinalIgnoreCase)
    {
        "RSMKIndicator",
        "ComparePriceMomentumOscillator",
        "KaufmanStressIndicator",
        "RelativeNormalizedVolatility",
        "RelativeStrength3DIndicator",
        "SectorRotationModel"
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all class declarations that might implement IStreamingIndicatorState
        var stateClassDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCandidateClass(s),
                transform: static (ctx, _) => GetStateClassInfo(ctx))
            .Where(static info => info is not null);

        // Combine with compilation
        var compilationAndClasses = context.CompilationProvider.Combine(stateClassDeclarations.Collect());

        // Generate source
        context.RegisterSourceOutput(compilationAndClasses, static (spc, source) => Execute(source.Left, source.Right!, spc));
    }

    private static bool IsCandidateClass(SyntaxNode node)
    {
        // Look for classes that end with "State" and are public sealed
        if (node is ClassDeclarationSyntax classDecl)
        {
            var name = classDecl.Identifier.Text;
            return name.EndsWith("State", StringComparison.Ordinal) &&
                   classDecl.Modifiers.Any(m => m.Text == "public") &&
                   classDecl.Modifiers.Any(m => m.Text == "sealed");
        }
        return false;
    }

    private static StateClassInfo? GetStateClassInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl);

        if (symbol is not INamedTypeSymbol namedType)
        {
            return null;
        }

        // Check if it implements IStreamingIndicatorState
        var implementsInterface = namedType.AllInterfaces.Any(i =>
            i.Name == "IStreamingIndicatorState" &&
            i.ContainingNamespace?.ToDisplayString() == "OoplesFinance.StockIndicators.Streaming");

        if (!implementsInterface)
        {
            return null;
        }

        // Get the Name property to find the IndicatorName value
        var nameProperty = namedType.GetMembers("Name")
            .OfType<IPropertySymbol>()
            .FirstOrDefault();

        if (nameProperty is null)
        {
            return null;
        }

        // Try to extract the IndicatorName from the property getter
        string? indicatorName = null;
        foreach (var syntaxRef in nameProperty.DeclaringSyntaxReferences)
        {
            var propSyntax = syntaxRef.GetSyntax() as PropertyDeclarationSyntax;
            // Only a literal IndicatorName.X names a fixed indicator. Any other member access - a
            // wrapper forwarding _inner.Name, for instance - is not an indicator of its own, and
            // reading its member name as one generated a factory entry for IndicatorName.Name.
            if (propSyntax?.ExpressionBody?.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Expression is IdentifierNameSyntax { Identifier.Text: "IndicatorName" })
            {
                indicatorName = memberAccess.Name.Identifier.Text;
                break;
            }
        }

        if (indicatorName is null)
        {
            return null;
        }

        // Skip multi-stock indicators
        if (MultiStockIndicators.Contains(indicatorName))
        {
            return null;
        }

        // Get constructor information
        var constructors = namedType.Constructors
            .Where(c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public)
            .Select(c => new ConstructorInfo
            {
                Parameters = c.Parameters.Select(p => new ParameterInfo
                {
                    Name = p.Name,
                    TypeName = p.Type.ToDisplayString(),
                    HasDefaultValue = p.HasExplicitDefaultValue,
                    DefaultValue = p.HasExplicitDefaultValue ? GetDefaultValueString(p) : null
                }).ToList()
            })
            .ToList();

        // Find the best constructor (prefer the one with most defaultable parameters)
        var bestConstructor = constructors
            .OrderByDescending(c => c.Parameters.Count(p => p.HasDefaultValue))
            .ThenBy(c => c.Parameters.Count)
            .FirstOrDefault();

        if (bestConstructor is null)
        {
            return null;
        }

        return new StateClassInfo
        {
            ClassName = namedType.Name,
            FullTypeName = namedType.ToDisplayString(),
            IndicatorName = indicatorName,
            Constructor = bestConstructor
        };
    }

    private static string? GetDefaultValueString(IParameterSymbol param)
    {
        if (!param.HasExplicitDefaultValue)
        {
            return null;
        }

        var defaultValue = param.ExplicitDefaultValue;

        return defaultValue switch
        {
            null => "null",
            int i => i.ToString(CultureInfo.InvariantCulture),
            double d => FormatDoubleLiteral(d),
            float f => f.ToString("G9", CultureInfo.InvariantCulture) + "f",
            bool b => b ? "true" : "false",
            string s => $"\"{s}\"",
            _ when param.Type.TypeKind == TypeKind.Enum => $"{param.Type.ToDisplayString()}.{defaultValue}",
            _ => defaultValue.ToString()
        };
    }

    /// <summary>
    /// Renders a double as a C# literal, appending ".0" only where that is actually valid.
    /// </summary>
    /// <remarks>
    /// The suffix exists to make an integral value a double literal rather than an int. G17 however
    /// switches to exponent form once the magnitude reaches about 1E17, and "1E+17.0" is not valid
    /// C# - it would break the generated factory. An exponent already makes the literal a double,
    /// so the suffix is needed only when the text carries neither a decimal point nor an exponent.
    /// No indicator default is currently large enough to reach this, so it guards a latent break
    /// rather than fixing a live one.
    /// </remarks>
    private static string FormatDoubleLiteral(double value)
    {
        var text = value.ToString("G17", CultureInfo.InvariantCulture);
        var alreadyDouble = text.IndexOf('.') >= 0
            || text.IndexOf('E') >= 0
            || text.IndexOf('e') >= 0;

        return alreadyDouble ? text : text + ".0";
    }

    private static void Execute(Compilation compilation, ImmutableArray<StateClassInfo?> stateClasses, SourceProductionContext context)
    {
        var validClasses = stateClasses
            .Where(c => c is not null)
            .Cast<StateClassInfo>()
            .GroupBy(c => c.IndicatorName) // Deduplicate by indicator name
            .Select(g => g.First())
            .OrderBy(c => c.IndicatorName)
            .ToList();

        if (validClasses.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using OoplesFinance.StockIndicators.Builder.Specs;");
        sb.AppendLine("using OoplesFinance.StockIndicators.Enums;");
        sb.AppendLine("using OoplesFinance.StockIndicators.Streaming;");
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine("namespace OoplesFinance.StockIndicators.Builder;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Auto-generated factory methods for creating StatefulIndicator instances.");
        sb.AppendLine($"/// Generated {validClasses.Count} indicator mappings.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static partial class StatefulIndicatorFactory");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Auto-generated method that creates StatefulIndicator from GenericIndicatorOptions.");
        sb.AppendLine("    /// This replaces the hand-written CreateFromGenericOptions method.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    private static IStreamingIndicatorState CreateFromGenericOptionsGenerated(IndicatorName name, GenericIndicatorOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        var p = options.Parameters;");
        sb.AppendLine();
        sb.AppendLine("        return name switch");
        sb.AppendLine("        {");

        foreach (var stateClass in validClasses)
        {
            var instantiation = GenerateInstantiation(stateClass);
            sb.AppendLine($"            IndicatorName.{stateClass.IndicatorName} => {instantiation},");
        }

        sb.AppendLine();
        sb.AppendLine("            _ => throw new NotSupportedException($\"Indicator '{name}' is not yet supported in V2. Please add V2-native StatefulIndicator implementation.\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Add helper methods
        sb.AppendLine("    // Helper methods for parameter extraction");
        sb.AppendLine("    private static int GetInt(object[] p, int idx, int defaultValue)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (idx >= p.Length) return defaultValue;");
        sb.AppendLine("        return p[idx] switch");
        sb.AppendLine("        {");
        sb.AppendLine("            int i => i,");
        sb.AppendLine("            double d => (int)d,");
        sb.AppendLine("            long l => (int)l,");
        sb.AppendLine("            _ => defaultValue");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static double GetDouble(object[] p, int idx, double defaultValue)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (idx >= p.Length) return defaultValue;");
        sb.AppendLine("        return p[idx] switch");
        sb.AppendLine("        {");
        sb.AppendLine("            double d => d,");
        sb.AppendLine("            int i => i,");
        sb.AppendLine("            float f => f,");
        sb.AppendLine("            long l => l,");
        sb.AppendLine("            _ => defaultValue");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("StatefulIndicatorFactory.Generated.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static string GenerateInstantiation(StateClassInfo stateClass)
    {
        var ctor = stateClass.Constructor;

        // No parameters - simple instantiation
        if (ctor.Parameters.Count == 0)
        {
            return $"new {stateClass.ClassName}()";
        }

        // Collect numeric parameters we want to pass
        var numericParams = new List<(ParameterInfo Param, int Index)>();
        var paramIndex = 0;
        bool hasSkippedParams = false;

        foreach (var param in ctor.Parameters)
        {
            var typeName = param.TypeName;

            // Skip InputName and selector parameters - use defaults
            if (typeName.Contains("InputName") || typeName.Contains("Func<"))
            {
                hasSkippedParams = true;
                continue;
            }

            // Handle MovingAvgType - skip (use default)
            if (typeName.Contains("MovingAvgType"))
            {
                hasSkippedParams = true;
                continue;
            }

            // Only process numeric parameters
            if (typeName == "int" || typeName == "System.Int32" ||
                typeName == "double" || typeName == "System.Double")
            {
                numericParams.Add((param, paramIndex));
                paramIndex++;
            }
            else
            {
                // Skip other non-numeric types
                hasSkippedParams = true;
            }
        }

        if (numericParams.Count == 0)
        {
            return $"new {stateClass.ClassName}()";
        }

        // Generate parameter strings
        var paramStrings = new List<string>();

        // Use named parameters if:
        // 1. We skipped any params (MovingAvgType, InputName, Func, etc.) anywhere in the constructor
        // 2. The first numeric param is not the first param in the constructor
        // This ensures we skip over MovingAvgType in any position (first, middle, or last)
        var firstNumericParam = numericParams[0].Param;
        var firstCtorParam = ctor.Parameters.FirstOrDefault();
        bool needsNamedParams = hasSkippedParams || (firstCtorParam != null && firstCtorParam != firstNumericParam);

        foreach (var (param, idx) in numericParams)
        {
            var typeName = param.TypeName;
            var defaultVal = param.DefaultValue;
            var paramName = param.Name;

            string valueExpr;
            if (typeName == "int" || typeName == "System.Int32")
            {
                var defaultInt = 14;
                if (defaultVal is not null)
                {
                    // Try to parse as int
                    if (int.TryParse(defaultVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    {
                        defaultInt = parsed;
                    }
                    else if (double.TryParse(defaultVal, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble))
                    {
                        defaultInt = (int)parsedDouble;
                    }
                }
                valueExpr = $"GetInt(p, {idx}, {defaultInt})";
            }
            else // double
            {
                var defaultDouble = defaultVal ?? "0.0";
                // Ensure it has decimal point for C#
                if (!defaultDouble.Contains(".") && !defaultDouble.Contains("E"))
                {
                    defaultDouble += ".0";
                }
                valueExpr = $"GetDouble(p, {idx}, {defaultDouble})";
            }

            if (needsNamedParams)
            {
                paramStrings.Add($"{paramName}: {valueExpr}");
            }
            else
            {
                paramStrings.Add(valueExpr);
            }
        }

        return $"new {stateClass.ClassName}({string.Join(", ", paramStrings)})";
    }

    private sealed class StateClassInfo
    {
        public string ClassName { get; set; } = "";
        public string FullTypeName { get; set; } = "";
        public string IndicatorName { get; set; } = "";
        public ConstructorInfo Constructor { get; set; } = new();
    }

    private sealed class ConstructorInfo
    {
        public List<ParameterInfo> Parameters { get; set; } = new();
    }

    private sealed class ParameterInfo
    {
        public string Name { get; set; } = "";
        public string TypeName { get; set; } = "";
        public bool HasDefaultValue { get; set; }
        public string? DefaultValue { get; set; }
    }
}
