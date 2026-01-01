namespace StickyNotes.Utils;

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public sealed partial class Backup
{
    private sealed class BackupSave(string path, DateTime backupDate)
    {
        public readonly string Path = path;

        public readonly DateTime BackupDate = backupDate;

        public bool IsOlderThan(BackupSave other)
            => BackupDate.CompareTo(other.BackupDate) < 0;
    }

    private static readonly ConsoleLogger<Backup> logger = new();
    private static readonly int MaxNumberOfBackups = 10;
    private static readonly int DateLength = 10;
    private static readonly Regex BackupFileNameRegex = BackupFileNameRegexBuilder();

    [GeneratedRegex(@"^notes-\d{4}-\d{2}-\d{2}\.json$")]
    private static partial Regex BackupFileNameRegexBuilder();

    public static bool RestoreNote()
    {
        ImmutableArray<BackupSave> backups = FindBackups();
        if (backups.Length == 0)
        {
            return false;
        }

        string saveFilePath = StickyNotePaths.GetSaveFilePath();
        if (File.Exists(saveFilePath))
        {
            try
            {
                File.Delete(saveFilePath);
            }
            catch (Exception e)
            {
                logger.Log($"Failed to restore backup because current save file could not be deleted. Cause: [{e}].");
                return false;
            }
        }

        File.Move(GetMostRecentBackup(backups).Path, saveFilePath);
        
        return true;
    }

    private static BackupSave GetMostRecentBackup(ImmutableArray<BackupSave> backups)
    {
        BackupSave recent = backups[0];
        for (int i = 1; i < backups.Length; i++)
        {
            if (recent.IsOlderThan(backups[i]))
            {
                recent = backups[i];
            }
        }
        return recent;
    }

    public static void BackupNotes()
    {
        string saveFilePath = StickyNotePaths.GetSaveFilePath();
        if (!File.Exists(saveFilePath))
        {
            logger.Log($"Save file could not be found at [{saveFilePath}]. No backup will be made.");
            return;
        }

        string backupFilePath = StickyNotePaths.GetTodaysBackupSaveFilePath();
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
            logger.Log($"Failed to backup file to [{backupFilePath}]. Cause: [{e.Message}].");
            return;
        }

        TrimBackups();
    }

    private static void TrimBackups()
    {
        ImmutableArray<BackupSave> backups = FindBackups();
        if (backups.Length <= MaxNumberOfBackups)
        {
            return;
        }

        logger.Log($"[{backups.Length}] backups found. Oldest will be deleted.");

        BackupSave oldestBackup = backups[0];
        for (int i = 1; i < backups.Length; i++)
        {
            if (!oldestBackup.IsOlderThan(backups[i]))
            {
                oldestBackup = backups[i];
            }
        }

        logger.Log($"Deleting oldest backup file: [{oldestBackup.Path}].");

        File.Delete(oldestBackup.Path);
    }

    private static ImmutableArray<BackupSave> FindBackups()
        => Directory.GetFiles(StickyNotePaths.CreateAndGetDataDir())
            .Where(path => BackupFileNameRegex.IsMatch(Path.GetFileName(path)))
            .Select(path => new BackupSave(path, ParseDateTime(path)))
            .ToImmutableArray();

    private static DateTime ParseDateTime(string path)
    {
        string fileName = Path.GetFileNameWithoutExtension(path);
        string dateComponent = fileName.Substring(fileName.Length - DateLength);
        return DateTime.Parse(dateComponent, CultureInfo.InvariantCulture);
    }
}