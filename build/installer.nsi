; ============================================================================
; SpeedScan Manager - NSIS Installer Script
; Fujitsu ScanSnap-style custom UI with dark sidebar + light content area
; ============================================================================
!include "MUI2.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "WinMessages.nsh"

; === Product Information ===================================================
!define PRODUCT_NAME        "SpeedScan Manager"
!define PRODUCT_PUBLISHER   "VeridonNetzwerk"
!define PRODUCT_VERSION     "1.0.0.0"
!define PRODUCT_URL         "https://github.com/VeridonNetzwerk/SpeedScanManager"
!define DISCORD_URL         "https://discord.gg/P2RQNYjWbp"
!define README_URL          "https://github.com/VeridonNetzwerk/SpeedScanManager/blob/main/README.md"

; === Sidebar / Content Colors ==============================================
!define CLR_SIDEBAR         0x2D2D2D  ; dark grey
!define CLR_CONTENT         0xF0F0F0  ; light grey
!define CLR_WHITE           0xFFFFFF
!define CLR_TEXT_DARK       0x333333
!define CLR_TEXT_MED        0x555555
!define CLR_TEXT_LIGHT      0xAAAAAA
!define CLR_LINK            0x0066CC
!define CLR_GREEN           0x2E7D32

; === Build Configuration ===================================================
Name "${PRODUCT_NAME}"
OutFile "..\dist\SpeedScanManager_Setup.exe"
InstallDir "$PROGRAMFILES32\SpeedScanManager"
InstallDirRegKey HKLM "Software\${PRODUCT_PUBLISHER}\SpeedScanManager" "InstallDir"
ShowInstDetails hide
RequestExecutionLevel admin
SetCompressor /SOLID lzma
Unicode True

; === Installer Icon ==========================================================
Icon "..\assets\SpeedScanManager.ico"

; === VersionInfo ============================================================
VIAddVersionKey "ProductName" "${PRODUCT_NAME}"
VIAddVersionKey "CompanyName" "${PRODUCT_PUBLISHER}"
VIAddVersionKey "FileDescription" "${PRODUCT_NAME} Installer"
VIAddVersionKey "LegalCopyright" "Copyright (c) 2026 ${PRODUCT_PUBLISHER}"
VIAddVersionKey "FileVersion" "${PRODUCT_VERSION}"
VIProductVersion "${PRODUCT_VERSION}"

; === Globals ================================================================
Var hSidebarBtnInstall
Var hSidebarBtnReadme
Var hSidebarBtnDiscord
Var hSidebarBtnExit
Var hContentDirEdit
Var hContentBrowseBtn
Var hContentInstallBtn
Var hContentProgress
Var hContentProgressText
Var hGithubIcon
Var hDiscordIcon
Var hLaunchCheckbox
Var bInstalling

; ============================================================================
; Page Flow
; ============================================================================
Page custom PageWelcome PageWelcomeLeave
Page InstFiles
Page custom PageSuccess PageSuccessLeave

; ============================================================================
; .onInit
; ============================================================================
Function .onInit
    StrCpy $bInstalling 0
FunctionEnd

; ============================================================================
; Helper: Paint sidebar background
; Creates a full-height Label with dark background to simulate a panel.
; ============================================================================
!macro SIDEBAR_BACKGROUND HEIGHT
    ${NSD_CreateLabel} 0 0 180u ${HEIGHT} ""
    Pop $0
    SetCtlColors $0 ${CLR_WHITE} ${CLR_SIDEBAR}
!macroend

; ============================================================================
; Helper: Paint sidebar logo
; ============================================================================
!macro SIDEBAR_LOGO
    ; Logo bitmap (if available)
    ${If} ${FileExists} "$EXEDIR\assets\installer\SpeedScanManager_Logo.bmp"
        ${NSD_CreateBitmap} 36u 10u 108u 60u ""
        Pop $0
        ${NSD_SetImage} $0 "$EXEDIR\assets\installer\SpeedScanManager_Logo.bmp" $6
    ${EndIf}

    ${NSD_CreateLabel} 18u 76u 144u 28u "SpeedScan"
    Pop $0
    SetCtlColors $0 ${CLR_WHITE} ${CLR_SIDEBAR}
    CreateFont $1 "Segoe UI" 16 700
    SendMessage $0 ${WM_SETFONT} $1 0

    ${NSD_CreateLabel} 18u 102u 144u 18u "Manager"
    Pop $0
    SetCtlColors $0 ${CLR_TEXT_LIGHT} ${CLR_SIDEBAR}
    CreateFont $1 "Segoe UI" 10 400
    SendMessage $0 ${WM_SETFONT} $1 0
