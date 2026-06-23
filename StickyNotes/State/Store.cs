namespace StickyNotes.State;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Text.Json.Serialization;
using StickyNotes.Models;
using StickyNotes.Utils;

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

public interface IStore
{
    event NoteCreated? OnNoteCreated;
    event NoteDeleted? OnNoteDeleted;
    void Initialize();
    void QueueCreateNote();
    void QueueUpdateNote(Note note);
    void QueueDeleteNote(Note note);
    void Flush();
}

#pragma warning disable CA1001
public sealed class Store : IStore
{
    private static readonly int DebounceTime = 5_000;
    private static readonly JsonSerializerOptions NoteSerializerOptions = new()
    {
        TypeInfoResolver = NoteContext.Default
    };

    private static readonly System.Threading.Lock SyncLock = new();
    private static readonly string CreateNewNoteInstructionId = "new_note_instruction";
    private static readonly string LoadFailedMessage = "Your previous notes could not be loaded and all recovery attempts failed. "
        + "The save file may have been corrupted or cannot be accessed.";
    private static readonly string LoadRecoveredMessage = "You notes could not be loaded. "
        + "A previous version of your notes was recovered and loaded instead.";

    public event NoteCreated? OnNoteCreated;
    public event NoteDeleted? OnNoteDeleted;

    private readonly IBackup backup;

    private readonly ConsoleLogger<Store> logger = new();
    private readonly Dictionary<string, UpdateInstruction> pendingUpdates = [];
    private readonly Subject<bool> debounceSubject = new();
    private readonly string saveFilePath;

    private bool isInitialized;
    private List<Note> notes = [];

    public Store(IStickyNotePaths stickyNotePaths, IBackup backup)
    {
        this.backup = backup;
        saveFilePath = stickyNotePaths.GetSaveFilePath();

        debounceSubject.Throttle(TimeSpan.FromMilliseconds(DebounceTime))
            .Subscribe(OnUpdateSubject);
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

            if (status == LoadStatus.Success || status == LoadStatus.Recovered)
            {
                backup.TryCreateTodaysBackup();
            }

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

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
        }
    }

    /// <summary>
    /// The process for loading notes attempts to load the main save
    /// file from the data directory then, if that fails, restoring a backup
    /// file then trying to load said backup. This process continues until
    /// a save has been successfully loaded or when there are no more backups
    /// to restore.
    /// <para>
    /// This assumes that the main save file exists. If no save file exists then
    /// this should not be invoked.
    /// </para>
    /// </summary>
    /// <param name="notes"></param>
    /// <returns>
    /// If the main save is loaded without issue (without needing to restore
    /// a backup) then the return value is Success. If the main save was corrupt
    /// and instead the data was loaded from a backup then the status will be
    /// Recovered. If data could not be loaded from the main save or from any backups
    /// then the status will be Failed.
    /// </returns>
    private LoadStatus LoadNotes(out List<Note> notes)
    {
        LoadStatus status = LoadStatus.Success;
        while (true)
        {
            if (TryLoadCurrentNotes(out List<Note> savedNotes))
            {
                logger.Log($"Successfully loaded [{savedNotes.Count}] notes.");
                notes = savedNotes;
                break;
            }
            else if (!backup.TryRestoreNextBackup())
            {
                logger.Log("Attempting to recover notes from previous backup.");
                notes = [];
                status = LoadStatus.Failed;
                break;
            }
            else
            {
                logger.Log("Recovered notes from previous backup.");
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
            logger.Error($"Failed to load notes from JSON file.", e);
            notes = [];
            return false;
        }
    }

    private void OnUpdateSubject(bool _)
    {
        logger.Log("Update subject.");

        lock (SyncLock)
        {
            Flush();
        }
    }

    public void Flush()
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
            try
            {
                string json = JsonSerializer.Serialize(notes, NoteSerializerOptions);
                File.WriteAllText(saveFilePath, json);
            }
            catch (Exception e)
            {
                logger.Error("Failed to save notes to file.", e);
            }
        }

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
    }

    private void ApplyCreateInstruction()
    {
        logger.Log($"Creating new note.");

        Note newNote = new();
        while (DoesNoteIdExist(newNote))
        {
            newNote = new();
        }

        notes.Add(newNote);
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

        notes[index] = note;
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
            Flush();
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
            debounceSubject.OnNext(true);
        }
    }

    public void QueueDeleteNote(Note note)
    {
        lock (SyncLock)
        {
            pendingUpdates[note.Id] = new UpdateInstruction(InstructionType.Delete, (Note)note.Clone());
            Flush();
        }
    }

    private bool IsNoteScheduledForDeletion(Note note)
        => pendingUpdates.TryGetValue(note.Id, out UpdateInstruction? existing)
            && existing.UpdateType == InstructionType.Delete;
}
