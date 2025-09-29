namespace StickyNotes.Utils;

using System;
using System.IO;

public static class StickyNotePaths
{
    public static readonly string SaveFileName = "notes.json";
    public static readonly string RequestNewNoteFileName = "newnote";
    public static readonly string AppName = "StickyNotes";

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
}