!macroend

; ============================================================================
; Helper: Hide standard NSIS navigation buttons
; ============================================================================
!macro HIDE_NAV_BUTTONS
    GetDlgItem $0 $HWNDPARENT 1  ; Next button
    ShowWindow $0 ${SW_HIDE}
    GetDlgItem $0 $HWNDPARENT 2  ; Back button
    ShowWindow $0 ${SW_HIDE}
    GetDlgItem $0 $HWNDPARENT 3  ; Cancel button
    ShowWindow $0 ${SW_HIDE}
!macroend

; ============================================================================
; Welcome / Custom Fujitsu-Style Page
; ============================================================================
Function PageWelcome
    nsDialogs::Create 1018
    Pop $0

    ; Set overall dialog background
    SetCtlColors $HWNDPARENT ${CLR_TEXT_DARK} ${CLR_CONTENT}

    ; === Left Sidebar (dark background) ====================================
    !insertmacro SIDEBAR_BACKGROUND "300u"
    !insertmacro SIDEBAR_LOGO

    ; === Sidebar Buttons ====================================================

    ; 1. Install Button
    ${NSD_CreateButton} 12u 130u 156u 36u "SpeedScan Manager installieren"
    Pop $hSidebarBtnInstall
    CreateFont $1 "Segoe UI" 9 700
    SendMessage $hSidebarBtnInstall ${WM_SETFONT} $1 0
    ${NSD_OnClick} $hSidebarBtnInstall SidebarInstallClick

    ; 2. README Button
    ${NSD_CreateButton} 12u 174u 156u 28u "README anzeigen"
    Pop $hSidebarBtnReadme
    CreateFont $1 "Segoe UI" 9 400
    SendMessage $hSidebarBtnReadme ${WM_SETFONT} $1 0
    ${NSD_OnClick} $hSidebarBtnReadme SidebarReadmeClick

    ; 3. Discord/Support Button
    ${NSD_CreateButton} 12u 210u 156u 28u "Support (Discord)"
    Pop $hSidebarBtnDiscord
    SendMessage $hSidebarBtnDiscord ${WM_SETFONT} $1 0
    ${NSD_OnClick} $hSidebarBtnDiscord SidebarDiscordClick

    ; 4. Exit Button (bottom of sidebar)
    ${NSD_CreateButton} 12u 272u 156u 28u "Beenden"
    Pop $hSidebarBtnExit
    SendMessage $hSidebarBtnExit ${WM_SETFONT} $1 0
    ${NSD_OnClick} $hSidebarBtnExit SidebarExitClick

    ; === Right Content Area (light) ========================================

    ; Welcome heading
    ${NSD_CreateLabel} 200u 24u 560u 24u "Willkommen beim SpeedScan Manager Setup."
    Pop $0
    SetCtlColors $0 ${CLR_TEXT_DARK} ${CLR_CONTENT}
    CreateFont $1 "Segoe UI" 12 700
    SendMessage $0 ${WM_SETFONT} $1 0

    ; Description text
    ${NSD_CreateLabel} 200u 56u 560u 40u "SpeedScan Manager ist eine modale, privat gehostete Alternative zum Fujitsu ScanSnap Manager. Open-Source TWAIN-Scanning-Software fuer Windows 10/11."
    Pop $0
    SetCtlColors $0 ${CLR_TEXT_MED} ${CLR_CONTENT}
    CreateFont $1 "Segoe UI" 9 400
    SendMessage $0 ${WM_SETFONT} $1 0

    ; Components header
    ${NSD_CreateLabel} 200u 108u 560u 18u "Installierbare Komponenten:"
    Pop $0
    SetCtlColors $0 ${CLR_TEXT_DARK} ${CLR_CONTENT}
    CreateFont $1 "Segoe UI" 9 700
    SendMessage $0 ${WM_SETFONT} $1 0

    ; Component list
    ${NSD_CreateLabel} 210u 130u 540u 18u "-  SpeedScan Manager (Core Application)"
    Pop $0
    SetCtlColors $0 0x444444 ${CLR_CONTENT}
    CreateFont $1 "Segoe UI" 9 400
    SendMessage $0 ${WM_SETFONT} $1 0

    ${NSD_CreateLabel} 210u 150u 540u 18u "-  SpeedScan Manager Quick-Menue und Tray Applet"
    Pop $0
    SetCtlColors $0 0x444444 ${CLR_CONTENT}
    SendMessage $0 ${WM_SETFONT} $1 0

    ; Installation path header
    ${NSD_CreateLabel} 200u 186u 560u 18u "Installationspfad:"
    Pop $0
    SetCtlColors $0 ${CLR_TEXT_DARK} ${CLR_CONTENT}
    CreateFont $1 "Segoe UI" 9 700
    SendMessage $0 ${WM_SETFONT} $1 0

    ; Path edit field
    ${NSD_CreateText} 210u 210u 380u 22u "$INSTDIR"
    Pop $hContentDirEdit
    SetCtlColors $hContentDirEdit 0x000000 ${CLR_WHITE}
    CreateFont $1 "Segoe UI" 9 400
    SendMessage $hContentDirEdit ${WM_SETFONT} $1 0

    ; Browse button
    ${NSD_CreateButton} 600u 210u 75u 22u "Durchsuchen..."
    Pop $hContentBrowseBtn
    SendMessage $hContentBrowseBtn ${WM_SETFONT} $1 0
    ${NSD_OnClick} $hContentBrowseBtn ContentBrowseClick

    ; Install button (right panel)
    ${NSD_CreateButton} 530u 248u 145u 28u "Installieren"
    Pop $hContentInstallBtn
    CreateFont $1 "Segoe UI" 9 700
    SendMessage $hContentInstallBtn ${WM_SETFONT} $1 0
    ${NSD_OnClick} $hContentInstallBtn ContentInstallClick

    ; Progress bar (hidden initially)
    ${NSD_CreateProgressBar} 210u 248u 300u 18u ""
    Pop $hContentProgress
    ShowWindow $hContentProgress ${SW_HIDE}

    ; Progress text (hidden initially)
    ${NSD_CreateLabel} 210u 274u 400u 18u ""
    Pop $hContentProgressText
    SetCtlColors $hContentProgressText ${CLR_TEXT_MED} ${CLR_CONTENT}
    ShowWindow $hContentProgressText ${SW_HIDE}

    ; === Bottom-right: GitHub + Discord icons ==============================
    ; GitHub icon
    ${NSD_CreateBitmap} 680u 290u 28u 28u ""
    Pop $hGithubIcon
    ${If} ${FileExists} "$EXEDIR\assets\installer\github_logo.bmp"
        ${NSD_SetImage} $hGithubIcon "$EXEDIR\assets\installer\github_logo.bmp" $2
    ${EndIf}

    ; Discord icon
    ${NSD_CreateBitmap} 648u 290u 28u 28u ""
    Pop $hDiscordIcon
    ${If} ${FileExists} "$EXEDIR\assets\installer\discord_logo.bmp"
        ${NSD_SetImage} $hDiscordIcon "$EXEDIR\assets\installer\discord_logo.bmp" $3
    ${EndIf}

    ; Hide standard NSIS navigation buttons
    !insertmacro HIDE_NAV_BUTTONS

    nsDialogs::Show
