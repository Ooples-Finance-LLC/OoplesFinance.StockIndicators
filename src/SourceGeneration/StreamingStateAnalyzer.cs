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
            start.RegisterSymbolStartAction(symbolStart => AnalyzeState(symbolStart, types), SymbolKind.NamedType);
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

    /// <summary>
    /// One state, judged once, whichever files it is written across.
    /// </summary>
    /// <remarks>
    /// A symbol action rather than a syntax one on purpose. The rules below have to read the whole class,
    /// and a state split across files gives a syntax action one part at a time - so reading the others
    /// meant asking the compilation for their semantic models, which an analyzer must not do (RS1030).
    /// Registered per symbol, each node arrives with the model for its own tree, the parts accumulate, and
    /// the symbol's end is where there is something to report.
    /// </remarks>
    private static void AnalyzeState(SymbolStartAnalysisContext context, KnownTypes types)
    {
        if (context.Symbol is not INamedTypeSymbol symbol
            || symbol.TypeKind != TypeKind.Class
            || symbol.IsAbstract
            || symbol.DeclaredAccessibility != Accessibility.Public
            || SymbolEqualityComparer.Default.Equals(symbol, types.Wrapper)
            || !symbol.AllInterfaces.Contains(types.State, SymbolEqualityComparer.Default))
        {
            return;
        }

        var found = new Findings();

        // Every part contributes, and the parts can be visited concurrently, so what they find is shared
        // behind a lock rather than assigned from several threads at once.
        context.RegisterSyntaxNodeAction(
            node => Observe(node, types, found),
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression,
            SyntaxKind.InvocationExpression);

        context.RegisterSymbolEndAction(end => Report(end, types, symbol, found));
    }

    /// <summary>Records what one node in one part says about the state's input.</summary>
    private static void Observe(SyntaxNodeAnalysisContext context, KnownTypes types, Findings found)
    {
        // Both the named form and the target-typed one: 'new StreamingInputResolver(...)' and '= new(...)'.
        // They are sibling syntax kinds, so matching only the first let the shorter spelling past the rule
        // whose whole point is that it cannot be got past.
        var arguments = context.Node switch
        {
            ObjectCreationExpressionSyntax created => created.ArgumentList,
            ImplicitObjectCreationExpressionSyntax implied => implied.ArgumentList,
            _ => null
        };

        if (arguments is not null)
        {
            if (!SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetTypeInfo(context.Node, context.CancellationToken).Type, types.Resolver))
            {
                return;
            }

            string? nonClose = null;
            var first = arguments.Arguments.FirstOrDefault()?.Expression;
            if (first is not null
                && context.SemanticModel.GetSymbolInfo(first, context.CancellationToken).Symbol is IFieldSymbol field
                && SymbolEqualityComparer.Default.Equals(field.ContainingType, types.InputName)
                && field.Name != "Close")
            {
                nonClose = field.Name;
            }

            found.Built(nonClose);
        }
        else if (context.Node is InvocationExpressionSyntax invocation
            && context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
            && method.Name == "GetValue"
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, types.Resolver))
        {
            found.Read();
        }
    }

    /// <summary>Says what the whole state turned out to be, once every part has been seen.</summary>
    private static void Report(SymbolAnalysisContext context, KnownTypes types, INamedTypeSymbol symbol, Findings found)
    {
        var name = symbol.Name;
        var at = symbol.Locations.FirstOrDefault() ?? Location.None;
        var publicCtors = symbol.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public).ToList();

        // SI0001: buildable with no arguments.
        if (!publicCtors.Any(c => c.Parameters.All(p => p.HasExplicitDefaultValue || p.IsParams)))
        {
            context.ReportDiagnostic(Diagnostic.Create(MustBeBuildableWithoutArguments, at, name));
        }

        // SI0002: no public selector parameter. An InputName parameter needs no rule of its own: the enum is
        // internal, so a public constructor taking one does not compile (CS0051) before this could report it.
        foreach (var ctor in publicCtors)
        {
            foreach (var parameter in ctor.Parameters)
            {
                if (IsBarSelector(parameter.Type, types.Bar))
                {
                    var location = parameter.Locations.FirstOrDefault() ?? at;
                    context.ReportDiagnostic(Diagnostic.Create(NoInputParameters, location, name,
                        parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                }
            }
        }

        var (buildsResolver, readsResolver, nonCloseDefault) = found.Taken();

        if (buildsResolver && !readsResolver)
        {
            context.ReportDiagnostic(Diagnostic.Create(ResolverNeverRead, at, name));
        }

        if (nonCloseDefault is not null
            && !symbol.AllInterfaces.Contains(types.Consumer, SymbolEqualityComparer.Default)
            && nonCloseDefault != IndicatorNameOf(symbol, context.CancellationToken))
        {
            context.ReportDiagnostic(Diagnostic.Create(NonCloseDefaultNeedsConsumer, at, name, nonCloseDefault));
        }
    }

    /// <summary>What the parts of one state said about its input, gathered as they are visited.</summary>
    private sealed class Findings
    {
        private readonly object _gate = new();
        private bool _builds;
        private bool _reads;
        private string? _nonCloseDefault;

        public void Built(string? nonCloseDefault)
        {
            lock (_gate)
            {
                _builds = true;
                _nonCloseDefault ??= nonCloseDefault;
            }
        }

        public void Read()
        {
            lock (_gate)
            {
                _reads = true;
            }
        }

        public (bool Builds, bool Reads, string? NonCloseDefault) Taken()
        {
            lock (_gate)
            {
                return (_builds, _reads, _nonCloseDefault);
            }
        }
    }

    /// <summary>Every part of the class, so a partial state is judged whole rather than a piece at a time.</summary>
    private static IEnumerable<ClassDeclarationSyntax> Parts(INamedTypeSymbol symbol, CancellationToken cancellationToken)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is ClassDeclarationSyntax part)
            {
                yield return part;
            }
        }
    }

    /// <summary>True for Func&lt;OhlcvBar, double&gt;.</summary>
    private static bool IsBarSelector(ITypeSymbol type, INamedTypeSymbol bar) =>
        type is INamedTypeSymbol { Name: "Func", TypeArguments.Length: 2 } func
        && SymbolEqualityComparer.Default.Equals(func.TypeArguments[0], bar)
        && func.TypeArguments[1].SpecialType == SpecialType.System_Double;

    /// <summary>The X in <c>Name =&gt; IndicatorName.X</c>, from whichever part declares it, or null.</summary>
    private static string? IndicatorNameOf(INamedTypeSymbol symbol, CancellationToken cancellationToken)
    {
        foreach (var part in Parts(symbol, cancellationToken))
        {
            foreach (var property in part.Members.OfType<PropertyDeclarationSyntax>())
            {
                if (property.Identifier.Text == "Name"
                    && property.ExpressionBody?.Expression is MemberAccessExpressionSyntax access
                    && access.Expression is IdentifierNameSyntax { Identifier.Text: "IndicatorName" })
                {
                    return access.Name.Identifier.Text;
                }
            }
        }

        return null;
    }
}
