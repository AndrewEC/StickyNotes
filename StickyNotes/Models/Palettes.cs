namespace StickyNotes.Models;

using System.Collections.Generic;
using System.Collections.Immutable;
using Avalonia.Media;

public static class Palettes
{
    public record class Palette(IBrush DarkBrush, IBrush LightBrush);

    private static readonly ImmutableDictionary<ColourStyles, Palette> ConfiguredPalettes = new Dictionary<ColourStyles, Palette>()
    {
        { ColourStyles.Blue, new Palette(Brush.Parse("#99BBFF"), Brush.Parse("#CCDDFF")) },
        { ColourStyles.Pink, new Palette(Brush.Parse("#FFCCFF"), Brush.Parse("#FFE6FF")) },
        { ColourStyles.Green, new Palette(Brush.Parse("#99FF99"), Brush.Parse("#CCFFCC")) }
    }.ToImmutableDictionary();

    public static Palette GetPalette(ColourStyles style) => ConfiguredPalettes[style];
}