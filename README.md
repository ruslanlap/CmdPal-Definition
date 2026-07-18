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

## What's New

### v1.0.3

- 💡 **Predictive Spelling Lookup** — when no definitions are found, the extension now shows "Did you mean?" suggestions ranked by fuzzy similarity. Click a suggestion to instantly look up that word.
- Multi-source suggestion engine: **Datamuse API** (English & Spanish), **Wiktionary opensearch** (12+ languages), and **local Levenshtein fallback** (offline).
- New **Enable Spelling Suggestions** toggle in Settings (on by default).
- Levenshtein distance scoring with proportional thresholds and prefix-match bonus.
- Localized "Did you mean?" strings for en-US and fr-FR.

### v1.0.2

- Expanded dictionary experience with support for: English, French, Chinese and Ukrainian.
- Improved lookup quality with cleaner grouping by part of speech, better examples, and faster repeated searches via in-memory caching.
- Better CI/CD reliability for Store publishing and release packaging.

### v1.0.1

- Initial release with English dictionary lookup via Free Dictionary API.
- Phonetics, synonyms, antonyms, and usage examples.
- Copy to clipboard and Wiktionary integration.

## Features

| Feature | Description |
|---------|-------------|
| **Instant Definitions** | Definitions from the [Free Dictionary API](https://dictionaryapi.dev/) grouped by part of speech |
| **4 Languages** | English, French, Chinese (CC-CEDICT offline), and Ukrainian (Wiktionary + goroh.pp.ua) |
| **Phonetics** | IPA transcriptions displayed alongside each word |
| **Synonyms & Antonyms** | Related words listed per part of speech |
| **Usage Examples** | Real-world example sentences |
| **Predictive Spelling** | "Did you mean?" suggestions when no definitions found — powered by Datamuse API, Wiktionary opensearch, and Levenshtein fuzzy matching |
| **Copy to Clipboard** | Click any result to copy |
| **Wiktionary Integration** | Right-click context menu to open in Wiktionary |
| **Configurable** | Settings for language, result count, display options, and spelling suggestions |
| **Smart Caching** | In-memory cache for instant repeat lookups |
| **Debounced Search** | 300ms debounce to minimize API calls while typing |
| **Offline Chinese** | CC-CEDICT database embedded — Chinese lookups work without internet |
| **Script Auto-Detection** | Automatically detects Latin, Cyrillic, CJK, or mixed script to route to the right provider |

## How Spelling Suggestions Work

When you type a word that isn't found in any dictionary, the extension queries three suggestion sources **in parallel**:

1. **[Datamuse API](https://api.datamuse.com/)** — fetches phonetically/orthographically similar words (English & Spanish only)
2. **Wiktionary opensearch** — prefix-based title search across 12+ language editions
3. **Local word lists** — built-in common English & Ukrainian words ranked by Levenshtein distance (works offline)

Results are merged, deduplicated, and ranked using a **fuzzy similarity score** (0–1) based on Levenshtein edit distance with a prefix-match bonus. The distance threshold scales proportionally with word length, so small typos in longer words aren't filtered out.

```
User types: "recieve"
→ No dictionary match found
→ Suggestions: receive (close match), relieve, recipe, ...
→ User clicks "receive" → full definition displayed instantly
```

Toggle suggestions on/off in **Settings → Enable Spelling Suggestions** (on by default).

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
winget upgrade "Definition for Command Palette"
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
6. If no definitions are found, click a **"Did you mean?"** suggestion to look up that word instead

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
    ├── DefinitionExtension.cs              # IExtension entry point (COM server)
    ├── DefinitionCommandsProvider.cs       # CommandProvider — top-level commands
    ├── Program.cs                          # Main entry point
    ├── Package.appxmanifest                # MSIX manifest with CmdPal registration
    ├── Pages/
    │   ├── DefinitionPage.cs               # DynamicListPage — search UI + suggestion flow
    │   └── DefinitionListItem.cs           # ListItem for each result (definitions, suggestions)
    ├── Helpers/
    │   ├── DictionaryService.cs            # Multi-provider lookup + caching layer
    │   ├── SuggestionService.cs            # Predictive spelling (Datamuse + Wiktionary + local)
    │   ├── FuzzyMatcher.cs                 # Levenshtein distance & similarity scoring
    │   ├── SettingsManager.cs              # Settings UI via CmdPal settings API
    │   ├── Models.cs                       # JSON data models (DictionaryEntry, DatamuseWord)
    │   ├── ScriptDetector.cs               # Latin / Cyrillic / CJK / Mixed detection
    │   ├── IDictionaryProvider.cs          # Provider interface
    │   ├── EnglishDictionaryProvider.cs    # Free Dictionary API (en)
    │   ├── FrenchDictionaryProvider.cs     # Collins + French Wiktionary (fr)
    │   ├── UkrainianDictionaryProvider.cs  # Ukrainian Wiktionary + goroh.pp.ua (uk)
    │   ├── ChineseDictionaryProvider.cs    # CC-CEDICT offline database (zh)
    │   └── DefinitionExtensionHost.cs      # Extension host singleton
    ├── Strings/
    │   ├── en-US/Resources.resw            # English localization
    │   └── fr-FR/Resources.resw            # French localization
    ├── Resources/
    │   └── cedict.txt.gz                   # Embedded CC-CEDICT Chinese dictionary
    └── Assets/                             # MSIX tile and logo assets
```

## APIs & Data Sources

| Source | Type | Languages | Notes |
|--------|------|-----------|-------|
| [Free Dictionary API](https://dictionaryapi.dev/) | Online | English, French | No API key required |
| [Ukrainian Wiktionary](https://uk.wiktionary.org/) | Online | Ukrainian | Wikitext parsing |
| [goroh.pp.ua](https://goroh.pp.ua/) | Online | Ukrainian | Fallback for Wiktionary |
| [Collins Dictionary](https://www.collinsdictionary.com/) | Online | French | French-English |
| [French Wiktionary](https://fr.wiktionary.org/) | Online | French | Fallback for Collins |
| [CC-CEDICT](https://www.mdbg.net/chinese/dictionary?page=cc-cedict) | Offline | Chinese | Embedded gzipped resource |
| [Datamuse API](https://api.datamuse.com/) | Online | English, Spanish | Spelling suggestions |
| Wiktionary opensearch | Online | 12+ languages | Spelling suggestions (prefix search) |
| Built-in word lists | Offline | English, Ukrainian | Spelling suggestions (Levenshtein) |

An internet connection is required for most lookups. Chinese lookups work offline via the embedded CC-CEDICT database.

## Store Metadata in CI

The workflow `.github/workflows/store-metadata.yml` can update Microsoft Store listing metadata from:

`store-metadata/metadata.json`

If this file is missing, the workflow now skips metadata update steps instead of failing.

Use this file only when you intentionally want to update listing metadata through CI.

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Extension not visible | Restart PowerToys. Verify MSIX is installed: `Get-AppPackage *Definition*` |
| Build errors | Ensure .NET 9.0 SDK and Windows App SDK are installed |
| MSIX install fails | Enable Developer Mode in Windows Settings |
| No results returned | Check your internet connection (Chinese works offline) |
| No spelling suggestions | Ensure **Enable Spelling Suggestions** is on in Settings |

## Contributing

Contributions are welcome! Please open an [issue](https://github.com/ruslanlap/CmdPal-Definition/issues) or submit a pull request.

## License

[MIT](LICENSE)
