### MCE Controller

By Charlie Kindel ([@ckindel on Twitter](http://www.twitter.com/ckindel)) -- Copyright © 2019 [Kindel Systems](http://www.kindel.com), LLC.  

![MCE Controller](https://tig.github.io/mcec/Documentation_MCE%20Controller_2.png)

## Download

[Click Here To download the Latest MCE Controller Installer](https://github.com/tig/mcec/releases)

## Documentation

[Read full documentation here.](https://github.com/tig/mcec/wiki/Documentation)

## Overview

**MCE Controller** lets you control a Windows HTPC (or any PC) over the network. It runs in the background listening on the network (or serial port) for commands. It then translates those commands into actions such as keystrokes, text input, and the starting of programs. Any remote control, home control system, or application that can send text strings via TCP/IP or a serial port can use **MCE Controller** to control a Windows PC.

For example:  

*   The command `mcestart` will cause the Windows Media Center application to start. This is equivalent to pressing the green button on the Windows remote control.
*   The command `maximize` will cause the current window to be maximized on the display. This is equivalent to choosing the "Maximize" button on the window's window menu.
*   The command `chars:Hello World!` will cause the text "Hello World" to be typed, as though it were typed on the keyboard.
*   The command `VK_MEDIA___NEXT_TRACK` will cause the currently running media player app (Spotify, Windows Media Player, etc...) to jump to the next media track, just as if the user had pressed the "next track" key on the keyboard.
*   The commands that MCE Controller support is extensible through a configuration file. If it does not natively support a function you wish, you can add new commands easily.

**MCE Controller** was initially developed (in 2004) to enable integration of a Windows based home theater PC (HTPC) into a Crestron whole-house audio/video system. However, it is general enough that others have used it within other control system that support sending text strings to a TCP/IP port. 

**MCE Controller** works great with any remote control system that supports TCP/IP or RS-232 connections. Examples include:

*   [**Control4**](https://www.control4.com/) - Control4 is the leading professionally installed & managed smart home platform. MCE Controller makes it easy to integrate a Windows PC into a Control4 system. The User Activity Montior added to version 2.0 of MCE Controller makes it easy to enhance an occupancy detector in a room (if the PC's being used, the room is occupied).
*   [**iRule**](http://www.iruleathome.com/) – iRule is an iPhone/iPad app that turns your iPhone/iPad into a universal remote control for your home. See [this tutorial](http://support.iruleathome.com/customer/portal/articles/474014-tutorial-mce-htpc-control) on how to use MCE Controller and iRule together.
*   [**Crestron**](http://www.crestron.com/)– Crestron provides control systems for homes and businesses. Integrating MCE Controller with Crestron is as simple as creating a TCP/IP Client in Crestron and defining a buffer with commands.
*   [**Premise Home Control**](http://cocoontech.com/forums/forum/51-premise-home-control/) – Premise (formerly known as SYS) is an extremely sophisticated Windows based home control system. Originally built by some ex-Microsoft people and later sold to Lantronix and then Motorola, Premise is not officially supported, but usage remains strong and there is a great community on [www.coccontech.com](http://www.coccontech.com). MCE Controller works great with Premise.

Below are some of the scenarios users have reported they’ve found **MCE Controller** useful for:

*   **Controlling Windows Media Center.** This is what **MCE Controller** was originally intended for, hence the name (the first version of [Windows Media Center](http://en.wikipedia.org/wiki/Windows_Media_Center) shipped with a version of Windows called “Windows Media Center Edition” aka MCE).
*   **Use the PC as an occupancy sensor for a room.** The User Activity Monitor feature will send a command to the home automation system when a user is using the PC (moving the mouse or typing).
*   **General Remote PC Management.** For example remotely shutting down or restarting a PC.
*   **Enhanced XBMC Control.** [XBMC](http://xbmc.org/) is an open source competitor to Windows Media Center. It supports it’s own network based control system, but when running on Windows, MCE Controller can be used in conjunction with XBMC to control non-XBMC specific PC functions.

## Features

* Can act as a TCP/IP client or server. Supports any number of simultaneous clients.  Supports Telnet protocol.
* Can act as a serial server listening on RS-232 COM port.
* Almost any action a user can perform on Windows can be invoked. This includes key presses (e.g. Alt-Tab, or Win-S), mouse movements, and Window management actions (e.g. maximize window or set a window to the foreground).
* Windows programs can be started (and stopped) (e.g. run notepad.exe) 
* Supports running multiple instances.
* Can start minimzed as a taskbar icon. 
* The User Activity Monitor features notifies a home control system of user activity, which can be used for room occupancy detection.
* Has a built-in test mode that makes it easy to test commands.
* Automatically checks to see if newer versions are available.

## [Full MCE Controller Documentation](https://github.com/tig/mcec/wiki/Documentation)