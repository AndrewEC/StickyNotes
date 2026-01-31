namespace StickyNotes.Core.State;

using System.Collections.Generic;
using Avalonia.Threading;
using StickyNotes.Core.Models;
using StickyNotes.Core.ViewModels;
using StickyNotes.Core.Views;

public class WindowManager
{
    public static readonly WindowManager Instance = new();

    private readonly Dictionary<string, MainWindow> windows = [];

    private WindowManager() { }

    public void Connect(Store store)
    {
        store.OnNoteCreated += OnNoteCreated;
        store.OnNoteDeleted += OnNoteDeleted;
    }

    private void OnNoteDeleted(Note note)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            CloseWindowForNote(note.Id);
        });
    }

    private void OnNoteCreated(Note note)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            MainWindow window = new();
            window.DataContext = new MainWindowViewModel(window, note);
            window.AttachedToVisualTree += (_, _) => window.Activate();
            window.Show();
            windows.Add(note.Id, window);
        });
    }

    public void CloseAllWindows()
    {
        foreach (string id in windows.Keys)
        {
            CloseWindowForNote(id);
        }
    }

    public void CascadeWindows()
    {
        int count = 1;
        foreach (MainWindow window in windows.Values)
        {
            int position = count * 50;
            (window.DataContext as MainWindowViewModel)!.ForceSetPosition(position, position);
            count++;
        }
        ActivateWindows();
    }

    private void CloseWindowForNote(string id)
    {
        if (windows.TryGetValue(id, out MainWindow? window))
        {
            (window.DataContext as MainWindowViewModel)!.ForceCloseWindow();
            windows.Remove(id);
        }
    }

    public void ActivateWindows()
    {
        foreach (MainWindow window in windows.Values)
        {
            window.Topmost = true;
            window.Topmost = false;
        }
    }
}