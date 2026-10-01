# ER Collection Checker JP

[日本語](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/README.md) | **English**

ER Collection Checker JP is a Windows desktop app that reads PC Steam save data for *ELDEN RING*
and shows collection progress for weapons, armor, sorceries, incantations, talismans, Ashes of War,
gestures, Spirit Ashes, Crystal Tears, spectral steed attire, and more.

> [!IMPORTANT]
> The app opens `ER0000.sl2` read-only and never writes to the save file. It does not access the
> game process or its memory.

> [!WARNING]
> This app supports the PC Steam release of *ELDEN RING* version 1.17 only. It is an unofficial
> community tool and is not affiliated with FromSoftware or Bandai Namco Entertainment.
> Saves from other game versions may not be parsed correctly. Back up your save before use.

## Features

- Select an occupied character slot by character name
- View overall and per-category owned, missing, and completion totals
- Switch between Collection and Strict All Items modes
- Switch the full UI between Japanese and English
- Display and search both Japanese and English item names
- Filter by status, category, and Base Game / SHADOW OF THE ERDTREE / Tarnished Pack
- Sort the item list by clicking column headers
- Copy the selected item's displayed-language name with **Copy name** or `Ctrl+C`
- Distinguish directly owned armor from forms covered by a verified conversion
- Inspect DataOnly entries, decision notes, Param IDs, and internal keys in developer mode
- Restore the last successfully opened save path on the next launch

See the [WPF UI notes](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/WPF_UI.md) for implementation details.

## Requirements

- Windows 10 or Windows 11
- 64-bit (x64) CPU
- PC Steam version of *ELDEN RING*
- Game version 1.17

## Download and launch

The latest public version is **v0.1.1**.

