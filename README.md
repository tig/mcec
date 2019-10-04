# MCE Controller

By Charlie Kindel ([@ckindel on Twitter](http://www.twitter.com/ckindel)) - Copyright © 2019 [Kindel Systems](http://www.kindel.com), LLC.

![icon](https://tig.github.io/mcec/Home_mcecontroller_2.png "MCE Controller Icon")

## Download

[Download the MCE Controller for Windows installer](https://github.com/tig/mcec/releases)

## Overview

MCE Controller is a Windows application enabling Windows PCs to be controlled by home automation systems. 
It runs in the background and receives commands from a home automation system (such as Control4) over the network (or serial port). 
It then translates those commands into actions such as keystrokes, text input, and the starting of programs. 

Any home automation system, control system, or application that can send text strings via TCP/IP or a serial port can use MCE Controller to control a Windows PC.

MCE Controller was originally written in 2004 to enable integration of a Windows Media Center Edition (hence the name "MCE Controller") based home theater PC (HTPC) into a Crestron whole-house audio/video system. It has been continually improved since and is general enough that others have used it with other control systems such as Control4, Creston, Home Assistant, and Home Seer. Anything that supports sending or receivng text strings to a TCP/IP port can work.

## Top Features

* Can act as a TCP/IP client or server. Supports any number of simultaneous clients.  Supports Telnet protocol.
* Can act as a serial server listening on RS-232 COM port.
* Almost any action a user can perform on Windows can be invoked. This includes key presses (e.g. Alt-Tab, or Win-S), mouse movements, and Window management actions (e.g. maximize window or set a window to the foreground).
* Windows programs can be started (and stopped) (e.g. run notepad.exe) 
* Supports running multiple instances.
* Can start minimzed as a taskbar icon. 
* The User Activity Monitor features notifies a home control system of user activity, which can be used for room occupancy detection.
* Has a built-in test mode that makes it easy to test commands.
* Automatically checks to see if newer versions are available.

## Documentation

* [MCE Controler Wiki](https://github.com/tig/mcec/wiki)
* [MCE Controller documentation](https://github.com/tig/mcec/wiki/Documentation)

## Support 

* [Charlie's Blog Posts on MCE Controller](http://ceklog.kindel.com/category/passions/homeautomation/mce-controller/)
