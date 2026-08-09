<div align="center">

<img src="assets/SpeedScanManager_Logo.png" height="128" alt="SpeedScan Manager Logo">

**Private, lokal gehostete Alternative zum Fujitsu ScanSnap Manager — vollständig offline.**

<p>
  <a href="https://github.com/VeridonNetzwerk/SpeedScanManager/blob/main/LICENSE">
    <img src="https://img.shields.io/github/license/VeridonNetzwerk/SpeedScanManager?style=flat-square" alt="License">
  </a>
  <a href="https://github.com/VeridonNetzwerk/SpeedScanManager/issues">
    <img src="https://img.shields.io/github/issues/VeridonNetzwerk/SpeedScanManager?style=flat-square" alt="Open Issues">
  </a>
  <a href="https://github.com/VeridonNetzwerk/SpeedScanManager/stargazers">
    <img src="https://img.shields.io/github/stars/VeridonNetzwerk/SpeedScanManager?style=flat-square" alt="Stars">
  </a>
  <a href="https://veridonnetzwerk.github.io/SpeedScanManager/">
    <img src="https://img.shields.io/badge/website-online-green" alt="Website">
  </a>
  <img src="https://img.shields.io/badge/Windows%2010%2F11-supported-blue" alt="Windows Supported">
  <img src="https://img.shields.io/badge/.NET-8.0-yellow" alt=".NET 8.0">
  <img src="https://img.shields.io/badge/C%23-12-cyan" alt="C# 12">
</p>

</div>

---

## ✨ Features

| Feature | Beschreibung |
|---------|-------------|
| 🔍 **Scanner-Monitoring** | Echtzeit-Überwachung des Scannerstatus via Tray-Icon (verbunden/getrennt) |
| ⚡ **Quick-Menü** | Schnellzugriff auf Presets: Empfohlen, Kleine Datei, Hohe Bildqualität, Benutzerdefiniert |
| 📂 **Speichern-Tab** | Speicherordner-Wahl, Dateiname-Format-Templates (Timestamp, Custom), Post-Scan-Umbenennung |
| 🔧 **Anwendung** | Scan to Folder, Scan to E-Mail, Scan to Print — extensible für eigene Targets |
| 🎨 **Scanmodus** | Bildqualität, Farbmodus, Scan-Seite mit Icons — Plus Optionen (Helligkeit, Deskew, etc.) |
| 📄 **Dateiart** | PDF, JPEG, PNG mit OCR-Integration, Schlüsselwort-Markierung, Zielseiten-Wahl |
| 📏 **Papier** | Papiergrößen-Dropdown (13+ Optionen), Mehrfacheinzugserkennung, Benutzerdefinierte Größen |
| 📊 **Dateigröße** | Komprimierungsrate-Slider mit visueller Keil-Indikation (Niedrig ↔ Hoch) |
| 👤 **Profilverwaltung** | Profiles speichern/laden von Scan-Einstellungen über Dropdown im Quick-Menü |
| 🔒 **100% Lokal** | Keine Cloud, kein Telemetrie, keine Daten verlassen deinen Rechner |

---

## 🛠️ Anforderungen

| Komponente | Version | Anmerkung |
|------------|---------|-----------|
| OS | Windows 10/11 | Mindestens Build 19041 |
| Scanner | Fujitsu fi-Serie (fi-xxx) | TWAIN-kompatibel mit PaperStream IP |
| .NET 8.0 Runtime | x86 | Laufzeit erforderlich — kein Installer, nur entpacken und starten |
| NTwain | 3.7+ | TWAIN-DLL im System-Pfad |

> **Hinweis:** Ein ScanSnap Manager-Installer (wie der offizielle Fujitsu-Setup) ist nicht notwendig. Das Projekt funktioniert rein über den NTwain TWAIN-Treiber.

---

## 🚀 Quick Start

### Vom Quellcode starten

```bash
git clone https://github.com/VeridonNetzwerk/SpeedScanManager.git
cd SpeedScanManager
dotnet restore
dotnet run --project SpeedScanManager
```

Oder als Published-Binary:

