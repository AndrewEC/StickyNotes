namespace StickyNotes.Core.Utils;

using System;
using System.Globalization;
using System.IO;
using System.Text;

public static class StickyNotePaths
{
    public static readonly string SaveFileName = "notes.json";
    public static readonly CompositeFormat BackupFileNameFormat = CompositeFormat.Parse("notes-{0}.json");
    public static readonly string RequestNewNoteFileName = "newnote";
    public static readonly string AppName = "StickyNotes";
    private static readonly string BackupDateFormat = "yyyy-MM-dd";

    public static string CreateAndGetDataDir()
    {
        string appDataRoamingFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string stickyDataFolder = Path.Join(appDataRoamingFolder, AppName);
        if (!Directory.Exists(stickyDataFolder))
        {
            Directory.CreateDirectory(stickyDataFolder);
        }

        return stickyDataFolder;
    }

    public static string GetSaveFilePath() => Path.Join(CreateAndGetDataDir(), SaveFileName);

    public static string GetRequestNewNoteFilePath() => Path.Join(CreateAndGetDataDir(), RequestNewNoteFileName);

    public static string GetTodaysBackupSaveFilePath()
    {
        string dateString = DateTime.Now.ToString(BackupDateFormat, CultureInfo.InvariantCulture);
        string backupFileName = string.Format(CultureInfo.InvariantCulture, BackupFileNameFormat, dateString);
        return Path.Join(CreateAndGetDataDir(), backupFileName);
    }
}