FunctionEnd

; ============================================================================
; Welcome Page Leave
; ============================================================================
Function PageWelcomeLeave
FunctionEnd

; ============================================================================
; Sidebar Button Handlers
; ============================================================================
Function SidebarInstallClick
    ; Read the path from the edit control
    ${NSD_GetText} $hContentDirEdit $0
    StrCpy $INSTDIR $0

    ; Show progress UI
    ShowWindow $hContentInstallBtn ${SW_HIDE}
    ShowWindow $hContentProgress ${SW_SHOW}
    ShowWindow $hContentProgressText ${SW_SHOW}
    ${NSD_SetText} $hContentProgressText "Dateien werden kopiert..."

    StrCpy $bInstalling 1

    ; Trigger the hidden Next button to advance to InstFiles page
    GetDlgItem $0 $HWNDPARENT 1
    SendMessage $0 ${BM_CLICK} 0 0
FunctionEnd

Function SidebarReadmeClick
    ExecShell "open" "${README_URL}"
FunctionEnd

Function SidebarDiscordClick
    ExecShell "open" "${DISCORD_URL}"
FunctionEnd

Function SidebarExitClick
    Quit
FunctionEnd

; ============================================================================
; Content Area Button Handlers
; ============================================================================
Function ContentBrowseClick
    nsDialogs::SelectFolderDialog "Installationsordner waehlen" $INSTDIR
    Pop $0
    ${If} $0 != "error"
        StrCpy $INSTDIR $0
        ${NSD_SetText} $hContentDirEdit $INSTDIR
    ${EndIf}
FunctionEnd

