## MCE Controller 

[**Version 1.4.0 Released February 11, 2012**](https://github.com/tig/mcecontroller/downloads)

Copyright © 2012 [Kindel Systems](http://www.kindel.com), LLC

![Screenshot](http://www.kindel.com/products/mcecontroller/MCE%20Controller.png)

[Support this project by donating to the author](http://sourceforge.net/donate/index.php?group_id=138158)

MCE Controller enables the remote control of a Windows PC over the network. It runs in the background on a Windows PC listening on the network for commands. It then translates those commands into actions such as keystrokes, text input, and the starting of programs. 

For example: 

* The command "mcestart" will cause the Windows Media Center application to start. This is equivalent to pressing the green button on the Windows remote control. 
* The command "maximize" will cause the current window to be maximized on the display. This is equivalent to choosing the "Maximize" button on the window's window menu. 
* The command "chars:Hello World!" will cause the text "Hello World" to be typed, as though it were typed on the keyboard. 
* The commands that MCE Controller support is extensible through a configuration file. If it does not natively support a function you wish, you can add new commands easily.

MCE Controller was initially developed to enable integration of a Windows based home theater PC (HTPC) into a Crestron whole-house audio/video system. However, it is general enough that it can be utilized from any control system that supports sending text strings to a TCP/IP port. Most control systems, such as Crestron or AMX, support IR emitting. For many applications, emitting the MCE IR commands will suffice. However, for some installations the reliability of IR emitting and other factors may make IR emitting problematic and MCE Controller offers a robust solution.

MCE Controller can act as either a TCP/IP client or server. When acting as a client the target host and port can be configured. When acting as a server the incoming port can be configured.

MCE Controller runs showing only a taskbar icon. By double clicking on the taskbar a status window is displayed that shows a log of all activity. You can also right-click on the taskbar icon for a menu.

## License
The source code for MCE Controller is available under the BSD license. The source code project can be found at http://github.com/tig/mcecontroller.

(Note: the source code was recently moved from Sourceforge to GitHub. SourceForge continues to be the place for downloading the latest binary and for getting help & particpating in usage discussions.)

## Features
* Can act as a TCP/IP client or server. Supports any number of simultaneous clients.
* Supports simulating keypresses (e.g. Alt-Tab, or Win-S) with SendInput commands.
* Supports simulating Windows messages (e.g. WM_SYSCOMMAND / SC_MAXIMIZE) with SendMessage commands.
* Supports simulating start process commands (e.g. run notepad.exe) with the StartProcess command.
* Supports simulating changing the window focus with the SetForegroundWindow command.
* Supports sending text (e.g. simulating typing) with the "chars:" command.
* MCEController.commands includes common Windows Media Center commands. It can easily be extended to suit your needs.
* Supports running multiple instances.
* TCP/IP port can be changed in Settings.
* Runs minimzed as a taskbar icon by default. This can be changed in Settings...

## Download
[Download the latest version](https://github.com/tig/mcecontroller/downloads)

## Documentation
* [Read the documentation](http://cloud.github.com/downloads/tig/mcecontroller/Readme.htm)
* [Charlie's Blog Posts on MCE Controller](http://ceklog.kindel.com/category/passions/homeautomation/mce-controller/)

## Support 
* For help and support, please visit the [MCE Controller forums](https://sourceforge.net/projects/mcecontroller/forums/forum/464956).
* More on [kindel.com](http://www.kindel.com).