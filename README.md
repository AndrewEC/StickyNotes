# StickyNotes
A bare bones sticky note implementation written in .NET Core.

### Features
* Ability to create, delete, colour, and re-arrange sticky notes.
* Notes are alt-tabbable.
* Automated daily backup and restoration in case of corrupt data.
    * Notes are only saved locally.
* Taskbar icon to access useful functions such as recovering notes lost off screen.
* Ahead-of-Time (AOT) compatible.

### Screenshots

![StickyNotes Window](./Images/window.png)

![StickNotes Task Bar Icon](./Images/task_bar.png)

### Dependencies
* [Avalonia](https://github.com/AvaloniaUI/Avalonia)
* [MessageBox.Avalonia](https://github.com/AvaloniaCommunity/MessageBox.Avalonia)
* [Font-Awesome](https://github.com/FortAwesome/Font-Awesome)
