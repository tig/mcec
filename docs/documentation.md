
# MCE Controller V2 Documentation

Copyright © 2019 [Kindel Systems](http://www.kindel.com), LLC. Licensed under the [MIT License](http://mcec.codeplex.com/license).

By Charlie Kindel ([@ckindel on Twitter](http://www.twitter.com/ckindel))

![MCE Controller](https://github.com/tig/mcec/blob/develop/docs/aboutbox.png?raw=true)

## Overview

**MCE Controller** lets you control a Windows HTPC (or any PC) over the network. It runs in the background listening on the network (or serial port) for commands. It then translates those commands into actions such as keystrokes, text input, and the starting of programs. Any remote control, home control system, or application that can send text strings via TCP/IP or a serial port can use **MCE Controller** to control a Windows PC.

For example:

*   The command `mcestart` will cause the Windows Media Center application to start. This is equivalent to pressing the green button on the Windows remote control.
*   The command `maximize` will cause the current window to be maximized on the display. This is equivalent to choosing the "Maximize" button on the window's window menu.
*   The command `chars:Hello World!` will cause the text "Hello World" to be typed, as though it were typed on the keyboard.
*   The command `VK_MEDIA_NEXT_TRACK` will cause the currently running media player app (Spotify, Windows Media Player, etc...) to jump to the next media track, just as if the user had pressed th "next track" key on the keyboard.
*   The commands that **MCE Controller** support is extensible through a configuration file. If it does not natively support a function you wish, you can add new commands easily.

**MCE Controller** was initially developed to enable integration of a Windows Media Center based home theater PC (HTPC) into a Crestron whole-house audio/video system. However, it is general enough that others have used it within other control system that support sending text strings to a TCP/IP port. Most control systems, such as Crestron or AMX, support IR emitting.

For many applications, emitting the Media Center IR commands will suffice. However, for some installations the reliability of IR emitting and other factors may make IR emitting problematic and **MCE Controller** offers a robust solution.

**MCE Controller** can act as either a TCP/IP client or server. When acting as a client the target host and port can be configured. When acting as a server the incoming port can be configured.

**MCE Controller** can also listen on an RS-232 serial port.

**MCE Controller** can run showing only a taskbar icon. By double clicking on the taskbar a status window is displayed that shows a log of all activity. You can also right-click on the taskbar icon for a menu.

## Features

*   Can act as a TCP/IP client or server. Supports any number of simultaneous clients.  Supports Telnet protocol.
*   Can act as a serial server listening on RS-232 COM port.
*   Supports simulating key presses (e.g. Alt-Tab, or Win-S) with `SendInput` commands.
*   Supports simulating the mouse.
*   Supports simulating Windows messages (e.g. `WM_SYSCOMMAND` / `SC_MAXIMIZE`) with `SendMessage`commands.
*   Supports simulating start process commands (e.g. run `notepad.exe`) with the `StartProcess`command.
*   Supports simulating changing the window focus with the `SetForegroundWindow` command.
*   Supports sending text (e.g. simulating typing) with the `chars:` command.
*   Includes built-in support for common Windows Media Center commands. It can easily be extended to suit your needs through a `MCEController.commands file.`
*   Supports running multiple instances.
*   Can start minimzed as a taskbar icon. This can be changed in Settings...
*   Has a built-in test mode that makes it easy to test commands. 
*   The `User Activity Monitor` feature will send a command to the home automation system when a user is using the PC (moving the mouse or typing).
*   Automatically checks to see if newer versions are available.
*   Logs diagnostics information to a file.

## Installation

Important Note: **MCE Controller** requires the .NET Framework 4.5+. Go to [http://www.microsoft.com/net](http://www.microsoft.com/net) to ensure you have this installed before running **MCE Controller**.

To install, simply run the `MCEController Setup.exe` to install. The following files will be installed in the directory you choose and a start menu item will be added. You can un-install **MCE Controller** either via add/remove programs or by using the uninstall icon in the **MCE Controller** start menu group.

*   `MCEControl.exe`

When **MCE Controller** runs, it defaults to showing itself as only a taskbar icon. Double clicking on the taskbar icon will show the configuration/status window.

If you would like it to show it’s configuration/status window upon startup, uncheck the _Hide Window at Startup_ checkbox in the settings dialog.

## Settings

[![Settings](https://tig.github.io/mcec/Documentation_Settings_2.png "Settings")](https://tig.github.io/mcec/Documentation_Settings_2.png)

Configuration settings are stored in a file that will be created in the `%APPDATA%\Roaming\Kindel Systems\MCE Controller` directory when MCE Controller is first run. The configuration settings file is named `MCEControl.settings`.

You can run multiple instances of MCE Controller. To do so simply copy the EXE to a 2nd directory. Each copy will then have its own independent `MCEControl.settings` file.

MCE Controller can act as either a TCP/IP client or server (it can actually operate as both simultaneously, which can be useful for testing, but not much else). By default MCE Controller is configured to act as a TCP/IP server listening on port 5150\. You can change this behavior using the Settings dialog described below.

## The Client Tab

The Client tab in the Settings dialog controls **MCE Controller’s** TCP/IP client functionality. When acting as a client, **MCE Controller** will repeatedly try to connect to the specified port on the specified host and wait for commands to be sent from the host. **MCE Controller** sends nothing to the host.

[![Client](https://tig.github.io/mcec/Documentation_Client_2.png "Client")](https://tig.github.io/mcec/Documentation_Client_2.png)

*   Enable Client. This checkbox enables or disables the TCP/IP client functionality. If enabled, the followings settings apply:
*   Host. This is the IP address or host name of the server **MCE Controller** is to connect to.
*   Port. This is the port that **MCE Controller** will connect to.
*   Reconnect Wait Time. This is the number of milliseconds (default is 30 seconds or 30000 ms) **MCE Controller** will wait before trying to reconnect to the host once a connection has been dropped or a connect fails.
*   Show "send command" Window. If checked, whenever the client is connected a window will appear that will allow you to send commands to another instance (or even the same instance with Server turned on) of **MCE Controller**. This is useful for testing that your server is working.

## The Server Tab

The Server tab in the Settings dialog controls **MCE Controller’s** TCP/IP server functionality. When acting as a server, **MCE Controller** will open the specified port and wait for a client to connect. When a client does connect **MCE Controller** will wait for incoming commands until the client closes the connection.

In server mode, **MCE Controller** supports any number of multiple-simultaneous connections.

[![Server](https://tig.github.io/mcec/Documentation_Server_2.png "Server")](https://tig.github.io/mcec/Documentation_Server_2.png)

*   Enable Server. This checkbox enables or disables the TCP/IP server functionality. If enabled, the followings settings apply:
*   Port. This is the port that **MCE Controller** will listen on.
*   Enable Wakeup. If enabled, **MCE Controller** will attempt to connect to the specified host/port, send the “Wakeup command” and disconnect when it first starts. When it shuts down it will send the “Closing command”. This functionality is useful when the remote client needs to be notified that **MCE Controller** is ready (for example after the server PC has rebooted).

## The Serial Server Tab

The Serial Server tab in the Settings dialog controls **MCE Controller’s** serial port (RS-232) functionality. When the Serial Server is enabled, **MCE Controller** will open the specified COM port (e.g. COM1) and wait commands to be sent.

[![Serial](https://tig.github.io/mcec/Documentation_Serial_2.png "Serial")](https://tig.github.io/mcec/Documentation_Serial_2.png)

*   Enable Server. This checkbox enables or disables the Serial Server functionality. It is disabled by default. If enabled, the followings settings apply:
*   Port. This is the serial port that **MCE Controller** will listen on (e.g. COM1).
*   Baud Rate. Sets the speed of the serial port.
*   Data Bits, Parity, Stop Bits, and Handshake: Set the serial port configuration.

## Testing MCE Controller

**MCE Controller** includes a built in TCP/IP client that can send commands to another instance of **MCE Controller** running on the same or different PC. You can even use the Client and Server within the same instance.

### Using the MCE Controller Client

Check the _Show "send command" window_ checkbox in the Client settings and as soon as **MCE Controller** connects to a server instance the _Send Command_ window will appear.

[![Commands](https://tig.github.io/mcec/Documentation_Commands_2.png "Commands")](https://tig.github.io/mcec/Documentation_Commands_2.png)

### Using PUTTY

PuTTY is a free terminal emulator (and Telnet and SSH client). It works well for testing MCE Controller. You can download [PuTTY here](http://www.chiark.greenend.org.uk/~sgtatham/putty/).

#### Using PUTTY to test TCP/IP interactions

1.  Run PUTTY.EXE
2.  Set _Host Name_ to _localhost_ (or the network name of the PC running MCE Controller
3.  Set _Port_ to the port MCE Controller is set to listen on (e.g. 5150)
4.  Set the _Connection Type_ to _Raw_.
5.  Click _Open_

Type commands in the PuTTY Window and see how MCE Controller reacts.

#### Using PUTTY to test serial connections

PuTTY supports connecting via serial ports. The usage is the same as in the TCP/IP example above except you set the appropriate COM port settings in PuTTY and choose the _Serial_ destination type.

## Commands

MCE Controller works with **Commands**. Commands are text strings like `greenbutton`, `hibernate`, and `winkey` tha MCE Controller listens to and acts on. Each command has a **Type**. When **MCE Controller** receives a command it causes an action to happen on the PC it is running on. The action taken is dependent on the type of command and the parameters set for that command.

### Types
The following command types are supported by **MCE Controller**:

*   **StartProcess** - Starts the specified process. Can specify the path to an executable, shortcut, or a URI. Supports embedded `nextCommand` elements allowing other form of MCE Controller commands to be invoke after the process starts.
*   **SetForgroundWindow** - Causes the specified window to be brought to the foreground.
*   **Shutdown** - Allows the host computer to be shutdown, restarted, put in standby, or hibernate mode.
*   **SendMessage** - Enables the sending of window messages to windows. E.g. the 'mcemaximize' command causes the Media Center window to go full screen.
*   **SendInput** - Sends keyboard input as though it were typed on a keyboard.
*   **Mouse** - Sends mouse movement and button actions.
*   **Built-In** - Single characters, `VK_` codes, `chars:`, `shiftdown:`, `shiftup:,` and `mcec:.`

### Built-in Commands

**MCE Controller** includes a set of pre-defined commands for controlling Windows Media Center as well as standard keyboard input. Pre-defined commands can be viewed in the built-in .commands file which is embedded into the MCE Controller executiable [here](https://github.com/tig/mcec/blob/master/Resources/MCEControl.commands). See the section titled “Defining Your Own Commands” below for instructions on how to add or change the commands **MCE Controller** supports.

**MCE Controller** commands are _not_ case-sensitive. Thus `VK_UP` is equivalent to `vk_up` and `shutdown` is equivalent to `ShutDown`.

The following describes the Built-In commands that are supported:

### Windows Virtual Key Code Commands

Any Windows virtual key code is supported by default. The form of the commands are `VK_<key name>`. For example you can send **MCE Controller** any of the following commands and the corresponding key press will be simulated.

    VK_ESCAPE
    VK_LWIN
    VK_VOLUME_MUTE
    VK_VOLUME_UP 
    VK_MEDIA_PLAY_PAUSE
    VK_F1

If you want a keystroke that includes a shift modifier (e.g. `Win-D` or `Ctrl-G`) you should define a custom `SendInput` command as described below.

You can find a list of all Window's virtual key codes on [this MSDN page](http://msdn.microsoft.com/en-us/library/dd375731.aspx)

### Any Single Character Commmands

This is equivalent to a single key press of a key on the keyboard. For example `a` will result in the A key being pressed. `1` will result in the `1` key being pressed. There is no difference between sending `a` and `A`. Use `shiftdown:/shiftup:` to simulate the pressing of the shift, control, alt, and windows keys.

### Shift Down/Up Commands

To simulate a key down event for one of the modifiers keys (shift, control, alt, and the Windows key) send a `shiftdown:` or `shiftup:` command. The syntax is:

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

(Although, using a `chars:` command would probably work better in most cases).

This scheme can be used as an alternative way of sending ctrl-, alt-, and win- keystrokes. For example to simulate ctrl-s:

    shiftdown:ctrl
    s
    shiftup:ctrl 

### chars: Commands

Anytime **MCE Controller** receives `chars:` plus some text, it simulates the typing of that text on the keyboard. The syntax of the command is `chars:*` where '*'' represents one or more characters. This is equivalent to typing those characters on the keyboard. E.g. `chars:3` will cause the number 3 to be typed as though the user had pressed the 3 key on the keyboard. `chars:Hello` will cause `Hello` to be typed.

Unicode (and other escaped character sequences are supported). `chars:\u20AC` will cause the € character to be input into the foreground window on the machine **MCE Controller** is running on.

NOTE: Older versions of MCE Controller supported a `keys:` command that purported to do the same thing. It never actually worked right and has been replaced with the new `chars:` command.

### mouse: Commands

**MCE Controller** can simulate mouse movement. With this it is possible to build a remote control that acts like a mouse (I have built a test app for Windows Phone 7 that enables WP7 to work like a touchpad; contact me if you are interested in it).

The general format of the mouse commands is:

    mouse:<action>[,<param>,...,<param>]

The available mouse actions are:

*   **lbc** - Left button click (`mouse:lbc`)
*   **lbdc** - Left button double-click (`mouse:lbdc`)
*   **lbd** - Left button down (`mouse:lbd`)
*   **lbu** - Left button up (`mouse:lbu`);
*   **rbc, rbdc, rbd, rbu** - Same same but for the right mouse button.
*   **xbc, etc...** - x button click where x is a button number (`mouse:xbc,3` for button 3 click)
*   **mm,x,y** - Move the mouse x, y pixels (`mouse:mm,7,-3` would move the mouse right 7 and up 3 pixels)
*   **mt,x,y** - Move the mouse to a location. The coordinates represent the absolute X/Y-coordinates on the primary display device where 0 is the extreme left/bottom of the display device and 65535 is the extreme right/bottom hand side of the display device (`mouse:mt,0,65535` would move the mouse to the bottom left corner of the primary display).
*   **mtv,x,y** - Move the mouse to a location on the virtual desktop. The coordinates represent the absolute X/Y-coordinates on the virtual desktop where 0 is the extreme left/top of the virtual desktop and 65535 is the extreme right/bottom (`mouse:mtv,65535,0` would move the mouse to the top right corner of the virtual desktop).
*   **hs,n** - Simlate a horizontal scroll gesture. `n` is the amount to scroll in clicks. A positive value indicates that the wheel was rotated to the right; a negative value indicates that the wheel was rotated to the left (`mouse:hs,3`).
*   **vs,n** - Simlate a vertical scroll gesture. `n` is the amount to scroll in clicks. A positive value indicates that the wheel was rotated forward, away from the user; a negative value indicates that the wheel was rotated backward, toward the user (`mouse:vs,3`).

Note that when sending mouse movements it is best if the **MCE Controller** window is hidden as the display log tends to chew up a lot of resources, making things jerky.

### mcec: Commands

The following commands control **MCE Controller** itself:

*   **mcec:ver** – Gets the version number.
*   **mcec:exit** – Causes **MCE Controller** to exit.
*   **mcec:cmds** – Lists all commands.

Values returned by commands in **MCE Controller** are of the format “command=value” where command is the command to the left of the command prefix (mcec:).

### Defining Your Own Commands

MCE Controller supports over 250 built-in commands. You can see how they are defined [here](https://github.com/tig/mcec/blob/master/Resources/MCEControl.commands). You can override or augment this set by creating a file named `MCEControl.commands` file and putting it in the same directory as the program executable (Use the `File.Open commands file...` menu to find the .EXE file location on your machine.

A sample `MCEControl.commands` file is in the MCE Controller source code here:

https://github.com/tig/mcec/blob/master/Example.MCEControl.commands

If a `MCEControl.commands` file is found in the working directory when MCE Controller starts any commands it defines will add to and over-ride matching built-in commands.

Create a new file named MCEControl.commands and put it in the same directory as the MCE Controller EXE file.

    <?xml version="1.0" encoding="utf-8"?>
    <MCEController xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <Commands xmlns="http://www.kindel.com/products/mcecontroller">
    <!-- Place command definitions here -->
    </Commands>
    </MCEController>

`MCEControl.commands` allows you to define four types of commands: `SendInput`, `SendMessage`, `StartProcess`, `Shutdown`, and `SetForegroundWindow`.

**Note on case sensitivity**: In the `MCEControl.commands` file, all XML element and attribute names are case-sensitive. E.g. ‘ctrl’ is NOT the same as ‘Ctrl’.  The commands themselves (the values of Cmd attributes) are NOT case-sensitive. E.g. ‘MonitorOff’ will be treated the same as ‘monitoroff’.

#### SendInput Commands

`SendInput` commands send keystrokes. Any combination of shift, ctrl, alt, and left/right Windows keys can be used with any "virtual key code". See the `winuser.h` file in the Windows SDK or [this MSDN page](http://msdn.microsoft.com/en-us/library/dd375731.aspx) for a definition of all standard `VK_` codes. **MCE Controller** understands key codes in hex (e.g. `0x2a`) or decimal format, or as a `VK_` name. **MCE Controller** uses the Windows `SendInput()` API to send keystrokes. Keystrokes go to the foreground window.

For example, the following causes a Ctrl-P to be sent to the foreground window, and if that window is Media Center, the My Pictures page appears:

     <SendInput Cmd="mypictures" vk="73" Shift="false" Ctrl="true" Alt="false" />

(The VK code or 'P' is 73 decimal).

This example causes a Windows-X to be simulated, which causes the Windows 8 "expert" menu to pop up:

     <SendInput Cmd="winx" vk="VK_X" Win="true"/>

#### SendMessage Commands

`SendMessage` commands are just that. They cause **MCE Controller** to send a Windows message using the `SendMessage()` API to the foreground window if no class name is specified, or to a particular window if that window’s class is specified. `Msg`, `wParam`, and `lParam` must be specified in decimal (not hex!).

For example, the following is equivalent to sending a `WM_SYSCOMMAND` with the `SC_MAXIMIZE` flag, causing the window with the class name of `ehshell` to be maximized (`WM_SYSCOMMAND == 247` and `SC_MAXIMIZE == 61488`):

    <SendMessage Cmd="mce_maximize" ClassName="ehshell" Msg="274" wParam="61488" lParam="0" />

These commands might be useful in some scenarios:

    <!--      WM_SYSCOMMAND, SC_SCREENSAVE                                 -->
    <SendMessage Cmd="screensaver" Msg="274" wParam="61760" lParam="0" />
    <!--      WM_SYSCOMMAND, SC_MONITORPOWER, 2 = off, -1 = on             -->
    <SendMessage Cmd="monitoroff" Msg="274" wParam="61808" lParam="2" />
    <SendMessage Cmd="monitoron" Msg="274" wParam="61808" lParam="-1" />

See the [MSDN documentation](http://msdn.microsoft.com/en-us/library/ms646360(v=VS.85).aspx) for more `WM_SYSCOMMAND` possibilities.

#### StartProcess Commands

`StartProcess` commands start processes. Process commands support chaining using the `nextCommand` element. The embedded command will be executed after the started application starts processing windows’ messages.

For example, the following launches Media Center and maximizes it:

    <StartProcess Cmd="mce_start" File="C:\windows\ehome\ehshell.exe">
        <nextCommand xsi:type="SendMessage" ClassName="ehshell" Msg="274" wParam="61488" lParam="0" />
    </StartProcess>

#### Shutdown Commands

The supported shutdown commands are self-explanatory.

    <Shutdown Cmd="shutdown" Type="shutdown"/>
    <Shutdown Cmd="restart" Type="restart"/>
    <Shutdown Cmd="abort" Type="abort"/>
    <Shutdown Cmd="standby" Type="standby"/>
    <Shutdown Cmd="hibernate" Type="hibernate"/>

#### SetForgroundWindow Commands

The `SetForegroundWindow` command sets the specified window (using the window’s class name) to the foreground.

For example, the following makes Media Center the foreground Window (assuming Media Center is running):

    <SetForegroundWindow Cmd="mce_activate" ClassName="ehshell"/>

Note that **MCE Controller** supports the `chars:`, `shiftup:`, and `shiftdown:` commands in addition to the commands defined in MCEControl.commands.

Also note that you should not make commands a single character or it will interfere with the ability to simulate individual character key presses.

#### Disabling All Internal Commands

You can force MCE Controller to only listen to and act on commands defined in the MCEControl.commands file. To do this use the Windows registry editor to create the `HKEY_LOCAL_MACHINE\SOFTWARE\Kindel Systems\MCE Controller` registry key and set <font face="Courier New">DisableInternalCommands</font> (a DWORD value) to anything other than 0.

This will disable ALL internal commands.

This is a machine wide setting and will apply to all instances of MCE Controller.

## Logging

All information shown in the main window is also logged to a file to ease diagnostics. If the program is started from the default location (in Program Files) the log will be written to %LocalAppData%\Kindel Systems\MCE Controller\MCEControl.log. Otherwise the log will be written to the directory the program is started from.

## Usage Notes

The `mcestart` command will launch Media Center and cause it to be maximized. If you do not want this behavior, change `MCEControl.commands` such that the `mcestart` command does not have the embedded `nextCommand` element.

For **MCE Contoller** to work property the target application (Media Center) must be the active window (foreground) on the desktop. You can use the `mceactivate` command to cause Media Center to be the foreground app if it’s already running. Alternatively you can just use `mcestart` as it will end up causing the same thing to happen (although not as quickly).

Also, you may find that `greenbutton` is a better function than `mcestart` because it is equivalent to the green-button on a Windows remote control. `mcestart` is a bit different because if Media Center is already running `mcestart` will not go to the "Start" screen of Media Center while `greenbutton` will. However, `greenbutton` does not cause the Media Center window to be maximized.

# Version History

*   Version 1.0.1 (February 22, 2004) – First publicly released version.
*   Version 1.0.2 (March 24, 2004) - New features: Added support for system shutdown, restart, standby, and hibernate (the Shutdown command type). Renamed a few commands ("mce_start" is now "mcestart" for example) to be more consistent.
*   Version 1.0.3 (March 26, 2004) - Added installer.
*   Version 1.0.4 (February 26, 2005) - Fixed bug that caused MCE Controller to prevent logoffs and shutdowns.
*   Version 1.0.5 (April, 2005) – Added support for arbitrary # of characters for the “key:” command.
*   Version 1.1.0 (May 11, 2005) – No functional changes. Changed the source license to the BSD license and posted on Sourceforge.
*   Version 1.3.0 (January 3, 2012) – Added support for "chars:". Removed support for "keys:". Added "enter" command. Now builds with VS2010.
*   Version 1.3.1 (January 4, 2012) – Fixed bug parsing -1 in the lParam of SendMessageCommands. Commented MCEController.commands. Minor code cleanup.
*   Version 1.3.2 (January 4, 2012) – Fixed bug in how .commands and .settings are stored (Win7 broke permissions).
*   Version 1.3.3 (January 9, 2012) – Added capability to send individual key presses with shift/ctrl/alt/win modifiers (what keys: originally was supposed to do).
*   Version 1.4.0 (February 11, 2012) - Server now supports any number of client connections. Expanded MCEController.commands to include commands used by iRule (http://iruleathome.com). Updated About Box & Help menu to reflect move to GitHub. Added menu item to open directory containing MCEController.commands.
*   Version 1.5.0 (March 27, 2012) - 'chars:' command now supports escaped characters. This allows the sending of Unicode characters such as € (e.g. 'chars:\u20AC' will cause the € character to be input on the server machine).
*   Version 1.5.1 (April 2, 2012) - Removed readme file from distribution and updated online docs.
*   Version 1.5.2 (October, 4, 2012) - Fixed .settings file bug where it would sometimes read from Program Files and write to AppData. Now always writes to AppData unless started outside of Program Files. Fixed Setting dialog to be more resilient to bad data. Fixed Send Awake so that it does not fault on bad data, but logs errors. General code clean up. Built with VS2012.
*   Version 1.6.0 (October 10, 2012) - Added mouse simulation support.
*   Version 1.6.1 (November 6, 2012) - Fixed bug with some Telnet clients that don't buffer each line before sending.
*   Version 1.7.0 (December 19, 2012) - Added Serial Server support.
*   Version 1.8.0 (December 30, 2012) - Added VK_ command support. Added 'command window'. New icon. Updated documentation.
*   Version 1.8.1 (January 1, 2013) - Updated links for CodePlex. Fixed crashing bug on exit.
*   Version 1.8.4 (March, 2014) - New icon by [http://guillendesign.deviantart.com/](http://guillendesign.deviantart.com/), Minor menu tweaks, MCEControl.commands is now an optional file. The previously defined set of commands from older builds are now built into the program. If a MCEControl.commands file is present it will add to and override these pre-defined commands. Upgrades and un-installs will no longer overwrite or delete the MCEControl.commands file.
*   Version 1.8.6 (May, 2014) – Internal commands can be disabled via a registry key. Fixed bug when client forcibly closed socket. Added logging.
*   Version 1.9.0 (April 15, 2017) - Moved from Codeplex to GitHub. 

