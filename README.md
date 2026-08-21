# Open Window Utility

Free, open-source Windows 10/11 setup utility for the community. Install apps, apply reversible tweaks, enable optional features, clean junk, and set a safer Windows Update policy — from a native WPF app.

Inspired by [Chris Titus Tech WinUtil](https://christitus.com/windows-tool/). This project is an independent rewrite (C# / WPF, MIT). It is **not** affiliated with CTT and does not copy WinUtil source, JSON, or branding.

## Download

Get the portable EXE from [Releases](https://github.com/HaydernCenterpoint/Open-Window-Utility/releases/latest). Copy the EXE to a USB or folder and run it as Administrator — no PowerShell, no install.

Tweaks, features, cleanup, and This PC work offline. Installing apps and checking for a newer EXE need the internet. Click the version chip when you want an update; startup does not call the network.

Settings, logs, and the undo journal live in a `data` folder next to the EXE when that location is writable.

```powershell
git clone https://github.com/HaydernCenterpoint/Open-Window-Utility.git
```

## Requirements

- Windows 10 22H2 or Windows 11 (x64)
- Administrator (the EXE requests elevation)
- [WinGet](https://aka.ms/getwinget) recommended; Chocolatey is optional fallback

## Run from source

```powershell
dotnet build OpenWindowUtility.slnx -c Release
dotnet run --project src/OpenWindowUtility.App/OpenWindowUtility.App.csproj
```

Publish a portable EXE:

```powershell
dotnet publish src/OpenWindowUtility.App/OpenWindowUtility.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts
```

## What v2 includes

- **Applications** — curated catalog (~80 packages) via WinGet / Chocolatey
- **Tweaks** — essential, advanced, and preference toggles with undo journal + restore point
- **System Configuration** — DISM features, repair actions, classic Control Panel shortcuts
- **Cleanup** — scan known junk, classify SAFE/DEEP, delete only what you select
- **Windows Update** — Default / Security / Disable All (typed confirmation)
- **Win11 Creator** — bypass TPM 2.0 / CPU / RAM / Secure Boot, skip Microsoft Account enforcement, auto-generate `autounattend.xml` or build custom bootable Windows 11 ISOs

## Safety

Read [docs/SAFETY.md](docs/SAFETY.md) before applying advanced tweaks. Create a restore point (on by default). Do not test BitLocker / Disable All Updates on your only machine.

## Language

English and Vietnamese. Default follows Windows UI language; switch under Settings.

## License

MIT. See [LICENSE](LICENSE).