1. Entpacke die ZIP aus [Releases](https://github.com/VeridonNetzwerk/SpeedScanManager/releases)
2. Starte `SpeedScanManager.exe` — das Tray-Icon erscheint in der Taskleiste
3. Scanner verbinden, Quick-Menü nutzen oder über Einstellungen expandieren

### Publish (lokaler Build)

```bash
cd SpeedScanManager
dotnet publish -c Release -r win-x86 --self-contained true -o dist
```

Output: `dist\SpeedScanManager.exe` — standalone, kein .NET Runtime-Download nötig.

---

## 🖼️ Screenshots & Website

<div align="center">

### Quick-Menü (eingeklappt)

| Quick-Menü | Einstellungen (ausgeklappt) |
|------------|---------------------------|
| Presets + Detail/OK/Schnellzugriff | 6 Tabs: Anwendung, Speichern, Scanmodus, Dateiart, Papier, Dateigröße |

### Kontextmenü im Tray

- Duplex-Scan / Simplex-Scan / Flachbettscannen
- Einstellungen der SCAN Taste...
- Profilverwaltung
- Hilfe → Hilfethemen / Versionsinformationen / Präferenzen

</div>

---

## 🏗️ Architektur

```
SpeedScanManager/
├── Program.cs                      # Entry point + global exception handler
├── SpeedScanManager.csproj         # Project file (.NET 8, x86, WinForms)
├── Core/                           # Scan engine, settings, profiles, OCR
│   ├── ScanPipeline.cs             # TWAIN scan execution (image acquisition)
│   ├── ScanOutputProcessor.cs      # Image → PDF/JPEG conversion + OCR
│   ├── ScanSettings.cs             # Central settings data class
│   ├── ScanProfile.cs              # Profile model
│   ├── ProfileManager.cs           # Profile collection + JSON persistence
│   ├── ScannerStateService.cs      # TWAIN scanner state cache
│   └── OcrProcessor.cs             # OCR via Tesseract
├── UI/                             # Forms, dialogs, tab content
│   ├── MainForm.cs                 # Main window (quick-menu + settings)
│   ├── TrayApplicationContext.cs   # Tray icon, scanner monitoring, context menu
│   ├── PreferencesDialog.cs        # Preferences dialog
│   ├── VersionInfoDialog.cs        # Version info window
│   ├── ScannerDriverInfoDialog.cs  # Scanner/driver info sub-dialog
│   ├── HelpForm.cs / HelpContent.cs
│   ├── ProfileManagementDialog.cs
│   ├── PdfOptionsDialog.cs
│   ├── FileNameFormatDialog.cs
│   ├── ApplicationManageDialog.cs
│   ├── CarrierSheetDialog.cs
│   ├── CustomSizeDialog.cs
│   ├── ScanModeOptionsDialog.cs
│   ├── ScanToEmailOptionsDialog.cs
│   ├── ScanToPrintOptionsDialog.cs
│   └── Tabs/                       # Tab content user controls
│       ├── SaveTabContent.cs
│       ├── ApplicationTabContent.cs
│       ├── ScanModeTabContent.cs
│       ├── FileTypeTabContent.cs
│       ├── PaperTabContent.cs
│       └── FileSizeTabContent.cs
├── Helpers/                        # Icons, mail, print, resources
│   ├── AppResources.cs             # Embedded resource loader (logo, text)
│   ├── TrayIcons.cs                # Tray icon generation from logo
│   ├── TabIcons.cs                 # Tab/combobox icon generation
│   ├── FlagIcons.cs                # Language flag icons
│   ├── MailHelper.cs               # E-mail via MAPI
│   └── PrintHelper.cs              # Print integration
├── assets/                         # Logo and wordmark images
└── build/                          # Build scripts and manifest
    ├── AppManifest.xml
    └── Create-SigningCert.ps1
```

### Tech Stack

| Layer | Technologie |
|-------|-----------|
| UI | WinForms .NET 8.0 Windows Forms |
| C# | C# 12 mit nullable & implicit usings |
| Scanner | [NTwain](https://github.com/matthewjberger/NTwain) (TWAIN DSM wrapper) |
| PDF | [PdfSharpCore](https://github.com/stickles/PdfSharpCore) (PDF-Erstellung) |
| OCR | [Tesseract](https://github.com/tesseract-ocr/tesseract) .NET wrapper |

---

## ⚙️ Konfiguration & Speicherorte

Die Anwendung speichert alle Einstellungen im Anwendungsverzeichnis. Keine Registry-Nutzung.

### Profile-Speicherort

| Pfad | Inhalt |
|------|--------|
| `%USERPROFILE%\SpeedScanManager\profiles.xml` | Benutzerdefinierte Scan-Profile |
| `%USERPROFILE%\Documents\SpeedScanManager\` | Default-Speicherordner für Scans |

---

## 🔨 Build & Publish

### Lokaler Build (mit Runtime)

```bash
cd SpeedScanManager
dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -o dist
```

**Ausgabe:** `dist\SpeedScanManager.exe` — vollständig standalone, ~50–70 MB.

### Ohne Self-Contained (kleiner, braucht .NET Runtime)

```bash
dotnet publish -c Release -r win-x86 -p:PublishSingleFile=true -o dist
```

**Ausgabe:** `dist\SpeedScanManager.exe` — ~15–25 MB, benötigt .NET 8.0 Runtime auf Ziel-System.

---

## 🐛 Meldungen

Einen Bug gefunden? Eröffne ein [**Issue**](https://github.com/VeridonNetzwerk/SpeedScanManager/issues/new) mit:

- Scannermodell (z.B. fi-xxx)
- Windows-Version
- TWAIN-Treiber (PaperStream IP Version)
- Screenshots / Fehlermeldungen
- Was du erwartetest vs. was passierte

---

## 💖 Support

Gefällt dir das Projekt? Gib ihm einen ⭐️ oder [eröffne ein Issue](https://github.com/VeridonNetzwerk/SpeedScanManager/issues/new)!

---

## 🙏 Credits & Dependencies

SpeedScan Manager basiert auf diesen großartigen Open-Source-Projekten:

| Projekt | Rolle |
|---------|-------|
| [NTwain](https://github.com/matthewjberger/NTwain) | TWAIN DSM wrapper für Scanner-Kommunikation |
| [PdfSharpCore](https://github.com/stickles/PdfSharpCore) | PDF-Erstellung und -Manipulation |
| [Tesseract OCR](https://github.com/tesseract-ocr/tesseract) | Texterkennung für durchsuchbare PDFs |

### 🤖 Built With AI

Teile dieses Projekts wurden mit der Unterstützung von KI-Tools erstellt und verfeinert.

---

<div align="center">
  <sub>© 2026 VeridonNetzwerk</sub>
</div>
