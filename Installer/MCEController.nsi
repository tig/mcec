; NSIS installer for MCEC (Model Context Environment Controller); .NET 10, self-contained.
;
; Build via Release.ps1 (recommended), or directly:
;   makensis /DVERSION=2.4.0 /DPUBLISHDIR=<abs path to publish folder> Installer\MCEController.nsi
;
; Defaults below let it also be built standalone from a prior `dotnet publish`
; into src\bin\publish. The publish output must be a self-contained build
; (the .NET runtime is bundled, so the installer has no prerequisites).

Unicode True

!ifndef VERSION
  !define VERSION "0.0.0"
!endif
!ifndef PUBLISHDIR
  !define PUBLISHDIR "${__FILEDIR__}\..\src\bin\publish"
!endif
!ifndef OUTFILE
  !define OUTFILE "${__FILEDIR__}\..\src\bin\mcec.Setup.exe"
!endif

!define PRODUCT_NAME "MCEC"
!define PRODUCT_VERSION "${VERSION}"
!define PRODUCT_PUBLISHER "Kindel"
!define PRODUCT_WEB_SITE "https://github.com/tig/mcec/wiki"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\mcec.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_UNINST_ROOT_KEY "HKLM"
!define PRODUCT_STARTMENU_REGVAL "NSIS:StartMenuDir"

!include MUI2.nsh
!include nsDialogs.nsh
!include LogicLib.nsh
!include x64.nsh

!define MUI_ABORTWARNING
!define MUI_ICON "${__FILEDIR__}\..\src\Resources\Guillen.Icon.ico"
!define MUI_UNICON "${__FILEDIR__}\..\src\Resources\Guillen.Icon.ico"
!define MUI_BGCOLOR ffffff
!define MUI_WELCOMEFINISHPAGE_BITMAP "${__FILEDIR__}\..\src\Resources\welcome.bmp"

;=========== Telemetry Checkbox ==========
!define MUI_PAGE_CUSTOMFUNCTION_SHOW telemetrycheckshow
!define MUI_PAGE_CUSTOMFUNCTION_LEAVE telemetrycheckleave

Var mycheckbox

Function telemetrycheckshow
${NSD_CreateCheckbox} 120u -18u 50% 12u "Collect Telemetry Information"
Pop $mycheckbox
SetCtlColors $mycheckbox "" ${MUI_BGCOLOR}
${NSD_Check} $mycheckbox
FunctionEnd

Function telemetrycheckleave
${NSD_GetState} $mycheckbox $0
${If} ${RunningX64}
    SetRegView 64
${EndIf}
# NOTE: This registry key is legacy infrastructure read by the app (TelemetryService /
# AppSettings DisableInternalCommands); it stays "MCE Controller" for back-compat (with
# fallback support in code), even though the product is now branded MCEC.
WriteRegDWORD HKLM "Software\Kindel\MCE Controller" "Telemetry" $0
${If} ${RunningX64}
SetRegView 32
${EndIf}
FunctionEnd
;=========== Telemetry Checkbox END ==========

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${__FILEDIR__}\license.txt"

var ICONS_GROUP
!define MUI_STARTMENUPAGE_NODISABLE
!define MUI_STARTMENUPAGE_DEFAULTFOLDER "MCEC"
!define MUI_STARTMENUPAGE_REGISTRY_ROOT "${PRODUCT_UNINST_ROOT_KEY}"
!define MUI_STARTMENUPAGE_REGISTRY_KEY "${PRODUCT_UNINST_KEY}"
!define MUI_STARTMENUPAGE_REGISTRY_VALUENAME "${PRODUCT_STARTMENU_REGVAL}"
!insertmacro MUI_PAGE_STARTMENU Application $ICONS_GROUP

!insertmacro MUI_PAGE_INSTFILES

!define MUI_FINISHPAGE_RUN "$INSTDIR\mcec.exe"
!define MUI_FINISHPAGE_LINK "MCEC Home Page."
!define MUI_FINISHPAGE_LINK_LOCATION "https://tig.github.io/mcec"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "${OUTFILE}"
InstallDir "$PROGRAMFILES64\Kindel\MCEC"
InstallDirRegKey HKLM "${PRODUCT_DIR_REGKEY}" ""
ShowInstDetails show
ShowUnInstDetails show

Section "MainSection" SEC01
  ; Stop any running instance so files can be overwritten during an upgrade
  nsExec::Exec 'taskkill /F /IM mcec.exe'
  Sleep 500
  SetOutPath "$INSTDIR"
  SetOverwrite on
  ; Recursively install the entire self-contained publish output
  File /r "${PUBLISHDIR}\*.*"
  CreateDirectory "$SMPROGRAMS\$ICONS_GROUP"
  CreateShortCut "$SMPROGRAMS\$ICONS_GROUP\MCEC.lnk" "$INSTDIR\mcec.exe"
  CreateShortCut "$DESKTOP\MCEC.lnk" "$INSTDIR\mcec.exe"
SectionEnd

Section -AdditionalIcons
  CreateShortCut "$SMPROGRAMS\$ICONS_GROUP\Uninstall.lnk" "$INSTDIR\uninst.exe"
SectionEnd

Section -Post
  WriteUninstaller "$INSTDIR\uninst.exe"
  WriteRegStr HKLM "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR\mcec.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninst.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\mcec.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "${PRODUCT_STARTMENU_REGVAL}" "$ICONS_GROUP"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
SectionEnd

Function un.onUninstSuccess
  HideWindow
  MessageBox MB_ICONINFORMATION|MB_OK "$(^Name) was successfully removed from your computer."
FunctionEnd

Function un.onInit
  MessageBox MB_ICONQUESTION|MB_YESNO|MB_DEFBUTTON2 "Are you sure you want to completely remove $(^Name) and all of its components?" IDYES +2
  Abort
FunctionEnd

Section Uninstall
  nsExec::Exec 'taskkill /F /IM mcec.exe'
  Sleep 500
  ReadRegStr $ICONS_GROUP ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "${PRODUCT_STARTMENU_REGVAL}"
  ; Remove the whole self-contained install tree
  RMDir /r "$INSTDIR"
  Delete "$SMPROGRAMS\$ICONS_GROUP\Uninstall.lnk"
  Delete "$DESKTOP\MCEC.lnk"
  Delete "$SMPROGRAMS\$ICONS_GROUP\MCEC.lnk"
  RMDir "$SMPROGRAMS\$ICONS_GROUP"
  DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
  DeleteRegKey HKLM "${PRODUCT_DIR_REGKEY}"
  SetAutoClose true
SectionEnd
