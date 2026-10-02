using FluentAssertions;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// A column adopted as a memory view and a column materialised as a list are two descriptions of the same
/// series, and they have to stay one series. Reading a column materialises it; writing to what you were
/// handed then has to be what every other accessor sees, or an indicator computes from values the caller has
/// already replaced.
/// </summary>
public sealed class AdoptedColumnTests
{
    private static StockData Adopted(int count = 8)
    {
        var o = new double[count]; var h = new double[count]; var l = new double[count];
        var c = new double[count]; var v = new double[count]; var d = new DateTime[count];
        for (var i = 0; i < count; i++)
        {
            o[i] = 100 + i; h[i] = 101 + i; l[i] = 99 + i; c[i] = 100.5 + i; v[i] = 1000;
            d[i] = new DateTime(2021, 1, 4, 14, 30, 0, DateTimeKind.Utc).AddMinutes(i);
        }

        return StockData.FromColumnViews(o, h, l, c, v, d);
    }

    [Fact]
    public void MutatingAMaterialisedColumnIsWhatTheSpanReports()
    {
        var data = Adopted();

        // Reading materialises the column from the adopted view. The list handed back is the column now.
        data.HighPrices[3] = 12345;
        data.LowPrices[2] = -42;
        data.ClosePrices[1] = 777;
        data.OpenPrices[0] = 5;
        data.Volumes[4] = 9;

        data.HighSpan[3].Should().Be(12345, "the high column is the list the caller was handed");
        data.LowSpan[2].Should().Be(-42);
        data.CloseSpan[1].Should().Be(777);
        data.OpenSpan[0].Should().Be(5);
        data.VolumeSpan[4].Should().Be(9);
    }

    [Fact]
    public void ColumnsOfDifferentLengthsAreRefusedWhenTheyAreAdopted()
    {
        var eight = new double[8];
        var seven = new double[7];
        var dates = new DateTime[8];

        // Adopted columns describe one series between them. Accepting a short one would show up later as an
        // indicator reading past the end of it, a long way from the call that caused it.
        var build = () => StockData.FromColumnViews(eight, eight, seven, eight, eight, dates);

        build.Should().Throw<ArgumentException>()
            .WithMessage("*7 lows*", "the message names the column that does not match and its length");
    }

    [Fact]
    public void AnUntouchedColumnStillReadsFromTheAdoptedViewWithoutCopying()
    {
        var data = Adopted();

        // Nothing has been materialised, so the spans still come straight off the caller's arrays.
        data.HighSpan[3].Should().Be(104);
        data.CloseSpan[1].Should().Be(101.5);
    }
}
