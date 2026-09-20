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
    // Members of MovingAvgType that smooth something other than price. Ehlers's Noise Elimination
    // Technology is a real smoother - it is what he applies inside the Market Meanness Index - but it
    // normalises a count of pairwise sign comparisons by 0.5 * n * (n - 1), so its output is a rank
    // statistic in [-1, 1] whatever the input is worth. It belongs on an oscillator or another bounded
    // series. IMovingAverage is what a caller hands to anything asking for a price average, so it stays
    // out of that surface and keeps its MovingAvgType member, which is how the batch still reaches it.
    private static readonly HashSet<string> NotAnAverage = new(StringComparer.Ordinal)
    {
        "EhlersNoiseEliminationTechnology",
    };

    // Indicators whose output needs more than `length` bars to mean anything, written as an expression in
    // $, the longest length the indicator declares.
    // Keys are IndicatorName members, not type names: a key naming no indicator is a silent no-op, and
    // WarmupBarsCoversTheWarmup is what catches the gap it leaves.
    //
    // The three exact rules are compositions of finite windows, so they are arithmetic, not estimates: a
    // triangular average is an average of an average, a Farey-weighted average spans the same two windows,
    // and a Hull average runs a length window and then a sqrt(length) one over it.
    //
    // The rest are recursions, which only approach their input. Each multiple is the bar the filter first
    // stays within 1e-6 of a constant series over 4000 bars, divided by the length it declares and rounded
    // up - measured at default parameters. That is the bar a step input settles by, not a promise for every
    // series: the adaptive ones (Ama, the Kaufman and deviation-scaled filters) slow down or speed up with
    // the data, which is why their multiples are the large ones.
    private static readonly Dictionary<string, string> WarmupRule = new(StringComparer.Ordinal)
    {
        // Exact.
        ["TriangularMovingAverage"] = "(2 * $) - 2",
        ["FareySequenceWeightedMovingAverage"] = "(2 * $) - 1",
        ["HullMovingAverage"] = "$ + (int)System.Math.Ceiling(System.Math.Sqrt($))",

        // Measured. 1763 / 20 = 88.2, 988 / 14 = 70.6, 784 / 20 = 39.2, and so on down.
        ["EhlersDeviationScaledMovingAverage"] = "$ * 89",
        ["AdaptiveMovingAverage"] = "$ * 71",
        ["EhlersKaufmanAdaptiveMovingAverage"] = "$ * 40",
        ["HoltExponentialMovingAverage"] = "$ * 20",
        ["AhrensMovingAverage"] = "$ * 17",
        ["ZeroLowLagMovingAverage"] = "$ * 15",
        ["RecursiveMovingTrendAverage"] = "$ * 11",
        ["ElasticVolumeWeightedMovingAverageV1"] = "$ * 10",
        ["WellRoundedMovingAverage"] = "$ * 10",
        ["IIRLeastSquaresEstimate"] = "$ * 9",
        ["EhlersInfiniteImpulseResponseFilter"] = "$ * 9",
        ["HampelFilter"] = "$ * 9",
        ["RegularizedExponentialMovingAverage"] = "$ * 9",
        ["EhlersMedianAverageAdaptiveFilter"] = "$ * 8",
        ["EhlersRecursiveMedianFilter"] = "$ * 7",
        ["Ehlers3PoleButterworthFilterV1"] = "$ * 6",
        ["EhlersSimpleDecycler"] = "$ * 5",
        ["FallingRisingFilter"] = "$ * 5",
        ["JurikMovingAverage"] = "$ * 5",
        ["Ehlers2PoleButterworthFilterV1"] = "$ * 4",
        ["Ehlers2PoleSuperSmootherFilterV2"] = "$ * 4",
        ["EhlersSuperSmootherFilter"] = "$ * 4",
        ["EhlersBetterExponentialMovingAverage"] = "$ * 4",
        ["BryantAdaptiveMovingAverage"] = "$ * 4",
        ["RepulsionMovingAverage"] = "$ * 3",
        ["HybridConvolutionFilter"] = "$ * 3",
        ["LinearExtrapolation"] = "$ * 2",
        ["ParametricCorrectiveLinearMovingAverage"] = "$ * 2",
        ["EhlersAllPassPhaseShifter"] = "$ * 2",
        ["VolatilityWaveMovingAverage"] = "$ * 2",
        ["EhlersGaussianFilter"] = "$ * 2",
        ["CompoundRatioMovingAverage"] = "$ * 2",
        ["VolatilityMovingAverage"] = "$ * 2",
    };

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

        // The batch methods carry the defaults a v1 caller has always seen, so the typed surface agrees
        // with what the library actually defaults to rather than inventing its own numbers.
        var batchDefaults = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is MethodDeclarationSyntax m
                    && m.Identifier.Text.StartsWith("Calculate", StringComparison.Ordinal),
                transform: static (ctx, _) => ReadBatchDefaults((MethodDeclarationSyntax)ctx.Node))
            .Where(static x => x is not null)
            .Collect();

        // Reuses the reader the output map is built from, so the typed members cannot describe outputs the
        // indicator does not publish - the two would otherwise drift the moment a calculation changed.
        var publishedOutputs = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IndicatorOutputMapGenerator.IsCalculationMethod(node),
                transform: static (ctx, _) => IndicatorOutputMapGenerator.ReadPublishedOutputs(ctx.Node))
            .Where(static x => x is not null)
            .Collect();

        // Every member of MovingAvgType is an average the batch calculations already know how to run, so a
        // generated type whose indicator shares that name is one of them and can be handed to a component
        // parameter that asks for IMovingAverage.
        var movingAverages = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is EnumDeclarationSyntax e
                    && e.Identifier.Text == "MovingAvgType",
                transform: static (ctx, _) => ((EnumDeclarationSyntax)ctx.Node).Members
                    .Select(m => m.Identifier.Text).ToImmutableArray())
            .Collect();

        var combined = optionsTypes.Combine(armTargets).Combine(categories)
            .Combine(batchDefaults).Combine(publishedOutputs).Combine(movingAverages);

        context.RegisterSourceOutput(combined, static (spc, source) =>
            Emit(source.Left.Left.Left.Left.Left, source.Left.Left.Left.Left.Right,
                source.Left.Left.Left.Right, source.Left.Left.Right, source.Left.Right,
                source.Right, spc));
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

            // BuilderArgument("Length", "fastLength") says the options property Length is the batch
            // method's fastLength parameter. Without it the default for a renamed argument is looked
            // up under a name the batch method does not have, and silently not found.
            var renames = ImmutableArray.CreateBuilder<(string Property, string Parameter)>();
            if (assignment.Right is BaseObjectCreationExpressionSyntax withArgs
                && withArgs.ArgumentList is not null)
            {
                foreach (var argument in withArgs.ArgumentList.Arguments)
                {
                    if (argument.Expression is BaseObjectCreationExpressionSyntax nested
                        && nested.ArgumentList is not null
                        && nested.ArgumentList.Arguments.Count == 2
                        && nested.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax propertyLiteral
                        && nested.ArgumentList.Arguments[1].Expression is LiteralExpressionSyntax parameterLiteral)
                    {
                        renames.Add((propertyLiteral.Token.ValueText, parameterLiteral.Token.ValueText));
                    }
                }
            }

            if (indicatorName is not null)
            {
                readings.Add(new ArmTargetReading(optionsType, indicatorName, outputKey, renames.ToImmutable()));
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

    /// <summary>Reads the defaults off a batch calculation, keyed by the method name.</summary>
    private static BatchReading? ReadBatchDefaults(MethodDeclarationSyntax method)
    {
        var defaults = ImmutableArray.CreateBuilder<(string Name, string Default)>();
        foreach (var parameter in method.ParameterList.Parameters)
        {
            if (parameter.Default is not null)
            {
                defaults.Add((parameter.Identifier.Text, parameter.Default.Value.ToString()));
            }
        }

        return defaults.Count > 0
            ? new BatchReading(method.Identifier.Text, defaults.ToImmutable())
            : null;
    }

    private static void Emit(
        ImmutableArray<OptionsReading?> optionsTypes,
        ImmutableArray<ImmutableArray<ArmTargetReading>?> armTargets,
        ImmutableArray<ImmutableArray<CategoryReading>?> categories,
        ImmutableArray<BatchReading?> batchReadings,
        ImmutableArray<IndicatorOutputMapGenerator.PublishedOutputs?> publishedOutputs,
        ImmutableArray<ImmutableArray<string>> movingAverageMembers,
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

        var batchOf = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var reading in batchReadings)
        {
            if (reading is null)
            {
                continue;
            }

            // Several overloads can share a name; the one declaring the most defaults is the fullest.
            if (!batchOf.TryGetValue(reading.MethodName, out var existing)
                || reading.Defaults.Length > existing.Count)
            {
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (name, value) in reading.Defaults)
                {
                    map[name] = value;
                }

                batchOf[reading.MethodName] = map;
            }
        }

        // One entry per indicator, keeping the fullest reading: a calculation can publish its outputs from
        // more than one branch, and the shorter branch would describe fewer members than exist.
        var outputsOf = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var reading in publishedOutputs)
        {
            if (reading is null)
            {
                continue;
            }

            if (!outputsOf.TryGetValue(reading.IndicatorName, out var existing)
                || reading.Keys.Count > existing.Count)
            {
                outputsOf[reading.IndicatorName] = reading.Keys;
            }
        }

        var movingAverages = new HashSet<string>(StringComparer.Ordinal);
        foreach (var batch in movingAverageMembers)
        {
            foreach (var member in batch)
            {
                if (NotAnAverage.Contains(member))
                {
                    continue;
                }

                movingAverages.Add(member);
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
        var multi = 0;
        var collided = 0;
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

            // A type that selects one named output of an average is not itself that average: LinRegSlope
            // publishes LinearRegression's "Slope" series, so tagging it IMovingAverage would let it be
            // handed to anything asking for an average, and IBuiltInMovingAverage.AvgType would then
            // silently substitute the regression line for the slope. Only the indicator's primary series -
            // no output key, or the first key it publishes - is the average itself.
            var isAverage = movingAverages.Contains(target.IndicatorName)
                && (target.OutputKey is null
                    || (outputsOf.TryGetValue(target.IndicatorName, out var ownKeys)
                        && ownKeys.Count > 0
                        && string.Equals(ownKeys[0], target.OutputKey, StringComparison.Ordinal)));
            var interfaces = new List<string> { "IBuiltInIndicator" };
            if (isAverage)
            {
                interfaces.Insert(0, "IMovingAverage");
                interfaces.Add("IBuiltInMovingAverage");
            }
            if (categoryOf.TryGetValue(target.IndicatorName, out var category))
            {
                interfaces.Insert(0, CategoryInterface(category));
            }

            // An options type standing for one named output is single-output however many its indicator
            // publishes - AlligatorJawSpecOptions is the Jaws series, not the whole Alligator.
            var outputKeys = new List<string>();
            if (target.OutputKey is null
                && outputsOf.TryGetValue(target.IndicatorName, out var published)
                && published.Count > 1)
            {
                outputKeys = published;
            }

            var memberNames = outputKeys.Select(Identifier).ToList();

            // A key matching the type name cannot be a member of it. MultiOutputIndicatorBase does not declare
            // Value, so the indicator's own series takes that name and its siblings keep theirs - 30 types
            // would otherwise lose every typed member over one word, Macd among them.
            for (var i = 0; i < memberNames.Count; i++)
            {
                if (string.Equals(memberNames[i], typeName, StringComparison.Ordinal))
                {
                    memberNames[i] = "Value";
                }
            }

            var reserved = new HashSet<string>(StringComparer.Ordinal)
            {
                "Source", "Outputs", "Components", "WarmupBars", "Of", "Uses", "CreateState",
                "Equals", "GetHashCode", "GetType", "ToString", typeName
            };
            foreach (var parameter in options.Parameters)
            {
                reserved.Add(Capitalise(parameter.Name));
            }

            // A key that collides with a parameter property, or with something the base already declares,
            // would not compile. Emitting the type as single-output loses the named members but keeps the
            // indicator usable, which is the better of the two failures.
            if (memberNames.Count != memberNames.Distinct(StringComparer.Ordinal).Count()
                || memberNames.Any(reserved.Contains))
            {
                outputKeys = new List<string>();
                memberNames = new List<string>();
                collided++;
            }

            var isMulti = outputKeys.Count > 1;
            var baseType = isMulti ? "MultiOutputIndicatorBase" : "IndicatorBase";

            builder.AppendLine();
            builder.AppendLine("/// <summary>" + Escape(typeName) + ".</summary>");
            builder.AppendLine("public sealed class " + typeName + " : " + baseType + ", "
                + string.Join(", ", interfaces));
            builder.AppendLine("{");

            batchOf.TryGetValue("Calculate" + target.IndicatorName, out var batchDefaults);
            var resolved = ResolveDefaults(options.Parameters, target, batchDefaults);

            // A MovingAvgType parameter becomes an IMovingAverage component, so a caller can hand in one of
            // ours or one of their own. Only where the batch method states a default, because that default is
            // what the options type still receives when the caller supplies nothing - without it there would
            // be nothing to fall back to.
            var asComponent = new bool[options.Parameters.Count];
            for (var i = 0; i < options.Parameters.Count; i++)
            {
                asComponent[i] = options.Parameters[i].Type == "MovingAvgType" && resolved[i] is not null;
            }

            var componentCount = asComponent.Count(x => x);

            var signature = string.Join(", ", options.Parameters.Select((p, i) =>
                asComponent[i]
                    ? "IMovingAverage? " + p.Name + " = null"
                    : p.Type + " " + p.Name + (resolved[i] is null ? string.Empty : " = " + resolved[i])));

            builder.AppendLine("    /// <summary>Creates " + Escape(typeName) + ".</summary>");
            builder.AppendLine("    public " + typeName + "(" + signature + ")"
                + (isMulti ? "\n        : base(" + outputKeys.Count + ")" : string.Empty));
            builder.AppendLine("    {");
            foreach (var parameter in options.Parameters)
            {
                builder.AppendLine("        " + Capitalise(parameter.Name) + " = " + parameter.Name + ";");
            }

            if (componentCount > 0)
            {
                // Explicit rather than a collection expression plus LINQ: generated code with no target type
                // for [a, b] is CS9176, and keeping System.Linq out of the emitted file is one less thing
                // that has to be true of it.
                builder.AppendLine("        var components = new System.Collections.Generic.List<IIndicator>("
                    + componentCount + ");");
                for (var i = 0; i < options.Parameters.Count; i++)
                {
                    if (asComponent[i])
                    {
                        var member = Capitalise(options.Parameters[i].Name);
                        builder.AppendLine("        if (" + member + " is not null) components.Add(" + member + ");");
                    }
                }

                builder.AppendLine("        if (components.Count > 0) Uses(components.ToArray());");
            }

            // Assigned from Outputs rather than deconstructed: the deconstruction overloads stop at five,
            // and generated code gains nothing from the nicer syntax.
            for (var i = 0; i < memberNames.Count; i++)
            {
                builder.AppendLine("        " + memberNames[i] + " = Outputs[" + i + "];");
            }

            builder.AppendLine("    }");

            for (var i = 0; i < memberNames.Count; i++)
            {
                builder.AppendLine();
                builder.AppendLine("    /// <summary>The " + Escape(outputKeys[i]) + " series.</summary>");
                builder.AppendLine("    public IIndicatorOutput " + memberNames[i] + " { get; }");
            }

            // One enum per indicator rather than one enum for the library. The containing type is what makes
            // a member unique, so names stay short - a flat enum would need 1,482 members prefixed by their
            // indicator, the longest 64 characters, and still could not say which keys belong to which
            // indicator. Each member's value is its slot, so casting to int is the lookup.
            if (memberNames.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("    /// <summary>The series " + Escape(typeName) + " publishes.</summary>");
                builder.AppendLine("    public enum Output");
                builder.AppendLine("    {");
                for (var i = 0; i < memberNames.Count; i++)
                {
                    builder.AppendLine("        /// <summary>The " + Escape(outputKeys[i]) + " series.</summary>");
                    builder.AppendLine("        " + memberNames[i] + " = " + i + ",");
                }

                builder.AppendLine("    }");
            }

            for (var i = 0; i < options.Parameters.Count; i++)
            {
                var parameter = options.Parameters[i];
                builder.AppendLine();
                builder.AppendLine("    /// <summary>The " + Escape(parameter.Name) + ".</summary>");
                builder.AppendLine("    public " + (asComponent[i] ? "IMovingAverage?" : parameter.Type)
                    + " " + Capitalise(parameter.Name) + " { get; }");
            }

            // The longest length, not the first one declared. Macd takes fastLength, slowLength and
            // signalLength in that order, so reading the first said 12 where the indicator needs 26 - an
            // understated warm-up publishes numbers before they mean anything.
            var lengthParameters = options.Parameters
                .Where(p => p.Type == "int" && p.Name.IndexOf("ength", StringComparison.Ordinal) >= 0)
                .ToList();
            var lengthParameter = lengthParameters.Count == 0 ? null : lengthParameters[0];
            if (lengthParameter is not null)
            {
                builder.AppendLine();
                builder.AppendLine("    /// <inheritdoc/>");
                // Math.Max takes two, so several lengths nest.
                var warmup = Capitalise(lengthParameters[0].Name);
                for (var i = 1; i < lengthParameters.Count; i++)
                {
                    warmup = "System.Math.Max(" + warmup + ", " + Capitalise(lengthParameters[i].Name) + ")";
                }

                // A filter whose output feeds back into itself does not reach its input in `length` bars.
                // WarmupBars is what a caller discards and what the builder pulls as history for a live
                // source, so an understated one publishes numbers before they mean anything.
                if (WarmupRule.TryGetValue(target.IndicatorName, out var rule))
                {
                    warmup = rule.Replace("$", warmup);
                }

                builder.AppendLine("    public override int WarmupBars => " + warmup + ";");
            }

            builder.AppendLine();
            if (isAverage)
            {
                builder.AppendLine();
                builder.AppendLine("    MovingAvgType IBuiltInMovingAverage.AvgType => MovingAvgType."
                    + target.IndicatorName + ";");
            }

            builder.AppendLine();
            builder.AppendLine("    IndicatorName IBuiltInIndicator.BatchName => IndicatorName."
                + target.IndicatorName + ";");
            builder.AppendLine();
            builder.AppendLine("    string? IBuiltInIndicator.BatchOutputKey => "
                + (target.OutputKey is null ? "null" : "\"" + target.OutputKey + "\"") + ";");
            builder.AppendLine();
            var arguments = string.Join(", ", options.Parameters.Select((p, i) => asComponent[i]
                ? Capitalise(p.Name) + " is IBuiltInMovingAverage __" + p.Name + " ? __" + p.Name
                    + ".AvgType : " + resolved[i]
                : Capitalise(p.Name)));

            builder.AppendLine("    IIndicatorSpecOptions IBuiltInIndicator.CreateOptions() => new "
                + options.Name + "(" + arguments + ");");
            builder.AppendLine("}");
            emitted++;
            if (isMulti)
            {
                multi++;
            }
        }

        if (emitted == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("// emitted=" + emitted + " multiOutput=" + multi + " nameCollisions=" + collided);

        context.AddSource("Indicators.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    /// <summary>
    /// The default for each parameter: the batch method''s, then the options type''s own, then none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The batch method is preferred because its default is what a v1 caller has always got, so the typed
    /// surface agrees with the library rather than with a number the options type happened to repeat.
    /// </para>
    /// <para>
    /// C# requires optional parameters to be last, so a default on an earlier parameter is dropped when any
    /// later one has none. Emitting them unconditionally is a compile error in the generated file, which is
    /// a bad place to discover it.
    /// </para>
    /// </remarks>
    private static string?[] ResolveDefaults(
        List<ParameterReading> parameters,
        ArmTargetReading target,
        Dictionary<string, string>? batchDefaults)
    {
        var resolved = new string?[parameters.Count];

        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            string? value = null;

            if (batchDefaults is not null)
            {
                var batchName = parameter.Name;
                foreach (var rename in target.Renames)
                {
                    if (string.Equals(rename.Property, parameter.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        batchName = rename.Parameter;
                        break;
                    }
                }

                batchDefaults.TryGetValue(batchName, out value);
            }

            // A batch method often declares int? length = null where the options type takes a plain int.
            // Copying that across is CS1750 in the generated file, so the null is simply not a default here.
            if (value == "null" && !parameter.Type.EndsWith("?", StringComparison.Ordinal))
            {
                value = null;
            }

            resolved[i] = value ?? parameter.Default;
        }

        // Keep only the trailing run.
        var cutoff = parameters.Count;
        while (cutoff > 0 && resolved[cutoff - 1] is not null)
        {
            cutoff--;
        }

        for (var i = 0; i < cutoff; i++)
        {
            resolved[i] = null;
        }

        return resolved;
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

    /// <summary>Turns a published output key into a member name.</summary>
    private static string Identifier(string key)
    {
        var builder = new StringBuilder(key.Length);
        foreach (var character in key)
        {
            if (char.IsLetterOrDigit(character) || character == '_')
            {
                builder.Append(character);
            }
        }

        var text = builder.ToString();
        if (text.Length == 0 || char.IsDigit(text[0]))
        {
            text = "Output" + text;
        }

        return Capitalise(text);
    }

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
        public ArmTargetReading(string optionsType, string indicatorName, string? outputKey,
            ImmutableArray<(string Property, string Parameter)> renames)
        {
            OptionsType = optionsType;
            IndicatorName = indicatorName;
            OutputKey = outputKey;
            Renames = renames;
        }

        public ImmutableArray<(string Property, string Parameter)> Renames { get; }

        public string OptionsType { get; }

        public string IndicatorName { get; }

        public string? OutputKey { get; }
    }

    private sealed class BatchReading
    {
        public BatchReading(string methodName, ImmutableArray<(string Name, string Default)> defaults)
        {
            MethodName = methodName;
            Defaults = defaults;
        }

        public string MethodName { get; }

        public ImmutableArray<(string Name, string Default)> Defaults { get; }
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