1. Download `ERCollectionCheckerJP-v0.1.1-win-x64.zip` from
   [GitHub Releases](https://github.com/saraefcat/ERCollectionCheckerJP/releases/latest).
2. Fully extract the ZIP into a new folder of your choice.
3. Do not run the app from inside a ZIP viewer. Launch `ERCollectionCheckerJP.exe` from the extracted folder.

The win-x64 ZIP is self-contained and includes the .NET 10 Desktop Runtime, so no separate .NET installation
is required. There is no installer.

The executable is not code-signed, so Windows may display an unknown-publisher warning. Confirm that the file
came from GitHub Releases and, if desired, verify its SHA-256 checksum as described below.

### Verify the SHA-256 checksum

Open Windows PowerShell in the folder containing the downloaded ZIP and run:

```powershell
Get-FileHash .\ERCollectionCheckerJP-v0.1.1-win-x64.zip -Algorithm SHA256
```

Compare the reported `Hash` with the value in the Release asset
`ERCollectionCheckerJP-v0.1.1-win-x64.zip.sha256.txt`.

## Basic usage

1. Select **Select file** and choose `ER0000.sl2`.
2. Select an occupied character.
3. Choose a collection mode.
4. Select **Analyze**.
5. Narrow the list with search, status, category, and content filters.
6. Select the item you want to work with.
7. Use **Copy name** or `Ctrl+C` to copy the selected item's name in the current display language.

Selecting a row does not change the clipboard. Click a column header to sort the list.

The standard save location has the following form. `<SteamID>` varies by account and computer.

```text
%APPDATA%\EldenRing\<SteamID>\ER0000.sl2
```

Choose the actual `ER0000.sl2` file, not its folder. If exactly one `ER0000.sl2` exists below
`%APPDATA%\EldenRing`, the app automatically proposes it at startup.

## Updating to a newer version

1. Close ER Collection Checker JP.
2. Download the new Release ZIP.
3. Fully extract it to a new folder, separate from the old version.
4. Launch the new `ERCollectionCheckerJP.exe`.
5. Confirm that it works correctly.
6. Delete the old version's folder if it is no longer needed.

Extracting each release to a new folder is recommended instead of overwriting the old one. Settings are stored
outside the application folder, so they normally carry over automatically:

```text
%LOCALAPPDATA%\ERCollectionCheckerJP\settings.json
```

The primary setting currently stored there is the last successfully used save-file path.
If the settings file is missing or invalid, or the saved path no longer exists, the app falls back to its defaults
and automatic detection.

## Collection modes

### Collection

Tracks **1,940** weapons, armor pieces, sorceries, incantations, talismans, Ashes of War, gestures,
Spirit Ashes, Crystal Tears, spectral steed attire, and other items whose persistent ownership can be checked.

Goods that cannot yet be safely identified as “previously obtained” after use or exchange remain `Pending`
and do not count toward progress instead of being incorrectly marked Missing.

### Strict All Items

Tracks all **2,768** items reviewed as obtainable during normal play. This includes Goods that remain pending
in Collection mode and is intended for a broader inventory check.

The **599** DataOnly entries that exist in game data but are not obtainable in normal play are excluded from
progress in both modes. Developer mode can display their existence and exclusion reason.

Unparsed or insufficiently verified information is reported as `Unknown`, not `Missing`.

For details, see [Detection rules](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/DETECTION_RULES.md)
and [Goods classifications](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/GOODS_CLASSIFICATIONS.md).

## Save-file safety

This app is not a save editor.

- It opens `.sl2` with `FileAccess.Read` and does not modify it.
- It has no API for writing save data back to disk.
- It does not access the game process or its memory.
- Each snapshot checks file size and last-write time before and after reading.
- It hashes the bytes read with SHA-256 and compares snapshots taken before and after analysis.
- If the save changes while it is being read or analyzed, the result is discarded.
- It does not use original game files, `regulation.bin`, Oodle, or external data-generation tools at runtime.

As with any external tool used around an important save, keeping a backup is recommended.

## Known limitations

- Game versions other than 1.17 are unsupported
- Automatic save watching and re-analysis are not implemented
- CSV / JSON export is not implemented
- Character level, play time, and journey count are not displayed
- Some Goods remain pending in Collection mode until a persistent obtained-state source is verified
- Distribution is a self-contained ZIP; no installer is provided
- The executable is not code-signed

## Troubleshooting

### The save file cannot be found

The usual location is `%APPDATA%\EldenRing\<SteamID>\ER0000.sl2`, where `<SteamID>` varies by environment.
Use **Select file** to choose `ER0000.sl2` directly.

### The save cannot be analyzed

- Confirm that it is a PC Steam save.
- Confirm that the game version is 1.17.
- If the game is currently saving, wait briefly and try again.
- The app deliberately discards a result if the file changes during reading or analysis.

### Windows displays a warning

The executable is unsigned, so Windows may show an unknown-publisher warning. Confirm that you obtained it from
[GitHub Releases](https://github.com/saraefcat/ERCollectionCheckerJP/releases/latest) and verify its SHA-256 checksum if needed.

### Report a problem

Report ordinary bugs through [GitHub Issues](https://github.com/saraefcat/ERCollectionCheckerJP/issues).
Do not attach an actual save file or disclose a Steam ID, character name, or other personal information in a public issue.
For security issues, follow the [security policy](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/SECURITY.md).

## For developers

### Run from source

> [!NOTE]
> Game-derived runtime data is not included in the public repository. A complete application cannot be built
> from a public clone alone. The following commands are for maintainers who have placed the private runtime data
> in its expected location.

```powershell
dotnet restore ERCollectionCheckerJP.sln
dotnet run --project src/ERCollectionCheckerJP.App -c Release
```

At startup, the app validates the bundled 1.17 runtime manifests, schemas, file sizes, SHA-256 hashes, and
cross-references. The analysis window does not open if validation fails.

### Build and test

```powershell
dotnet build ERCollectionCheckerJP.sln -c Release
dotnet test ERCollectionCheckerJP.sln -c Release --no-build
```

Run `.\tools\Test-WithCoverage.ps1` to write coverage results to the untracked `TestResults/Coverage/` folder.
The normal solution includes product code, automated tests, and the read-only `SaveDiagnostics` utility.
The complete app and data-dependent tests require private data maintained separately by the project owner;
its location can be supplied with the `ERCollectionCheckerJPPrivateDataRoot` MSBuild property.

### Create a self-contained release

The following script checks for a clean Git worktree and passing Release tests, then creates a self-contained,
single-executable win-x64 package, ZIP, and SHA-256 checksum in the release output location:

```powershell
.\tools\Publish-Release.ps1
```

A version can be supplied explicitly, for example `.\tools\Publish-Release.ps1 -Version 0.1.1`.
`SOURCE_COMMIT.txt` records the source commit. `RUNTIME_DATA.txt` records the game version, runtime-file count,
aggregate SHA-256, .NET SDK, and bundled .NET Runtime version. Raw runtime JSON and game files are not included.
See the [release procedure](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/RELEASE.md) for details.

### Project structure

```text
src/
├─ ERCollectionCheckerJP.App/          WPF / MVVM UI
├─ ERCollectionCheckerJP.Application/  Analysis use cases and progress aggregation
├─ ERCollectionCheckerJP.Domain/       Domain models
├─ ERCollectionCheckerJP.ItemDatabase/ Bundled-data validation and composition
└─ ERCollectionCheckerJP.SaveParser/   Read-only save parsing
tests/                                 Automated tests
tools/
├─ ERCollectionCheckerJP.SaveDiagnostics/ Read-only diagnostics
└─ Publish-Release.ps1                    Self-contained release builder
docs/                                  Design and audit documentation
```

Game-data extraction or generation tools, original game files, runtime data, shared save fixtures, and release
artifacts are not included in the public repository.

### Bundled data

The bundled item data is generated and reviewed in a private offline maintenance process using legitimately
obtained game data. Original game files and save files are not distributed.

Runtime JSON is embedded in the executable and is not placed beside it as individual files. At startup, the app
validates manifests, schemas, file sizes, SHA-256 hashes, and cross-references, loads the data into memory, and
removes the temporary extraction. App-specific temporary directories left after an abnormal exit are removed on
the next launch once they are more than 24 hours old. Embedding keeps the distribution tidy; it is not encryption
and does not make the data secret.

## Developer documentation

- [Design](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/DESIGN.md)
- [Application integration](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/APPLICATION_INTEGRATION.md)
- [Data pipeline](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/DATA_PIPELINE.md)
- [Item database](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/ITEM_DATABASE.md)
- [Save-format notes](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/SAVE_FORMAT_NOTES.md)
- [Test strategy](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/TEST_STRATEGY.md)
- [Armor conversions](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/ARMOR_CONVERSIONS.md)
- [Gesture mappings](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/GESTURE_MAPPINGS.md)
- [Goods classifications](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/GOODS_CLASSIFICATIONS.md)
- [Third-party notices](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/THIRD_PARTY_NOTICES.md)
- [Asset provenance](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/ASSET_PROVENANCE.md)
- [Release procedure](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/RELEASE.md)
- [Security policy](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/SECURITY.md)
- [Changelog](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/CHANGELOG.md)

## License and disclaimer

Original source code and documentation from this project are released under the [MIT License](LICENSE).
The MIT License does not apply to game-derived data embedded in the executable, game names or trademarks,
or other third-party works. See [THIRD_PARTY_NOTICES.md](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/THIRD_PARTY_NOTICES.md).

This is an unofficial community project and is not affiliated with FromSoftware or Bandai Namco Entertainment.
*ELDEN RING* and related names and trademarks belong to their respective owners.
