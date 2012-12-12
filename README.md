# MCE Controller 1.6

By Charlie Kindel (@tig) - Copyright © 2012 [Kindel Systems](http://www.kindel.com), LLC. 

[Follow Charlie on Twitter](http://www.twitter.com/ckindel)

Licensed under the [MIT License](http://www.opensource.org/licenses/mit-license.php).

[Support this project](http://sourceforge.net/donate/index.php?group_id=138158)

## Download 

Download MCE Controller for Windows (installer)
[Download the latest version](http://ceklog.wpengine.netdna-cdn.com/wp-content/uploads/2012/12/MCEController-1.6.1-Setup.exe)

![Screenshot](http://www.kindel.com/products/mcecontroller/MCE%20Controller.png)

MCE Controller lets you control a Windows HTPC (or any PC) over the network. It runs in the background listening on the network for commands. It then translates those commands into actions such as keystrokes, text input, and the starting of programs. Any remote control, home control system, or application that can send text strings via TCP/IP can use MCE Controller to control a Windows PC.

For example: 

* The command `mcestart` will cause the Windows Media Center application to start. This is equivalent to pressing the green button on the Windows remote control. 
* The command `maximize` will cause the current window to be maximized on the display. This is equivalent to choosing the "Maximize" button on the window's window menu. 
* The command `chars:Hello World!` will cause the text "Hello World" to be typed, as though it were typed on the keyboard. 
* The commands that MCE Controller support is extensible through a configuration file. If it does not natively support a function you wish, you can add new commands easily.

MCE Controller was initially developed to enable integration of a Windows based home theater PC (HTPC) into a Crestron whole-house audio/video system. However, it is general enough that others have used ti within other control system that support sending text strings to a TCP/IP port. Most control systems, such as Crestron or AMX, support IR emitting. 

For many applications, emitting the MCE IR commands will suffice. However, for some installations the reliability of IR emitting and other factors may make IR emitting problematic and MCE Controller offers a robust solution.

MCE Controller can act as either a TCP/IP client or server. When acting as a client the target host and port can be configured. When acting as a server the incoming port can be configured.

MCE Controller runs showing only a taskbar icon. By double clicking on the taskbar a status window is displayed that shows a log of all activity. You can also right-click on the taskbar icon for a menu.

## License

MCE Controller is free to use. [Donations are encouraged](http://sourceforge.net/donate/index.php?group_id=138158). The source code for MCE Controller is available under the MIT license. The source code project can be found at http://github.com/tig/mcecontroller.

(Note: the source code was recently moved from Sourceforge to GitHub. SourceForge continues to be the place for downloading the latest binary and for getting help & particpating in usage discussions.)

## Features

* Can act as a TCP/IP client or server. Supports any number of simultaneous clients.
* Supports simulating keypresses (e.g. Alt-Tab, or Win-S) with `SendInput` commands.
* Supports simulating the mouse.
* Supports simulating Windows messages (e.g. `WM_SYSCOMMAND` / `SC_MAXIMIZE`) with `SendMessage` commands.
* Supports simulating start process commands (e.g. run `notepad.exe`) with the `StartProcess` command.
* Supports simulating changing the window focus with the `SetForegroundWindow` command.
* Supports sending text (e.g. simulating typing) with the `chars:` command.
* `MCEController.commands` includes common Windows Media Center commands. It can easily be extended to suit your needs.
* Supports running multiple instances.
* TCP/IP port can be changed in Settings.
* Runs minimzed as a taskbar icon by default. This can be changed in Settings...

## Support 

* For help and support, please visit the [MCE Controller forums](http://sourceforge.net/projects/mcecontroller/forums/forum/464957).
* [Charlie's Blog Posts on MCE Controller](http://ceklog.kindel.com/category/passions/homeautomation/mce-controller/)
* More on [kindel.com](http://www.kindel.com).

# Documentation

## Installation

Important Note: MCE Controller requires the .NET Framework 4.0. [Windows Update]  (http://windowsupdate.microsoft.com) to  ensure you have this installed before running MCE Controller.

To install, simply run the `MCEController 1.x.x Setup.exe` to install. The following files will be installed in the directory you choose and a start menu item will be added. You can un-install MCE Controller either via add/remove programs or by using the uninstall icon in the MCE Controller start menu group.

* `MCEControl.exe`
* `MCEControl.commands`

`MCEControl.exe` is the program executable and `MCEControl.commands` is an XML file that defines the commands MCE Controller will respond to and what actions it will take. 

When MCEControl runs, it defaults to showing itself as only a taskbar icon. Double clicking on the taskbar icon will show the configuration/status window.

![MCE Controller running in the taskbar] (http://i.imgur.com/AsrJP.png)

If you would like it to show it’s configuration/status window upon startup, uncheck the “Hide Window at Startup” checkbox in the settings dialog.

## Configuration

![Settings Dialog] (http://i.imgur.com/rL2P0.png)

Note that all configuration settings are stored in a file that will be created in the `%APPDATA%\Roaming\Kindel Systems\MCE Controller` directory when MCE Controller is first run. The configuration settings file is named `MCEControl.settings`.

You can run multiple instances of MCE Controller. To do so simply copy the EXE to a 2nd directory along with the `MCEControl.commands` file. Each copy will then have its own independent `MCEControl.settings file`.

MCE Controller can act as either a TCP/IP client or server (it can actually operate as both simultaneously, but it’s unlikely it would ever be useful to do so). By default MCE Controller is configured to act as a TCP/IP server listening on port 5150. You can change this behavior using the Settings dialog described below.

## The Client Tab

The Client tab in the Settings dialog controls MCE Controller’s TCP/IP client functionality. When acting as a client, MCE Controller will repeatedly try to connect to the specified port on the specified host and wait for commands to be sent from the host. MCE Controller sends nothing to the host.

![Client Tab] (http://i.imgur.com/95d3U.png)

* Enable Client. This checkbox enables or disables the TCP/IP client functionality. If enabled, the followings settings apply:
* Host. This is the IP address or host name of the server MCE Controller is to connect to.
* Port. This is the port that MCE Controller will connect to.
* Reconnect Wait Time. This is the number of milliseconds (default is 20 seconds or 20000 ms) MCE Controller will wait before trying to reconnect to the host once a connection has been dropped or a connect fails.

## The Server Tab

The Server tab in the Settings dialog controls MCE Controller’s TCP/IP server functionality.  When acting as a server, MCE Controller will open the specified port and wait for a client to connect. When a client does connect MCE Controller will wait for incoming commands until the client closes the connection.

In server mode, MCE Controller supports any number of multiple-simultaneous connections. 

![Server Tab] (http://i.imgur.com/M2THm.png)

* Enable Server. This checkbox enables or disables the TCP/IP server functionality. If enabled, the followings settings apply:
* Port. This is the port that MCE Controller will listen on.
* Enable Wakeup. If enabled, MCE Controller will attempt to connect to the specified host/port, send the “Wakeup command” and disconnect when it first starts. When it shuts down it will send the “Closing command”. This functionality is useful when the remote client needs to be notified that MCE Controller is ready (for example after the server PC has rebooted).

## Testing MCE Controller

To get a feel for how MCE Controller works you can test it on your local machine fairly easily. First you need some program that can send arbitrary text strings via TCP/IP. Examples of such programs:

### Using Telent

There is a telnet client supplied with Windows, but it is not installed by default.  Use the Windows Features in Add/Remove Programs to install it. To test MCE Controller (in server mode) open a telnet session to whatever port MCE Controller is configured for type commands. For example `telnet localhost 5150` followed by `notepad` and ENTER will cause Notepad to start.

### Using NetCat

NetCat (nc.exe) is an open source program ported from Unix that supports sending network packets. You can get [NetCat here] (http://netcat.sourceforge.net/). Like this:

![Using NetCat] (http://i.imgur.com/DUhXP.png)

Note the Windows version of NetCat does not exit automaticaly. The `-w` parameter you see in the screenshot above tells it to wait 1 second before exiting. On other platforms NetCat works as expected. 

## Commands

MCE Controller works with *Commands*. Commands are text strings like `greenbutton`, `hibernate`, and `alttab`. Each command has a *Type*. When MCE Controller recieves a command it causes an action to happen on the PC it is running on. The action taken is dependent on the type of command and the parameters set for that command in the `MCEControl.commands` file.

The following command types are supported by MCE Controller:

* **StartProcess** - Starts the specified process. Can specify the path to an executable, shortcut, or a URI. Supports embedded `nextCommand` elements allowing other form of MCE Controller commands to be invoke after the process starts.
* **SetForgroundWindow** - Causes the specified window to be brought to the foreground.
* **Shutdown** - Allows the host computer to be shutdown, restarted, put in standby, or hibernate mode.
* **SendMessage** - Enables the sending of window messages to windows. E.g. the 'mcemaximize' command causes the Media Center window to go full screen.
* **SendInput** - Sends keyboard input to the forground window.
* **Mouse** - Sends mouse movement and button actions.
* Built-In - Single characters, `chars:`, `shiftdown:`, and `shiftup:`.

The `MCEControl.commands` file included with MCE Controller includes a set of default commands for controlling Windows Media Center as well as standard keyboard input. See the section below for instructions on how to add, remove, or change these commands. Note that there are some other commands in `MCEControl.commands` such as "notepad" which starts `notepad.exe`; these are there just for illustrative purposes.

The File menu includes a menu item that will open the folder containing the `MCEController.commands` file so you can use your favorite editor to change it as you see fit.

Please view the contents of `MCEController.commands` for the definitive list of default commands. 

The following describes the Built-In Type commands that are supported:

### Any single character

This is equivalent to a single keypress of a key on the keyboard.  For example `a` will result in the A key being pressed. `1` will result in the `1` key being pressed. There is no difference between sending `a` and `A`.  Use `shiftdown:/shiftup:` to simulate the pressing of the shift, control, alt, and windows keys.

### Shiftdown/up

To simluate a keydown event for one of the modifiers kesy (shift, control, alt, and the Windows key) send a `shiftdown:` or `shiftup:` command.  The syntax is:

    shiftdown:[shift|ctrl|alt|lwin|rwin]

and 

    shiftup:[shift|ctrl|alt|lwin|rwin]

For example, to simulate the typing of 'Test!' send the following commands:

    shiftdown:shift
    t
    shiftup:shift
    e
    s
    t
    shiftdown:shift
    1
    shiftup:shift

This scheme can be used as an alternative way of sending ctrl-, alt-, and win- keystrokes.  For example to simulate ctrl-s:

    shiftdown:ctrl
    o
    shiftup:ctrl 

### chars:

Anytime MCE Controller recevies `chars:` plus some text, it simluates the typing of that text on the keyboard. The syntax of the command is `chars:*` where '*'' represents one or more characters. This is equivalent to typing those characters on the keyboard. E.g. `chars:3` will cause the number 3 to be typed as though the user had pressed the 3 key on the keyboard. `chars:Hello` will cause `Hello` to be typed.

Unicode (and other excaped charcter sequences are supported). `chars:\u20AC` will cause the &euro; character to be input into the foreground window on the machine MCE Controller is running on.

### mouse:

MCE Controller can simulate mouse movement. With this it is possible to build a remote control that acts like a mouse (I have built a test app for Windows Phone 7 that enables WP7 to work like a touchpad; contact me if you are interested in it).

The general format of the mouse commands is:

    mouse:<action>[,<param>,...,<param>]

The available mouse actions are:

* **lbc** - Left button click (`mouse:lbc`)
* **lbdc** - Left button double-click (`mouse:lbdc`)
* **lbd** - Left button down (`mouse:lbd`)
* **lbu** - Left button up (`mouse:lbu`);
* **rbc, rbdc, rbd, rbu** - Same same but for the right mouse button.
* **xbc, etc...** - x button click where x is a button number (`mouse:xbc,3` for button 3 click)
* **mm,x,y** - Move the mouse x, y pixels (`mouse:mm,7,-3` would move the mouse right 7 and up 3 pixels)
* **mt,x,y** - Move the mouse to a location. The coordinates represent the absolute X/Y-coordinates on the primary display device where 0 is the extreme left/bottom of the display device and 65535 is the extreme right/bottom hand side of the display device (`mouse:mt,0,65535` would move the mouse to the bottom left corner of the primary display).
* **mtv,x,y** - Move the mouse to a location on the virtual desktop. The coordinates represent the absolute X/Y-coordinates on the virtual desktop where 0 is the extreme left/top of the virtual desktop and 65535 is the extreme right/bottom (`mouse:mtv,65535,0` would move the mouse to the top right corner of the virutal desktop).
* **hs,n** - Simlate a horizontal scroll gesture. `n` is the amount to scroll in clicks. A positive value indicates that the wheel was rotated to the right; a negative value indicates that the wheel was rotated to the left (`mouse:hs,3`).
* **vs,n** - Simlate a vertical scroll gesture. `n` is the amount to scroll in clicks. A positive value indicates that the wheel was rotated forward, away from the user; a negative value indicates that the wheel was rotated backward, toward the user (`mouse:vs,3`).

Note that when sending mouse movements it is best if the MCE Controller window is hidden as the display log tends to chew up a lot of resoruces, making things jerky.

NOTE: Older versions of MCE Controller suppored a `keys:` command that purported to do the same thing. It never actually worked right and has been replaced with the new `chars:` command.

## Defining Your Own Commands

MCE Controller has no dependencies on Windows Media Center; the name is simply a legacy from how it was originally inteneded to be used. All Windows Media Center specific data is encapsulated in the `MCEControl.commands` file found in the same directory as `MCEControl.exe`. Therefore, MCE Controller is actually a generic mechanism for sending input and other commands to a Windows based PC over the network. It supports sending any message or keystroke and can launch arbitrary processes.

To utilize this functionality all you have to do is edit the `MCEControl.commands` file to suit your needs.

`MCEControl.commands` allows you to define four types of commands: `SendInput`, `SendMessage`, `StartProcess`, `Shutdown`, and `SetForegroundWindow`:

`SendInput` commands send keystrokes. Any combination of shift, ctrl, alt, and left/right Windows keys can be used with any "virtual key code". See the `winuser.h` file in the Windows SDK or [this MSDN page](http://msdn.microsoft.com/en-us/library/ms927178.aspx) for a definition of all standard VK codes.  **NOTE: MCE Controller only understands keycodes in decimal format. You need to convert from hex if you use either of these sources**. MCE Controller uses the Windows `SendInput()` API to send keystrokes. Keystrokes go to the foreground window. 

Use a `SetForegroundWindow` element to set the foreground window to the target app by specifying the app’s top-level window class name (e.g. `ehshell`).

For example, the following causes a Ctrl-P to be sent to the foreground window, and if that window is Media Center, the My Pictures page appears:

     <SendInput Cmd="mypictures" vk="73" Shift="false" Ctrl="true" Alt="false" />

`SendMessage` commands are just that. They cause MCE Controller to send a Windows message using the `SendMessage()` API to the foreground window if no class name is specified, or to a particular window if that window’s class is specified.

For example, the following is equivalent to sending a `WM_SYSCOMMAND` with the `SC_MAXIMIZE` flag, causing the window with the class name of `ehshell` to be maximized (`WM_SYSCOMMAND == 247` and `SC_MAXIMIZE == 61488`):

    <SendMessage Cmd="mce_maximize" ClassName="ehshell" Msg="274" wParam="61488" lParam="0" />

`StartProcess` commands start processes. Process commands support chaining using the `nextCommand` element. The embedded command will be executed after the started application starts processing windows’ messages.

For example, the following launches Media Center and maximizes it:

    <StartProcess Cmd="mce_start" File="C:\windows\ehome\ehshell.exe">
        <nextCommand xsi:type="SendMessage" ClassName="ehshell" Msg="274" wParam="61488" lParam="0" />
    </StartProcess>

The `SetForegroundWindow` command sets the specified window (using the window’s class name) to the foreground.

For example, the following makes Media Center the foreground Window (assuming Media Center is running):

    <SetForegroundWindow Cmd="mce_activate" ClassName="ehshell"/>

Note that MCE Controller supports the `chars:`, `shiftup:`, and `shiftdown:` commands in addition to the commands defined in MCEControl.commands.  

Also note that you should not make commands a single character or it will interfere with the ability to simulate individual character key presses.

## Usage Notes

The `mcestart` command will launch Media Center and cause it to be maximized. If you do not want this behavior, change `MCEControl.commands` such that the `mcestart` command does not have the embedded `nextCommand` element.

For MCEContoller to work property the target application (Media Center) must be the active window (foreground) on the desktop.  You can use the `mceactivate` command to cause Media Center to be the foreground app if it’s already running. Alternatively you can just use `mcestart` as it will end up causing the same thing to happen (although not as quickly).

Also, you may find that `greenbutton` is a better function than `mcestart` because it is equivalent to the green-button on a Windows remote control. `mcestart` is a bit different because if Media Center is already running `mcestart` will not go to the "Start" screen of Media Center while `greenbutton` will. However, `greenbutton` does not cause the Media Center window to be maximized.

# Version History

* Version 1.0.1 (February 22, 2004) – First publicly released version.
* Verison 1.0.2 (March 24, 2004) - New features:
Added support for system shutdown, restart, standby, and hibernate (the Shutdown command type).
Renamed a few commands ("mce_start" is now "mcestart" for example) to be more consistent.
* Verison 1.0.3 (March 26, 2004) - Added installer.
* Version 1.0.4 (February 26, 2005) - Fixed bug that caused MCE Controller to prevent logoffs and shutdowns.
* Version 1.0.5 (April, 2005) – Added support for arbitrary # of characters for the “key:” command.
* Verison 1.1.0 (May 11, 2005) – No functional changes. Changed the source license to the BSD license and posted on Sourceforge.
* Version 1.3.0 (January 3, 2012) – Added support for "chars:". Removed support for "keys:". Added "enter" command. Now builds with VS2010.
* Version 1.3.1 (January 4, 2012) – Fixed bug parsing -1 in the lParam of SendMessageCommands. Commented MCEController.commands. Minor code cleanup.
* Version 1.3.2 (January 4, 2012) – Fixed bug in how .commands and .settings are stored (Win7 broke permissions).
* Version 1.3.3 (January 9, 2012) – Added capability to send individual key presses with shift/ctrl/alt/win modifiers (what keys: originally was supposed to do).
* Version 1.4.0 (February 11, 2012) - Server now supports any number of client connections. Expanded MCEController.commands to include commands used by iRule (http://iruleathome.com). Updated About Box & Help menu to reflect move to GitHub. Added menu item to open directory containing MCEController.commands.
* Version 1.5.0 (March 27, 2012) - 'chars:' command now supports escaped characters. This allows the sending of Unicode characters such as &euro; (e.g. 'chars:\u20AC' will cause the &euro; character to be input on the server machine).</li>
* Version 1.5.1 (April 2, 2012) - Removed readme file from distribution and updated online docs. 
* Version 1.5.2 (October, 4, 2012) - Fixed .settings file bug where it would sometimes read from Program Files and write to AppData. Now always writes to AppData unless started outside of Program Files. Fixed Setting dialog to be more resilient to bad data. Fixed Send Awake so that it does not fault on bad data, but logs errors. General code clean up. Built with VS2012.
* Version 1.6.0 (October 10, 2012) - Added mouse simulation support. 
* Version 1.6.1 (November 6, 2012) - Fixed bug with some Telnet clients that don't buffer each line before sending.

