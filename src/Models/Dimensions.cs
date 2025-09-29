namespace StickyNotes.Models;

using System;
using System.Text;

public sealed class Dimensions : ICloneable
{
    public Dimensions() { }

    public Dimensions(int width, int height, int x, int y)
    {
        Width = width;
        Height = height;
        X = x;
        Y = y;
    }

    public int Width { get; set; } = 400;

    public int Height { get; set; } = 400;

    public int X { get; set; } = 200;

    public int Y { get; set; } = 200;

    public object Clone() => new Dimensions(Width, Height, X, Y);

#pragma warning disable CA1834
    public override string ToString() => new StringBuilder()
        .Append("Dimensions(")
        .Append(nameof(Width)).Append("=[").Append(Width).Append("], ")
        .Append(nameof(Height)).Append("=[").Append(Height).Append("], ")
        .Append(nameof(X)).Append("=[").Append(X).Append("], ")
        .Append(nameof(Y)).Append("=[").Append(Y).Append("])")
        .ToString();
#pragma warning restore CA1834
}