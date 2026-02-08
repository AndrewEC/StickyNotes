namespace StickyNotes.Utils;

using System;
using System.Globalization;
using System.IO;
using System.Text;

public interface IStickyNotePaths
{
    string CreateAndGetDataDir();
    string GetSaveFilePath();
    string GetRequestNewNoteFilePath();
    string GetTodaysBackupSaveFilePath();
}

public sealed class StickyNotePaths : IStickyNotePaths
{
    public static readonly string SaveFileName = "notes.json";
    public static readonly CompositeFormat BackupFileNameFormat = CompositeFormat.Parse("notes-{0}.json");
    public static readonly string RequestNewNoteFileName = "newnote";
    public static readonly string AppName = "StickyNotes";
    private static readonly string BackupDateFormat = "yyyy-MM-dd";

    public string CreateAndGetDataDir()
    {
        string appDataRoamingFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string stickyDataFolder = Path.Join(appDataRoamingFolder, AppName);
        if (!Directory.Exists(stickyDataFolder))
        {
            Directory.CreateDirectory(stickyDataFolder);
        }

        return stickyDataFolder;
    }

    public string GetSaveFilePath() => Path.Join(CreateAndGetDataDir(), SaveFileName);

    public string GetRequestNewNoteFilePath() => Path.Join(CreateAndGetDataDir(), RequestNewNoteFileName);

    public string GetTodaysBackupSaveFilePath()
    {
        string dateString = DateTime.Now.ToString(BackupDateFormat, CultureInfo.InvariantCulture);
        string backupFileName = string.Format(CultureInfo.InvariantCulture, BackupFileNameFormat, dateString);
        return Path.Join(CreateAndGetDataDir(), backupFileName);
    }
}