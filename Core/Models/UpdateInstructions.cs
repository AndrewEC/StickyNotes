namespace StickyNotes.Core.Models;

public record UpdateInstruction(InstructionType UpdateType, Note Note);
