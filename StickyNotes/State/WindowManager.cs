namespace StickyNotes.State;

using System.Collections.Generic;
using Avalonia.Threading;
using StickyNotes.Models;
using StickyNotes.ViewModels;
using StickyNotes.Views;

public interface IWindowManager
{
    void CloseAllWindows();
    void CascadeWindows();
    void ActivateWindows();
}

public class WindowManager : IWindowManager
{
    private readonly Dictionary<string, MainWindow> windows = [];

    public WindowManager(IStore store)
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
            int position = count * 20;
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