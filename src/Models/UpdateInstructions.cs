namespace StickyNotes.Models;

public sealed class UpdateInstruction(UpdateInstruction.InstructionType updateType, Note note)
{
    public enum InstructionType
    {
        Create,
        Update,
        Delete,
    }

    public InstructionType Type { get; } = updateType;

    public Note Note { get; } = note;
}