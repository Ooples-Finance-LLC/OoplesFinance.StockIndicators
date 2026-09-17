using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using OoplesFinance.StockIndicators.SourceGeneration;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// The build-gating rules in <see cref="StreamingStateAnalyzer"/>, each checked on a state that breaks it and
/// on one that does not.
/// </summary>
/// <remarks>
/// <para>
/// The rules were verified by hand when they landed: compile a state that breaks each one, confirm the
/// expected SI000x, then compile ones that should pass and confirm silence. That probe was ad-hoc and
/// deleted afterwards, so nothing stopped a later edit from quietly disabling a rule - and an analyzer that
/// no longer fires is indistinguishable from one with nothing to report. This is that probe, kept. See #189.
/// </para>
/// <para>
/// Every case asserts the snippet compiles clean before looking at the analyzer. That is not ceremony: a
/// snippet with a compile error produces no analyzer diagnostics at all, so a silent control arm would prove
/// nothing while looking exactly like success. The compile-error assertion is what stops this suite from
/// passing on broken source.
/// </para>
/// <para>
/// Each violation is isolated, because the rules overlap. A constructor taking a selector also has no
/// no-argument form, so the SI0002 case gives its parameter a default or it would report SI0001 too and the
/// test would no longer say which rule fired.
/// </para>
/// </remarks>
public sealed class StreamingStateAnalyzerTests
{
    /// <summary>
    /// The snippets name internal types, so the compilation borrows the test assembly's identity.
    /// </summary>
    /// <remarks>
    /// <c>StreamingInputResolver</c>, <c>InputName</c> and <c>ICustomInputConsumer</c> are all internal, and
    /// three of the five rules are about them, so a compilation that cannot see them could only exercise two.
    /// The library grants this name access with InternalsVisibleTo, and the analyzer itself needs those same
    /// types resolvable or it returns without checking anything.
    /// </remarks>
    private const string CompilationName = "OoplesFinance.StockIndicators.Tests.Unit";

    private const string Preamble = """
        using System;
        using OoplesFinance.StockIndicators.Enums;
        using OoplesFinance.StockIndicators.Streaming;

        namespace Probe;

        """;

    /// <summary>A state with nothing wrong with it: the baseline every other case is a single edit away from.</summary>
    private const string WellFormed = """
        [PrimaryOutput("Value")]
        public sealed class ProbeState : IStreamingIndicatorState
        {
            private readonly StreamingInputResolver _input = new(InputName.Close, null);

            public IndicatorName Name => IndicatorName.SimpleMovingAverage;

            public void Reset() { }

            public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
                => new(_input.GetValue(bar), null);
        }
        """;

    [Fact]
    public void AWellFormedStateReportsNothing()
    {
        Ids(WellFormed).Should().BeEmpty(
            "this state is buildable with no arguments, takes no selector, reads the input it resolves, "
            + "defaults to the close and declares its primary output");
    }

    /// <summary>SI0005: a state must say which of its outputs its value is.</summary>
    [Fact]
    public void AStateWithoutAPrimaryOutputReportsSI0005()
    {
        var source = WellFormed.Replace("[PrimaryOutput(\"Value\")]\n", string.Empty)
            .Replace("[PrimaryOutput(\"Value\")]\r\n", string.Empty);

        Ids(source).Should().Equal(new[] { "SI0005" }, "the attribute is the only thing removed");
    }

    /// <summary>SI0001: CustomInputState builds the state it wraps, so it must be buildable with no arguments.</summary>
    [Fact]
    public void AStateThatCannotBeBuiltWithoutArgumentsReportsSI0001()
    {
        var source = WithConstructor("public ProbeState(int length) { }");

        Ids(source).Should().Equal(new[] { "SI0001" }, "a required parameter leaves no no-argument form");
    }

    /// <summary>
    /// The control arm for SI0001: an optional parameter still leaves a no-argument form, so the rule is
    /// about being callable rather than about having no parameters.
    /// </summary>
    [Fact]
    public void AStateWhoseConstructorParametersAreAllOptionalReportsNothing()
    {
        var source = WithConstructor("public ProbeState(int length = 14) { }");

        Ids(source).Should().BeEmpty("a defaulted parameter can still be called with no arguments");
    }

    /// <summary>SI0002: callers pass values, not a way for the state to fetch them.</summary>
    [Fact]
    public void AStateTakingABarSelectorReportsSI0002()
    {
        var source = WithConstructor("public ProbeState(Func<OhlcvBar, double>? selector = null) { }");

        Ids(source).Should().Equal(new[] { "SI0002" },
            "the parameter is optional, so the state is still buildable and only the selector rule applies");
    }

    /// <summary>
    /// The control arm for SI0002: it is the selector shape that is refused, not any delegate. A
    /// Func&lt;OhlcvBar, decimal&gt; is not how an input series is supplied and the rule leaves it alone.
    /// </summary>
    [Fact]
    public void AStateTakingSomeOtherDelegateReportsNothing()
    {
        var source = WithConstructor("public ProbeState(Func<OhlcvBar, decimal>? other = null) { }");

        Ids(source).Should().BeEmpty("only Func<OhlcvBar, double> is the input-selector shape");
    }

