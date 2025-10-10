namespace StickyNotes.Utils;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

public sealed class Backup
{
    private sealed class BackupSave(string path, DateTime backupDate)
    {
        public readonly string Path = path;

        public readonly DateTime BackupDate = backupDate;
    }

    private static readonly ConsoleLogger<Backup> logger = new();
    private static readonly int MaxNumberOfBackups = 10;
    private static readonly string JsonExtension = ".json";
    private static readonly int BackupFileNameLength = 21;
    private static readonly int DateLength = 10;

    public static void BackupNotes()
    {
        string saveFilePath = StickyNotePaths.GetSaveFilePath();
        if (!File.Exists(saveFilePath))
        {
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
        List<BackupSave> backups = FindBackups();
        if (backups.Count <= MaxNumberOfBackups)
        {
            return;
        }

        BackupSave oldest = backups[0];
        for (int i = 1; i < backups.Count; i++)
        {
            if (oldest.BackupDate.CompareTo(backups[i].BackupDate) > 0)
            {
                oldest = backups[i];
            }
        }

        logger.Log($"Deleting oldest backup file: [{oldest.Path}].");

        File.Delete(oldest.Path);
    }

    private static List<BackupSave> FindBackups()
    {
        string dataDir = StickyNotePaths.CreateAndGetDataDir();

        List<BackupSave> backups = [];
        foreach (string path in Directory.GetFiles(dataDir))
        {
            if (Path.GetExtension(path) != JsonExtension)
            {
                continue;
            }

            DateTime backupDate = ParseDateTime(path);
            if (backupDate != default)
            {
                backups.Add(new BackupSave(path, backupDate));
            }
        }
        return backups;
    }

    private static DateTime ParseDateTime(string path)
    {
        string fullFileName = Path.GetFileName(path);
        if (fullFileName.Length < BackupFileNameLength)
        {
            return default;
        }

        string fileName = Path.GetFileNameWithoutExtension(path);
        string dateComponent = fileName.Substring(fileName.Length - DateLength);

        try
        {
            return DateTime.Parse(dateComponent, CultureInfo.InvariantCulture);
        }
        catch (Exception e)
        {
            logger.Log($"Failed to parse DateTime from file name: [{fileName}]. Cause: [{e.Message}].");
            return default;
        }
    }
}