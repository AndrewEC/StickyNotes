namespace StickyNotes.Utils;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Timers;
using StickyNotes.Models;

public delegate void NoteCreated(Note note);

public delegate void NoteDeleted(Note note);

#pragma warning disable CA1001
public sealed class Store
{
    public static readonly Store Instance = new();

    private static readonly object SyncLock = new();
    private static readonly string CreateNewNoteInstructionId = "new_note_instruction";

    public event NoteCreated? OnNoteCreated;
    public event NoteDeleted? OnNoteDeleted;

    private readonly ConsoleLogger<Store> logger = new();
    private readonly string saveFilePath = StickyNotePaths.GetSaveFilePath();
    private readonly Timer timer;
    private readonly Dictionary<string, UpdateInstruction> pendingUpdates = [];

    private bool isInitialized;
    private List<Note> notes = [];

    private Store()
    {
        timer = new Timer(100);
        timer.Elapsed += OnTimerElapsed;
        timer.AutoReset = true;
        timer.Enabled = true;
        timer.Start();
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (sender != timer)
        {
            return;
        }

        lock (SyncLock)
        {
            ApplyPendingUpdates();
        }
    }

    private void ApplyPendingUpdates()
    {
        if (pendingUpdates.Count == 0)
        {
            return;
        }

        logger.Log($"Applying [{pendingUpdates.Count}] note updates.");
        foreach (UpdateInstruction instruction in pendingUpdates.Values)
        {
            switch (instruction.Type)
            {
                case UpdateInstruction.InstructionType.Create:
                    ApplyCreateInstruction();
                    break;
                case UpdateInstruction.InstructionType.Update:
                    ApplyUpdateInstruction(instruction.Note);
                    break;
                case UpdateInstruction.InstructionType.Delete:
                    ApplyDeleteInstruction(instruction.Note);
                    break;
            }
        }

        pendingUpdates.Clear();

        if (notes.Count == 0)
        {
            logger.Log($"No notes remain. Save file [{saveFilePath}] will be deleted.");
            File.Delete(saveFilePath);
        }
        else
        {
            string json = JsonSerializer.Serialize(notes);
            File.WriteAllText(saveFilePath, json);
        }
    }

    private void ApplyCreateInstruction()
    {
        logger.Log($"Creating new note.");

        Note newNote = new();
        while (DoesNoteIdExist(newNote))
        {
            newNote = new();
        }

        notes.Add((Note)newNote.Clone());
        OnNoteCreated?.Invoke((Note)newNote.Clone());
    }

    private void ApplyUpdateInstruction(Note note)
    {
        logger.Log($"Applying update instruction to note: [{note.Id}].");

        int index = notes.FindIndex(n => n.Id == note.Id);
        notes[index] = (Note)note.Clone();
    }

    private void ApplyDeleteInstruction(Note note)
    {
        logger.Log($"Applying delete instruction to note: [{note.Id}].");

        int index = notes.FindIndex(n => n.Id == note.Id);
        if (index == -1)
        {
            logger.Log($"Could not apply instruction to note with ID [{note.Id}] because said note could not be found.");
            return;
        }

        Note noteToDelete = notes[index];
        notes.RemoveAt(index);
        OnNoteDeleted?.Invoke((Note)noteToDelete.Clone());
    }

    public void LoadNotes()
    {
        lock (SyncLock)
        {
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;

            logger.Log($"Loading notes from save file [{saveFilePath}].");
            if (!File.Exists(saveFilePath))
            {
                logger.Log("Save file path not found. Defaulting to single empty note.");
                notes = [new Note()];
            }
            else
            {
                string fileContents = File.ReadAllText(saveFilePath);
                notes = JsonSerializer.Deserialize<List<Note>>(fileContents)!;
                logger.Log($"Loaded [{notes.Count}] notes from save file.");
            }

            if (OnNoteCreated != null)
            {
                foreach (Note note in notes)
                {
                    OnNoteCreated.Invoke((Note)note.Clone());
                }
            }
        }
    }

    public void QueueCreateNote()
    {
        lock (SyncLock)
        {
            pendingUpdates[CreateNewNoteInstructionId] = new(UpdateInstruction.InstructionType.Create, new Note());
        }
    }

    private bool DoesNoteIdExist(Note note)
        => notes.Any(n => n.Id == note.Id);

    public void QueueUpdateNote(Note note)
    {
        lock (SyncLock)
        {
            if (IsNoteScheduledForDeletion(note))
            {
                logger.Log($"An update for note [{note.Id}] was requested but it is in the process of being deleted. "
                    + "Update will be ignored.");
                return;
            }
            else if (IsNoteScheduledForCreation(note))
            {
                logger.Log($"An update for note [{note.Id}] was requested but it is in the process of being created. "
                    + "Update will be ignored.");
                return;
            }

            pendingUpdates[note.Id] = new(UpdateInstruction.InstructionType.Update, (Note)note.Clone());
        }
    }

    private bool IsNoteScheduledForDeletion(Note note)
        => pendingUpdates.TryGetValue(note.Id, out UpdateInstruction? existing)
            && existing.Type == UpdateInstruction.InstructionType.Delete;

    private bool IsNoteScheduledForCreation(Note note)
        => pendingUpdates.TryGetValue(note.Id, out UpdateInstruction? existing)
            && existing.Type == UpdateInstruction.InstructionType.Create;

    public void QueueDeleteNote(Note note)
    {
        lock (SyncLock)
        {
            pendingUpdates[note.Id] = new(UpdateInstruction.InstructionType.Delete, (Note)note.Clone());
        }
    }
}
