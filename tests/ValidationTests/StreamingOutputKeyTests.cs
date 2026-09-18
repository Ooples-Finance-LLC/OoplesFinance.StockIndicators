using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Exceptions;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// Starting a streaming runtime refuses an output key the indicator does not publish.
/// </summary>
/// <remarks>
/// <para>
/// <c>IndicatorRuntime.StartStreaming</c> registered the state without looking at <c>spec.OutputKey</c>, and the
/// callback then asked <c>StreamingIndicatorFactory.ExtractValue</c> for it on every bar. A key the indicator does
/// not publish is absent from the update's outputs, so that answered <c>double.NaN</c> - for ever, with no error.
/// The same request through the batch path raises in <c>BuilderArmBinding</c>, so one spec was an exception in one
/// engine and a permanently dead series in the other. Raised by review on PR #230; the substitution itself is the
/// one #186 removed and #219 finished removing.
/// </para>
/// <para>
/// Nothing covered this because nothing in the suite started a streaming runtime at all.
/// <c>BuilderStreamingArmTests</c> drives <c>StatefulIndicatorFactory.Create</c> and <c>state.Update</c> directly,
/// which never reaches the registration loop, and <c>StreamingOptionsTests</c> only exercises the options object.
/// That is why a NaN series could sit in the streaming path unnoticed.
/// </para>
/// <para>
/// Held from both sides on purpose. Asserting only that a bad key raises would pass just as well if
/// <c>Start</c> threw for a reason having nothing to do with the key - no symbol configured, or
/// <c>CreateState</c> failing to cast the options - so a key the indicator does publish is required to start
/// cleanly through the very same path. That is what attributes the refusal to the key rather than to the
/// scaffolding.
/// </para>
/// <para>
/// Bollinger Bands is the subject because it has a streaming state, so execution reaches the registration rather
/// than stopping at the <c>CreateState</c> null check, and because it publishes more than one key. Its options are
/// the typed ones for the same reason: <c>CreateState</c> casts to the concrete options type, so a spec carrying
/// <c>GenericIndicatorOptions</c> would fail before the check under test.
/// </para>
/// </remarks>
public sealed class StreamingOutputKeyTests
{
    private const string Unpublished = "NotAKey";

    [Fact]
    public void StartingWithAKeyTheIndicatorDoesNotPublishRaises()
    {
        GeneratedIndicatorOutputs.KeysFor(IndicatorName.BollingerBands).Should().NotContain(Unpublished,
            "the key this asks for has to be one the indicator really does not publish");

        var act = () => Start(Unpublished);

        act.Should().Throw<CalculationException>(
                "a key the indicator does not publish must be refused, not answered with NaN on every bar")
            .WithMessage($"*{IndicatorName.BollingerBands}*{Unpublished}*Available outputs:*",
                "the streaming refusal is worded as the batch one is, so a caller cannot tell which refused");
    }

    [Fact]
    public void StartingWithAPublishedKeyDoesNotRaise()
    {
        var published = GeneratedIndicatorOutputs.KeysFor(IndicatorName.BollingerBands);
        published.Should().Contain("UpperBand", "the positive arm has to name a key the indicator really publishes");

        var act = () => Start("UpperBand");

        act.Should().NotThrow(
            "a published key must register, or the test above would pass for any reason Start happened to throw");
    }

    /// <summary>Builds a streaming runtime over a stub stream and starts it, which is what registers.</summary>
    private static void Start(string outputKey)
    {
        var builder = new StockIndicatorBuilder(IndicatorDataSource.FromStreaming(new StubStreamSource()));

        builder.ConfigureIndicators(catalog =>
        {
            var spec = new IndicatorSpec(
                IndicatorName.BollingerBands, new BollingerBandsSpecOptions(20, 2.0), outputKey);
            var price = catalog.Price();
            builder.AddIndicator(spec, price, builder.ResolveSeriesKey(price), key: null);
        });

        using var runtime = builder.Build();
        runtime.Start();
    }

    /// <summary>A stream that yields nothing: the registration is what is under test, not any bar.</summary>
    private sealed class StubStreamSource : IStreamSource
    {
        public IStreamSubscription Subscribe(StreamSubscriptionRequest request, IStreamObserver observer) =>
            new StubSubscription();
    }

    private sealed class StubSubscription : IStreamSubscription
    {
        public void Start()
        {
            // Nothing to deliver: no bar has to arrive for a registration to be refused.
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }
}
