!include "MUI2.nsh"
!include "LogicLib.nsh"

!define APP_NAME "TaskNet"
!define APP_VERSION "4.5.0"

Name "${APP_NAME}"
OutFile "TaskNet_Setup.exe"
InstallDir "$PROGRAMFILES64\${APP_NAME}"

RequestExecutionLevel admin

; UI Settings
!define MUI_ABORTWARNING
!define MUI_ICON "tasknet.ico"
!define MUI_UNICON "tasknet.ico"

; Pages
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

Section "Install"
  SetOutPath "$INSTDIR"
  
  ; Kill process if running
  nsExec::Exec 'taskkill /F /IM TaskNet.exe'
  Sleep 500
  
  ; Copy ALL files from publish folder (self-contained needs all runtime files)
  File /r "publish\*.*"
  File "tasknet.ico"
  
  CreateShortcut "$SMPROGRAMS\${APP_NAME}.lnk" "$INSTDIR\TaskNet.exe" "" "$INSTDIR\tasknet.ico"
  
  ; Write uninstaller
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  
  ; Write registry keys
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\TaskNetByAli" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\TaskNetByAli" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\TaskNetByAli" "DisplayIcon" "$INSTDIR\tasknet.ico"
SectionEnd

Section "Desktop Shortcut"
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\TaskNet.exe" "" "$INSTDIR\tasknet.ico"
SectionEnd

Section "Uninstall"
  nsExec::Exec 'taskkill /F /IM TaskNet.exe'
  Sleep 500
  
  Delete "$INSTDIR\TaskNet.exe"
  Delete "$INSTDIR\tasknet.ico"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  
  Delete "$SMPROGRAMS\${APP_NAME}.lnk"
  Delete "$DESKTOP\${APP_NAME}.lnk"
  
  ; Clean config
  RMDir /r "$APPDATA\TaskNetByAli"
  
  ; Remove startup registry
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "TaskNetByAli"
  
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\TaskNetByAli"
SectionEnd
