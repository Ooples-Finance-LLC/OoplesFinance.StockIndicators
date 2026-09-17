using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace OoplesFinance.StockIndicators.SourceGeneration;

/// <summary>
/// Makes "a calculation that never says which indicator it is" a build error.
/// </summary>
/// <remarks>
/// <para>
/// The generated output map is read from the <c>stockData.IndicatorName</c> assignment in each calculation,
/// and the Builder resolves every named slot out of that map. A calculation that assigns nothing therefore
/// has no entry, and the Builder answers every request for one of its outputs with "Available outputs:
/// none" - while <c>IndicatorInvoker</c>, which keys off the method name, computes it perfectly. Nothing
/// fails, because the tests drive the calculations directly. See issue #199.
/// </para>
/// <para>
/// The rule is deliberately narrow. It fires only when a public calculation neither assigns a literal
/// <c>IndicatorName</c> nor passes one to another calculation, which is the shape of forgetting entirely.
/// It does not try to decide whether the name assigned is the <i>right</i> one, nor to follow a helper
/// across files: <c>IndicatorOutputMapCompletenessTests</c> does both exactly, by reading the map the
/// generator actually emitted. Severity is Error and this library builds with TreatWarningsAsErrors, so a
/// rule that guessed would stop the build on a calculation that was fine.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IndicatorReachabilityAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "IndicatorOutputs";
    private const string LibraryAssemblyName = "OoplesFinance.StockIndicators";

    internal static readonly DiagnosticDescriptor MustReachAnIndicatorName = new(
        id: "SI0006",
        title: "A calculation must say which indicator it is",
        messageFormat: "'{0}' neither assigns stockData.IndicatorName nor hands an IndicatorName to another " +
                       "calculation, so the generated output map has no entry for it and the Builder cannot " +
                       "resolve any of its named outputs",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(MustReachAnIndicatorName);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            // This ships in the package and runs in consumer compilations too, where a method of theirs
            // beginning with Calculate is none of this rule's business.
            if (start.Compilation.AssemblyName != LibraryAssemblyName)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        if (!IsIndicatorCalculation(method))
        {
            return;
        }

        SyntaxNode? body = method.Body;
        body ??= method.ExpressionBody;
        if (body is null)
        {
            return;
        }

        if (AssignsAnIndicatorName(body) || HandsOnAnIndicatorName(body))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MustReachAnIndicatorName, method.Identifier.GetLocation(), method.Identifier.Text));
    }

    /// <summary>
    /// An indicator calculation, and not merely something whose name begins with Calculate.
    /// </summary>
    /// <remarks>
    /// The name alone is far too broad: the library also has CalculateSharpeRatio, CalculatePipValue,
    /// CalculateGreeks, CalculateTaxImpact and a catalog method called plain Calculate, none of which is an
    /// indicator and none of which has any business in the output map. What makes one an indicator is the
    /// shape IndicatorInvoker dispatches on - a public static extension method taking a StockData and
    /// returning one - so this asks the same question of the syntax.
    /// </remarks>
    private static bool IsIndicatorCalculation(MethodDeclarationSyntax method)
    {
        if (!method.Identifier.Text.StartsWith("Calculate", StringComparison.Ordinal)
            || !method.Modifiers.Any(SyntaxKind.PublicKeyword)
            || !method.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return false;
        }

        if (method.ReturnType is not IdentifierNameSyntax returnType
            || returnType.Identifier.Text != "StockData")
        {
            return false;
        }

        var first = method.ParameterList.Parameters.FirstOrDefault();

        return first is not null
            && first.Modifiers.Any(SyntaxKind.ThisKeyword)
            && first.Type is IdentifierNameSyntax parameterType
            && parameterType.Identifier.Text == "StockData";
    }

    private static bool AssignsAnIndicatorName(SyntaxNode body) =>
        body.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Any(assignment => assignment.Left is MemberAccessExpressionSyntax left
                && left.Name.Identifier.Text == "IndicatorName"
                && NamesAnIndicator(assignment.Right));

    /// <summary>
    /// Whether the value assigned actually names an indicator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>default</c> and <c>IndicatorName.None</c> both satisfy "there is an assignment" while leaving the
    /// calculation exactly as unreachable as assigning nothing at all: IndicatorOutputMapGenerator emits an
    /// entry only for a concrete member that is not None, and HelperRoutedOutputs applies the same rule to
    /// the names it resolves. Accepting a sentinel here would let this rule pass on a calculation the
    /// Builder still cannot reach, which is the one thing it exists to prevent.
    /// </para>
    /// <para>
    /// A bare identifier is accepted on purpose. That is a helper handing on the name it was given, and the
    /// literal it stands for sits at the caller rather than here - <c>HelperRoutedOutputs</c> resolves it
    /// across the two, so this rule must not demand a literal the method cannot have.
    /// </para>
    /// </remarks>
    private static bool NamesAnIndicator(ExpressionSyntax value) => value switch
    {
        MemberAccessExpressionSyntax member =>
            member.Expression is IdentifierNameSyntax qualifier
            && qualifier.Identifier.Text == "IndicatorName"
            && member.Name.Identifier.Text != "None",
        IdentifierNameSyntax => true,
        _ => false
    };

    /// <summary>
    /// Whether it passes an <c>IndicatorName</c> to something else, which is how the routed calculations
    /// say what they are - the helper does the assigning on their behalf.
    /// </summary>
    private static bool HandsOnAnIndicatorName(SyntaxNode body) =>
        body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .SelectMany(invocation => invocation.ArgumentList.Arguments)
            .Any(argument => argument.Expression is MemberAccessExpressionSyntax member
                && member.Expression is IdentifierNameSyntax name
                && name.Identifier.Text == "IndicatorName"
                && member.Name.Identifier.Text != "None");
}
