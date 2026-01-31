namespace StickyNotes.Core.Models;

public record class UpdateInstruction(InstructionType UpdateType, Note Note);