    /// <summary>SI0004: a state that resolves an input and never reads it accepts custom values and ignores them.</summary>
    [Fact]
    public void AStateThatNeverReadsItsResolverReportsSI0004()
    {
        var source = WellFormed.Replace("=> new(_input.GetValue(bar), null);", "=> new(bar.Close, null);");

        Ids(source).Should().Equal(new[] { "SI0004" },
            "the resolver is still built, so the state claims an input it then bypasses");
    }

    /// <summary>SI0003: a non-close default is only safe if CustomInputState can redirect it.</summary>
    [Fact]
    public void AStateWithANonCloseDefaultAndNoConsumerReportsSI0003()
    {
        var source = WellFormed.Replace("new(InputName.Close, null)", "new(InputName.TypicalPrice, null)");

        Ids(source).Should().Equal(new[] { "SI0003" },
            "it reads a typical price by default but cannot be told to read the caller's series instead");
    }

    /// <summary>The control arm for SI0003: implementing the consumer interface is what makes it redirectable.</summary>
    [Fact]
    public void AStateWithANonCloseDefaultAndAConsumerReportsNothing()
    {
        var source = WellFormed
            .Replace("new(InputName.Close, null)", "new(InputName.TypicalPrice, null)")
            .Replace(": IStreamingIndicatorState", ": IStreamingIndicatorState, ICustomInputConsumer")
            .Replace("public void Reset() { }",
                "void ICustomInputConsumer.ReadCloseAsInput() { }\n\n    public void Reset() { }");

        Ids(source).Should().BeEmpty("CustomInputState can now make it read the caller's series");
    }

    /// <summary>
    /// The second control arm for SI0003, and the one that matters most: the price presets.
    /// </summary>
    /// <remarks>
    /// A median-price indicator reads a median price by definition and must NOT be redirected to the close.
    /// The analyzer recognises that structurally rather than from a list - the state's resolved input is the
    /// same name as the indicator it says it is - so this arm is what proves the exemption is live. Without
    /// it, a rule that simply fired on every non-close default would pass every other test here.
    /// </remarks>
    [Fact]
    public void AnInputTransformWhoseDefaultIsItsOwnIndicatorReportsNothing()
    {
        var source = WellFormed
            .Replace("new(InputName.Close, null)", "new(InputName.MedianPrice, null)")
            .Replace("IndicatorName.SimpleMovingAverage", "IndicatorName.MedianPrice");

        Ids(source).Should().BeEmpty("the median-price transform IS that input and must not be redirected");
    }

    /// <summary>Puts a constructor into the well-formed state, leaving everything else alone.</summary>
    private static string WithConstructor(string constructor) =>
        WellFormed.Replace("public IndicatorName Name",
            constructor + "\n\n    public IndicatorName Name");

    /// <summary>The analyzer's diagnostic ids for one snippet, ordered so assertions read the same way twice.</summary>
    /// <remarks>
    /// Callers pass the expected ids as an explicit array. <c>Equal("SI0001", "because...")</c> binds to
    /// <c>Equal(params string[])</c> and silently takes the reason as a second EXPECTED ID, so the failure
    /// reads "expected {SI0001, a required parameter...} but {SI0001} contains 1 item(s) less" - which looks
    /// like the analyzer under-reporting and is nothing of the sort.
    /// </remarks>
    private static ImmutableArray<string> Ids(string classSource)
    {
        var source = Preamble + classSource;
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

        var compilation = CSharpCompilation.Create(
            CompilationName,
            new[] { tree },
            PlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        // Before the analyzer, and deliberately: a snippet that does not compile reports no analyzer
        // diagnostics, so without this an expectation of silence would be met by a typo.
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"{d.Id}: {d.GetMessage()}")
            .ToList();
        errors.Should().BeEmpty("the snippet under test must compile, or the analyzer has nothing to judge");

        var diagnostics = compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new StreamingStateAnalyzer()))
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();

        return diagnostics.Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal).ToImmutableArray();
    }

    /// <summary>
    /// Everything the test host already has loaded, plus the library itself.
    /// </summary>
    /// <remarks>
    /// Taken from the trusted-platform list rather than assembled by hand, because the analyzer resolves its
    /// known types by metadata name and returns without checking anything if any of them is missing - so a
    /// short reference list would turn every case here silently green.
    /// </remarks>
    private static IReadOnlyList<MetadataReference> PlatformReferences()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = new List<MetadataReference>();

        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        foreach (var path in trusted.Split(Path.PathSeparator))
        {
            if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                && File.Exists(path)
                && seen.Add(Path.GetFileName(path)))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        var library = typeof(IStreamingIndicatorState).Assembly.Location;
        if (seen.Add(Path.GetFileName(library)))
        {
            references.Add(MetadataReference.CreateFromFile(library));
        }

        return references;
    }
}
