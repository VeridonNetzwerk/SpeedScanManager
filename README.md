<div align="center">

<img src="assets/SpeedScanManager_Logo.png" height="100" alt="SpeedScan Manager Logo">

# SpeedScan Manager

Open-Source TWAIN-Scanning-Software für Fujitsu fi-Series Scanner.

<p>
  <a href="https://github.com/VeridonNetzwerk/SpeedScanManager/blob/main/LICENSE"><img src="https://img.shields.io/github/license/VeridonNetzwerk/SpeedScanManager?style=flat-square" alt="License"></a>
  <a href="https://github.com/VeridonNetzwerk/SpeedScanManager/releases"><img src="https://img.shields.io/github/v/release/VeridonNetzwerk/SpeedScanManager?style=flat-square" alt="Release"></a>
  <img src="https://img.shields.io/badge/.NET-8.0-yellow" alt=".NET 8">
  <img src="https://img.shields.io/badge/Windows-10%2F11-blue" alt="Windows">
</p>

</div>

---

## Features

- **Tray-Icon** mit Live-Scanner-Status (verbunden / getrennt)
- **Quick-Menü** mit Presets: Empfohlen, Kleine Datei, Hohe Qualität
- **6 Einstellungs-Tabs**: Anwendung, Speichern, Scanmodus, Dateiart, Papier, Dateigröße
- **PDF / JPEG / PNG** Ausgabe mit OCR (Tesseract) für durchsuchbare PDFs
- **Profile** speichern und laden
- **Scan to Folder / E-Mail / Print**

## Anforderungen

- Windows 10/11
- .NET 8.0 Runtime (x86) — oder self-contained Build
- TWAIN-kompatibler Scanner (getestet mit Fujitsu fi-Series)

## Installation

### Release herunterladen

1. ZIP von [Releases](https://github.com/VeridonNetzwerk/SpeedScanManager/releases) entpacken
2. `SpeedScanManager.exe` starten — Tray-Icon erscheint

### Selber bauen

```bash
git clone https://github.com/VeridonNetzwerk/SpeedScanManager.git
cd SpeedScanManager
dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -o dist
```

## Tech Stack

| Komponente | Technologie |
|------------|-------------|
| UI | WinForms, .NET 8 |
| Scanner | [NTwain](https://github.com/soukoku/NTwain) |
| PDF | [PdfSharpCore](https://github.com/fmenounos/PdfSharpCore) |
| OCR | [Tesseract](https://github.com/tesseract-ocr/tesseract) |

## Bug melden

[Issue erstellen](https://github.com/VeridonNetzwerk/SpeedScanManager/issues/new) mit Scannermodell, Windows-Version und Fehlerbeschreibung.

---

<div align="center">
  <sub>© 2026 VeridonNetzwerk · MIT License</sub>
</div>
