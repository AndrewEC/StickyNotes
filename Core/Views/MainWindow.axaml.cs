namespace StickyNotes.Core.Views;

using Avalonia.Controls;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        TextBox inputBox = this.FindControl<TextBox>("MainInputBox")!;
        inputBox.AttachedToVisualTree += (sender, args) => inputBox.Focus();
    }
}