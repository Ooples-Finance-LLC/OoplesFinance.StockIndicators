using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace OoplesFinance.StockIndicators.SourceGeneration;

/// <summary>
/// Emits one <c>IIndicator</c> type per options type, so a caller writes <c>new Rsi(14)</c> rather than
/// naming an indicator with a string.
/// </summary>
/// <remarks>
/// <para>
/// Read from the <c>*SpecOptions</c> constructors rather than from the <c>IndicatorName</c> enum, because the
/// enum knows only that an indicator exists. <c>IndicatorCatalogGenerator</c> reads the enum and can therefore
/// only ever offer a single <c>int? length</c>; the options constructors carry the real parameter lists -
/// <c>BollingerBandsSpecOptions(int, double, MovingAvgType)</c>, <c>MacdSpecOptions(int, int, int)</c> - which
/// is what makes a faithful typed surface possible at all.
/// </para>
/// <para>
/// Three readings feed one emit: the options constructors for parameters, <c>BuilderArmTargets</c> for which
/// batch indicator and output each options type stands for, and the <c>[Category]</c> attribute on each
/// <c>IndicatorName</c> member for which category interface the type implements.
/// </para>
/// </remarks>
[Generator]
public class IndicatorTypeGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Indicators written by hand, which the emitter must not duplicate.
    /// </summary>
    /// <remarks>
    /// These are the specimens the generated output is held to. They stay hand-written until the emitted form
    /// is shown to match them, at which point they are deleted and their tests keep running against generated
    /// code - which is the only way to know the generator reproduces the shape rather than something near it.
    /// </remarks>
    private static readonly HashSet<string> HandWritten = new(StringComparer.Ordinal)
    {
        "Sma", "Ema", "BollingerBands"
    };

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var optionsTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsOptionsType(node),
                transform: static (ctx, _) => ReadOptionsType(ctx.Node))
            .Where(static x => x is not null)
            .Collect();

        var armTargets = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsArmTargetEntry(node),
                transform: static (ctx, _) => ReadArmTarget(ctx.Node))
            .Where(static x => x is not null)
            .Collect();

        var categories = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is EnumDeclarationSyntax e
                    && e.Identifier.Text == "IndicatorName",
                transform: static (ctx, _) => ReadCategories((EnumDeclarationSyntax)ctx.Node))
            .Where(static x => x is not null)
            .Collect();

        var combined = optionsTypes.Combine(armTargets).Combine(categories);

        context.RegisterSourceOutput(combined, static (spc, source) =>
            Emit(source.Left.Left, source.Left.Right, source.Right, spc));
    }

    private static bool IsOptionsType(SyntaxNode node) =>
        node is ClassDeclarationSyntax c
        && c.Identifier.Text.EndsWith("SpecOptions", StringComparison.Ordinal)
        && c.BaseList is not null
        && c.BaseList.Types.Any(t => t.Type.ToString() == "IIndicatorSpecOptions")
        && !IsObsolete(c);

    /// <summary>
    /// A deprecated options type gets no indicator.
    /// </summary>
    /// <remarks>
    /// Emitting one is a CS0618 build error, because the generated file has no file-wide suppression the way
    /// <c>IndicatorCompute.cs</c> does - and suppressing it would be the wrong fix anyway. These are deprecated
    /// because they compute the wrong thing: <c>ComparePriceMomentumOscillator</c> compares a stock against a
    /// market series and cannot be computed from one series at all, so a typed indicator for it would offer a
    /// caller something that cannot work.
    /// </remarks>
    private static bool IsObsolete(ClassDeclarationSyntax declaration) =>
        declaration.AttributeLists.Any(list => list.Attributes.Any(attribute =>
        {
            var name = attribute.Name.ToString();
            return name == "Obsolete" || name == "ObsoleteAttribute"
                || name.EndsWith(".Obsolete", StringComparison.Ordinal)
                || name.EndsWith(".ObsoleteAttribute", StringComparison.Ordinal);
        }));

    /// <summary>
    /// The widest constructor is the canonical one: the narrower overloads exist to default a parameter the
    /// widest one takes, so reading a narrow one would silently drop a knob the caller should have.
    /// </summary>
    private static OptionsReading? ReadOptionsType(SyntaxNode node)
    {
        var declaration = (ClassDeclarationSyntax)node;

        ConstructorDeclarationSyntax? widest = null;
        foreach (var member in declaration.Members)
        {
            if (member is not ConstructorDeclarationSyntax ctor)
            {
                continue;
            }

            // A constructor that chains to another with ": this(...)" is one of the narrower overloads.
            if (ctor.Initializer is not null
                && ctor.Initializer.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword))
            {
                continue;
            }

            if (widest is null || ctor.ParameterList.Parameters.Count > widest.ParameterList.Parameters.Count)
            {
                widest = ctor;
            }
        }

        var parameters = new List<ParameterReading>();
        if (widest is not null)
        {
            foreach (var parameter in widest.ParameterList.Parameters)
            {
                if (parameter.Type is null)
                {
                    continue;
                }

                parameters.Add(new ParameterReading(
                    parameter.Type.ToString(),
                    parameter.Identifier.Text,
                    parameter.Default?.Value.ToString()));
            }
        }

        return new OptionsReading(declaration.Identifier.Text, parameters);
    }

    private static bool IsArmTargetEntry(SyntaxNode node) =>
        node is InitializerExpressionSyntax init
        && init.Parent is ObjectCreationExpressionSyntax creation
        && creation.Type.ToString().Contains("Dictionary<Type, BuilderArmTarget>");

    /// <summary>
    /// Reads the <c>[typeof(XSpecOptions)] = new(IndicatorName.Y, "Key")</c> entries that say what each
    /// options type actually computes.
    /// </summary>
    private static ImmutableArray<ArmTargetReading>? ReadArmTarget(SyntaxNode node)
    {
        var initializer = (InitializerExpressionSyntax)node;
        var readings = ImmutableArray.CreateBuilder<ArmTargetReading>();

        foreach (var expression in initializer.Expressions)
        {
            if (expression is not AssignmentExpressionSyntax assignment)
            {
                continue;
            }

            if (assignment.Left is not ImplicitElementAccessSyntax access
                || access.ArgumentList.Arguments.Count != 1)
            {
                continue;
            }

            if (access.ArgumentList.Arguments[0].Expression is not TypeOfExpressionSyntax typeOf)
            {
                continue;
            }

            var optionsType = typeOf.Type.ToString();
            string? indicatorName = null;
            string? outputKey = null;

            if (assignment.Right is BaseObjectCreationExpressionSyntax creation
                && creation.ArgumentList is not null
                && creation.ArgumentList.Arguments.Count > 0)
            {
                var first = creation.ArgumentList.Arguments[0].Expression.ToString();
                const string prefix = "IndicatorName.";
                if (first.StartsWith(prefix, StringComparison.Ordinal))
                {
                    indicatorName = first.Substring(prefix.Length);
                }

                if (creation.ArgumentList.Arguments.Count > 1
                    && creation.ArgumentList.Arguments[1].Expression is LiteralExpressionSyntax literal
                    && literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    outputKey = literal.Token.ValueText;
                }
            }

            if (indicatorName is not null)
            {
                readings.Add(new ArmTargetReading(optionsType, indicatorName, outputKey));
            }
        }

        return readings.Count > 0 ? readings.ToImmutable() : null;
    }

    /// <summary>Reads the category each indicator was tagged with, which decides its interface.</summary>
    private static ImmutableArray<CategoryReading>? ReadCategories(EnumDeclarationSyntax declaration)
    {
        var readings = ImmutableArray.CreateBuilder<CategoryReading>();

        foreach (var member in declaration.Members)
        {
            foreach (var list in member.AttributeLists)
            {
                foreach (var attribute in list.Attributes)
                {
                    if (attribute.Name.ToString() != "Category"
                        || attribute.ArgumentList is null
                        || attribute.ArgumentList.Arguments.Count == 0)
                    {
                        continue;
                    }

                    var value = attribute.ArgumentList.Arguments[0].Expression.ToString();
                    const string prefix = "IndicatorType.";
                    if (value.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        readings.Add(new CategoryReading(member.Identifier.Text, value.Substring(prefix.Length)));
                    }
                }
            }
        }

        return readings.Count > 0 ? readings.ToImmutable() : null;
    }

    private static void Emit(
        ImmutableArray<OptionsReading?> optionsTypes,
        ImmutableArray<ImmutableArray<ArmTargetReading>?> armTargets,
        ImmutableArray<ImmutableArray<CategoryReading>?> categories,
        SourceProductionContext context)
    {
        var targets = new Dictionary<string, ArmTargetReading>(StringComparer.Ordinal);
        foreach (var batch in armTargets)
        {
            if (batch is null)
            {
                continue;
            }

            foreach (var reading in batch)
            {
                targets[reading.OptionsType] = reading;
            }
        }

        var categoryOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var batch in categories)
        {
            if (batch is null)
            {
                continue;
            }

            foreach (var reading in batch)
            {
                categoryOf[reading.IndicatorName] = reading.Category;
            }
        }

        if (targets.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using OoplesFinance.StockIndicators.Builder.Specs;");
        builder.AppendLine("using OoplesFinance.StockIndicators.Enums;");
        builder.AppendLine();
        builder.AppendLine("namespace OoplesFinance.StockIndicators.Indicators;");

        var emitted = 0;
        foreach (var options in optionsTypes)
        {
            if (options is null || options.Parameters.Count == 0)
            {
                continue;
            }

            var typeName = options.Name.Substring(0, options.Name.Length - "SpecOptions".Length);
            if (HandWritten.Contains(typeName) || !targets.TryGetValue(options.Name, out var target))
            {
                continue;
            }

            var interfaces = new List<string> { "IBuiltInIndicator" };
            if (categoryOf.TryGetValue(target.IndicatorName, out var category))
            {
                interfaces.Insert(0, CategoryInterface(category));
            }

            builder.AppendLine();
            builder.AppendLine("/// <summary>" + Escape(typeName) + ".</summary>");
            builder.AppendLine("public sealed class " + typeName + " : Indicator, " + string.Join(", ", interfaces));
            builder.AppendLine("{");

            var signature = string.Join(", ", options.Parameters.Select(p =>
                p.Type + " " + p.Name + (p.Default is null ? string.Empty : " = " + p.Default)));

            builder.AppendLine("    /// <summary>Creates " + Escape(typeName) + ".</summary>");
            builder.AppendLine("    public " + typeName + "(" + signature + ")");
            builder.AppendLine("    {");
            foreach (var parameter in options.Parameters)
            {
                builder.AppendLine("        " + Capitalise(parameter.Name) + " = " + parameter.Name + ";");
            }

            builder.AppendLine("    }");

            foreach (var parameter in options.Parameters)
            {
                builder.AppendLine();
                builder.AppendLine("    /// <summary>The " + Escape(parameter.Name) + ".</summary>");
                builder.AppendLine("    public " + parameter.Type + " " + Capitalise(parameter.Name) + " { get; }");
            }

            var lengthParameter = options.Parameters.FirstOrDefault(p =>
                p.Type == "int" && p.Name.IndexOf("ength", StringComparison.Ordinal) >= 0);
            if (lengthParameter is not null)
            {
                builder.AppendLine();
                builder.AppendLine("    /// <inheritdoc/>");
                builder.AppendLine("    public override int WarmupBars => " + Capitalise(lengthParameter.Name) + ";");
            }

            builder.AppendLine();
            builder.AppendLine("    IndicatorName IBuiltInIndicator.BatchName => IndicatorName."
                + target.IndicatorName + ";");
            builder.AppendLine();
            builder.AppendLine("    string? IBuiltInIndicator.BatchOutputKey => "
                + (target.OutputKey is null ? "null" : "\"" + target.OutputKey + "\"") + ";");
            builder.AppendLine();
            builder.AppendLine("    IIndicatorSpecOptions IBuiltInIndicator.CreateOptions() => new "
                + options.Name + "(" + string.Join(", ", options.Parameters.Select(p => Capitalise(p.Name)))
                + ");");
            builder.AppendLine("}");
            emitted++;
        }

        if (emitted == 0)
        {
            return;
        }

        context.AddSource("Indicators.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    private static string CategoryInterface(string category) => category switch
    {
        "Trend" => "ITrendIndicator",
        "Momentum" => "IMomentumIndicator",
        "Volatility" => "IVolatilityIndicator",
        "Volume" => "IVolumeIndicator",
        "Cycle" => "ICycleIndicator",
        "SupportAndResistance" => "ISupportAndResistanceIndicator",
        _ => "IIndicator"
    };

    private static string Capitalise(string name) =>
        name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);

    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private sealed class OptionsReading
    {
        public OptionsReading(string name, List<ParameterReading> parameters)
        {
            Name = name;
            Parameters = parameters;
        }

        public string Name { get; }

        public List<ParameterReading> Parameters { get; }
    }

    private sealed class ParameterReading
    {
        public ParameterReading(string type, string name, string? defaultValue)
        {
            Type = type;
            Name = name;
            Default = defaultValue;
        }

        public string Type { get; }

        public string Name { get; }

        public string? Default { get; }
    }

    private sealed class ArmTargetReading
    {
        public ArmTargetReading(string optionsType, string indicatorName, string? outputKey)
        {
            OptionsType = optionsType;
            IndicatorName = indicatorName;
            OutputKey = outputKey;
        }

        public string OptionsType { get; }

        public string IndicatorName { get; }

        public string? OutputKey { get; }
    }

    private sealed class CategoryReading
    {
        public CategoryReading(string indicatorName, string category)
        {
            IndicatorName = indicatorName;
            Category = category;
        }

        public string IndicatorName { get; }

        public string Category { get; }
    }
}
