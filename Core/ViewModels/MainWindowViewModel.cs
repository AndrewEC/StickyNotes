namespace StickyNotes.Core.ViewModels;

using System;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using StickyNotes.Core.Models;
using StickyNotes.Core.Utils;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ConsoleLogger<MainWindowViewModel> logger = new();
    private readonly Note note;
    private readonly Window parentWindow;
    private readonly Panel clickDragPanel;

    private bool canClose;
    private bool mousePressed;
    private Point? currentPoint;

    public MainWindowViewModel(Window parentWindow, Note note)
    {
        this.note = note;
        this.parentWindow = parentWindow;

        Body = note.Body;
        SetNoteColour(note.ColourStyle);

        logger.Log($"Initializing note window from note: [{note}].");

        parentWindow.Width = note.NoteWindowDimensions.Width;
        parentWindow.Height = note.NoteWindowDimensions.Height;
        parentWindow.Position = new(note.NoteWindowDimensions.X, note.NoteWindowDimensions.Y);
        parentWindow.SizeChanged += OnWindowSizeChanged;
        parentWindow.Closing += OnWindowClosing;

        ConfirmDeleteNoteCommand = ReactiveCommand.Create(ConfirmDeleteNote);
        CreateNoteCommand = ReactiveCommand.Create(CreateNote);
        ToggleColourOptionCommand = ReactiveCommand.Create(ToggleColourOption);
        SetNoteColourCommand = ReactiveCommand.Create<string>(SetNoteColour);

        clickDragPanel = parentWindow.FindControl<Panel>("ClickDragPanel")!;
        clickDragPanel.PointerPressed += OnPanelPointerPressed;
        clickDragPanel.PointerReleased += OnPanelPointerReleased;
        clickDragPanel.PointerMoved += OnPanelPointerMoved;
    }

    public bool IsColourOptionVisible
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsPink
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsBlue
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsGreen
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string Body
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            note.Body = value;
            Store.Instance.QueueUpdateNote(note);
        }
    }

    public ReactiveCommand<Unit, Unit> ConfirmDeleteNoteCommand { get; }

    public ReactiveCommand<Unit, Unit> CreateNoteCommand { get; }

    public ReactiveCommand<Unit, Unit> ToggleColourOptionCommand { get; }

    public ReactiveCommand<string, Unit> SetNoteColourCommand { get; }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!canClose)
        {
            e.Cancel = true;
        }
    }

    public void ForceCloseWindow()
    {
        canClose = true;
        parentWindow.Close();
    }

    private void OnPanelPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!mousePressed || currentPoint == null)
        {
            return;
        }

        Point nextPoint = e.GetPosition(sender as Control);

        double xChange = nextPoint.X - currentPoint.Value.X;
        double yChange = nextPoint.Y - currentPoint.Value.Y;

        parentWindow.Position = new(
            (int)(parentWindow.Position.X + xChange),
            (int)(parentWindow.Position.Y + yChange));
    }

    public void OnPanelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        mousePressed = true;
        currentPoint = e.GetPosition(sender as Control);
    }

    private void OnPanelPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        mousePressed = false;
        currentPoint = null;

        PersistNoteDimensions();
    }

    public void ForceSetPosition(int x, int y)
    {
        parentWindow.Position = new(x, y);
        PersistNoteDimensions();
    }

    private void PersistNoteDimensions()
    {
        note.NoteWindowDimensions = new Dimensions(
            (int)parentWindow.Width,
            (int)parentWindow.Height,
            parentWindow.Position.X,
            parentWindow.Position.Y
        );
        Store.Instance.QueueUpdateNote(note);
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender != parentWindow)
        {
            return;
        }

        PersistNoteDimensions();
    }

    private async void ConfirmDeleteNote()
    {
        var result = await MessageBoxManager.GetMessageBoxStandard(
            "Delete Note",
            "Are you sure you want to delete this note? This action cannot be undone.",
            ButtonEnum.YesNo)
            .ShowAsync();

        if (result != ButtonResult.Yes)
        {
            return;
        }

        Store.Instance.QueueDeleteNote(note);
    }

    private void CreateNote() => Store.Instance.QueueCreateNote();

    private void ToggleColourOption() => IsColourOptionVisible = !IsColourOptionVisible;

    private void SetNoteColour(string value)
    {
        logger.Log($"Setting note colour to: [{value}]");

        if (Enum.TryParse(value, out ColourStyles style))
        {
            SetNoteColour(style);
        }
        else
        {
            SetNoteColour(ColourStyles.Pink);
        }
    }

    private void SetNoteColour(ColourStyles style)
    {
        IsPink = false;
        IsBlue = false;
        IsGreen = false;

        switch (style)
        {
            case ColourStyles.Pink:
                IsPink = true;
                break;
            case ColourStyles.Blue:
                IsBlue = true;
                break;
            case ColourStyles.Green:
                IsGreen = true;
                break;
        }

        note.ColourStyle = style;
        Store.Instance.QueueUpdateNote(note);
    }
}
