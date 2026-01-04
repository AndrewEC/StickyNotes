namespace StickyNotes.Core.Utils;

using System;
using System.Diagnostics;
using System.IO;

#pragma warning disable CA1001
public class GlobalWatcher
{
    public static readonly GlobalWatcher Instance = new();

    private readonly ConsoleLogger<GlobalWatcher> logger = new();

    private FileSystemWatcher? watcher;

    private GlobalWatcher()
    {
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    public static bool IsStickyNotesAlreadyRunning()
        => Process.GetProcessesByName(StickyNotePaths.AppName).Length > 1;

    public void RequestCreateNewNote()
    {
        try
        {
            string requestNewNoteFilePath = StickyNotePaths.GetRequestNewNoteFilePath();

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
            logger.Log($"Could not create new note request file. Cause: [{e.Message}].");
        }
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        watcher?.Dispose();
    }

    public void WatchForChanges()
    {
        string dataDir = StickyNotePaths.CreateAndGetDataDir();

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

        Store.Instance.QueueCreateNote();

        try
        {
            File.Delete(e.FullPath);
        }
        catch (Exception ex)
        {
            logger.Log($"Failed to delete new note file. Cause: [{ex}].");
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