namespace StickyNotes.State;

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using StickyNotes.Utils;

public interface IBackup
{
    bool TryRestoreNextBackup();
    void TryCreateTodaysBackup();
}

public sealed partial class Backup(IStickyNotePaths stickyNotePaths) : IBackup
{
    private sealed record class BackupSave(string Path, DateTime BackupDate)
    {
        public bool IsOlderThan(BackupSave other)
            => BackupDate.CompareTo(other.BackupDate) < 0;
    }

    private static readonly ConsoleLogger<Backup> logger = new();
    private static readonly int MaxNumberOfBackups = 10;
    private static readonly int DateLength = 10;
    private static readonly Regex BackupFileNameRegex = BackupFileNameRegexBuilder();

    // Backup files names are in the format "notes-yyyy-MM-dd.json"
    [GeneratedRegex(@"^notes-\d{4}-\d{2}-\d{2}\.json$")]
    private static partial Regex BackupFileNameRegexBuilder();

    private readonly IStickyNotePaths stickyNotePaths = stickyNotePaths;
    private int restoreAttempts;

    /// <summary>
    /// This attempts to restore one of the backup save files. The restoration process
    /// works by attempting to locate a backup save then move said save over the current
    /// save file.
    /// <para>
    /// Every time a restoration is attempted a counter will be incremented. This counter
    /// is used to help determine how many restoration attempts have already been made
    /// and which backup file should next be restored.
    /// </para>
    /// <para>
    /// This will not check if the backup save itself is valid.
    /// </para>
    /// </summary>
    /// <returns>True if the restoration process was successful. False if there are no more
    /// backups that can be restored or if there were any exceptions generated when attempting
    /// to copy the backup file.</returns>
    public bool TryRestoreNextBackup()
    {
        logger.Log("Restoring backup...");

        ImmutableArray<BackupSave> backups = FindBackups();

        string saveFilePath = stickyNotePaths.GetSaveFilePath();

        if (!TryGetMostRecentBackup(out BackupSave recentBackupSave, backups, restoreAttempts))
        {
            return false;
        }

        string backupPath = recentBackupSave.Path;
        restoreAttempts++;

        logger.Log($"Restoring backup from: [{backupPath}].");

        if (File.Exists(saveFilePath))
        {
            try
            {
                File.Delete(saveFilePath);
                logger.Log("Removed existing save file.");
            }
            catch (Exception e)
            {
                logger.Error($"Failed to restore backup because current save file could not be deleted.", e);
                return false;
            }
        }

        File.Move(backupPath, saveFilePath);
        
        return true;
    }

    private static bool TryGetMostRecentBackup(
        out BackupSave backupSave, ImmutableArray<BackupSave> backups, int restoreAttempts)
    {
        if (backups.Length == restoreAttempts)
        {
            backupSave = new BackupSave(string.Empty, new DateTime());
            return false;
        }

        BackupSave recent = backups[restoreAttempts];
        for (int i = restoreAttempts + 1; i < backups.Length; i++)
        {
            if (recent.IsOlderThan(backups[i]))
            {
                recent = backups[i];
            }
        }

        backupSave = recent;
        return true;
    }

    /// <summary>
    /// Attempts to create a new backup save. This will copy the current save file
    /// into a new file within the same data directory with name in the format
    /// notes-yyyy-MM-dd.json where yyyy-MM-dd is the system's current date.
    /// <para>
    /// If a backup file with the current date already exists then no backup will
    /// be created.
    /// </para>
    /// <para>
    /// This method will silently fail if an Exception is caught while creating
    /// the backup.
    /// </para>
    /// </summary>
    public void TryCreateTodaysBackup()
    {
        string saveFilePath = stickyNotePaths.GetSaveFilePath();
        if (!File.Exists(saveFilePath))
        {
            logger.Log($"Save file could not be found at [{saveFilePath}]. No backup will be made.");
            return;
        }

        string backupFilePath = stickyNotePaths.GetTodaysBackupSaveFilePath();
        if (File.Exists(backupFilePath))
        {
            logger.Log($"Today's backup has already been created. No backup will be made.");
            return;
        }

        logger.Log($"Backing up file from [{saveFilePath}] to [{backupFilePath}].");

        try
        {
            File.Copy(saveFilePath, backupFilePath);
        }
        catch (Exception e)
        {
            logger.Error($"Failed to backup file to [{backupFilePath}].", e);
            return;
        }

        TrimBackups();
    }

    /// <summary>
    /// This method will ensure there will only be, at most, 10 backup files available.
    /// It will scan the data directory, look for any backups, then delete the
    /// oldest backup if there are more than 10.
    /// <para>
    /// This will swallow any exceptions raised when attempting to delete the oldest
    /// backup.
    /// </para>
    /// </summary>
    private void TrimBackups()
    {
        ImmutableArray<BackupSave> backups = FindBackups();
        logger.Log($"[{backups.Length}] backups found.");
        if (backups.Length <= MaxNumberOfBackups)
        {
            logger.Log("No backups will be trimmed.");
            return;
        }

        logger.Log($"Trimming oldest backup.");

        BackupSave oldestBackup = backups[0];
        for (int i = 1; i < backups.Length; i++)
        {
            if (!oldestBackup.IsOlderThan(backups[i]))
            {
                oldestBackup = backups[i];
            }
        }

        logger.Log($"Deleting oldest backup file: [{oldestBackup.Path}].");

        try
        {
            File.Delete(oldestBackup.Path);
        }
        catch (Exception e)
        {
            logger.Error($"Failed to delete backup file: [{oldestBackup.Path}].", e);
        }
    }

    private ImmutableArray<BackupSave> FindBackups()
        => Directory.GetFiles(stickyNotePaths.CreateAndGetDataDir())
            .Where(path => BackupFileNameRegex.IsMatch(Path.GetFileName(path)))
            .Select(path =>
            {
                if (!ParseDateTime(out DateTime dateTime, path))
                {
                    return null;
                }
                return new BackupSave(path, dateTime);
            })
            .Where(parsed => parsed != null)
            .Select(parsed => parsed!)
            .ToImmutableArray();

    /// <summary>
    /// Attempts to parse a DateTime from a backup file name.
    /// <para>
    /// File names are in the format notes-yyyy-MM-dd.json. This will
    /// first attempt to pull the yyyy-MM-dd portion of the filename before
    /// using DateTime.parse.
    /// </para>
    /// <para>
    /// Since the filename doesn't contain a time component the resulting
    /// DateTime will have a zeroed out time.
    /// </para>
    /// </summary>
    /// <param name="dateTime">If the method returns true this will be set to
    /// a DateTime with the parsed time. Otherwise, this will be set to a
    /// default new DateTime instance.</param>
    /// <param name="path">The path to the backup file to parse the time from.</param>
    /// <returns>True if a DateTime instance could be parsed from the file name,
    /// otherwise false.</returns>
    private static bool ParseDateTime(out DateTime dateTime, string path)
    {
        // File names are in the format notes-yyyy-MM-dd.json.
        // This will first find the yyyy-MM-dd portion of the file name then attempt to
        // parse it to a DateTime object.
        try
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            string dateComponent = fileName.Substring(fileName.Length - DateLength);
            dateTime = DateTime.Parse(dateComponent, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception e)
        {
            logger.Error($"Could not parse DateTime from file: [{path}].", e);
            dateTime = new DateTime();
            return false;
        }
    }
}