namespace StickyNotes.Models;

public record class UpdateInstruction(InstructionType UpdateType, Note Note);
