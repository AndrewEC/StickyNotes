namespace StickyNotes.Tests.State;

using System.Collections.Generic;
using System.IO;
using Moq;
using StickyNotes.Models;
using StickyNotes.State;
using StickyNotes.Utils;

#pragma warning disable CA1707
[TestFixture]
public sealed class StoreTests
{
    private static readonly string InvalidNoteJson = "[{]}";
    private static readonly string ValidNoteJson = "[{\"Body\":\"This is a Test Note.\",\"Id\":\"dc3bfb1d-31e6-4134-ad4d-61c285d313e5\",\"NoteWindowDimensions\":{\"Width\":10,\"Height\":20,\"X\":30,\"Y\":40},\"ColourStyle\":0}]";

    private static readonly string LoadRecoveredMessage = "You notes could not be loaded. "
        + "A previous version of your notes was recovered and loaded instead.";
    private static readonly string LoadFailedMessage = "Your previous notes could not be loaded and all recovery attempts failed. "
        + "The save file may have been corrupted or cannot be accessed.";

    private readonly Mock<IStickyNotePaths> mockPaths = new(MockBehavior.Strict);
    private readonly Mock<IBackup> mockBackup = new(MockBehavior.Strict);

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
        mockPaths.Reset();
        mockBackup.Reset();
    }

    [Test]
    public void Initialize_ShouldFullyInitialize_WhenSaveFileExistsAndIsValid()
    {
        TestUtils.CreateFile(TestUtils.SaveFilePath, ValidNoteJson);
        Assert.That(Directory.GetFiles(TestUtils.TestDataDir), Has.Length.EqualTo(1));

        mockBackup.Setup(mock => mock.TryCreateTodaysBackup()).Verifiable();
        mockPaths.Setup(mock => mock.GetSaveFilePath()).Returns(TestUtils.SaveFilePath);

        Store store = new(mockPaths.Object, mockBackup.Object);
        EventRecorder recorder = new(store);

        store.Initialize();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorder.DeletedNotes, Has.Count.EqualTo(0));
            Assert.That(recorder.CreatedNotes, Has.Count.EqualTo(1));
        }

        AssertNote(recorder.CreatedNotes[0]);

        mockBackup.Verify(mock => mock.TryCreateTodaysBackup(), Times.Once());
    }

    [Test]
    public void Initialize_ShouldInitializeWithErrorNote_WhenSaveIsInvalidAndBackupIsInvalid()
    {
        TestUtils.CreateFile(TestUtils.SaveFilePath, InvalidNoteJson);
        Assert.That(Directory.GetFiles(TestUtils.TestDataDir), Has.Length.EqualTo(1));

        mockBackup.Setup(mock => mock.TryCreateTodaysBackup()).Verifiable();
        mockBackup.Setup(mock => mock.TryRestoreNextBackup()).Returns(false);
        mockPaths.Setup(mock => mock.GetSaveFilePath()).Returns(TestUtils.SaveFilePath);

        Store store = new(mockPaths.Object, mockBackup.Object);
        EventRecorder recorder = new(store);

        store.Initialize();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorder.CreatedNotes, Has.Count.EqualTo(1));
            Assert.That(recorder.DeletedNotes, Has.Count.EqualTo(0));
        }

        Assert.That(recorder.CreatedNotes[0].Body, Is.EqualTo(LoadFailedMessage));
        mockBackup.Verify(mock => mock.TryCreateTodaysBackup(), Times.Once());
        mockBackup.Verify(mock => mock.TryRestoreNextBackup(), Times.Once());
    }

    [Test]
    public void Initialize_ShouldInitializeWithSuccessAndRecoveryNote_WhenSaveIsInvalidAndBackupIsValid()
    {
        TestUtils.CreateFile(TestUtils.SaveFilePath, InvalidNoteJson);
        TestUtils.CreateFile(TestUtils.ValidBackupFilePath, ValidNoteJson);
        Assert.That(Directory.GetFiles(TestUtils.TestDataDir), Has.Length.EqualTo(2));

        mockPaths.Setup(mock => mock.GetSaveFilePath()).Returns(TestUtils.SaveFilePath);
        mockBackup.Setup(mock => mock.TryCreateTodaysBackup()).Verifiable();
        mockBackup.Setup(mock => mock.TryRestoreNextBackup()).Callback(() =>
        {
            TestUtils.CreateFile(TestUtils.SaveFilePath, ValidNoteJson);
        })
        .Returns(true);

        Store store = new(mockPaths.Object, mockBackup.Object);
        EventRecorder recorder = new(store);

        store.Initialize();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorder.CreatedNotes, Has.Count.EqualTo(2));
            Assert.That(recorder.DeletedNotes, Has.Count.EqualTo(0));
        }

        AssertNote(recorder.CreatedNotes[0]);
        Assert.That(recorder.CreatedNotes[1].Body, Is.EqualTo(LoadRecoveredMessage));

        mockBackup.Verify(mock => mock.TryCreateTodaysBackup(), Times.Once());
        mockBackup.Verify(mock => mock.TryRestoreNextBackup(), Times.Once());
    }

    private static void AssertNote(Note note)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(note.Body, Is.EqualTo("This is a Test Note."));
            Assert.That(note.Id, Is.EqualTo("dc3bfb1d-31e6-4134-ad4d-61c285d313e5"));
            Assert.That(note.NoteWindowDimensions.Width, Is.EqualTo(10));
            Assert.That(note.NoteWindowDimensions.Height, Is.EqualTo(20));
            Assert.That(note.NoteWindowDimensions.X, Is.EqualTo(30));
            Assert.That(note.NoteWindowDimensions.Y, Is.EqualTo(40));
        }
    }

    private sealed class EventRecorder
    {
        public List<Note> CreatedNotes { get; } = [];
        public List<Note> DeletedNotes { get; } = [];

        public EventRecorder(Store store)
        {
            store.OnNoteCreated += OnNoteCreated;
            store.OnNoteDeleted += OnNoteDeleted;
        }

        private void OnNoteCreated(Note note) => CreatedNotes.Add(note);

        private void OnNoteDeleted(Note note) => DeletedNotes.Add(note);
    }
}
#pragma warning restore CA1707
