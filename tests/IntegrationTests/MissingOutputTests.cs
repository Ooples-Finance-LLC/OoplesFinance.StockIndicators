using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Exceptions;

namespace OoplesFinance.StockIndicators.Tests.Unit.IntegrationTests;

/// <summary>
/// Asking an indicator for an output it does not publish is an error, not a different series.
/// </summary>
/// <remarks>
/// <para>
/// Resolving a slot used to fall through to the primary series when the indicator published no key for
/// it. That returns a number, and the number is wrong: the caller asked for the signal line and received
/// the indicator itself. Alligator, Gator, Aroon, Elder Ray and Trix each handed back the same series for
/// every band that way, which is why <c>GetOutputKey</c> now returns null rather than guessing at a
/// literal key, and why the resolver raises instead of substituting.
/// </para>
/// <para>
/// The subject is a single-output indicator on purpose. 484 of the indicators in the generated map publish
/// exactly one slot, so this is the common shape rather than a corner: every one of them would have
/// answered a request for a signal line with its own primary values.
/// </para>
/// </remarks>
public sealed class MissingOutputTests : GlobalTestData
{
    private const IndicatorName SingleOutput = IndicatorName.AlphaDecreasingExponentialMovingAverage;

    [Fact]
    public void AskingForASlotTheIndicatorDoesNotPublishRaises()
    {
        var act = () => Evaluate(IndicatorOutput.Signal);

        act.Should().Throw<CalculationException>(
                "an indicator asked for an output it does not publish must say so, not answer with another series")
            .WithMessage($"*{SingleOutput}*")
            .WithMessage("*Signal*");
    }

    /// <summary>The message names what the indicator does publish, so the caller can correct the request.</summary>
    [Fact]
    public void TheErrorNamesTheOutputsTheIndicatorDoesPublish()
    {
        var act = () => Evaluate(IndicatorOutput.Histogram);

        act.Should().Throw<CalculationException>().WithMessage("*Ema*");
    }

    /// <summary>
    /// The primary slot still resolves, because that is where a single-output indicator publishes its
    /// value. The fix narrows the fallback rather than removing it.
    /// </summary>
    [Fact]
    public void ThePrimarySlotStillResolves()
    {
        var series = Evaluate(IndicatorOutput.Primary);

        series.Should().NotBeEmpty("the primary slot is the one output this indicator publishes");
        series.Count(v => !double.IsNaN(v) && v != 0)
            .Should().BeGreaterThan(0, "the primary series must carry values, not only warm-up zeros");
    }

    private static double[] Evaluate(IndicatorOutput output)
    {
        var stockData = new StockData(StockTestData.Take(200));
        var builder = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(stockData));

        SeriesHandle handle = default;
        builder.ConfigureIndicators(catalog =>
        {
            var spec = IndicatorSpecs.Create(SingleOutput, new GenericIndicatorOptions(new object[] { 14 }), output);
            var price = catalog.Price();
            handle = builder.AddIndicator(spec, price, builder.ResolveSeriesKey(price), key: null);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle);

        return runtime.GetSeries(handle).ToArray();
    }
}
