namespace StickyNotes.Tests;

using System;
using System.IO;
using System.Text;
using StickyNotes.Utils;

internal static class TestUtils
{
    public static readonly string TestDirName = "TestData";
    public static readonly string TestDataDir = Path.Join(AppContext.BaseDirectory, TestDirName);
    public static readonly string SaveFilePath = Path.Join(TestDataDir, StickyNotePaths.SaveFileName);

    public static readonly string ValidBackupFileName = "notes-2025-01-21.json";
    public static readonly string ValidBackupFilePath = Path.Join(TestDataDir, ValidBackupFileName);
    public static readonly string InvalidBackupFileName = "notes-invalid-backup.json";
    public static readonly string InvalidBackupFilePath = Path.Join(TestDataDir, InvalidBackupFileName);

    public static void CreateFile(string filePath, string? content = null)
    {
        using (FileStream stream = File.Create(filePath))
        {
            if (content != null)
            {
                stream.Write(Encoding.UTF8.GetBytes(content));
            }
        }
    }
}