namespace StickyNotes;

using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using StickyNotes.Core.State;
using StickyNotes.Core.Utils;

public partial class App : Application
{
    private readonly ConsoleLogger<App> logger = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested += OnShutdownRequested;
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
        }
        else
        {
            string lifetimeTypeName = ApplicationLifetime?.GetType().FullName ?? "Unknown Type";
            logger.Log($"ApplicationLifetime is of unusable type [{lifetimeTypeName}]. App will Exit.");
            Environment.Exit(1);
            return;
        }

        if (GlobalWatcher.IsStickyNotesAlreadyRunning())
        {
            logger.Log("Sticky notes appears to already be running. Creating new note instead.");
            GlobalWatcher.Instance.RequestCreateNewNote();
            Environment.Exit(0);
            return;
        }

        WindowManager.Instance.Connect(Store.Instance);

        Store.Instance.Initialize();

        GlobalWatcher.Instance.WatchForChanges();

        base.OnFrameworkInitializationCompleted();
    }

    public void OnRevealNotesClicked(object? sender, EventArgs args)
    {
        logger.Log("Reveal Notes clicked.");
        WindowManager.Instance.ActivateWindows();
    }

    public void OnCascadeClicked(object? sender, EventArgs args)
    {
        logger.Log("Cascade Notes clicked. Cascading note windows.");
        WindowManager.Instance.CascadeWindows();
    }

    public void OnShowDataFolderClicked(object? sender, EventArgs args)
    {
        logger.Log("Show Data Folder clicked. Opening explorer.");
        Process.Start("explorer.exe", "/select," + StickyNotePaths.GetSaveFilePath());
    }

    public void OnCloseClicked(object? sender, EventArgs args)
    {
        logger.Log("Close tray menu clicked. Closing all windows.");
        DoShutdownApp();
    }

    public void OnNewNoteClicked(object? sender, EventArgs args)
    {
        logger.Log("New Note tray menu clicked. Creating new note.");
        Store.Instance.QueueCreateNote();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        logger.Log("Shutdown event received. Closing all note windows.");
        DoShutdownApp();
    }

    private void DoShutdownApp()
    {
        WindowManager.Instance.CloseAllWindows();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            logger.Log("Shutting down application.");
            lifetime.Shutdown();
        }
    }
}