# MCE Controller

By Charlie Kindel ([@ckindel on Twitter](http://www.twitter.com/ckindel)) -- Copyright © 2019 [Kindel Systems](http://www.kindel.com), LLC.

![MCE Controller](mainwindow.png)

## Download

**[Download and Install the Latest Version](https://github.com/tig/mcec/releases)**

## Documentation

**[Full MCE Controller Documentation](documentation.md)**

## Overview

**MCE Controller** provides robust remote control a Windows HTPC (or any PC) over the network. It runs in the background listening on the network (or serial port) for commands. It then translates those commands into actions such as keystrokes, text input, and the starting of programs. Any remote control, home control system, or application that can send text strings via TCP/IP or a serial port can use **MCE Controller** to control a Windows PC.

Almost any action a user can perform on Windows can be invoked remotely from another device on the network. This includes key presses (e.g. Alt-Tab, or Win-S), mouse movements, and Window management actions (e.g. maximize window or set a window to the foreground)

**MCE Controller** works great with any remote control system that supports TCP/IP or RS-232 connections. Examples include [**Control4**](https://www.control4.com/), [**iRule**](http://www.iruleathome.com/), [**Crestron**](http://www.crestron.com/), and [**Premise Home Control**](http://cocoontech.com/forums/forum/51-premise-home-control/)

**[Full MCE Controller Documentation](documentation.md)**

## Version History

* Version 1.0.1 (February 22, 2004) – First publicly released version.
* Version 1.0.2 (March 24, 2004) - New features: Added support for system shutdown, restart, standby, and hibernate (the Shutdown command type) Renamed a few commands ("mce_start" is now "mcestart" for example) to be more consistent.
* Version 1.0.3 (March 26, 2004) - Added installer.
* Version 1.0.4 (February 26, 2005) - Fixed bug that caused MCE Controller to prevent logoffs and shutdowns.
* Version 1.0.5 (April, 2005) – Added support for arbitrary # of characters for the “key:” command.
* Version 1.1.0 (May 11, 2005) – No functional changes. Changed the source license to the BSD license and posted on Sourceforge.
* Version 1.3.0 (January 3, 2012) – Added support for "chars:". Removed support for "keys:". Added "enter" command. Now builds with VS2010.
* Version 1.3.1 (January 4, 2012) – Fixed bug parsing -1 in the lParam of SendMessageCommands. Commented MCEController.commands. Minor code cleanup.
* Version 1.3.2 (January 4, 2012) – Fixed bug in how .commands and .settings are stored (Win7 broke permissions).
* Version 1.3.3 (January 9, 2012) – Added capability to send individual key presses with shift/ctrl/alt/win modifiers (what keys: originally was supposed to do).
* Version 1.4.0 (February 11, 2012) - Server now supports any number of client connections. Expanded MCEController.commands to include commands used by iRule (http://iruleathome.com). Updated About Box & Help menu to reflect move to GitHub. Added menu item to open directory containing MCEController.commands.
* Version 1.5.0 (March 27, 2012) - 'chars:' command now supports escaped characters. This allows the sending of Unicode characters such as € (e.g. 'chars:\u20AC' will cause the € character to be input on the server machine).
* Version 1.5.1 (April 2, 2012) - Removed readme file from distribution and updated online docs.
* Version 1.5.2 (October, 4, 2012) - Fixed .settings file bug where it would sometimes read from Program Files and write to AppData. Now always writes to AppData unless started outside of Program Files. Fixed Setting dialog to be more resilient to bad data. Fixed Send Awake so that it does not fault on bad data, but logs errors. General code clean up. Built with VS2012.
* Version 1.6.0 (October 10, 2012) - Added mouse simulation support.
* Version 1.6.1 (November 6, 2012) - Fixed bug with some Telnet clients that don't buffer each line before sending.
* Version 1.7.0 (December 19, 2012) - Added Serial Server support.
* Version 1.8.0 (December 30, 2012) - Added VK_ command support. Added 'command window'. New icon. Updated documentation.
* Version 1.8.1 (January 1, 2013) - Updated links for CodePlex. Fixed crashing bug on exit.
* Version 1.8.4 (March, 2014) - New icon by [http://guillendesign.deviantart.com/](http://guillendesign.deviantart.com/), Minor menu tweaks, MCEControl.commands is now an optional file. The previously defined set of commands from older builds are now built into the program. If a MCEControl.commands file is present it will add to and override these pre-defined commands. Upgrades and un-installs will no longer overwrite or delete the MCEControl.commands file.
* Version 1.8.6 (May, 2014) – Internal commands can be disabled via a registry key. Fixed bug when client forcibly closed socket. Added logging.
* Version 1.9.0 (April 15, 2017) - Moved from Codeplex to GitHub.
* Version 2.0.0 (October 8, 2019) - Version 2. Major update.
  * Use the PC as an occupancy sensor for a room. The User Activity Monitor feature will send a command to the home automation system when a user is using the PC (moving the mouse or typing).
  * Re-engineered Client & Server implementation is more robust.
  * New/enhanced built-in test mode that makes it easy to test commands. The new Commands Window shows all available commands.
  * Significantly updated UI throughout. Menus and dialog boxes reorganized based on user feedback. Full Windows 10 system font and dpi scaling support.
  * Command extension has been enhanced. User defined MCEControl.commands is now automatically generated.
  * StartProcess commands are now more robust and flexible.
  * Settings, Command files, and log files are stored in %appdata%.
  * Improved logging.
* Version 2.0.4 (October 11, 2019) - Fixed bug where Server was not sending commands back to client.
* Version 2.1.0 (October 25, 2019) - Lots of updates
  * Commands defined in `MCECommands.command` now *really* override any built-ins. 
  * Reverted the set of built-in commands to include tons of defaults.
  * Key and Attribute names (e.g. `<sendinput>` or `Shift=`) in MCECommands.commands` are no longer case senstive.
  * Default pacing for commands is settable. See `General` settings tab. Default is 0. Specify in ms.
  * New `Pause` Command enables putting delays between commands
  * Shutdown commands are expanded and more reliable. Almost all funcdtions supported by the Windows `shutdown.exe` command are supported.
  * Command window now supports sending multiple lines (scripts)
  * All `<Commands>` in `MCEControl.commands` can now be nested. This makes it easy to create compound commands (scripts).
  * Added `Chars` Command. Useful in nested commands.
* Version 2.2.0 (March 24, 2020) - 
  * Activity Monitor will now send activity messages every `Debounce Time` seconds whenever the Windows desktop/session is unlocked.
  * Added telemetry and modified Setup to support opt-in/out. 
