# StickyNotes
A bare bones sticky note implementation written in .NET Core.

### Features
* Ability to create, delete, colour, and re-arrange sticky notes.
* Notes are alt-tabbable.
* Automated backup and restoration in case of corrupt data. (Local only.)
    * Notes are saved to a single local JSON file.
* Taskbar icon to access useful functions such as recovering notes lost off screen.

### Screenshots

![StickyNotes Window](./Images/window.png)

![StickNotes Task Bar Icon](./Images/task_bar.png)

### Dependencies
* [Avalonia](https://github.com/AvaloniaUI/Avalonia)
* [MessageBox.Avalonia](https://github.com/AvaloniaCommunity/MessageBox.Avalonia)
* [Font-Awesome](https://github.com/FortAwesome/Font-Awesome)
