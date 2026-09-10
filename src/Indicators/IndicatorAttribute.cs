//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>
/// Marks a type as an indicator and gives it a display name.
/// </summary>
/// <remarks>
/// <para>
/// Optional. An <see cref="IndicatorBase"/> is an indicator whether or not it carries this, and takes
/// its name from the type. The attribute is for the cases the type name cannot carry: a display name
/// with spacing and capitalisation of its own, and a description.
/// </para>
/// <code>
/// [Indicator("Squeeze Momentum", Description = "Bollinger and Keltner compression.")]
/// public sealed class SqueezeMomentum : IndicatorBase { ... }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class IndicatorAttribute : Attribute
{
    /// <summary>
    /// Marks the type as an indicator, named after the type.
    /// </summary>
    public IndicatorAttribute()
    {
    }

    /// <summary>
    /// Marks the type as an indicator with an explicit display name.
    /// </summary>
    /// <param name="name">The name to show, for example "Squeeze Momentum".</param>
    public IndicatorAttribute(string name)
    {
        Name = name;
    }

    /// <summary>The display name, or null to use the type name.</summary>
    public string? Name { get; }

    /// <summary>A short description of what the indicator measures.</summary>
    public string? Description { get; set; }
}
