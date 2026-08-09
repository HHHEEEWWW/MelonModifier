# MelonModifier

**Local GUI manager for MelonLoader** — install, upgrade, and uninstall [MelonLoader](https://github.com/LavaGang/MelonLoader) (Il2Cpp / Mono) for Unity games, manage local Mods & Plugins, view run logs and edit `Loader.cfg` — all in a sci-fi HUD interface.

[中文说明](README.md) | English

![Game Library](docs/screenshot-library.png)

![Mods Management](docs/screenshot-mods.png)

![Appearance (Light Theme)](docs/screenshot-light-appearance.png)

## Features

| Page | Description |
|------|-------------|
| **Game Library** | Scan Steam library for Unity games, add folders manually, one-click install / upgrade / uninstall MelonLoader, engine & version detection (offline detection + GitHub latest comparison) |
| **Mods** | Browse `Mods/` and `Plugins/`, toggle enable/disable (`.disabled` suffix), delete, drag-and-drop install |
| **Logs** | View run logs under the game's `MelonLoader/Logs/` (crash troubleshooting) |
| **Config** | Edit `UserData/Loader.cfg` (full-text mode, preserves comments & unknown keys) |
| **Appearance** | Theme switching (Dark/Light), font family, UI scaling (85%~130%), auto-saved settings |
| **About** | Version & upstream info |

## Tech Stack

- **C# / .NET 8 (net8.0-windows)** + **WPF** (MVVM: CommunityToolkit.Mvvm)
- Core logic separated from UI: `MelonModifier.Core` (pure .NET, unit-testable) + `MelonModifier.App` (WPF)
- Dependencies: `Tomlyn` (TOML parsing for Loader.cfg), `CommunityToolkit.Mvvm`
- Sci-fi HUD theme: dark neon custom control templates (no third-party UI library)

## Quick Start

On Windows, double-click `启动管理器.bat` in the repo root (auto-builds if the executable is missing); or manually:

```bash
dotnet build MelonModifier.sln
dotnet run --project src/MelonModifier.App
```

Data directory: `%AppData%\MelonModifier` (manually added game list + download cache).

## How It Works

"Installing" MelonLoader essentially writes into the game root:
- `version.dll` — proxy DLL, loaded by Windows' DLL search mechanism at game startup
- `MelonLoader/` — framework directory (net6/net472/net35 runtimes)

Uninstalling removes those two items (`Mods/`, `Plugins/`, `UserData/` are kept).

## Known Limitations

- Currently downloads the latest release from GitHub Releases only (v0.7.x structure: `version.dll` + `MelonLoader/`, no `dobby.dll`)
- Version comparison is string-based (local file version `0.7.3.0` vs tag `v0.7.3` may show as upgradable; reinstalling is idempotent and harmless)
- No Thunderstore online mod database integration (local management only)

## Roadmap

- MOD development workflow: generate C# mod template project → compile → one-click deploy to the game's `Mods/`
- Semantic version comparison, install backup/restore

## Documentation

- [Development docs (中文)](docs/DEVELOPMENT.md) — architecture, key designs, pitfalls
- [IRON NEST MOD principles (中文)](docs/IRON-NEST-MOD-原理.md) — MelonLoader MOD development (skeleton / IL2CPP rules / hot-reload architecture / game data reference)

## License

[GPL-3.0](LICENSE)
