; TaskNet By Ali - NSIS Installer

!define APP_NAME "TaskNet By Ali"
!define APP_VERSION "2.0"
!define APP_EXE "TaskNet.exe"
!define REG_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\TaskNetByAli"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "TaskNet_Setup.exe"
InstallDir "$PROGRAMFILES64\TaskNet By Ali"
InstallDirRegKey HKLM "${REG_KEY}" "InstallDir"
RequestExecutionLevel admin
SetCompressor lzma

!include "MUI2.nsh"

!define MUI_ICON "tasknet.ico"
!define MUI_UNICON "tasknet.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "Welcome to TaskNet By Ali"
!define MUI_WELCOMEPAGE_TEXT "TaskNet By Ali shows live CPU, RAM and Network stats on your taskbar.$\n$\nClick Next to continue."
!define MUI_FINISHPAGE_RUN "$INSTDIR\TaskNet.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch TaskNet By Ali"
!define MUI_FINISHPAGE_TITLE "Installation Complete!"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "LICENSE.txt"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Section "Install"
    SetOutPath "$INSTDIR"
    File "publish\TaskNet.exe"
    File "tasknet.ico"

    CreateShortcut "$DESKTOP\TaskNet By Ali.lnk" "$INSTDIR\TaskNet.exe" "" "$INSTDIR\tasknet.ico"
    CreateDirectory "$SMPROGRAMS\TaskNet By Ali"
    CreateShortcut "$SMPROGRAMS\TaskNet By Ali\TaskNet By Ali.lnk" "$INSTDIR\TaskNet.exe" "" "$INSTDIR\tasknet.ico"
    CreateShortcut "$SMPROGRAMS\TaskNet By Ali\Uninstall.lnk" "$INSTDIR\Uninstall.exe"

    WriteUninstaller "$INSTDIR\Uninstall.exe"

    WriteRegStr   HKLM "${REG_KEY}" "DisplayName"     "TaskNet By Ali"
    WriteRegStr   HKLM "${REG_KEY}" "DisplayVersion"  "2.0"
    WriteRegStr   HKLM "${REG_KEY}" "Publisher"       "Ali"
    WriteRegStr   HKLM "${REG_KEY}" "DisplayIcon"     "$INSTDIR\tasknet.ico"
    WriteRegStr   HKLM "${REG_KEY}" "InstallDir"      "$INSTDIR"
    WriteRegStr   HKLM "${REG_KEY}" "UninstallString" "$INSTDIR\Uninstall.exe"
    WriteRegDWORD HKLM "${REG_KEY}" "NoModify"        1
    WriteRegDWORD HKLM "${REG_KEY}" "NoRepair"        1
SectionEnd

Section "Uninstall"
    ExecWait 'taskkill /f /im TaskNet.exe'
    Delete "$INSTDIR\TaskNet.exe"
    Delete "$INSTDIR\tasknet.ico"
    Delete "$INSTDIR\Uninstall.exe"
    RMDir  "$INSTDIR"
    Delete "$DESKTOP\TaskNet By Ali.lnk"
    Delete "$SMPROGRAMS\TaskNet By Ali\TaskNet By Ali.lnk"
    Delete "$SMPROGRAMS\TaskNet By Ali\Uninstall.lnk"
    RMDir  "$SMPROGRAMS\TaskNet By Ali"
    DeleteRegKey HKLM "${REG_KEY}"
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "TaskNetByAli"
    RMDir /r "$APPDATA\TaskNetByAli"
SectionEnd
