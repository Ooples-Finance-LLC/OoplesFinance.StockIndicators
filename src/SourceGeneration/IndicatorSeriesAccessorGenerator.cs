using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace OoplesFinance.StockIndicators.SourceGeneration;

/// <summary>
/// Emits one accessor per published output name, so a chain can be continued from a named series with
/// the name checked at compile time.
/// </summary>
/// <remarks>
/// <para>
/// Chaining needs no special syntax - <c>Calculate*</c> takes a <c>StockData</c> and returns one, so
/// <c>data.CalculateSma(20).CalculateBollingerBands()</c> already works. The gap is choosing which
/// series to continue from when a result publishes several. <c>StockData.SeriesView(string)</c> answers
/// that at run time; these accessors put the same question to the compiler:
/// </para>
/// <code>
/// var bands = data.CalculateBollingerBands();
/// var rsi = bands.UpperBand().CalculateRsi(14);   // bands.MiddleBnd() is CS1061
/// </code>
/// <para>
/// The names are read out of the <c>SetOutputValues</c> calls themselves rather than from a list kept
/// alongside them. A hand-maintained list had already drifted - the catalog generator records
/// <c>AlligatorIndex</c> as publishing "Jaw" where the calculation publishes "Jaws" - and deriving the
/// names from the code removes that possibility instead of correcting one instance of it.
/// </para>
/// <para>
/// They are emitted into <c>OoplesFinance.StockIndicators.Series</c> rather than the root namespace.
/// There are several hundred distinct output names across the library, and adding all of them to every
/// <c>StockData</c> would bury the calculations in completion lists. A <c>using</c> opts in.
/// </para>
/// </remarks>
[Generator]
public class IndicatorSeriesAccessorGenerator : IIncrementalGenerator
{
    private const string AccessorNamespace = "OoplesFinance.StockIndicators.Series";

    /// <summary>
    /// Names that would collide with something already on <see cref="object"/> or on StockData itself.
    /// </summary>
    private static readonly HashSet<string> ReservedNames = new HashSet<string>
    {
        "Equals", "GetHashCode", "GetType", "ToString", "Count", "InputName", "IndicatorName",
        "CustomValuesList", "OutputValues", "SignalsList", "Options", "InputValues", "OpenPrices",
        "HighPrices", "LowPrices", "ClosePrices", "Volumes", "Dates", "TickerDataList", "SeriesView",
        "WithValues"
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var outputNames = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsSetOutputValuesInvocation(node),
                transform: static (ctx, _) => ExtractOutputNames(ctx.Node))
            .Where(static names => names.Length > 0)
            .Collect();

        context.RegisterSourceOutput(outputNames, static (spc, names) => Emit(spc, names));
    }

    private static bool IsSetOutputValuesInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text == "SetOutputValues",
            IdentifierNameSyntax identifier => identifier.Identifier.Text == "SetOutputValues",
            _ => false
        };
    }

    private static ImmutableArray<string> ExtractOutputNames(SyntaxNode node)
    {
        var invocation = (InvocationExpressionSyntax)node;

        // The argument is a lambda producing a dictionary initialiser, so look for any initialiser
        // underneath the invocation rather than assuming a fixed shape.
        var initializers = invocation.DescendantNodes()
            .OfType<InitializerExpressionSyntax>()
            .Where(i => i.IsKind(SyntaxKind.ObjectInitializerExpression)
                || i.IsKind(SyntaxKind.CollectionInitializerExpression));

        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var initializer in initializers)
        {
            foreach (var element in initializer.Expressions)
            {
                // Each entry is { "Name", series }.
                if (element is not InitializerExpressionSyntax entry || entry.Expressions.Count == 0)
                {
                    continue;
                }

                if (entry.Expressions[0] is LiteralExpressionSyntax literal
                    && literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    var name = literal.Token.ValueText;
                    if (IsUsableIdentifier(name))
                    {
                        builder.Add(name);
                    }
                }
            }
        }

        return builder.ToImmutable();
    }

    private static bool IsUsableIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name) || ReservedNames.Contains(name))
        {
            return false;
        }

        if (!char.IsLetter(name[0]) && name[0] != '_')
        {
            return false;
        }

        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None
            && SyntaxFacts.GetContextualKeywordKind(name) == SyntaxKind.None;
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<ImmutableArray<string>> collected)
    {
        var names = new SortedSet<string>(System.StringComparer.Ordinal);
        foreach (var group in collected)
        {
            foreach (var name in group)
            {
                names.Add(name);
            }
        }

        if (names.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using OoplesFinance.StockIndicators.Models;");
        sb.AppendLine();
        sb.AppendLine($"namespace {AccessorNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Continue a chain from a named output of an indicator result.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");
        sb.AppendLine("/// <para>");
        sb.AppendLine("/// Each accessor returns a view over the same bars whose input series is that output, so any");
        sb.AppendLine("/// calculation continues off it unchanged and the result it came from is left alone:");
        sb.AppendLine("/// </para>");
        sb.AppendLine("/// <code>");
        sb.AppendLine("/// var bands = data.CalculateBollingerBands();");
        sb.AppendLine("/// var upperRsi = bands.UpperBand().CalculateRelativeStrengthIndex(14);");
        sb.AppendLine("/// var lowerRsi = bands.LowerBand().CalculateRelativeStrengthIndex(14);");
        sb.AppendLine("/// </code>");
        sb.AppendLine("/// <para>");
        sb.AppendLine("/// Generated from the SetOutputValues calls in the calculations, so these names cannot drift");
        sb.AppendLine("/// from what the indicators actually publish. An accessor exists when some indicator publishes");
        sb.AppendLine("/// that name; asking a result for one it does not publish throws and lists what it does.");
        sb.AppendLine("/// </para>");
        sb.AppendLine("/// </remarks>");
        sb.AppendLine("public static class IndicatorSeriesAccessors");
        sb.AppendLine("{");

        var first = true;
        foreach (var name in names)
        {
            if (!first)
            {
                sb.AppendLine();
            }

            first = false;
            sb.AppendLine($"    /// <summary>Continues the chain from the <c>{name}</c> output.</summary>");
            sb.AppendLine($"    /// <param name=\"result\">The indicator result publishing <c>{name}</c>.</param>");
            sb.AppendLine("    /// <exception cref=\"OoplesFinance.StockIndicators.Exceptions.CalculationException\">");
            sb.AppendLine($"    /// Thrown when the result does not publish <c>{name}</c>.");
            sb.AppendLine("    /// </exception>");
            sb.AppendLine($"    public static StockData {name}(this StockData result) =>");
            sb.AppendLine($"        result.SeriesView(\"{name}\");");
        }

        sb.AppendLine("}");

        context.AddSource("IndicatorSeriesAccessors.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}
