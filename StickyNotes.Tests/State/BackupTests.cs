namespace StickyNotes.Tests.State;

using System.IO;
using System.Linq;
using Moq;
using StickyNotes.State;
using StickyNotes.Utils;

#pragma warning disable CA1707
[TestFixture]
public sealed class BackupTests
{
    private readonly Mock<IStickyNotePaths> mockPaths = new(MockBehavior.Strict);
    private readonly Backup backup;

    public BackupTests()
    {
        backup = new(mockPaths.Object);
    }

    [SetUp]
    public void SetUp()
    {
        if (Directory.Exists(TestUtils.TestDataDir))
        {
            Directory.Delete(TestUtils.TestDataDir, true);
        }

        Directory.CreateDirectory(TestUtils.TestDataDir);
    }

    [TearDown]
    public void TearDown()
    {
        mockPaths.VerifyAll();
        mockPaths.Reset();
    }

    private void MockSaveFilePath() => mockPaths.Setup(mock => mock.GetSaveFilePath())
        .Returns(TestUtils.SaveFilePath)
        .Verifiable();

    private void MockValidBackupFilePath() => mockPaths.Setup(mock => mock.GetTodaysBackupSaveFilePath())
        .Returns(TestUtils.ValidBackupFilePath)
        .Verifiable();

    private void MockCreateAndGetDataDir() => mockPaths.Setup(mock => mock.CreateAndGetDataDir())
        .Returns(TestUtils.TestDataDir)
        .Verifiable();

    [Test]
    public void TryCreateTodaysBackup_ShouldNotCreateBackup_WhenNoSaveFileExists()
    {
        MockSaveFilePath();

        Assert.That(Directory.GetFiles(TestUtils.TestDataDir), Has.Length.EqualTo(0));

        backup.TryCreateTodaysBackup();

        Assert.That(Directory.GetFiles(TestUtils.TestDataDir), Has.Length.EqualTo(0));
    }

    [Test]
    public void TryCreateTodaysBackup_ShouldCreateBackup_WhenSaveFileExists()
    {
        MockSaveFilePath();
        MockValidBackupFilePath();
        MockCreateAndGetDataDir();

        Assert.That(Directory.GetFiles(TestUtils.TestDataDir), Has.Length.EqualTo(0));

        TestUtils.CreateFile(TestUtils.SaveFilePath);

        backup.TryCreateTodaysBackup();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Directory.GetFiles(TestUtils.TestDataDir), Has.Length.EqualTo(2));
            Assert.That(File.Exists(TestUtils.SaveFilePath), Is.True);
            Assert.That(File.Exists(TestUtils.ValidBackupFilePath), Is.True);
        }
    }

    [Test]
    public void TryCreateTodaysBackup_ShouldTrimBackup_WhenMoreThanTenBackupsExist()
    {
        MockSaveFilePath();
        MockValidBackupFilePath();
        MockCreateAndGetDataDir();

        Assert.That(Directory.GetFiles(TestUtils.TestDataDir), Has.Length.EqualTo(0));

        TestUtils.CreateFile(TestUtils.SaveFilePath);
        string[] backupFilePaths = Enumerable.Range(10, 10)
            .Select(i => "notes-2025-02-" + i + ".json")
            .Select(name => Path.Join(TestUtils.TestDataDir, name))
            .ToArray();

        foreach (string backupFilePath in backupFilePaths)
        {
            TestUtils.CreateFile(backupFilePath);
        }

        Assert.That(Directory.GetFiles(TestUtils.TestDataDir), Has.Length.EqualTo(11));

        backup.TryCreateTodaysBackup();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Directory.GetFiles(TestUtils.TestDataDir), Has.Length.EqualTo(11));
            Assert.That(File.Exists(TestUtils.ValidBackupFilePath), Is.False); // Oldest backup. Should be deleted.
            foreach (string backupFilePath in backupFilePaths)
            {
                Assert.That(File.Exists(backupFilePath), Is.True, $"File expected but not found at: {backupFilePath}");
            }
        }
    }

    [Test]
    public void TryRestoreTodaysBackup_ShouldRestoreBackup_WhenASingleValidBackupExists()
    {
        MockCreateAndGetDataDir();
        MockSaveFilePath();

        Assert.That(Directory.GetFiles(TestUtils.TestDataDir), Has.Length.EqualTo(0));

        TestUtils.CreateFile(TestUtils.SaveFilePath);
        TestUtils.CreateFile(TestUtils.ValidBackupFilePath);

        Assert.That(Directory.GetFiles(TestUtils.TestDataDir), Has.Length.EqualTo(2));

        backup.TryRestoreNextBackup();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Directory.GetFiles(TestUtils.TestDataDir), Has.Length.EqualTo(1));
            Assert.That(File.Exists(TestUtils.SaveFilePath), Is.True);
            Assert.That(File.Exists(TestUtils.ValidBackupFilePath), Is.False);
        }
    }

    [Test]
    public void TryRestoreTodaysBackup_ShouldNotRestoreBackup_WhenOnlyBackupFileIsInvalid()
    {
        MockCreateAndGetDataDir();
        MockSaveFilePath();

        Assert.That(Directory.GetFiles(TestUtils.TestDataDir), Has.Length.EqualTo(0));

        TestUtils.CreateFile(TestUtils.SaveFilePath);
        TestUtils.CreateFile(TestUtils.InvalidBackupFilePath);

        Assert.That(Directory.GetFiles(TestUtils.TestDataDir), Has.Length.EqualTo(2));
        
        backup.TryRestoreNextBackup();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Directory.GetFiles(TestUtils.TestDataDir), Has.Length.EqualTo(2));
            Assert.That(File.Exists(TestUtils.SaveFilePath), Is.True);
            Assert.That(File.Exists(TestUtils.InvalidBackupFilePath), Is.True);
        }
    }
}
#pragma warning restore CA1707