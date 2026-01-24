namespace StickyNotes.Core.Utils;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Timers;
using StickyNotes.Core.Models;

public delegate void NoteCreated(Note note);

public delegate void NoteDeleted(Note note);

#pragma warning disable CA1852
[JsonSerializable(typeof(Note))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(Dimensions))]
[JsonSerializable(typeof(ColourStyles))]
[JsonSerializable(typeof(List<Note>))]
internal partial class NoteContext : JsonSerializerContext
{
}
#pragma warning restore CA1852

#pragma warning disable CA1001
public sealed class Store
{
    private static readonly JsonSerializerOptions NoteSerializerOptions = new()
    {
        TypeInfoResolver = NoteContext.Default
    };

    public static readonly Store Instance = new();

    private static readonly System.Threading.Lock SyncLock = new();
    private static readonly string CreateNewNoteInstructionId = "new_note_instruction";
    private static readonly string LoadFailedMessage = "Your previous notes could not be loaded and all recovery attempts failed. "
        + "The save file may have been corrupted or cannot be accessed.";
    private static readonly string LoadRecoveredMessage = "You notes could not be loaded. "
        + "A previous version of your notes was recovered and loaded instead.";

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

    public void Initialize()
    {
        lock (SyncLock)
        {
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;

            LoadStatus status = LoadStatus.Success;

            logger.Log($"Loading notes from save file [{saveFilePath}].");
            if (!File.Exists(saveFilePath))
            {
                logger.Log("Save file path not found. Defaulting to single empty note.");
                notes = [new Note()];
            }
            else
            {
                status = LoadNotes(out notes);
            }

            Backup.BackupNotes();

            if (status == LoadStatus.Failed)
            {
                notes.Add(new Note() { Body = LoadFailedMessage });
            }
            else if (status == LoadStatus.Recovered)
            {
                notes.Add(new Note() { Body = LoadRecoveredMessage });
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

    private LoadStatus LoadNotes(out List<Note> notes)
    {
        LoadStatus status = LoadStatus.Success;
        while (true)
        {
            if (TryLoadCurrentNotes(out List<Note> savedNotes))
            {
                notes = savedNotes;
                break;
            }
            else if (!Backup.TryRestoreNextBackup())
            {
                notes = [];
                status = LoadStatus.Failed;
                break;
            }
            else
            {
                status = LoadStatus.Recovered;
            }
        }
        return status;
    }

    private bool TryLoadCurrentNotes(out List<Note> notes)
    {
        try
        {   
            string fileContents = File.ReadAllText(saveFilePath);
            notes = JsonSerializer.Deserialize<List<Note>>(fileContents, NoteSerializerOptions)!;
            if (notes.Count == 0)
            {
                logger.Log("No notes found in save file. Defaulting to single empty note.");
                notes = [new Note()];
            }
            logger.Log($"Loaded [{notes.Count}] notes from save file.");
            return true;
        }
        catch (Exception e)
        {
            logger.Log($"Failed to load notes from JSON file. Cause: [{e}].");
            notes = [];
            return true;
        }
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
            switch (instruction.UpdateType)
            {
                case InstructionType.Create:
                    ApplyCreateInstruction();
                    break;
                case InstructionType.Update:
                    ApplyUpdateInstruction(instruction.Note);
                    break;
                case InstructionType.Delete:
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
            string json = JsonSerializer.Serialize(notes, NoteSerializerOptions);
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

    private bool DoesNoteIdExist(Note note)
        => notes.Any(n => n.Id == note.Id);

    private void ApplyUpdateInstruction(Note note)
    {
        logger.Log($"Applying update instruction to note: [{note.Id}].");

        int index = notes.FindIndex(n => n.Id == note.Id);
        if (index == -1)
        {
            logger.Log($"Could not apply update instruction to note with ID [{note.Id}] because said note could not be found.");
            return;
        }

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

    public void QueueCreateNote()
    {
        lock (SyncLock)
        {
            pendingUpdates[CreateNewNoteInstructionId] = new UpdateInstruction(InstructionType.Create, new Note());
        }
    }

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

            pendingUpdates[note.Id] = new UpdateInstruction(InstructionType.Update, (Note)note.Clone());
        }
    }

    public void QueueDeleteNote(Note note)
    {
        lock (SyncLock)
        {
            pendingUpdates[note.Id] = new UpdateInstruction(InstructionType.Delete, (Note)note.Clone());
        }
    }

    private bool IsNoteScheduledForDeletion(Note note)
        => pendingUpdates.TryGetValue(note.Id, out UpdateInstruction? existing)
            && existing.UpdateType == InstructionType.Delete;
}
