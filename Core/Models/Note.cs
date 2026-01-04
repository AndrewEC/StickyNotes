namespace StickyNotes.Core.Models;

using System;
using System.Text;

public sealed class Note : ICloneable
{
    public string Body { get; set; } = string.Empty;

    public string Id { get; set; } = Guid.NewGuid().ToString();

    public Dimensions NoteWindowDimensions { get; set; } = new();

    public ColourStyles ColourStyle { get; set; } = ColourStyles.Pink;

    public object Clone() => new Note()
    {
        Body = Body,
        Id = Id,
        NoteWindowDimensions = (Dimensions)NoteWindowDimensions.Clone(),
        ColourStyle = ColourStyle
    };

    public override string ToString() => new StringBuilder()
        .Append("Note(")
        .Append(nameof(Body)).Append("=[").Append(Body).Append("], ")
        .Append(nameof(Id)).Append("=[").Append(Id).Append("], ")
        .Append(nameof(NoteWindowDimensions)).Append("=[").Append(NoteWindowDimensions).Append("], ")
        .Append(nameof(ColourStyle)).Append("=[").Append(ColourStyle.ToString()).Append(']')
        .Append(')')
        .ToString();
}