using System.Reflection;
using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// Holds the arms that agree with their batch to the last bit, so the exact comparisons elsewhere are
/// evidenced rather than assumed.
/// </summary>
/// <remarks>
/// <para>
/// The review on PR #243 asked for a 1e-8 tolerance in <see cref="SignalOutputTests"/> on the grounds that
/// two independent implementations need not produce bit-identical doubles. That is true of some arms and
/// not others, and it was worth measuring rather than deciding: of the series that agree within 1e-8,
/// the observed differences are recorded below. This list is a rounding regression ledger,
/// not a mathematical error guarantee.
/// </para>
/// <para>
/// SignalOutputTests uses a floating route tolerance. A new entry here records a change in operation
/// order that has started to differ from its
/// batch within tolerance, which is worth knowing even though no ratchet would fail.
/// </para>
/// </remarks>
public sealed class ExactAgreementTests
{
    // Series that agree with their batch within 1e-8 but NOT to the last bit.
    private static readonly HashSet<string> WithinToleranceOnly = new(StringComparer.Ordinal)
    {
        // Centered population moments and compensated sums change operation order.
        // These additional outputs were measured within the existing 1e-8 route budget.
        "BollingerBandsWithAtrPct.LowerBand",
        "BollingerBandsWithAtrPct.UpperBand",
        "CloseToCloseVolatility.Ctcv",
        "CommoditySelectionIndex.Signal",
        "DampedSineWaveWeightedFilter.Dswwf",
        "EhlersCommodityChannelIndexInverseFisherTransform.Eiftcci",
        "EhlersRelativeStrengthIndexInverseFisherTransform.Eiftrsi",
        "EhlersReverseEmaIndicatorV2.EremaTrend",
        "EhlersSuperPassbandFilter.LowerBand",
        "EhlersSuperPassbandFilter.UpperBand",
        "GarmanKlassVolatility.Gcv",
        "GarmanKlassVolatility.Signal",
        "HirashimaSugitaRS.LowerBand2",
        "HirashimaSugitaRS.UpperBand2",
        "InternalBarStrengthIndicator.Signal",
        "JrcFractalDimension.Jrcfd",
        "JrcFractalDimension.Signal",
        "MovingAverageSupportResistance.LowerBand",
        "PseudoPolynomialChannel.MiddleBand",
        "RateOfChangeBands.LowerBand",
        "RateOfChangeBands.UpperBand",
        "SmoothedVolatilityBands.LowerBand",
        "SmoothedVolatilityBands.UpperBand",

        "Cmf.Cmf",
        "DemarkPressureRatioV1.Dpr",
        "DemarkPressureRatioV2.Dpr",
        "EhlersReverseEmaIndicatorV1.Erema",
        "EhlersReverseEmaIndicatorV2.EremaCycle",
        "InternalBarStrengthIndicator.Ibs",
        "MacZVwapIndicator.Histogram",
        "MacZVwapIndicator.Macz",
        "MacZVwapIndicator.Signal",
    };

    [Fact]
    public void EveryAgreeingSeriesIsBitIdenticalExceptTheRecordedRoundingDifferences()
    {
        var bars = Walk(150);

        StockData Batch() => new(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList());

        var calculations = typeof(StockData).Assembly.GetTypes()
            .Where(t => t.IsAbstract && t.IsSealed && t.Name == "Calculations")
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.Name.StartsWith("Calculate", StringComparison.Ordinal))
            .GroupBy(m => m.Name)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var nearOnly = new HashSet<string>(StringComparer.Ordinal);
        var agreeing = 0;

        foreach (var type in typeof(IIndicator).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true })
            .Where(t => t.Namespace == "OoplesFinance.StockIndicators.Indicators")
            .Where(typeof(IIndicator).IsAssignableFrom)
            .OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            var constructor = type.GetConstructors().FirstOrDefault(c => c.GetParameters().All(p => p.IsOptional));
            if (constructor is null) { continue; }

            IIndicator indicator;
            IBuiltInIndicator builtIn;
            try
            {
                indicator = (IIndicator)constructor.Invoke(constructor.GetParameters().Select(p => p.DefaultValue).ToArray());
                if (indicator is not IBuiltInIndicator built) { continue; }
                builtIn = built;
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            if (!calculations.TryGetValue("Calculate" + builtIn.BatchName, out var method)) { continue; }

            Dictionary<string, List<double>> published;
            try
            {
                var arguments = method.GetParameters()
                    .Select((p, i) => i == 0 ? (object?)Batch() : Type.Missing).ToArray();
                if (method.Invoke(null, arguments) is not StockData result) { continue; }
                published = result.OutputValues;
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            double[][] mine;
            try
            {
                using var run = new StockIndicatorBuilder()
                    .ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator)
                    .BuildAsync().GetAwaiter().GetResult();
                mine = indicator.Outputs.Select(o => run[o].ToArray()).ToArray();
            }
            catch (Exception)
            {
                continue;
            }

            var keys = published.Keys.ToList();
            for (var slot = 0; slot < mine.Length && slot < keys.Count; slot++)
            {
                var theirs = published[keys[slot]];
                if (mine[slot].Length != theirs.Count) { continue; }

                // Only series that already AGREE are in question here; the ones that do not are the
                // business of the parity ratchets.
                if (mine[slot].Where((x, i) => Math.Abs(x - theirs[i]) > 1e-8).Any()) { continue; }

                agreeing++;
                if (mine[slot].Where((x, i) => x != theirs[i]).Any())
                {
                    nearOnly.Add(type.Name + "." + keys[slot]);
                }
            }
        }

        agreeing.Should().BeGreaterThan(700, "the sweep must reach the agreeing series for its verdict to mean anything");

        var appeared = nearOnly.Except(WithinToleranceOnly).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var tightened = WithinToleranceOnly.Except(nearOnly).OrderBy(x => x, StringComparer.Ordinal).ToList();

        appeared.Should().BeEmpty("these arms have started to differ from their batch in the last bits: " + string.Join(", ", appeared));
        tightened.Should().BeEmpty("these are bit-identical now, so delete them from WithinToleranceOnly: " + string.Join(", ", tightened));
    }

    private static List<Bar> Walk(int count)
    {
        var random = new Random(31);
        var bars = new List<Bar>(count);
        var last = 100d;
        for (var i = 0; i < count; i++)
        {
            var open = last + ((random.NextDouble() - 0.5) * 0.6);
            var close = Math.Max(1, open + ((random.NextDouble() - 0.5) * 1.8));
            bars.Add(new Bar(new DateTime(2021, 1, 4, 14, 30, 0, DateTimeKind.Utc).AddMinutes(i),
                open, Math.Max(open, close) + random.NextDouble(),
                Math.Max(0.01, Math.Min(open, close) - random.NextDouble()), close, random.Next(1000, 400000)));
            last = close;
        }

        return bars;
    }
}
