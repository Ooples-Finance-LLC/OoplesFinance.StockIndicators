using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace OoplesFinance.StockIndicators.SourceGeneration;

/// <summary>
/// Makes "an indicator that cannot take custom values, or takes them and ignores them" a build error.
/// </summary>
/// <remarks>
/// <para>
/// Every streaming state takes custom input through <c>CustomInputState</c>, which wraps it and hands it
/// bars whose close is the caller's series. That only works if the state can be built at all, reads
/// the input it resolves, and - when its own default input is not the close - can be told to read the
/// close instead. <c>StreamingCustomInputTests</c> checks the outcome at run time over every state; these
/// rules stop the shapes that break it from compiling in the first place, one state at a time.
/// </para>
/// <para>
/// There is no exemption list. The price presets (median, typical, weighted close...) read a non-close
/// input by definition and must NOT be redirected, and they are recognised structurally: a state whose
/// resolved input is the same name as the indicator it is IS that input transform.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StreamingStateAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "StreamingInput";

    internal static readonly DiagnosticDescriptor MustBeBuildableWithoutArguments = new(
        id: "SI0001",
        title: "A streaming state must be buildable with no arguments",
        messageFormat: "'{0}' has no public constructor that can be called without arguments, so it cannot be " +
                       "wrapped in CustomInputState or checked by the custom-input invariant",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor NoInputParameters = new(
        id: "SI0002",
        title: "A streaming state's public constructor must not take an input name or a selector",
        messageFormat: "A public constructor of '{0}' takes '{1}'; callers pass values, not a name for them - " +
                       "custom input is CustomInputState with an InputSeries",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor NonCloseDefaultNeedsConsumer = new(
        id: "SI0003",
        title: "A state whose default input is not the close must implement ICustomInputConsumer",
        messageFormat: "'{0}' reads InputName.{1} by default but does not implement ICustomInputConsumer, so " +
                       "CustomInputState cannot make it read the caller's series and it would ignore it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor ResolverNeverRead = new(
        id: "SI0004",
        title: "A streaming state must read the input it resolves",
        messageFormat: "'{0}' builds a StreamingInputResolver but never calls GetValue on it, so its input is " +
                       "accepted and ignored",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        MustBeBuildableWithoutArguments, NoInputParameters, NonCloseDefaultNeedsConsumer, ResolverNeverRead);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var compilation = start.Compilation;
            var state = compilation.GetTypeByMetadataName("OoplesFinance.StockIndicators.Streaming.IStreamingIndicatorState");
            var consumer = compilation.GetTypeByMetadataName("OoplesFinance.StockIndicators.Streaming.ICustomInputConsumer");
            var resolver = compilation.GetTypeByMetadataName("OoplesFinance.StockIndicators.Streaming.StreamingInputResolver");
            var wrapper = compilation.GetTypeByMetadataName("OoplesFinance.StockIndicators.Streaming.CustomInputState");
            var inputName = compilation.GetTypeByMetadataName("OoplesFinance.StockIndicators.Enums.InputName");
            var bar = compilation.GetTypeByMetadataName("OoplesFinance.StockIndicators.Streaming.OhlcvBar");

            // Only the library itself defines these; in any other compilation there is nothing to check.
            if (state is null || consumer is null || resolver is null || inputName is null || bar is null)
            {
                return;
            }

            var types = new KnownTypes(state, consumer, resolver, wrapper, inputName, bar);
            start.RegisterSyntaxNodeAction(ctx => AnalyzeClass(ctx, types), SyntaxKind.ClassDeclaration);
        });
    }

    private sealed class KnownTypes
    {
        public KnownTypes(INamedTypeSymbol state, INamedTypeSymbol consumer, INamedTypeSymbol resolver,
            INamedTypeSymbol? wrapper, INamedTypeSymbol inputName, INamedTypeSymbol bar)
        {
            State = state;
            Consumer = consumer;
            Resolver = resolver;
            Wrapper = wrapper;
            InputName = inputName;
            Bar = bar;
        }

        public INamedTypeSymbol State { get; }
        public INamedTypeSymbol Consumer { get; }
        public INamedTypeSymbol Resolver { get; }
        public INamedTypeSymbol? Wrapper { get; }
        public INamedTypeSymbol InputName { get; }
        public INamedTypeSymbol Bar { get; }
    }

    private static void AnalyzeClass(SyntaxNodeAnalysisContext context, KnownTypes types)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol symbol
            || symbol.IsAbstract
            || symbol.DeclaredAccessibility != Accessibility.Public
            || SymbolEqualityComparer.Default.Equals(symbol, types.Wrapper)
            || !symbol.AllInterfaces.Contains(types.State, SymbolEqualityComparer.Default))
        {
            return;
        }

        var name = symbol.Name;
        var at = declaration.Identifier.GetLocation();
        var publicCtors = symbol.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public).ToList();

        // SI0001: buildable with no arguments.
        if (!publicCtors.Any(c => c.Parameters.All(p => p.HasExplicitDefaultValue || p.IsParams)))
        {
            context.ReportDiagnostic(Diagnostic.Create(MustBeBuildableWithoutArguments, at, name));
        }

        // SI0002: no public input-name or selector parameters.
        foreach (var ctor in publicCtors)
        {
            foreach (var parameter in ctor.Parameters)
            {
                if (SymbolEqualityComparer.Default.Equals(parameter.Type, types.InputName) || IsBarSelector(parameter.Type, types.Bar))
                {
                    var location = parameter.Locations.FirstOrDefault() ?? at;
                    context.ReportDiagnostic(Diagnostic.Create(NoInputParameters, location, name,
                        parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                }
            }
        }

        // SI0003 and SI0004 read the class body.
        string? nonCloseDefault = null;
        var buildsResolver = false;
        var readsResolver = false;
        foreach (var node in declaration.DescendantNodes())
        {
            if (node is ObjectCreationExpressionSyntax creation
                && SymbolEqualityComparer.Default.Equals(context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type, types.Resolver))
            {
                buildsResolver = true;
                var first = creation.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
                if (first is not null
                    && context.SemanticModel.GetSymbolInfo(first, context.CancellationToken).Symbol is IFieldSymbol field
                    && SymbolEqualityComparer.Default.Equals(field.ContainingType, types.InputName)
                    && field.Name != "Close")
                {
                    nonCloseDefault ??= field.Name;
                }
            }
            else if (node is InvocationExpressionSyntax invocation
                && context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
                && method.Name == "GetValue"
                && SymbolEqualityComparer.Default.Equals(method.ContainingType, types.Resolver))
            {
                readsResolver = true;
            }
        }

        if (buildsResolver && !readsResolver)
        {
            context.ReportDiagnostic(Diagnostic.Create(ResolverNeverRead, at, name));
        }

        if (nonCloseDefault is not null
            && !symbol.AllInterfaces.Contains(types.Consumer, SymbolEqualityComparer.Default)
            && nonCloseDefault != IndicatorNameOf(declaration))
        {
            context.ReportDiagnostic(Diagnostic.Create(NonCloseDefaultNeedsConsumer, at, name, nonCloseDefault));
        }
    }

    /// <summary>True for Func&lt;OhlcvBar, double&gt;.</summary>
    private static bool IsBarSelector(ITypeSymbol type, INamedTypeSymbol bar) =>
        type is INamedTypeSymbol { Name: "Func", TypeArguments.Length: 2 } func
        && SymbolEqualityComparer.Default.Equals(func.TypeArguments[0], bar)
        && func.TypeArguments[1].SpecialType == SpecialType.System_Double;

    /// <summary>The X in <c>Name =&gt; IndicatorName.X</c>, or null.</summary>
    private static string? IndicatorNameOf(ClassDeclarationSyntax declaration)
    {
        foreach (var property in declaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (property.Identifier.Text == "Name"
                && property.ExpressionBody?.Expression is MemberAccessExpressionSyntax access
                && access.Expression is IdentifierNameSyntax { Identifier.Text: "IndicatorName" })
            {
                return access.Name.Identifier.Text;
            }
        }

        return null;
    }
}
