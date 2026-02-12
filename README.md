<div align="center">

<img src="DefinitionExtension/Assets/StoreLogo.png" alt="Definition for Command Palette" width="120"/>

# Definition for Command Palette

**Instant dictionary lookups right from [PowerToys](https://github.com/microsoft/PowerToys) Command Palette**

[![Latest Release](https://img.shields.io/github/v/release/ruslanlap/CmdPal-Definition?style=flat-square&color=blue)](https://github.com/ruslanlap/CmdPal-Definition/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/ruslanlap/CmdPal-Definition/total?style=flat-square&color=green)](https://github.com/ruslanlap/CmdPal-Definition/releases)
[![License](https://img.shields.io/github/license/ruslanlap/CmdPal-Definition?style=flat-square)](LICENSE)
[![Microsoft Store](https://img.shields.io/badge/Microsoft_Store-available-blue?style=flat-square&logo=microsoft)](https://apps.microsoft.com/detail/9NMJ8S70L69M)

<br/>

<a href="https://apps.microsoft.com/detail/9NMJ8S70L69M?referrer=appbadge&mode=direct">
  <img src="https://get.microsoft.com/images/en-us%20dark.svg" width="200" alt="Get it from Microsoft Store"/>
</a>

</div>

<br/>

<p align="center">
  <img src="assets/screen 1.png" width="80%" alt="Definition lookup for 'hello'"/>
</p>

---

## Overview

**Definition** is a [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette) extension that gives you instant access to word definitions, phonetics, synonyms, antonyms, and usage examples — without ever leaving your keyboard.

Type a word, get results. That simple.

## Features

| Feature | Description |
|---------|-------------|
| **Instant Definitions** | Definitions from the [Free Dictionary API](https://dictionaryapi.dev/) grouped by part of speech |
| **11 Languages** | English, Spanish, French, German, Italian, Portuguese, Japanese, Korean, Turkish, Arabic, Hindi |
| **Phonetics** | IPA transcriptions displayed alongside each word |
| **Synonyms & Antonyms** | Related words listed per part of speech |
| **Usage Examples** | Real-world example sentences |
| **Copy to Clipboard** | Click any result to copy |
| **Wiktionary Integration** | Right-click context menu to open in Wiktionary |
| **Configurable** | Settings for language, result count, and display options |
| **Smart Caching** | In-memory cache for instant repeat lookups |
| **Debounced Search** | 300ms debounce to minimize API calls while typing |

## Screenshots

<p align="center">
  <img src="assets/screen 2.png" width="80%" alt="Definition lookup"/>
  <br/><br/>
  <img src="assets/screen 3.png" width="80%" alt="Multiple parts of speech"/>
</p>

## Installation

### Prerequisites

- **Windows 10** (build 19041+) or **Windows 11**
- **[Microsoft PowerToys](https://github.com/microsoft/PowerToys)** v0.70.0+ with Command Palette enabled

### Option 1 — WinGet (Recommended)

```powershell
   winget install "Definition for Command Palette"
```

### Option 2 — Microsoft Store

<a href="https://apps.microsoft.com/detail/9NMJ8S70L69M">
  <img src="https://get.microsoft.com/images/en-us%20dark.svg" width="160" alt="Microsoft Store"/>
</a>

### Option 3 — Manual Download

Download the latest `.msixbundle` from [Releases](https://github.com/ruslanlap/CmdPal-Definition/releases/latest), then:

```powershell
Add-AppPackage -Path DefinitionForCommandPalette.msixbundle
```

> After installing, **restart PowerToys** for the extension to appear.

### Updating

```powershell
winget upgrade"Definition for Command Palette"
```

### Uninstalling

```powershell
winget uninstall "Definition for Command Palette"
```

Or remove via **Settings → Apps → Installed apps**.

## Usage

1. Open **Command Palette** (`Win + Ctrl + T` by default)
2. Select **Definition**
3. Type any word
4. Browse definitions, examples, synonyms, and antonyms
5. Press **Enter** to copy — or right-click for more actions

## Building from Source

### Requirements

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) with:
  - .NET desktop development workload
  - Windows App SDK / WinUI 3 tooling
- **Developer Mode** enabled (Settings → Update & Security → For developers)

### Build & Deploy

```bash
git clone https://github.com/ruslanlap/CmdPal-Definition.git
cd CmdPal-Definition
```

**Visual Studio:** Open `CmdPal-Definition.sln`, select your platform (`x64` / `ARM64`), right-click the project → **Deploy**.

**Command line:**

```bash
# x64
dotnet build CmdPal-Definition.sln -c Release -p:Platform=x64

# ARM64
dotnet build CmdPal-Definition.sln -c Release -p:Platform=ARM64
```

Then install the generated `.msix` from the build output:

```powershell
Add-AppPackage -Path "path\to\DefinitionExtension.msix"
```

Restart PowerToys after installation.

## Architecture

```
CmdPal-Definition/
├── CmdPal-Definition.sln
└── DefinitionExtension/
    ├── DefinitionExtension.cs          # IExtension entry point (COM server)
    ├── DefinitionCommandsProvider.cs   # CommandProvider — top-level commands
    ├── Program.cs                      # Main entry point
    ├── Package.appxmanifest            # MSIX manifest with CmdPal registration
    ├── Pages/
    │   ├── DefinitionPage.cs           # DynamicListPage — search UI
    │   └── DefinitionListItem.cs       # ListItem for each result
    ├── Helpers/
    │   ├── DictionaryService.cs        # HTTP client + caching layer
    │   ├── SettingsManager.cs          # Settings UI via CmdPal settings API
    │   ├── Models.cs                   # JSON data models
    │   └── DefinitionExtensionHost.cs  # Extension host singleton
    └── Assets/                         # MSIX tile and logo assets
```

## API

Uses the [Free Dictionary API](https://dictionaryapi.dev/) — **no API key required**.

```
GET https://api.dictionaryapi.dev/api/v2/entries/{lang}/{word}
```

An internet connection is required for lookups.

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Extension not visible | Restart PowerToys. Verify MSIX is installed: `Get-AppPackage *Definition*` |
| Build errors | Ensure .NET 9.0 SDK and Windows App SDK are installed |
| MSIX install fails | Enable Developer Mode in Windows Settings |
| No results returned | Check your internet connection |

## Contributing

Contributions are welcome! Please open an [issue](https://github.com/ruslanlap/CmdPal-Definition/issues) or submit a pull request.

## License

[MIT](LICENSE)
