# Example MCE Controller Commands

Here's a bunch of example commands users have defined and posted. They may serve as inspiration or guidance...

## Start playing the movie Blade Runner on Netflix

```xml
<startprocess cmd="bladerunner" enabled="true" file="shell:AppsFolder\4DF9E0F8.Netflix_mcm4njqhnhss8!Netflix.App">
    <pause args="5000" enabled="true" />
    <sendinput enabled="true" alt="false" ctrl="false" shift="false" win="false" vk="VK_TAB" />
    <sendinput enabled="true" alt="false" ctrl="false" shift="false" win="false" vk="VK_TAB" />
    <sendinput enabled="true" alt="false" ctrl="false" shift="false" win="false" vk="VK_RETURN" />
    <pause args="1000" enabled="true" />
    <chars args="Blade Runner" enabled="true" />
    <pause args="1000" enabled="true" />
    <sendinput enabled="true" alt="false" ctrl="false" shift="false" win="false" vk="VK_RETURN" />
    <pause args="1000" enabled="true" />
    <sendinput enabled="true" alt="false" ctrl="false" shift="false" win="false" vk="VK_TAB" />
    <sendinput enabled="true" alt="false" ctrl="false" shift="false" win="false" vk="VK_TAB" />
    <sendinput enabled="true" alt="false" ctrl="false" shift="false" win="false" vk="VK_RETURN" />
    <sendinput enabled="true" alt="false" ctrl="false" shift="false" win="false" vk="VK_TAB" />
    <sendinput enabled="true" alt="false" ctrl="false" shift="false" win="false" vk="VK_RETURN" />
</startprocess>
```

Note, this could also be achieved by having the control system send MCE Controller a set of discrete commands, as shown in this screenshot: 

![Commands](commands_test.png "Commands")

## Start Notepad and do stupid tricks with the window

```xml
<StartProcess enabled="true" Cmd="notepad" File="notepad.exe" > <!-- start notepad -->
    <Pause Args="100"/>                          <!-- wait 100ms for it to start -->
    <Chars Cmd="test" Args="this is a test." />  <!-- type some text -->
    <SendInput vk="VK_RETURN"/>                  <!-- hit enter -->
    <Pause Args="100"/>                      <!-- pause -->
    <SendInput vk="VK_RIGHT" Shift="true" Win="true"/> <!-- Win-Shift-Right to move Notepad to 2nd monitor -->
    <Pause Args="100"/>                      <!-- pause -->
    <SendMessage Cmd="maximize" Msg="274" wParam="61488" lParam="0" /> <!-- maximize notepad -->
    <SendInput vk="VK_RETURN">                   <!-- hit enter -->
    <Chars Args="Second "/>                      <!-- type a second line of text -->
    <Chars Args="line.." />
    <SendInput vk="h" Alt="true"/>           <!-- Alt-H, Alt-A to pop Help About dialog -->
    <SendInput vk="a" Alt="false"/>
</StartProcess>
```

## Move the mouse

```xml
<Chars enabled="true" Cmd="movemouse">
<Mouse Args="mm,100,100"/>
<Pause Args="250"/>
<SendInput vk="moved"/>
</Chars>
```

## Controlling HDHomeRun

```xml
<StartProcess enabled="true" Cmd="Start_HDHomeRun" File="C:\AppShortcuts\HDHomeRun.lnk" />
<SendInput Cmd="Nfs" vk="13" Shift="false" Ctrl="false" Alt="true" />
<SendInput Cmd="Npause" vk="81" Shift="false" Ctrl="true" Alt="false" />
<SendInput Cmd="Nplay" vk="80" Shift="false" Ctrl="true" Alt="false" />
<SendInput Cmd="Nstop" vk="83" Shift="false" Ctrl="true" Alt="false" />
<SendInput Cmd="Nrecord" vk="75" Shift="false" Ctrl="true" Alt="false" />
<SendInput Cmd="Nch+" vk="33" Shift="false" Ctrl="false" Alt="false" />
<SendInput Cmd="Nch-" vk="34" Shift="false" Ctrl="false" Alt="false" />
<SendInput Cmd="Nprev" vk="87" Shift="false" Ctrl="true" Alt="false" />
<SendInput Cmd="Ntvguide" vk="F1" Shift="false" Ctrl="false" Alt="false" />
<SendInput Cmd="Nrew" vk="82" Shift="false" Ctrl="true" Alt="false" />
<SendInput Cmd="Nfwd" vk="70" Shift="false" Ctrl="true" Alt="false" />
<SendInput Cmd="Nskipback" vk="37" Shift="false" Ctrl="true" Alt="false" />
<SendInput Cmd="Nskipfwd" vk="39" Shift="false" Ctrl="true" Alt="false" />
<SendInput Cmd="Nexit" vk="115" Shift="false" Ctrl="false" Alt="true" />
<SendInput Cmd="Nmute" vk="173" Shift="false" Ctrl="false" Alt="false" />
```

## Start Media Center (eHome)

```xml
<StartProcess enabled="true" Cmd="mcestart" File="C:\windows\ehome\ehshell.exe">
<nextCommand xsi:type="SendMessage" 
            ClassName="ehshell"
            Msg="274" wParam="61488" lParam="0" />
</StartProcess>
```
