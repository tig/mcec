@echo off
mkdir build
copy .\bin\Release\MCEControl.exe .\build
copy .\bin\Release\HtmlAgilityPack.dll .\build
copy .\MCEControl.commands .\build
copy .\Installer\license.txt .\build
copy .\Installer\MCEController.nsi .\build
start .\build