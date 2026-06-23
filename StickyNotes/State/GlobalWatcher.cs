namespace StickyNotes.State;

using System;
using System.Diagnostics;
using System.IO;
using StickyNotes.Utils;

public interface IGlobalWatcher
{
    void RequestCreateNewNote();
    void WatchForChanges();
}

#pragma warning disable CA1001
public sealed class GlobalWatcher : IGlobalWatcher
{
    private readonly ConsoleLogger<GlobalWatcher> logger = new();
    private readonly IStickyNotePaths stickyNotePaths;
    private readonly IStore store;

    private FileSystemWatcher? watcher;

    public GlobalWatcher(IStickyNotePaths stickyNotePaths, IStore store)
    {
        this.stickyNotePaths = stickyNotePaths;
        this.store = store;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    public static bool IsStickyNotesAlreadyRunning()
        => Process.GetProcessesByName(StickyNotePaths.AppName).Length > 1;

    /// <summary>
    /// Creates an empty file in the StickyNotes data directory. When the new file
    /// is created an already running instance of StickyNotes will detect the new file
    /// then create a new empty sticky note window.
    /// <para>
    /// This should only be invoked when the user attempts to launch StickyNotes if
    /// StickyNotes is already running so we don't have two instances of the process.
    /// </para>
    /// </summary>
    public void RequestCreateNewNote()
    {
        try
        {
            string requestNewNoteFilePath = stickyNotePaths.GetRequestNewNoteFilePath();

            logger.Log($"Writing new note request file to: [{requestNewNoteFilePath}].");
            if (File.Exists(requestNewNoteFilePath))
            {
                logger.Log("New note request file already exists. Deleting file.");
                File.Delete(requestNewNoteFilePath);
            }

            File.WriteAllText(requestNewNoteFilePath, string.Empty);
        }
        catch (Exception e)
        {
            logger.Error($"Could not create new note request file.", e);
        }
    }

    private void OnProcessExit(object? sender, EventArgs e) => watcher?.Dispose();

    public void WatchForChanges()
    {
        string dataDir = stickyNotePaths.CreateAndGetDataDir();

        logger.Log($"Watching for changes in data directory: [{dataDir}].");

        watcher?.Dispose();

        watcher = new(dataDir);
        watcher.Created += OnFileCreated;
        watcher.IncludeSubdirectories = false;
        watcher.EnableRaisingEvents = true;
    }

    private void OnFileCreated(object? sender, FileSystemEventArgs e)
    {
        logger.Log($"Detected new file created in data dir with path: [{e.FullPath}].");

        if (!IsFileIndicatingNewNoteShouldBeCreated(e.FullPath))
        {
            return;
        }

        logger.Log("Detected new note requested file in data dir. Creating new note.");

        store.QueueCreateNote();

        try
        {
            File.Delete(e.FullPath);
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to delete new note file.", ex);
        }

        // Call to dispose of the current file watcher and create a new one.
        // This is to resolve an issue where the file watcher stops reporting
        // changes for reasons unknown.
        WatchForChanges();
    }

    private static bool IsFileIndicatingNewNoteShouldBeCreated(string fullPath)
        => File.Exists(fullPath)
            && fullPath.EndsWith(StickyNotePaths.RequestNewNoteFileName, StringComparison.InvariantCultureIgnoreCase);
}