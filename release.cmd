@echo off
mkdir build
copy .\bin\Release\MCEControl.exe .\build
copy .\bin\Release\HtmlAgilityPack.dll .\build
copy .\bin\Release\log4net.dll .\build
copy .\Installer\license.txt .\build
copy .\Installer\MCEController.nsi .\build
start .\build