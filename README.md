# MCE Controller

By Charlie Kindel ([@ckindel on Twitter](http://www.twitter.com/ckindel)) - Copyright © 2019 [Kindel Systems](http://www.kindel.com), LLC.

![icon](https://tig.github.io/mcec/Home_mcecontroller_2.png "MCE Controller Icon")

## Download

[Download the MCE Controller for Windows installer](https://github.com/tig/mcec/releases)

## Overview

MCE Controller makes it easy to control Windows PCs to from home automation systems. 
It runs in the background receiving commands from a home automation system (such as Control4) over the network (or serial port). 
It then translates those commands into actions such as keystrokes, text input, and the starting of programs. 

Any home automation system, control system, or application that can send text strings via TCP/IP or a serial port can use MCE Controller to control a Windows PC.

MCE Controller was originally written in 2004 to enable integration of a Windows Media Center Edition (hence the name "MCE Controller") based home theater PC (HTPC) into a Crestron whole-house audio/video system. It has been continually improved since and is general enough that others have used it with other control systems such as Control4, Home Assistant, and Home Seer. Anything that supports sending or receiving text strings to a TCP/IP port can work.

## Top Features

* Almost any action a user can perform using Windows can be remotely commanded. This includes key presses (e.g. Alt-Tab, or Win-S), mouse movements, and Window management actions (e.g. maximize window or set a window to the foreground).
* Includes over 250 built-in commands, and new commands can be easily defined by the user.
* Can act as a TCP/IP client or server. Supports any number of simultaneous clients. Supports Telnet protocol.
* Can act as a serial server listening on RS-232 COM port.
* Windows programs can be started (and stopped) (e.g. to start Netflix). 
* Running multiple instances is supported, each with a different configuration.
* Can start minimized as a taskbar icon. 
* The User Activity Monitor features notifies a home control system of user activity (mouse & keyboard), which can be used for room occupancy detection.
* Has a built-in test mode that makes it easy to test commands. Simply enable the Client to connect to `localhost` and set the Server to the same port. The Commands Window supports sending all built-in and user defined commands.
* Automatically checks to see if newer versions are available.

## Documentation

* [MCE Controller Overview](https://github.com/tig/mcec/wiki/MCE-Controller-Overview)
* [Full MCE Controller Documentation](https://github.com/tig/mcec/wiki/Documentation)
* [Charlie's Blog Posts on MCE Controller](http://ceklog.kindel.com/category/passions/homeautomation/mce-controller/)
