namespace StickyNotes.Models;

public record UpdateInstruction(InstructionType UpdateType, Note Note);