Function ContentInstallClick
    ; Same as sidebar install
    Call SidebarInstallClick
FunctionEnd

; ============================================================================
; Success Page
; ============================================================================
Function PageSuccess
    nsDialogs::Create 1018
    Pop $0

    SetCtlColors $HWNDPARENT ${CLR_TEXT_DARK} ${CLR_CONTENT}

    ; === Left Sidebar (dark) ================================================
    !insertmacro SIDEBAR_BACKGROUND "300u"
    !insertmacro SIDEBAR_LOGO

    ; === Right Content Area ================================================
    ${NSD_CreateLabel} 200u 40u 560u 28u "Installation erfolgreich!"
    Pop $0
    SetCtlColors $0 ${CLR_GREEN} ${CLR_CONTENT}
    CreateFont $1 "Segoe UI" 14 700
    SendMessage $0 ${WM_SETFONT} $1 0

    ${NSD_CreateLabel} 200u 78u 560u 40u "SpeedScan Manager wurde erfolgreich auf Ihrem Computer installiert.$\r$\n$\r$\nSie koennen SpeedScan Manager jetzt ueber das Startmenue oder das Desktop-Symbol starten."
    Pop $0
    SetCtlColors $0 ${CLR_TEXT_MED} ${CLR_CONTENT}
    CreateFont $1 "Segoe UI" 9 400
    SendMessage $0 ${WM_SETFONT} $1 0

    ; Launch checkbox
    ${NSD_CreateCheckbox} 210u 140u 300u 20u "SpeedScan Manager jetzt starten"
    Pop $hLaunchCheckbox
    SetCtlColors $hLaunchCheckbox ${CLR_TEXT_DARK} ${CLR_CONTENT}
    SendMessage $hLaunchCheckbox ${WM_SETFONT} $1 0

    ; Links section
    ${NSD_CreateLabel} 200u 180u 560u 18u "Links:"
    Pop $0
    SetCtlColors $0 ${CLR_TEXT_DARK} ${CLR_CONTENT}
    CreateFont $1 "Segoe UI" 9 700
    SendMessage $0 ${WM_SETFONT} $1 0

    ; GitHub link
    ${NSD_CreateLink} 210u 204u 300u 18u "GitHub Repository"
    Pop $0
    SetCtlColors $0 ${CLR_LINK} ${CLR_CONTENT}
    ${NSD_OnClick} $0 SuccessGithubClick

    ; Discord link
    ${NSD_CreateLink} 210u 226u 300u 18u "Discord Support"
    Pop $0
    SetCtlColors $0 ${CLR_LINK} ${CLR_CONTENT}
    ${NSD_OnClick} $0 SuccessDiscordClick

    ; GitHub + Discord icons (bottom-right)
    ${If} ${FileExists} "$EXEDIR\assets\installer\github_logo.bmp"
        ${NSD_CreateBitmap} 680u 290u 28u 28u ""
        Pop $0
        ${NSD_SetImage} $0 "$EXEDIR\assets\installer\github_logo.bmp" $2
    ${EndIf}

    ${If} ${FileExists} "$EXEDIR\assets\installer\discord_logo.bmp"
        ${NSD_CreateBitmap} 648u 290u 28u 28u ""
        Pop $0
        ${NSD_SetImage} $0 "$EXEDIR\assets\installer\discord_logo.bmp" $3
    ${EndIf}

    ; Finish button
    ${NSD_CreateButton} 530u 278u 145u 28u "Fertig"
    Pop $0
    CreateFont $1 "Segoe UI" 9 700
    SendMessage $0 ${WM_SETFONT} $1 0
    ${NSD_OnClick} $0 SuccessFinishClick

    ; Hide standard NSIS buttons
    !insertmacro HIDE_NAV_BUTTONS

    nsDialogs::Show
FunctionEnd

Function PageSuccessLeave
FunctionEnd

Function SuccessGithubClick
    Pop $0
    ExecShell "open" "${PRODUCT_URL}"
    Abort
FunctionEnd

Function SuccessDiscordClick
    Pop $0
    ExecShell "open" "${DISCORD_URL}"
    Abort
FunctionEnd

Function SuccessFinishClick
    ; Check if launch checkbox is checked
    ${NSD_GetState} $hLaunchCheckbox $0
    ${If} $0 == ${BST_CHECKED}
        ExecShell "open" "$INSTDIR\SpeedScanManager.exe"
    ${EndIf}
    Quit
FunctionEnd

; ============================================================================
; Installation Section
; ============================================================================
Section "Install" SecInstall
    SetOutPath "$INSTDIR"

    ; === Core application files ============================================
    File "..\dist\SpeedScanManager.exe"

    ; === Native dependencies ===============================================
    File "..\dist\D3DCompiler_47_cor3.dll"
    File "..\dist\PenImc_cor3.dll"
    File "..\dist\PresentationNative_cor3.dll"
    File "..\dist\vcruntime140_cor3.dll"
    File "..\dist\wpfgfx_cor3.dll"

    ; === Tesseract OCR native libs (x86) ===================================
    SetOutPath "$INSTDIR\x86"
    File "..\dist\x86\leptonica-1.82.0.dll"
    File "..\dist\x86\tesseract50.dll"

    ; === Tesseract OCR native libs (x64) ===================================
    SetOutPath "$INSTDIR\x64"
    File "..\dist\x64\leptonica-1.82.0.dll"
    File "..\dist\x64\tesseract50.dll"

    ; === Back to install root ==============================================
    SetOutPath "$INSTDIR"

    ; === Registry entries ==================================================
    WriteRegStr HKLM "Software\${PRODUCT_PUBLISHER}\SpeedScanManager" "InstallDir" "$INSTDIR"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SpeedScanManager" "DisplayName" "${PRODUCT_NAME}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SpeedScanManager" "UninstallString" '"$INSTDIR\uninstall.exe"'
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SpeedScanManager" "DisplayIcon" '"$INSTDIR\SpeedScanManager.exe"'
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SpeedScanManager" "Publisher" "${PRODUCT_PUBLISHER}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SpeedScanManager" "DisplayVersion" "${PRODUCT_VERSION}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SpeedScanManager" "URLInfoAbout" "${PRODUCT_URL}"
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SpeedScanManager" "NoModify" 1
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SpeedScanManager" "NoRepair" 1

    ; === Calculate install size for Add/Remove Programs ====================
    ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
    IntFmt $0 "0x%08X" $0
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SpeedScanManager" "EstimatedSize" "$0"

    ; === Shortcuts =========================================================
    CreateDirectory "$SMPROGRAMS\${PRODUCT_NAME}"
    CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\${PRODUCT_NAME}.lnk" "$INSTDIR\SpeedScanManager.exe" "" "$INSTDIR\SpeedScanManager.exe" 0
    CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\Deinstallieren.lnk" "$INSTDIR\uninstall.exe" "" "$INSTDIR\uninstall.exe" 0

    ; Desktop shortcut
    CreateShortCut "$DESKTOP\${PRODUCT_NAME}.lnk" "$INSTDIR\SpeedScanManager.exe" "" "$INSTDIR\SpeedScanManager.exe" 0

    ; === Uninstaller =======================================================
    WriteUninstaller "$INSTDIR\uninstall.exe"
SectionEnd

; ============================================================================
; Uninstaller Section
; ============================================================================
Section "Uninstall"
    ; === Remove files ======================================================
    Delete "$INSTDIR\SpeedScanManager.exe"
    Delete "$INSTDIR\D3DCompiler_47_cor3.dll"
    Delete "$INSTDIR\PenImc_cor3.dll"
    Delete "$INSTDIR\PresentationNative_cor3.dll"
    Delete "$INSTDIR\vcruntime140_cor3.dll"
    Delete "$INSTDIR\wpfgfx_cor3.dll"
    Delete "$INSTDIR\uninstall.exe"
    Delete "$INSTDIR\SpeedScanManager.pdb"

    ; Tesseract native libs
    Delete "$INSTDIR\x86\leptonica-1.82.0.dll"
    Delete "$INSTDIR\x86\tesseract50.dll"
    Delete "$INSTDIR\x64\leptonica-1.82.0.dll"
    Delete "$INSTDIR\x64\tesseract50.dll"
    RMDir "$INSTDIR\x86"
    RMDir "$INSTDIR\x64"

    ; === Remove shortcuts ==================================================
    Delete "$SMPROGRAMS\${PRODUCT_NAME}\${PRODUCT_NAME}.lnk"
    Delete "$SMPROGRAMS\${PRODUCT_NAME}\Deinstallieren.lnk"
    RMDir "$SMPROGRAMS\${PRODUCT_NAME}"
    Delete "$DESKTOP\${PRODUCT_NAME}.lnk"

    ; === Remove registry entries ==========================================
    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SpeedScanManager"
    DeleteRegKey HKLM "Software\${PRODUCT_PUBLISHER}\SpeedScanManager"

    ; === Remove install directory (if empty) ==============================
    RMDir "$INSTDIR"
SectionEnd
